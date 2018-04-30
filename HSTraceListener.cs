using System;
using System.Diagnostics;
using System.Globalization;

namespace Hspi
{
    internal class HSTraceListener : TraceListener
    {
        public HSTraceListener(ILogger logger)
        {
            loggerWeakReference = new WeakReference<ILogger>(logger);
        }

        public override void Write(string message)
        {
            LogDebug(message);
        }

        public override void WriteLine(string message)
        {
            LogDebug(message);
        }

        public override bool IsThreadSafe => true;


        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            if (Filter != null && !Filter.ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
            {
                return;
            }

            TraceEvent(eventCache, source, eventType, id, string.Format(CultureInfo.InvariantCulture, format, args));
        }

        public override void TraceEvent(TraceEventCache eventCache, String source, TraceEventType eventType, int id, string message)
        {
            if (Filter != null && !Filter.ShouldTrace(eventCache, source, eventType, id, message, null, null, null))
            {
                return;
            }

            if (loggerWeakReference.TryGetTarget(out var logger))
            {
                switch (eventType)
                {
                    case TraceEventType.Critical:
                    case TraceEventType.Error:
                        logger.LogError(message);
                        break;
                    case TraceEventType.Warning:
                        logger.LogWarning(message);
                        break;
                    case TraceEventType.Information:
                        logger.LogInfo(message);
                        break;
                    case TraceEventType.Verbose:
                        logger.LogDebug(message);
                        break;
                    case TraceEventType.Start:
                    case TraceEventType.Stop:
                    case TraceEventType.Suspend:
                    case TraceEventType.Resume:
                    case TraceEventType.Transfer:
                        logger.LogInfo(message);
                        break;
                }
            }
        }

        private void LogDebug(string message)
        {
            if (loggerWeakReference.TryGetTarget(out var logger))
            {
                logger.LogDebug(message);
            }
        }

        private readonly WeakReference<ILogger> loggerWeakReference;
    }
}