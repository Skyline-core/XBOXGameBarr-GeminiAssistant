using System;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace GeminiAssistant.Extensions
{
    public static class DispatcherTaskExtensions
    {
        public static Task RunTaskAsync(
            this CoreDispatcher dispatcher,
            Func<Task> func,
            CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (dispatcher.HasThreadAccess)
            {
                return func();
            }

            var tcs = new TaskCompletionSource<bool>();
            var ignored = dispatcher.RunAsync(priority, () =>
            {
                func().ContinueWith(
                    t =>
                    {
                        if (t.IsCanceled)
                        {
                            tcs.TrySetCanceled();
                        }
                        else if (t.IsFaulted)
                        {
                            tcs.TrySetException(t.Exception?.GetBaseException() ?? t.Exception);
                        }
                        else
                        {
                            tcs.TrySetResult(true);
                        }
                    },
                    TaskScheduler.FromCurrentSynchronizationContext());
            });
            return tcs.Task;
        }
    }
}
