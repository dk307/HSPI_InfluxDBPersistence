using HomeSeerAPI;
using NullGuard;
using System;
using System.Diagnostics;
using System.Threading;

namespace Hspi
{
    using Scheduler.Classes;
    using System.Threading.Tasks;
    using static System.FormattableString;

    /// <summary>
    /// Plugin class
    /// </summary>
    /// <seealso cref="Hspi.HspiBase" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class PlugIn : HspiBase
    {
        public PlugIn()
            : base(PlugInData.PlugInName)
        {
        }

        private InfluxDBMeasurementsCollector DbCollector
        {
            get { lock (collectorLock) { return dbCollector; } }
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

                StartCollector();
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
                string deviceString = device.get_devString(HS);
                Trace.WriteLine(Invariant($"Recording Device Ref Id: {deviceRefId}"));

                //string deviceVSP = GetDeviceVSP(num, num2);
                RecordData recordData = new RecordData(deviceRefId,
                                                       deviceValue,
                                                       deviceString,
                                                       device.get_Name(HS),
                                                       device.get_Location(HS),
                                                       device.get_Location2(HS),
                                                       device.get_Last_Change(HS));

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
            StartCollector();
        }

        private async Task RecordDeviceValue(int deviceRefId)
        {
            var collector = DbCollector;
            if ((collector != null) && collector.IsTracked(deviceRefId))
            {
                DeviceClass device = HS.GetDeviceByRef(deviceRefId) as DeviceClass;
                await RecordDeviceValue(collector, HS, device).ConfigureAwait(false);
            }
        }

        private async Task RecordTrackedDevices()
        {
            var collector = DbCollector;
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

        private void StartCollector()
        {
            lock (collectorLock)
            {
                bool recreate = (dbCollector == null) || (!dbCollector.LoginInformation.Equals(pluginConfig.DBLoginInformation));

                if (recreate)
                {
                    collectionShutdownToken?.Cancel();
                    collectionShutdownToken = new CancellationTokenSource();
                    if (pluginConfig.DBLoginInformation.IsValid)
                    {
                        dbCollector = new InfluxDBMeasurementsCollector(pluginConfig.DBLoginInformation);
                        dbCollector.Start(pluginConfig.DevicePersistenceData.Values,
                                          CancellationTokenSource.CreateLinkedTokenSource(collectionShutdownToken.Token, ShutdownCancellationToken).Token);
                    }
                }
                else
                {
                    dbCollector.UpdatePeristenceData(pluginConfig.DevicePersistenceData.Values);
                }

                Task.Factory.StartNew(RecordTrackedDevices, ShutdownCancellationToken, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);
            }
        }

        private CancellationTokenSource collectionShutdownToken;
        private object collectorLock = new object();
        private ConfigPage configPage;
        private InfluxDBMeasurementsCollector dbCollector;
        private bool disposedValue = false;
        private PluginConfig pluginConfig;
    }
}