using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Marshaling explicito al hilo UI del widget (Game Bar no siempre tiene SynchronizationContext).
    /// </summary>
    internal static class CoreUiDispatcher
    {
        private static CoreDispatcher _dispatcher;

        public static void Bind(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public static bool IsOnUiThread =>
            _dispatcher != null && _dispatcher.HasThreadAccess;

        public static Task RunOnUiAsync(Action action)
        {
            if (action == null)
            {
                return Task.CompletedTask;
            }

            return RunOnUiAsync(() =>
            {
                action();
                return Task.CompletedTask;
            });
        }

        public static Task RunOnUiAsync(Func<Task> action)
        {
            if (action == null)
            {
                return Task.CompletedTask;
            }

            if (_dispatcher == null || _dispatcher.HasThreadAccess)
            {
                return action();
            }

            var tcs = new TaskCompletionSource<bool>();
            var ignored = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var task = action() ?? Task.CompletedTask;
                task.ContinueWith(
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
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
            });
            return tcs.Task;
        }

        public static Task<T> RunOnUiAsync<T>(Func<Task<T>> action)
        {
            if (action == null)
            {
                return Task.FromResult(default(T));
            }

            if (_dispatcher == null || _dispatcher.HasThreadAccess)
            {
                return action();
            }

            var tcs = new TaskCompletionSource<T>();
            var ignored = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var task = action() ?? Task.FromResult(default(T));
                task.ContinueWith(
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
                            tcs.TrySetResult(t.Result);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
            });
            return tcs.Task;
        }
    }
}
