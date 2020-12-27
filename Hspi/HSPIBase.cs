using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using NullGuard;
using System;
using System.Diagnostics;
using System.Threading;

namespace Hspi
{
    using static System.FormattableString;

    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal abstract class HspiBase : AbstractPlugin, ILogger, IDisposable
    {
        protected HspiBase(string id, string name)
        {
            this.id = id;
            this.name = name;
        }

        public override string Id => id;
        public override string Name => name;

        protected CancellationToken ShutdownCancellationToken => cancellationTokenSource.Token;

        protected override void Initialize()
        {
            hsTraceListener = new HSTraceListener(this as ILogger);
            Debug.Listeners.Add(hsTraceListener);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public bool EnableLogDebug
        {
            get { return base.LogDebug; }
            set { base.LogDebug = value; }
        }

        public new virtual void LogDebug(string message)
        {
            if (EnableLogDebug)
            {
                HomeSeerSystem?.WriteLog(HomeSeer.PluginSdk.Logging.ELogType.Debug, Invariant($"Debug:{message}"), Name);
            }
        }

        public void LogError(string message)
        {
            HomeSeerSystem?.WriteLog(HomeSeer.PluginSdk.Logging.ELogType.Error, Invariant($"Error:{message}"), Name, "#FF0000");
        }

        public void LogInfo(string message)
        {
            HomeSeerSystem?.WriteLog(HomeSeer.PluginSdk.Logging.ELogType.Info, Invariant($"Error:{message}"), Name);
        }

        public void LogWarning(string message)
        {
            HomeSeerSystem?.WriteLog(HomeSeer.PluginSdk.Logging.ELogType.Warning, Invariant($"Error:{message}"), Name, "#D58000");
        }

        protected override void OnShutdown()
        {
            Trace.WriteLine("Disconnecting Hspi Connection");
            cancellationTokenSource.Cancel();

            if (hsTraceListener != null)
            {
                Debug.Listeners.Remove(hsTraceListener);
            }

            Trace.WriteLine("Disconnected Hspi Connection");
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
                    hsTraceListener?.Dispose();
                    cancellationTokenSource.Dispose();
                }
                disposedValue = true;
            }
        }

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly string id;
        private readonly string name;
        private bool disposedValue;
        private HSTraceListener hsTraceListener;
    }
}