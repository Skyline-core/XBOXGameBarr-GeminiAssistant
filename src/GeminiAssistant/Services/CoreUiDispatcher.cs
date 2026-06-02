using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Marshaling explicito al hilo UI del widget (Game Bar no restaura SynchronizationContext).
    /// </summary>
    internal static class CoreUiDispatcher
    {
        private static CoreDispatcher _dispatcher;
        private static CoreDispatcherSyncContext _syncContext;

        public static void Bind(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _syncContext = dispatcher != null ? new CoreDispatcherSyncContext(dispatcher) : null;
            SynchronizationContext.SetSynchronizationContext(_syncContext);
        }

        public static bool IsOnUiThread =>
            _dispatcher != null && _dispatcher.HasThreadAccess;

        /// <summary>
        /// Tras un await, asegura que el codigo siguiente corre en el hilo UI del widget.
        /// </summary>
        public static Task YieldToUiAsync()
        {
            if (_dispatcher == null || _dispatcher.HasThreadAccess)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            var ignored = _dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () => tcs.TrySetResult(true));
            return tcs.Task;
        }

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
                return InvokeOnUiAsync(action);
            }

            var tcs = new TaskCompletionSource<bool>();
            var ignored = _dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () => RunOnUiThread(action, tcs));
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
                return InvokeOnUiAsync(action);
            }

            var tcs = new TaskCompletionSource<T>();
            var ignored = _dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () => RunOnUiThread(action, tcs));
            return tcs.Task;
        }

        private static async void RunOnUiThread(Func<Task> action, TaskCompletionSource<bool> tcs)
        {
            try
            {
                await action().ConfigureAwait(true);
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        private static async void RunOnUiThread<T>(Func<Task<T>> action, TaskCompletionSource<T> tcs)
        {
            try
            {
                var result = await action().ConfigureAwait(true);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        private static async Task InvokeOnUiAsync(Func<Task> action)
        {
            await action().ConfigureAwait(true);
        }

        private static async Task<T> InvokeOnUiAsync<T>(Func<Task<T>> action)
        {
            return await action().ConfigureAwait(true);
        }
    }
}
