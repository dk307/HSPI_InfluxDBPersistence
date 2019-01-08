using Nito.AsyncEx.Synchronous;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi.Utils
{
    internal static class TaskHelper
    {
        public static T ResultForSync<T>(this Task<T> @this)
        {
            // https://blogs.msdn.microsoft.com/pfxteam/2012/04/13/should-i-expose-synchronous-wrappers-for-asynchronous-methods/
            return Task.Run(() => @this).Result;
        }

        public static Task StartAsync(Func<Task> taskAction, CancellationToken token)
        {
            var task = Task.Factory.StartNew(() => taskAction(), token,
                                          TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                          TaskScheduler.Current).WaitAndUnwrapException(token);
            return task;
        }

        public static Task StartAsync(Action action, CancellationToken token)
        {
            var task = Task.Factory.StartNew(action, token,
                                          TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                          TaskScheduler.Current);
            return task;
        }
    }
}