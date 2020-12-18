using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi.DeviceData;
using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi
{
    /// <summary>
    /// Plugin class
    /// </summary>
    /// <seealso cref="Hspi.HspiBase" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal partial class PlugIn : HspiBase
    {
        public PlugIn()
            : base(PlugInData.PlugInId, PlugInData.PlugInName)
        {
        }

        //public override string ConfigDevice(int deviceId, [AllowNull] string user, int userRights, bool newDevice)
        //{
        //    if (newDevice)
        //    {
        //        return string.Empty;
        //    }

        //    try
        //    {
        //        var deviceClass = (DeviceClass)HomeSeerSystem.GetDeviceByRef(deviceId);

        //        if (deviceClass.get_Interface(HomeSeerSystem) == PlugInData.PlugInName)
        //        {
        //            var deviceIdentifier = DeviceIdentifier.Identify(deviceClass);
        //            if (deviceIdentifier != null)
        //            {
        //                return configPage.GetDeviceImportTab(deviceIdentifier);
        //            }
        //        }
        //        else
        //        {
        //            return configPage.GetDeviceHistoryTab(deviceId);
        //        }
        //        return string.Empty;
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError(Invariant($"ConfigDevice for {deviceId} With {ex.Message}"));
        //        return string.Empty;
        //    }
        //}

        //public override Enums.ConfigDevicePostReturn ConfigDevicePost(int deviceId, [AllowNull] string data, [AllowNull] string user, int userRights)
        //{
        //    try
        //    {
        //        return configPage.GetDeviceHistoryPost(Callback, deviceId, data);
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError(Invariant($"ConfigDevicePost for {deviceId} With {ex.Message}"));
        //        return Enums.ConfigDevicePostReturn.DoneAndCancelAndStay;
        //    }
        //}

        //public override string GetPagePlugin(string page, [AllowNull]string user, int userRights, [AllowNull]string queryString)
        //{
        //    if (page == ConfigPage.Name)
        //    {
        //        return configPage.GetWebPage(queryString);
        //    }

        //    return string.Empty;
        //}

        public override void HsEvent(Constants.HSEvent eventType, [AllowNull] object[] parameters)
        {
            HSEventImpl(eventType, parameters).Wait(ShutdownCancellationToken);
        }

        protected override void Initialize()
        {
            base.Initialize();
            string result = string.Empty;
            try
            {
                pluginConfig = new PluginConfig(HomeSeerSystem);
                Trace.TraceInformation("Starting Plugin");
                LogConfiguration();

                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.VALUE_CHANGE, Id);
                HomeSeerSystem.RegisterEventCB(Constants.HSEvent.STRING_CHANGE, Id);

                // Feature pages
                HomeSeerSystem.RegisterFeaturePage(Id, "dbconfiguration.html", "Database configuration");

                RestartProcessing();

                Trace.TraceInformation("Plugin Started");
            }
            catch (Exception ex)
            {
                result = Invariant($"Failed to initialize PlugIn with {ex.GetFullMessage()}");
                Trace.TraceError(result);
                throw;
            }
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
                Trace.TraceError(Invariant($"Failed to import value for Ref Id: {devOrFeatRef} with {ex.GetFullMessage()}"));
                return EPollResponse.ErrorGettingStatus;
            }
        }

        //public override IPlugInAPI.PollResultInfo PollDevice(int deviceId)
        //{
        //}

        //public override string PostBackProc(string page, string data, [AllowNull]string user, int userRights)
        //{
        //    if (page == ConfigPage.Name)
        //    {
        //        return configPage.PostBackProc(data, user, userRights);
        //    }

        //    return string.Empty;
        //}

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                Shutdown();
                disposedValue = true;
            }

            base.Dispose(disposing);
        }

        private async Task RecordDeviceValue(InfluxDBMeasurementsCollector collector,
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
                        var status = HomeSeerSystem.GetStatusControlForValue(deviceRefId, deviceValue);
                        if (status != null)
                        {
                            deviceString = status.Label;
                        }
                    }
                    Trace.WriteLine(Invariant($"Recording Device Ref Id: {deviceRefId} with [{deviceValue}] & [{deviceString}]"));

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
                    Trace.TraceWarning(Invariant($"Not recording Device Ref Id: {deviceRefId} as it has invalid value."));
                }
            }
        }

        private async Task<InfluxDBMeasurementsCollector> GetInfluxDBMeasurementsCollector()
        {
            using (var sync = await influxDBMeasurementsCollectorLock.EnterAsync().ConfigureAwait(false))
            {
                return influxDBMeasurementsCollector;
            }
        }

        private async Task HSEventImpl(Constants.HSEvent eventType, object[] parameters)
        {
            try
            {
                if ((eventType == Constants.HSEvent.VALUE_CHANGE) && (parameters.Length > 4))
                {
                    int deviceRefId = Convert.ToInt32(parameters[4], CultureInfo.InvariantCulture);
                    await RecordDeviceValue(deviceRefId, TrackedType.Value).ConfigureAwait(false);
                }
                else if ((eventType == Constants.HSEvent.STRING_CHANGE) && (parameters.Length > 3))
                {
                    int deviceRefId = Convert.ToInt32(parameters[3], CultureInfo.InvariantCulture);
                    await RecordDeviceValue(deviceRefId, TrackedType.String).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(Invariant($"Failed to process HSEvent {eventType} with {ex.GetFullMessage()}"));
            }
        }

        private void LogConfiguration()
        {
            var dbConfig = pluginConfig.DBLoginInformation;
            Trace.WriteLine(Invariant($"Url:{dbConfig.DBUri} User:{dbConfig.User} Database:{dbConfig.DB}"));
        }

        private void PluginConfigChanged()
        {
            this.EnableLogDebug = pluginConfig.DebugLogging;
            RestartProcessing();
        }

        private async Task RecordDeviceValue(int deviceRefId, TrackedType trackedType)
        {
            var collector = await GetInfluxDBMeasurementsCollector().ConfigureAwait(false);
            if ((collector != null) && collector.IsTracked(deviceRefId, trackedType))
            {
                var device = HomeSeerSystem.GetDeviceByRef(deviceRefId);
                await RecordDeviceValue(collector, device).ConfigureAwait(false);
            }
        }

        private async Task RecordTrackedDevices()
        {
            var collector = await GetInfluxDBMeasurementsCollector().ConfigureAwait(false);
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

                        Trace.TraceError(Invariant($"Error in recording RefId:{refId} Error:{ex.GetFullMessage()}"));
                    }

                    ShutdownCancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        private async Task<bool> RecordTrackedDevices(InfluxDBMeasurementsCollector collector, int refId)
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
            using (var sync = influxDBMeasurementsCollectorLock.Enter())
            {
                influxDBMeasurementsCollector?.Dispose();
            }

            using (var sync = deviceRootDeviceManagerLock.Enter())
            {
                deviceRootDeviceManager?.Dispose();
            }
        }

        private async Task StartDeviceImport()
        {
            using (var sync = await deviceRootDeviceManagerLock.EnterAsync(ShutdownCancellationToken))
            {
                deviceRootDeviceManager?.Dispose();
                deviceRootDeviceManager = new DeviceRootDeviceManager(HomeSeerSystem,
                                                                      pluginConfig.DBLoginInformation,
                                                                      ShutdownCancellationToken);
            }
        }

        private async Task StartInfluxDBMeasurementsCollector()
        {
            using (var sync = await influxDBMeasurementsCollectorLock.EnterAsync(ShutdownCancellationToken))
            {
                bool recreate = (influxDBMeasurementsCollector == null) ||
                                (!influxDBMeasurementsCollector.LoginInformation.Equals(pluginConfig.DBLoginInformation));

                if (recreate)
                {
                    if (pluginConfig.DBLoginInformation.IsValid)
                    {
                        influxDBMeasurementsCollector?.Dispose();
                        influxDBMeasurementsCollector = new InfluxDBMeasurementsCollector(pluginConfig.DBLoginInformation, ShutdownCancellationToken);
                        influxDBMeasurementsCollector.Start(pluginConfig.DevicePersistenceData.Values);
                    }
                }
                else
                {
                    influxDBMeasurementsCollector.UpdatePeristenceData(pluginConfig.DevicePersistenceData.Values);
                }
            }

            await RecordTrackedDevices().ConfigureAwait(false);
        }

        //public override string ActionBuildUI([AllowNull]string uniqueControlId, IPlugInAPI.strTrigActInfo actionInfo)
        //{
        //    try
        //    {
        //        switch (actionInfo.TANumber)
        //        {
        //            case ActionRefreshTANumber:
        //                return configPage.GetRefreshActionUI(uniqueControlId ?? string.Empty, actionInfo);

        //            default:
        //                return base.ActionBuildUI(uniqueControlId, actionInfo);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.TraceError(Invariant($"Failed to give build Action UI with {ex.GetFullMessage()}"));
        //        throw;
        //    }
        //}

        //public override bool ActionConfigured(IPlugInAPI.strTrigActInfo actionInfo)
        //{
        //    try
        //    {
        //        switch (actionInfo.TANumber)
        //        {
        //            case ActionRefreshTANumber:
        //                if (actionInfo.DataIn != null)
        //                {
        //                    RefreshDeviceAction refreshDeviceAction = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as RefreshDeviceAction;
        //                    if ((refreshDeviceAction != null) && (refreshDeviceAction.DeviceRefId != 0))
        //                    {
        //                        return HomeSeerSystem.GetDeviceByRef(refreshDeviceAction.DeviceRefId) != null;
        //                    }
        //                }

        //                return false;

        //            default:
        //                return base.ActionConfigured(actionInfo);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.TraceError(Invariant($"Failed to ActionConfigured with {ex.GetFullMessage()}"));
        //        return false;
        //    }
        //}

        //public override int ActionCount()
        //{
        //    return 1;
        //}

        //public override string ActionFormatUI(IPlugInAPI.strTrigActInfo actionInfo)
        //{
        //    try
        //    {
        //        switch (actionInfo.TANumber)
        //        {
        //            case ActionRefreshTANumber:
        //                if (actionInfo.DataIn != null)
        //                {
        //                    var refreshDeviceAction = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as RefreshDeviceAction;
        //                    if ((refreshDeviceAction != null) && (refreshDeviceAction.DeviceRefId != 0))
        //                    {
        //                        HSHelper hSHelper = new HSHelper(HomeSeerSystem);
        //                        return Invariant($"Refresh {hSHelper.GetName(refreshDeviceAction.DeviceRefId)} from Influx DB");
        //                    }
        //                }
        //                return Invariant($"{PlugInData.PlugInName} Unknown Device Import Refresh");

        //            default:
        //                return base.ActionFormatUI(actionInfo);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.TraceError(Invariant($"Failed to ActionFormatUI with {ex.GetFullMessage()}"));
        //        throw;
        //    }
        //}

        //public override IPlugInAPI.strMultiReturn ActionProcessPostUI([AllowNull] NameValueCollection postData, IPlugInAPI.strTrigActInfo actionInfo)
        //{
        //    try
        //    {
        //        switch (actionInfo.TANumber)
        //        {
        //            case ActionRefreshTANumber:
        //                return ConfigPage.GetRefreshActionPostUI(postData, actionInfo);

        //            default:
        //                return base.ActionProcessPostUI(postData, actionInfo);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.TraceError(Invariant($"Failed to ActionProcessPostUI with {ex.GetFullMessage()}"));
        //        throw;
        //    }
        //}

        //public override bool ActionReferencesDevice(IPlugInAPI.strTrigActInfo actionInfo, int deviceId)
        //{
        //    try
        //    {
        //        switch (actionInfo.TANumber)
        //        {
        //            case ActionRefreshTANumber:
        //                if (actionInfo.DataIn != null)
        //                {
        //                    var refreshDeviceAction = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as RefreshDeviceAction;
        //                    return (refreshDeviceAction != null) && (refreshDeviceAction.DeviceRefId == deviceId);
        //                }
        //                return false;

        //            default:
        //                return false;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.TraceError(Invariant($"Failed to ActionReferencesDevice with {ex.GetFullMessage()}"));
        //        return false;
        //    }
        //}

        //public override string get_ActionName(int actionNumber)
        //{
        //    try
        //    {
        //        switch (actionNumber)
        //        {
        //            case ActionRefreshTANumber:
        //                return Invariant($"{Name}:Refresh From InfluxDB");

        //            default:
        //                return base.get_ActionName(actionNumber);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.TraceError(Invariant($"Failed to give Action Name with {ex.GetFullMessage()}"));
        //        throw;
        //    }
        //}

        //public override bool HandleAction(IPlugInAPI.strTrigActInfo actionInfo)
        //{
        //    try
        //    {
        //        switch (actionInfo.TANumber)
        //        {
        //            case ActionRefreshTANumber:
        //                if (actionInfo.DataIn != null)
        //                {
        //                    var refreshDeviceAction = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as RefreshDeviceAction;
        //                    if ((refreshDeviceAction != null) && (refreshDeviceAction.DeviceRefId != 0))
        //                    {
        //                        if (ImportDeviceFromDB(refreshDeviceAction.DeviceRefId))
        //                        {
        //                            return true;
        //                        }
        //                    }
        //                }
        //                Trace.TraceWarning(Invariant($"Failed to execute action with Device not Found"));
        //                return false;

        //            default:
        //                return base.HandleAction(actionInfo);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.TraceWarning(Invariant($"Failed to execute action with {ex.GetFullMessage()}"));
        //        return false;
        //    }
        //}

        private async Task<bool> ImportDeviceFromDB(int deviceRefId)
        {
            DeviceRootDeviceManager deviceRootDeviceManagerCopy;
            using (var sync = deviceRootDeviceManagerLock.Enter())
            {
                deviceRootDeviceManagerCopy = deviceRootDeviceManager;
            }

            return await deviceRootDeviceManagerCopy.ImportDataForDevice(deviceRefId).ConfigureAwait(false);
        }

        protected override void BeforeReturnStatus()
        {
        }

        private readonly AsyncMonitor deviceRootDeviceManagerLock = new AsyncMonitor();
        private readonly AsyncMonitor influxDBMeasurementsCollectorLock = new AsyncMonitor();
        private DeviceRootDeviceManager deviceRootDeviceManager;
        private bool disposedValue;
        private InfluxDBMeasurementsCollector influxDBMeasurementsCollector;
        private PluginConfig pluginConfig;
    }
}