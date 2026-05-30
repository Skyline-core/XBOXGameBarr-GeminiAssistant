using System;
using System.Threading;
using System.Threading.Tasks;
using GeminiAssistant.Models;
using Microsoft.Gaming.XboxGameBar;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Orchestrates voice input sessions while Game Bar stays active via WidgetActivity.
    /// MVP: Windows speech recognition to text, then GeminiChatService.
    /// Full Gemini Live API (WebSocket) can replace the speech path in a future iteration.
    /// </summary>
    public sealed class GeminiLiveService : IDisposable
    {
        private const string VoiceActivityId = "GeminiVoiceSession";
        private readonly SpeechInputService _speech = new SpeechInputService();
        private readonly GeminiChatService _chat = new GeminiChatService();
        private XboxGameBarWidgetActivity _activity;
        private XboxGameBarWidget _widget;

        public bool IsListening { get; private set; }

        public string LastTranscript { get; private set; }

        public void AttachWidget(XboxGameBarWidget widget)
        {
            _widget = widget;
        }

        public void BeginVoiceActivity()
        {
            if (_widget == null)
            {
                return;
            }

            if (_activity == null)
            {
                _activity = new XboxGameBarWidgetActivity(_widget, VoiceActivityId);
            }
        }

        public void EndVoiceActivity()
        {
            if (_activity != null)
            {
                _activity.Complete();
                _activity = null;
            }
        }

        /// <summary>
        /// Listens via microphone (speech-to-text) and sends the transcript to Gemini.
        /// </summary>
        public async Task<string> ListenAndAskGeminiAsync(
            GameContextInfo gameContext,
            PendingScreenshot screenshot,
            CancellationToken cancellationToken = default)
        {
            if (IsListening)
            {
                throw new InvalidOperationException("Ya hay una sesion de voz activa.");
            }

            IsListening = true;
            BeginVoiceActivity();

            try
            {
                var transcript = await _speech.ListenOnceAsync(TimeSpan.FromSeconds(30))
                    .ConfigureAwait(true);
                LastTranscript = transcript;

                if (string.IsNullOrWhiteSpace(transcript))
                {
                    throw new InvalidOperationException("No se detecto voz.");
                }

                var prompt = "[Mensaje por voz] " + transcript;
                return await _chat.SendMessageAsync(prompt, gameContext, screenshot, cancellationToken)
                    .ConfigureAwait(true);
            }
            finally
            {
                IsListening = false;
                EndVoiceActivity();
            }
        }

        public void Dispose()
        {
            EndVoiceActivity();
            _speech.Dispose();
        }
    }
}
