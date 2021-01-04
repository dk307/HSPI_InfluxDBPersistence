using HomeSeer.PluginSdk;
using NLog;
using NLog.Targets;
using NullGuard;
using System;
using System.Globalization;

namespace Hspi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal static class Logger
    {
        public static void ConfigureLogging(bool enableLogging, [AllowNull] IHsController hsController = null)
        {
            var config = new NLog.Config.LoggingConfiguration();
            config.DefaultCultureInfo = CultureInfo.InvariantCulture;
#pragma warning disable CA2000 // Dispose objects before losing scope
            var logconsole = new ConsoleTarget("logconsole");
#pragma warning restore CA2000 // Dispose objects before losing scope

            LogLevel minLevel = enableLogging ? LogLevel.Debug : LogLevel.Info;

            if (hsController != null)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                var hsTarget = new HomeSeerTarget(hsController);
#pragma warning restore CA2000 // Dispose objects before losing scope
                config.AddRule(minLevel, LogLevel.Fatal, hsTarget);
            }
            config.AddRule(enableLogging ? LogLevel.Debug : LogLevel.Info, LogLevel.Fatal, logconsole);

            NLog.LogManager.Configuration = config;
        }

        [Target("homeseer")]
        public sealed class HomeSeerTarget : Target
        {
            public HomeSeerTarget(IHsController hsController = null)
            {
                this.loggerWeakReference = new WeakReference<IHsController>(hsController);
            }

            protected override void Write(LogEventInfo logEvent)
            {
                if (loggerWeakReference.TryGetTarget(out var logger))
                {
                    if (logEvent.Level.Equals(LogLevel.Debug))
                    {
                        logger?.WriteLog(HomeSeer.PluginSdk.Logging.ELogType.Debug, logEvent.FormattedMessage, PlugInData.PlugInName);
                    }
                    else if (logEvent.Level.Equals(LogLevel.Info))
                    {
                        logger?.WriteLog(HomeSeer.PluginSdk.Logging.ELogType.Info, logEvent.FormattedMessage, PlugInData.PlugInName);
                    }
                    else if (logEvent.Level.Equals(LogLevel.Warn))
                    {
                        logger?.WriteLog(HomeSeer.PluginSdk.Logging.ELogType.Warning, logEvent.FormattedMessage, PlugInData.PlugInName, "#D58000");
                    }
                    else if (logEvent.Level >= LogLevel.Error)
                    {
                        logger?.WriteLog(HomeSeer.PluginSdk.Logging.ELogType.Error, logEvent.FormattedMessage, PlugInData.PlugInName, "#FF0000");
                    }
                }
            }

            private readonly WeakReference<IHsController> loggerWeakReference;
        }
    }
}