using HomeSeer.PluginSdk;
using Hspi.DeviceData;
using Nito.AsyncEx;
 using System.Threading;
using System.Threading.Tasks;

namespace Hspi
{
     internal sealed class PluginStatusCalculator
    {
        public PluginStatusCalculator(IHsController hs)
        {
            statusDevice = InfluxDbStatusDevice.CreateOrGet(hs);
        }

        public PluginStatus PluginStatus
        {
            get
            {
                if (exportWorkingStatus.HasValue && !exportWorkingStatus.Value)
                {
                    return PluginStatus.Warning("Exporting to InfluxDB is failing");
                }

                return PluginStatus.Ok();
            }
        }

        public async Task ExportErrored(CancellationToken cancellationToken)
        {
            using (var lockInterval = await instanceLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                UpdateExportworkingStatus(false);
            }
        }

        public async Task ExportWorked(CancellationToken cancellationToken)
        {
            using (var lockInterval = await instanceLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                UpdateExportworkingStatus(true);
            }
        }

        private void UpdateExportworkingStatus(bool connected)
        {
            if (!exportWorkingStatus.HasValue || (exportWorkingStatus.Value != connected))
            {
                exportWorkingStatus = connected;
                statusDevice.UpdateExportConnectionStatus(connected);
            }
        }

        private readonly AsyncLock instanceLock = new AsyncLock();
        private readonly InfluxDbStatusDevice statusDevice;
        private bool? exportWorkingStatus;
    }
}