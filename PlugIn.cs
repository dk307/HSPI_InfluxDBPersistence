using HomeSeerAPI;
using Hspi.DeviceData;
using Hspi.Pages;
using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using Scheduler.Classes;
using System;
using System.Collections.Specialized;
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
    internal class PlugIn : HspiBase
    {
        public PlugIn()
            : base(PlugInData.PlugInName, supportConfigDevice: true, supportConfigDeviceAll: true)
        {
        }

        public override string ConfigDevice(int deviceId, [AllowNull] string user, int userRights, bool newDevice)
        {
            if (newDevice)
            {
                return string.Empty;
            }

            try
            {
                var deviceClass = (DeviceClass)HS.GetDeviceByRef(deviceId);

                if (deviceClass.get_Interface(HS) == PlugInData.PlugInName)
                {
                    var deviceIdentifier = DeviceIdentifier.Identify(deviceClass);
                    if (deviceIdentifier != null)
                    {
                        return configPage.GetDeviceImportTab(deviceIdentifier);
                    }
                }
                else
                {
                    return configPage.GetDeviceHistoryTab(deviceId);
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                LogError(Invariant($"ConfigDevice for {deviceId} With {ex.Message}"));
                return string.Empty;
            }
        }

        public override Enums.ConfigDevicePostReturn ConfigDevicePost(int deviceId, [AllowNull] string data, [AllowNull] string user, int userRights)
        {
            try
            {
                return configPage.GetDeviceHistoryPost(Callback, deviceId, data);
            }
            catch (Exception ex)
            {
                LogError(Invariant($"ConfigDevicePost for {deviceId} With {ex.Message}"));
                return Enums.ConfigDevicePostReturn.DoneAndCancelAndStay;
            }
        }

        public override string GetPagePlugin(string page, [AllowNull]string user, int userRights, [AllowNull]string queryString)
        {
            if (page == ConfigPage.Name)
            {
                return configPage.GetWebPage(queryString);
            }

            return string.Empty;
        }

        public override void HSEvent(Enums.HSEvent eventType, [AllowNull]object[] parameters)
        {
            HSEventImpl(eventType, parameters).Wait(ShutdownCancellationToken);
        }

        public override string InitIO(string port)
        {
            string result = string.Empty;
            try
            {
                pluginConfig = new PluginConfig(HS);
                configPage = new ConfigPage(HS, pluginConfig);
                Trace.TraceInformation("Starting Plugin");
                LogConfiguration();

                pluginConfig.ConfigChanged += PluginConfig_ConfigChanged;

                RegisterConfigPage();

                Callback.RegisterEventCB(Enums.HSEvent.VALUE_CHANGE, Name, string.Empty);
                Callback.RegisterEventCB(Enums.HSEvent.STRING_CHANGE, Name, string.Empty);

                RestartProcessing();

                Trace.TraceInformation("Plugin Started");
            }
            catch (Exception ex)
            {
                result = Invariant($"Failed to initialize PlugIn with {ex.GetFullMessage()}");
                Trace.TraceError(result);
            }

            return result;
        }

        public override void LogDebug(string message)
        {
            if ((pluginConfig != null) && pluginConfig.DebugLogging)
            {
                base.LogDebug(message);
            }
        }

        public override IPlugInAPI.PollResultInfo PollDevice(int deviceId)
        {
            if (ImportDeviceFromDB(deviceId))
            {
                var pollResult = new IPlugInAPI.PollResultInfo
                {
                    Result = IPlugInAPI.enumPollResult.OK,
                    Value = HS.DeviceValueEx(deviceId),
                };

                return pollResult;
            }
            else
            {
                return base.PollDevice(deviceId);
            }
        }

        public override string PostBackProc(string page, string data, [AllowNull]string user, int userRights)
        {
            if (page == ConfigPage.Name)
            {
                return configPage.PostBackProc(data, user, userRights);
            }

            return string.Empty;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (pluginConfig != null)
                {
                    pluginConfig.ConfigChanged -= PluginConfig_ConfigChanged;
                }

                configPage?.Dispose();

                Shutdown();
                disposedValue = true;
            }

            base.Dispose(disposing);
        }

        private static async Task RecordDeviceValue(InfluxDBMeasurementsCollector collector,
                                                    IHSApplication HS,
                                                    DeviceClass device)
        {
            if (device != null)
            {
                int deviceRefId = device.get_Ref(HS);
                bool notValid = HS.get_DeviceInvalidValue(deviceRefId);
                if (!notValid)
                {
                    double deviceValue = device.get_devValue(HS);
                    string deviceString = HS.DeviceString(deviceRefId);
                    if (string.IsNullOrWhiteSpace(deviceString))
                    {
                        deviceString = HS.DeviceVSP_GetStatus(deviceRefId, deviceValue, ePairStatusControl.Status);
                    }
                    Trace.WriteLine(Invariant($"Recording Device Ref Id: {deviceRefId} with [{deviceValue}] & [{deviceString}]"));

                    DateTime lastChange = device.get_Last_Change(HS);

                    RecordData recordData = new RecordData(deviceRefId,
                                                           deviceValue,
                                                           deviceString,
                                                           device.get_Name(HS),
                                                           device.get_Location(HS),
                                                           device.get_Location2(HS),
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

        private async Task HSEventImpl(Enums.HSEvent eventType, object[] parameters)
        {
            try
            {
                if ((eventType == Enums.HSEvent.VALUE_CHANGE) && (parameters.Length > 4))
                {
                    int deviceRefId = Convert.ToInt32(parameters[4], CultureInfo.InvariantCulture);
                    await RecordDeviceValue(deviceRefId, TrackedType.Value).ConfigureAwait(false);
                }
                else if ((eventType == Enums.HSEvent.STRING_CHANGE) && (parameters.Length > 3))
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

        private void PluginConfig_ConfigChanged(object sender, EventArgs e)
        {
            RestartProcessing();
        }

        private async Task RecordDeviceValue(int deviceRefId, TrackedType trackedType)
        {
            var collector = await GetInfluxDBMeasurementsCollector().ConfigureAwait(false);
            if ((collector != null) && collector.IsTracked(deviceRefId, trackedType))
            {
                var device = HS.GetDeviceByRef(deviceRefId) as DeviceClass;
                await RecordDeviceValue(collector, HS, device).ConfigureAwait(false);
            }
        }

        private async Task RecordTrackedDevices()
        {
            var collector = await GetInfluxDBMeasurementsCollector().ConfigureAwait(false);
            if (collector != null)
            {
                var deviceEnumerator = HS.GetDeviceEnumerator() as clsDeviceEnumeration;
                do
                {
                    DeviceClass device = deviceEnumerator.GetNext();
                    if (device != null)
                    {
                        if (collector.IsTracked(device.get_Ref(HS), null))
                        {
                            await RecordDeviceValue(collector, HS, device).ConfigureAwait(false);
                        }
                    }
                    ShutdownCancellationToken.ThrowIfCancellationRequested();
                }
                while (!deviceEnumerator.Finished);
            }
        }

        private void RegisterConfigPage()
        {
            string link = ConfigPage.Name;
            HS.RegisterPage(link, Name, string.Empty);

            var wpd = new HomeSeerAPI.WebPageDesc()
            {
                plugInName = Name,
                link = link,
                linktext = "Configuration",
                page_title = Invariant($"{Name} Configuration"),
            };
            Callback.RegisterConfigLink(wpd);
            Callback.RegisterLink(wpd);
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
                deviceRootDeviceManager = new DeviceRootDeviceManager(HS,
                                                                      pluginConfig.DBLoginInformation,
                                                                      pluginConfig.ImportDevicesData,
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

            // do not leave thread to avoid leaving thread as it can delay startup
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
            await RecordTrackedDevices();
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
        }

        #region "Action Override"

        public override string ActionBuildUI([AllowNull]string uniqueControlId, IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionRefreshTANumber:
                        return configPage.GetRefreshActionUI(uniqueControlId ?? string.Empty, actionInfo);

                    default:
                        return base.ActionBuildUI(uniqueControlId, actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to give build Action UI with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override bool ActionConfigured(IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionRefreshTANumber:
                        if (actionInfo.DataIn != null)
                        {
                            RefreshDeviceAction refreshDeviceAction = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as RefreshDeviceAction;
                            if ((refreshDeviceAction != null) && (refreshDeviceAction.DeviceRefId != 0))
                            {
                                return HS.GetDeviceByRef(refreshDeviceAction.DeviceRefId) != null;
                            }
                        }

                        return false;

                    default:
                        return base.ActionConfigured(actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to ActionConfigured with {ex.GetFullMessage()}"));
                return false;
            }
        }

        public override int ActionCount()
        {
            return 1;
        }

        public override string ActionFormatUI(IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionRefreshTANumber:
                        if (actionInfo.DataIn != null)
                        {
                            var refreshDeviceAction = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as RefreshDeviceAction;
                            if ((refreshDeviceAction != null) && (refreshDeviceAction.DeviceRefId != 0))
                            {
                                HSHelper hSHelper = new HSHelper(HS);
                                return Invariant($"Refresh {hSHelper.GetName(refreshDeviceAction.DeviceRefId)} from Influx DB");
                            }
                        }
                        return Invariant($"{PlugInData.PlugInName} Unknown Device Import Refresh");

                    default:
                        return base.ActionFormatUI(actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to ActionFormatUI with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override IPlugInAPI.strMultiReturn ActionProcessPostUI([AllowNull] NameValueCollection postData, IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionRefreshTANumber:
                        return ConfigPage.GetRefreshActionPostUI(postData, actionInfo);

                    default:
                        return base.ActionProcessPostUI(postData, actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to ActionProcessPostUI with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override bool ActionReferencesDevice(IPlugInAPI.strTrigActInfo actionInfo, int deviceId)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionRefreshTANumber:
                        if (actionInfo.DataIn != null)
                        {
                            var refreshDeviceAction = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as RefreshDeviceAction;
                            return (refreshDeviceAction != null) && (refreshDeviceAction.DeviceRefId == deviceId);
                        }
                        return false;

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to ActionReferencesDevice with {ex.GetFullMessage()}"));
                return false;
            }
        }

        public override string get_ActionName(int actionNumber)
        {
            try
            {
                switch (actionNumber)
                {
                    case ActionRefreshTANumber:
                        return Invariant($"{Name}:Refresh From InfluxDB");

                    default:
                        return base.get_ActionName(actionNumber);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to give Action Name with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override bool HandleAction(IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionRefreshTANumber:
                        if (actionInfo.DataIn != null)
                        {
                            var refreshDeviceAction = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as RefreshDeviceAction;
                            if ((refreshDeviceAction != null) && (refreshDeviceAction.DeviceRefId != 0))
                            {
                                if (ImportDeviceFromDB(refreshDeviceAction.DeviceRefId))
                                {
                                    return true;
                                }
                            }
                        }
                        Trace.TraceWarning(Invariant($"Failed to execute action with Device not Found"));
                        return false;

                    default:
                        return base.HandleAction(actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(Invariant($"Failed to execute action with {ex.GetFullMessage()}"));
                return false;
            }
        }

        private bool ImportDeviceFromDB(int deviceRefId)
        {
            DeviceRootDeviceManager deviceRootDeviceManagerCopy;
            using (var sync = deviceRootDeviceManagerLock.Enter())
            {
                deviceRootDeviceManagerCopy = deviceRootDeviceManager;
            }

            return deviceRootDeviceManagerCopy.ImportDataForDevice(deviceRefId).ResultForSync();
        }

        #endregion "Action Override"

        private const int ActionRefreshTANumber = 1;
        private readonly AsyncMonitor deviceRootDeviceManagerLock = new AsyncMonitor();
        private readonly AsyncMonitor influxDBMeasurementsCollectorLock = new AsyncMonitor();
        private ConfigPage configPage;
        private DeviceRootDeviceManager deviceRootDeviceManager;
        private bool disposedValue;
        private InfluxDBMeasurementsCollector influxDBMeasurementsCollector;
        private PluginConfig pluginConfig;
    }
}