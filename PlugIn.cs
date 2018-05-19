using HomeSeerAPI;
using Hspi.DeviceData;
using NullGuard;
using Scheduler.Classes;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi
{
    using static System.FormattableString;

    /// <summary>
    /// Plugin class
    /// </summary>
    /// <seealso cref="Hspi.HspiBase" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class PlugIn : HspiBase
    {
        public PlugIn()
            : base(PlugInData.PlugInName, supportConfigDevice: true)
        {
        }

        private InfluxDBMeasurementsCollector InfluxDBMeasurementsCollector
        {
            get
            {
                lock (influxDBMeasurementsCollectorLock)
                {
                    return influxDBMeasurementsCollector;
                }
            }
        }

        public override string ConfigDevice(int deviceId, [AllowNull] string user, int userRights, bool newDevice)
        {
            if (newDevice)
            {
                return string.Empty;
            }

            try
            {
                DeviceClass deviceClass = (DeviceClass)HS.GetDeviceByRef(deviceId);
                var deviceIdentifier = DeviceIdentifier.Identify(deviceClass);

                if (deviceIdentifier != null)
                {
                    foreach (var device in pluginConfig.ImportDevicesData)
                    {
                        if (device.Key == deviceIdentifier.DeviceId)
                        {
                            StringBuilder stb = new StringBuilder();

                            stb.Append(@"<table style='width:100%;border-spacing:0px;'");
                            stb.Append("<tr height='5'><td style='width:25%'></td><td style='width:20%'></td><td style='width:55%'></td></tr>");
                            stb.Append(Invariant($"<tr><td class='tableheader' colspan=3>Import Settings</td></tr>"));
                            stb.Append(Invariant($"<tr><td class='tablecell'>Name:</td><td class='tablecell' colspan=2>{device.Value.Name ?? string.Empty}</td></tr>"));
                            stb.Append(Invariant($"<tr><td class='tablecell'>Sql:</td><td class='tablecell' colspan=2>{device.Value.Sql ?? string.Empty}</td></tr>"));
                            stb.Append(Invariant($"<tr><td class='tablecell'>Refresh Interval(seconds):</td><td class='tablecell' colspan=2>{device.Value.Interval.TotalSeconds}</td></tr>"));
                            stb.Append(Invariant($"<tr><td class='tablecell'>Unit:</td><td class='tablecell' colspan=2>{device.Value.Unit ?? string.Empty}</td></tr>"));
                            stb.Append(Invariant($"</td><td></td></tr>"));
                            stb.Append("<tr height='5'><td colspan=3></td></tr>");
                            stb.Append(@" </table>");

                            return stb.ToString();
                        }
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                LogError(Invariant($"ConfigDevice for {deviceId} With {ex.Message}"));
                return string.Empty;
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
            try
            {
                if ((eventType == Enums.HSEvent.VALUE_CHANGE) && (parameters.Length > 4))
                {
                    int deviceRefId = Convert.ToInt32(parameters[4]);
                    RecordDeviceValue(deviceRefId).Wait(ShutdownCancellationToken);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(Invariant($"Failed to process HSEvent {eventType} with {ex.GetFullMessage()}"));
            }
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

                RestartProcessing();

                Trace.TraceInformation("Plugin Started");
            }
            catch (Exception ex)
            {
                result = Invariant($"Failed to initialize PlugIn With {ex.GetFullMessage()}");
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
                if (configPage != null)
                {
                    configPage.Dispose();
                }

                if (pluginConfig != null)
                {
                    pluginConfig.Dispose();
                }

                if (collectionShutdownToken != null)
                {
                    collectionShutdownToken.Dispose();
                }
                disposedValue = true;
            }

            base.Dispose(disposing);
        }

        private static async Task RecordDeviceValue(InfluxDBMeasurementsCollector collector, IHSApplication HS, DeviceClass device)
        {
            if (device != null)
            {
                int deviceRefId = device.get_Ref(HS);
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

        private async Task RecordDeviceValue(int deviceRefId)
        {
            var collector = InfluxDBMeasurementsCollector;
            if ((collector != null) && collector.IsTracked(deviceRefId))
            {
                DeviceClass device = HS.GetDeviceByRef(deviceRefId) as DeviceClass;
                await RecordDeviceValue(collector, HS, device).ConfigureAwait(false);
            }
        }

        private async Task RecordTrackedDevices()
        {
            var collector = InfluxDBMeasurementsCollector;
            if (collector != null)
            {
                var deviceEnumerator = HS.GetDeviceEnumerator() as clsDeviceEnumeration;
                do
                {
                    DeviceClass device = deviceEnumerator.GetNext();
                    if (device != null)
                    {
                        if (collector.IsTracked(device.get_Ref(HS)))
                        {
                            await RecordDeviceValue(collector, HS, device); // keep in same thread
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

            HomeSeerAPI.WebPageDesc wpd = new HomeSeerAPI.WebPageDesc()
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
            Task.Factory.StartNew(StartInfluxDBMeasurementsCollector, ShutdownCancellationToken, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);
            Task.Factory.StartNew(StartDeviceImport, ShutdownCancellationToken, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);
        }

        private void StartDeviceImport()
        {
            lock (deviceRootDeviceManagerLock)
            {
                if (deviceRootDeviceManager != null)
                {
                    deviceRootDeviceManager.Cancel();
                    deviceRootDeviceManager.Dispose();
                }

                deviceRootDeviceManager = new DeviceRootDeviceManager(HS,
                                                                      pluginConfig.DBLoginInformation,
                                                                      pluginConfig.ImportDevicesData,
                                                                      ShutdownCancellationToken);
            }
        }

        private async Task StartInfluxDBMeasurementsCollector()
        {
            lock (influxDBMeasurementsCollectorLock)
            {
                bool recreate = (influxDBMeasurementsCollector == null) || (!influxDBMeasurementsCollector.LoginInformation.Equals(pluginConfig.DBLoginInformation));

                if (recreate)
                {
                    collectionShutdownToken?.Cancel();
                    collectionShutdownToken = new CancellationTokenSource();
                    if (pluginConfig.DBLoginInformation.IsValid)
                    {
                        influxDBMeasurementsCollector = new InfluxDBMeasurementsCollector(pluginConfig.DBLoginInformation);
                        influxDBMeasurementsCollector.Start(pluginConfig.DevicePersistenceData.Values,
                                          CancellationTokenSource.CreateLinkedTokenSource(collectionShutdownToken.Token, ShutdownCancellationToken).Token);
                    }
                }
                else
                {
                    influxDBMeasurementsCollector.UpdatePeristenceData(pluginConfig.DevicePersistenceData.Values);
                }

                RecordTrackedDevices();
            }
        }

        private readonly object deviceRootDeviceManagerLock = new object();
        private readonly object influxDBMeasurementsCollectorLock = new object();
        private CancellationTokenSource collectionShutdownToken;
        private ConfigPage configPage;
        private DeviceRootDeviceManager deviceRootDeviceManager;
        private bool disposedValue = false;
        private InfluxDBMeasurementsCollector influxDBMeasurementsCollector;
        private PluginConfig pluginConfig;
    }
}