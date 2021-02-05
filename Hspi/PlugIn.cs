using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi.DeviceData;
using Hspi.Utils;
using Nito.AsyncEx;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using static System.FormattableString;

#nullable enable

namespace Hspi
{
    internal partial class PlugIn : HspiBase
    {
        public PlugIn()
            : base(PlugInData.PlugInId, PlugInData.PlugInName)
        {
        }

        public override void HsEvent(Constants.HSEvent eventType, object[]? parameters)
        {
            HSEventImpl(eventType, parameters).Wait(ShutdownCancellationToken);
        }

        public override EPollResponse UpdateStatusNow(int devOrFeatRef)
        {
            try
            {
                bool result = ImportDeviceFromDB(devOrFeatRef).ResultForSync();
                return result ? EPollResponse.NotFound : EPollResponse.Ok;
            }
            catch (Exception ex)
            {
                logger.Error(Invariant($"Failed to import value for Ref Id: {devOrFeatRef} with {ex.GetFullMessage()}"));
                return EPollResponse.ErrorGettingStatus;
            }
        }

        protected override void BeforeReturnStatus()
        {
            this.Status = pluginStatusCalculator?.PluginStatus ?? PluginStatus.Critical("Unknown Status");
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                Shutdown();
            }

            base.Dispose(disposing);
        }

        protected override void Initialize()
        {
            string result = string.Empty;
            try
            {
                pluginConfig = new PluginConfig(HomeSeerSystem);
                UpdateLoggingInformation();

                logger.Info("Starting Plugin");
                LogConfiguration();

                pluginStatusCalculator = new PluginStatusCalculator(HomeSeerSystem);

                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.VALUE_CHANGE, Id);
                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.STRING_CHANGE, Id);
                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.CONFIG_CHANGE, Id);

                // Feature pages
                HomeSeerSystem.RegisterFeaturePage(Id, "configuration.html", "Configuration");
                HomeSeerSystem.RegisterFeaturePage(Id, "dbconfiguration.html", "Database configuration");
                HomeSeerSystem.RegisterFeaturePage(Id, "persistentlist.html", "Persistence list");

                // Device Add Page
                HomeSeerSystem.RegisterDeviceIncPage(Id, "adddeviceimport.html", "Add Device Import Device");

                RestartProcessing();

                logger.Info("Plugin Started");
            }
            catch (Exception ex)
            {
                result = Invariant($"Failed to initialize PlugIn with {ex.GetFullMessage()}");
                logger.Error(result);
                throw;
            }
        }

        private async Task HSEventImpl(Constants.HSEvent eventType, object[]? parameters)
        {
            try
            {
                if (parameters != null)
                {
                    switch (eventType)
                    {
                        case Constants.HSEvent.VALUE_CHANGE:
                            if (parameters.Length > 4)
                            {
                                int deviceRefId = Convert.ToInt32(parameters[4], CultureInfo.InvariantCulture);
                                await RecordDeviceValue(deviceRefId, TrackedType.Value).ConfigureAwait(false);
                            }
                            break;

                        case Constants.HSEvent.STRING_CHANGE:
                            if (parameters.Length > 3)
                            {
                                int deviceRefId = Convert.ToInt32(parameters[3], CultureInfo.InvariantCulture);
                                await RecordDeviceValue(deviceRefId, TrackedType.String).ConfigureAwait(false);
                            }
                            break;

                        case Constants.HSEvent.CONFIG_CHANGE:
                            if (parameters.Length >= 6)
                            {
                                if (Convert.ToInt32(parameters[1], CultureInfo.InvariantCulture) == 0) // Device Type change
                                {
                                    if (Convert.ToInt32(parameters[4], CultureInfo.InvariantCulture) == 2) //Delete
                                    {
                                        int deletedDeviceRefId = Convert.ToInt32(parameters[3], CultureInfo.InvariantCulture);
                                        RestartProcessingIfNeeded(deletedDeviceRefId);
                                    }
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn(Invariant($"Failed to process HSEvent {eventType} with {ex.GetFullMessage()}"));
            }
        }

        private void RestartProcessingIfNeeded(int deletedDeviceRefId)
        {
            var deletedPersistanceIds = this.pluginConfig!.DevicePersistenceData.Values.Where(x => x.DeviceRefId == deletedDeviceRefId);

            if (deletedPersistanceIds.Any())
            {
                logger.Info($"History Persisted Device refId {deletedDeviceRefId} deleted.");
                foreach (var persist in deletedPersistanceIds)
                {
                    this.pluginConfig.RemoveDevicePersistenceData(persist.Id);
                }
                PluginConfigChanged();
            }
            else
            {
                var deviceRootDeviceManagerCopy = deviceRootDeviceManager;
                if (deviceRootDeviceManagerCopy != null)
                {
                    if (deviceRootDeviceManagerCopy.HasDevice(deletedDeviceRefId))
                    {
                        PluginConfigChanged();
                    }
                }
            }
        }

        private async Task<bool> ImportDeviceFromDB(int deviceRefId)
        {
            var deviceRootDeviceManagerCopy = deviceRootDeviceManager; 
            if (deviceRootDeviceManagerCopy != null)
            {
                return await deviceRootDeviceManagerCopy.ImportDataForDevice(deviceRefId).ConfigureAwait(false);
            }

            return false;
        }

        private void LogConfiguration()
        {
            if (pluginConfig != null)
            {
                var dbConfig = pluginConfig.DBLoginInformation;
                logger.Info(Invariant($"Url:{dbConfig.DBUri} User:{dbConfig.User} Database:{dbConfig.DB}"));
            }
        }

        private void PluginConfigChanged()
        {
            UpdateLoggingInformation();
            RestartProcessing();
        }

        private async ValueTask RecordDeviceValue(InfluxDBMeasurementsCollector collector,
                                                  AbstractHsDevice device)
        {
            if (device != null)
            {
                int deviceRefId = device.Ref;
                bool notValid = device.IsValueInvalid;
                if (!notValid)
                {
                    double deviceValue = device.Value;
                    string deviceString = device.Status;

                    if (string.IsNullOrWhiteSpace(deviceString))
                    {
                        var graphic = HomeSeerSystem.GetStatusGraphicForValue(deviceRefId, deviceValue);
                        if (graphic != null)
                        {
                            deviceString = graphic.Label;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(deviceString))
                    {
                        var status = HomeSeerSystem.GetStatusControlForValue(deviceRefId, deviceValue);
                        if (status != null)
                        {
                            deviceString = status.Label;
                        }
                    }

                    logger.Debug(Invariant($"Recording Device Ref Id: {deviceRefId} with [{deviceValue}] & [{deviceString}]"));

                    DateTime lastChange = device.LastChange;

                    RecordData recordData = new RecordData(deviceRefId,
                                                           deviceValue,
                                                           deviceString,
                                                           device.Name,
                                                           device.Location,
                                                           device.Location2,
                                                           lastChange);

                    await collector.Record(recordData).ConfigureAwait(false);
                }
                else
                {
                    logger.Warn(Invariant($"Not recording Device Ref Id: {deviceRefId} as it has invalid value."));
                }
            }
        }

        private async ValueTask RecordDeviceValue(int deviceRefId, TrackedType trackedType)
        {
            var collector = influxDBMeasurementsCollector;
            if ((collector != null) && collector.IsTracked(deviceRefId, trackedType))
            {
                var device = HomeSeerSystem.GetDeviceByRef(deviceRefId);
                await RecordDeviceValue(collector, device).ConfigureAwait(false);
            }
        }

        private async ValueTask RecordTrackedDevices()
        {
            var collector = influxDBMeasurementsCollector;
            if (collector != null)
            {
                var deviceEnumerator = HomeSeerSystem.GetAllRefs();
                foreach (var refId in deviceEnumerator)
                {
                    try
                    {
                        await RecordTrackedDevices(collector, refId).ConfigureAwait(false);
                        ShutdownCancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (Exception ex)
                    {
                        if (ex.IsCancelException())
                        {
                            throw;
                        }

                        logger.Error(Invariant($"Error in recording RefId:{refId} Error:{ex.GetFullMessage()}"));
                    }

                    ShutdownCancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        private async ValueTask<bool> RecordTrackedDevices(InfluxDBMeasurementsCollector collector, int refId)
        {
            var device = HomeSeerSystem.GetDeviceByRef(refId);
            if (device != null)
            {
                if (collector.IsTracked(refId, null))
                {
                    await RecordDeviceValue(collector, device).ConfigureAwait(false);
                    return true;
                }
            }

            return false;
        }

        private void RestartProcessing()
        {
            Utils.TaskHelper.StartAsyncWithErrorChecking("Measurements Collector", StartInfluxDBMeasurementsCollector, ShutdownCancellationToken);
            Utils.TaskHelper.StartAsyncWithErrorChecking("Device Import", StartDeviceImport, ShutdownCancellationToken);
        }

        private void Shutdown()
        {
            using (var _ = influxDBMeasurementsCollectorLock.Lock())
            {
                influxDBMeasurementsCollector?.Dispose();
            }

            using (var _ = deviceRootDeviceManagerLock.Lock())
            {
                deviceRootDeviceManager?.Dispose();
            }
        }

        private async Task StartDeviceImport()
        {
            using (var sync = await deviceRootDeviceManagerLock.LockAsync(ShutdownCancellationToken))
            {
                deviceRootDeviceManager?.Dispose();
                deviceRootDeviceManager = new DeviceImportDeviceManager(HomeSeerSystem,
                                                                        pluginConfig!.DBLoginInformation,
                                                                        pluginStatusCalculator!,
                                                                        ShutdownCancellationToken);
            }
        }

        private async Task StartInfluxDBMeasurementsCollector()
        {
            using (var _ = await influxDBMeasurementsCollectorLock.LockAsync(ShutdownCancellationToken))
            {
                var data = pluginConfig!.DevicePersistenceData;

                bool recreate = (influxDBMeasurementsCollector == null) ||
                                (!influxDBMeasurementsCollector.LoginInformation.Equals(pluginConfig!.DBLoginInformation));

                if (!recreate && (influxDBMeasurementsCollector != null))
                {
                    var existingData = influxDBMeasurementsCollector.PersistantValues;
                    recreate = data.Count == existingData.Count() &&
                               existingData.All(x => data.TryGetValue(x.Id, out var value) && value == x);
                }

                if (recreate)
                {
                    if (pluginConfig!.DBLoginInformation.IsValid)
                    {
                        influxDBMeasurementsCollector?.Dispose();
                        influxDBMeasurementsCollector = new InfluxDBMeasurementsCollector(pluginConfig!.DBLoginInformation,
                                                                                          this.pluginStatusCalculator!,
                                                                                          data.Values,
                                                                                          ShutdownCancellationToken);
                    }
                } 
            }

            await RecordTrackedDevices().ConfigureAwait(false);
        }

        private void UpdateLoggingInformation()
        {
            this.LogDebug = pluginConfig!.DebugLogging;
            Logger.ConfigureLogging(LogDebug, pluginConfig!.LogToFile, HomeSeerSystem);
        }

        private readonly static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly AsyncLock deviceRootDeviceManagerLock = new AsyncLock();
        private readonly AsyncLock influxDBMeasurementsCollectorLock = new AsyncLock();
        private volatile DeviceImportDeviceManager? deviceRootDeviceManager;
        private volatile InfluxDBMeasurementsCollector? influxDBMeasurementsCollector;
        private PluginConfig? pluginConfig;
        private PluginStatusCalculator? pluginStatusCalculator;
    }
}