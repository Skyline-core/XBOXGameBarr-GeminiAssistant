using System;
using System.Threading.Tasks;
using Microsoft.Gaming.XboxGameBar;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Actividades Game Bar. Cada Begin usa un activityId unico (el SDK lanza si el id ya existe).
    /// </summary>
    public sealed class GeminiLiveService
    {
        private XboxGameBarWidgetActivity _activity;
        private XboxGameBarWidgetActivity _captureActivity;
        private XboxGameBarWidgetActivity _requestActivity;
        private XboxGameBarWidget _widget;

        private static void SafeComplete(ref XboxGameBarWidgetActivity activity, string label)
        {
            if (activity == null)
            {
                return;
            }

            try
            {
                activity.Complete();
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write(label + " Complete: " + WidgetExceptionFormatter.Format(ex));
            }
            finally
            {
                activity = null;
            }
        }

        private static XboxGameBarWidgetActivity TryBeginActivity(
            XboxGameBarWidget widget,
            string activityIdPrefix,
            ref XboxGameBarWidgetActivity slot,
            string label)
        {
            if (widget == null)
            {
                return null;
            }

            SafeComplete(ref slot, label);

            var activityId = activityIdPrefix + "_" + Guid.NewGuid().ToString("N");
            try
            {
                slot = new XboxGameBarWidgetActivity(widget, activityId);
                WidgetFileLog.Write(label + " activity: " + activityId);
                return slot;
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write(label + " activity fallo: " + WidgetExceptionFormatter.Format(ex));
                slot = null;
                return null;
            }
        }

        public void AttachWidget(XboxGameBarWidget widget)
        {
            _widget = widget;
        }

        public Task BeginVoiceActivityAsync()
        {
            return CoreUiDispatcher.RunOnUiAsync(() =>
            {
                TryBeginActivity(_widget, "GeminiVoice", ref _activity, "Voz");
                return Task.CompletedTask;
            });
        }

        public Task EndVoiceActivityAsync()
        {
            return CoreUiDispatcher.RunOnUiAsync(() =>
            {
                SafeComplete(ref _activity, "Voz");
                return Task.CompletedTask;
            });
        }

        public Task BeginCaptureActivityAsync()
        {
            return CoreUiDispatcher.RunOnUiAsync(() =>
            {
                TryBeginActivity(_widget, "GeminiCapture", ref _captureActivity, "Captura");
                return Task.CompletedTask;
            });
        }

        public Task EndCaptureActivityAsync()
        {
            return CoreUiDispatcher.RunOnUiAsync(() =>
            {
                SafeComplete(ref _captureActivity, "Captura");
                return Task.CompletedTask;
            });
        }

        public Task BeginRequestActivityAsync()
        {
            return CoreUiDispatcher.RunOnUiAsync(() =>
            {
                TryBeginActivity(_widget, "GeminiApiRequest", ref _requestActivity, "Request");
                return Task.CompletedTask;
            });
        }

        public Task EndRequestActivityAsync()
        {
            return CoreUiDispatcher.RunOnUiAsync(() =>
            {
                SafeComplete(ref _requestActivity, "Request");
                return Task.CompletedTask;
            });
        }

        public void Dispose()
        {
            CoreUiDispatcher.RunOnUiAsync(() =>
            {
                SafeComplete(ref _activity, "Voz");
                SafeComplete(ref _captureActivity, "Captura");
                SafeComplete(ref _requestActivity, "Request");
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
        }
    }
}
