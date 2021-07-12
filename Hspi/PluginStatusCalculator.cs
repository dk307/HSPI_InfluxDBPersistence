using HomeSeer.PluginSdk;
using Hspi.DeviceData;
using Nito.AsyncEx;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Hspi
{
    internal sealed class PluginStatusCalculator
    {
        public PluginStatusCalculator(IHsController hs, TimeSpan erroredDelay, CancellationToken token)
        {
            statusDevice = InfluxDbStatusDevice.CreateOrGet(hs);

            this.erroredDelay = erroredDelay;
            this.token = token;
            stopwatch.Start();

            Utils.TaskHelper.StartAsyncWithErrorChecking("Device Status", UpdateWorkingState, token);
        }

        public async Task ExportWorked(CancellationToken cancellationToken)
        {
            using var _ = await instanceLock.LockAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Restart();
        }

        private async Task UpdateWorkingState()
        {
            bool? currentState = null;
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(30000, token).ConfigureAwait(false);

                using var _ = await instanceLock.LockAsync(token).ConfigureAwait(false);
                bool newState = stopwatch.Elapsed > erroredDelay;

                if (newState != currentState)
                {
                    statusDevice.UpdateExportConnectionStatus(!newState);
                    currentState = newState;
                }
            }
        }

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly AsyncLock instanceLock = new AsyncLock();
        private readonly InfluxDbStatusDevice statusDevice;
        private readonly TimeSpan erroredDelay;
        private readonly CancellationToken token;
    }
}