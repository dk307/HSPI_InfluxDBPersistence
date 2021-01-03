using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using NullGuard;
using System;
using System.Threading;

namespace Hspi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal abstract class HspiBase : AbstractPlugin, IDisposable
    {
        protected HspiBase(string id, string name)
        {
            this.id = id;
            this.name = name;
        }

        public override string Id => id;
        public override string Name => name;

        protected CancellationToken ShutdownCancellationToken => cancellationTokenSource.Token;

        public void Dispose()
        {
            Dispose(true);
        }

        protected override void OnShutdown()
        {
            cancellationTokenSource.Cancel();
        }

        protected override bool OnSettingChange(string pageId, AbstractView currentView, AbstractView changedView)
        {
            return true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cancellationTokenSource.Dispose();
                }
                disposedValue = true;
            }
        }

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly string id;
        private readonly string name;
        private bool disposedValue;
    }
}