using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.Utils
{
    internal static class TaskHelper
    {
        public static T ResultForSync<T>(this Task<T> @this)
        {
            // https://blogs.msdn.microsoft.com/pfxteam/2012/04/13/should-i-expose-synchronous-wrappers-for-asynchronous-methods/
            return Task.Run(() => @this).Result;
        }

        public static void StartAsyncWithErrorChecking(string taskName,
                                                       Func<Task> taskAction,
                                                       CancellationToken token)
        {
            var task = Task.Factory.StartNew(() => RunInLoop(taskName, taskAction, token), token,
                                         TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                         TaskScheduler.Current);
        }

        private static async Task RunInLoop(string taskName, Func<Task> taskAction, CancellationToken token)
        {
            bool loop = true;
            while (loop && !token.IsCancellationRequested)
            {
                try
                {
                    Trace.WriteLine(Invariant($"{taskName} Starting"));
                    await taskAction().ConfigureAwait(false);
                    Trace.WriteLine(Invariant($"{taskName} Finished"));
                    loop = false;  //finished sucessfully
                }
                catch (Exception ex)
                {
                    if (ex.IsCancelException())
                    {
                        throw;
                    }

                    Trace.TraceError(Invariant($"{taskName} failed with {ex.GetFullMessage()}. Restarting ..."));
                }
            }
        }
    }
}