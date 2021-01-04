using HomeSeer.PluginSdk;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi
{
    public sealed class PluginStatusCalculator
    {
        public PluginStatus PluginStatus => pluginStatus;

        public async Task DBConnectionFailed(CancellationToken cancellationToken)
        {
            using (var lockInterval = await instanceLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                if (pluginStatus.Status != PluginStatus.EPluginStatus.Critical)
                {
                    logger.Error("Plugin is in critical state as connection to Influx DB is lost");
                    pluginStatus = PluginStatus.Critical("Influx DB is inaccessible");
                }
            }
        }

        public async Task DeviceErrored(int refId, CancellationToken cancellationToken)
        {
            using (var lockInterval = await instanceLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                lock (erroredDevices)
                {
                    erroredDevices.Add(refId);
                }

                SetOkState();
            }
        }

        public async Task DeviceWorked(int refId, CancellationToken cancellationToken)
        {
            using (var lockInterval = await instanceLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                lock (erroredDevices)
                {
                    erroredDevices.Remove(refId);
                }

                SetOkState();
            }
        }

        public async Task PluginConfigurationChanged(CancellationToken cancellationToken)
        {
            using (var lockInterval = await instanceLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                erroredDevices.Clear();
                SetOkState();
            }
        }

        private void SetOkState()
        {
            if (pluginStatus.Status == PluginStatus.EPluginStatus.Critical)
            {
                logger.Info("Plugin is back in working state");
            }

            if (erroredDevices.Count > 0)
            {
                if (pluginStatus.Status != PluginStatus.EPluginStatus.Warning)
                {
                    pluginStatus = PluginStatus.Warning("Some Devices Errored");
                }
            }
            else
            {
                if (pluginStatus.Status != PluginStatus.EPluginStatus.Ok)
                {
                    pluginStatus = PluginStatus.Ok();
                }
            }
        }

        private readonly AsyncLock instanceLock = new AsyncLock();
        private readonly HashSet<int> erroredDevices = new HashSet<int>();
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private volatile PluginStatus pluginStatus = PluginStatus.Ok();
    }
}