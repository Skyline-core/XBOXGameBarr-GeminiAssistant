using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.ExtendedExecution;

namespace GeminiAssistant.Services
{
    internal sealed class WidgetKeepAlive : IDisposable
    {
        private ExtendedExecutionSession _session;
        private bool _active;

        public bool IsActive => _active;

        public static Task<WidgetKeepAlive> BeginAsync()
        {
            return CoreUiDispatcher.RunOnUiAsync(async () =>
            {
                var lease = new WidgetKeepAlive();
                await lease.ActivateAsync().ConfigureAwait(true);
                return lease;
            });
        }

        public static Task ReleaseAsync(WidgetKeepAlive lease)
        {
            if (lease == null)
            {
                return Task.CompletedTask;
            }

            return CoreUiDispatcher.RunOnUiAsync(() =>
            {
                lease.DisposeCore();
                return Task.CompletedTask;
            });
        }

        private async Task ActivateAsync()
        {
            WidgetFileLog.Write("KeepAlive: solicitar extension");

            try
            {
                _session = new ExtendedExecutionSession
                {
                    Reason = ExtendedExecutionReason.Unspecified,
                    Description = "Gemini Assistant API"
                };
                _session.Revoked += OnSessionRevoked;

                var result = await _session.RequestExtensionAsync().AsTask().ConfigureAwait(true);
                WidgetFileLog.Write("KeepAlive: extension=" + result);
                _active = true;
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("KeepAlive: no disponible " + WidgetExceptionFormatter.Format(ex));
                if (_session != null)
                {
                    _session.Revoked -= OnSessionRevoked;
                    _session.Dispose();
                    _session = null;
                }

                _active = false;
            }
        }

        private void OnSessionRevoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            WidgetFileLog.Write("KeepAlive: revocado " + args.Reason);
        }

        private void DisposeCore()
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            if (_session != null)
            {
                _session.Revoked -= OnSessionRevoked;
                _session.Dispose();
                _session = null;
            }

            WidgetFileLog.Write("KeepAlive: liberado");
        }

        public void Dispose()
        {
            if (CoreUiDispatcher.IsOnUiThread)
            {
                DisposeCore();
                return;
            }

            ReleaseAsync(this).GetAwaiter().GetResult();
        }
    }
}
