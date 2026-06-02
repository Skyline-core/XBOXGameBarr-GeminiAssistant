using System;
using System.Threading;
using Windows.UI.Core;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Permite que ConfigureAwait(true) vuelva al hilo del widget en Game Bar.
    /// </summary>
    internal sealed class CoreDispatcherSyncContext : SynchronizationContext
    {
        private readonly CoreDispatcher _dispatcher;

        public CoreDispatcherSyncContext(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public override SynchronizationContext CreateCopy()
        {
            return this;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            if (d == null)
            {
                return;
            }

            if (_dispatcher.HasThreadAccess)
            {
                d(state);
                return;
            }

            var ignored = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => d(state));
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            if (d == null)
            {
                return;
            }

            if (_dispatcher.HasThreadAccess)
            {
                d(state);
                return;
            }

            using (var gate = new ManualResetEventSlim(false))
            {
                Exception captured = null;
                var ignored = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        d(state);
                    }
                    catch (Exception ex)
                    {
                        captured = ex;
                    }
                    finally
                    {
                        gate.Set();
                    }
                });

                gate.Wait();
                if (captured != null)
                {
                    throw captured;
                }
            }
        }
    }
}
