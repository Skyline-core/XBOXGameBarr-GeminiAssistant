using Microsoft.Gaming.XboxGameBar;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Keeps the Game Bar widget process alive during microphone recording.
    /// </summary>
    public sealed class GeminiLiveService
    {
        private const string VoiceActivityId = "GeminiVoiceSession";
        private XboxGameBarWidgetActivity _activity;
        private XboxGameBarWidget _widget;

        public void AttachWidget(XboxGameBarWidget widget)
        {
            _widget = widget;
        }

        public void BeginVoiceActivity()
        {
            if (_widget == null || _activity != null)
            {
                return;
            }

            _activity = new XboxGameBarWidgetActivity(_widget, VoiceActivityId);
        }

        public void EndVoiceActivity()
        {
            if (_activity != null)
            {
                _activity.Complete();
                _activity = null;
            }
        }

        public void Dispose()
        {
            EndVoiceActivity();
        }
    }
}
