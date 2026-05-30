using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Media.Capture;
using Windows.Media.SpeechRecognition;

namespace GeminiAssistant.Services
{
    public sealed class SpeechInputService : IDisposable
    {
        private SpeechRecognizer _recognizer;
        private bool _initialized;

        public async Task EnsureInitializedAsync()
        {
            if (_initialized)
            {
                return;
            }

            await EnsureMicrophoneAccessAsync().ConfigureAwait(true);

            _recognizer = TryCreateRecognizer("es-ES")
                ?? TryCreateRecognizer("es-MX")
                ?? new SpeechRecognizer();

            _recognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(8);
            _recognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(1.2);
            _recognizer.Timeouts.BabbleTimeout = TimeSpan.FromSeconds(0);

            var compileResult = await _recognizer.CompileConstraintsAsync().AsTask().ConfigureAwait(true);
            if (compileResult.Status != SpeechRecognitionResultStatus.Success)
            {
                throw new InvalidOperationException(
                    "No se pudo inicializar el reconocimiento de voz. Instala el idioma de voz espanol en Windows.");
            }

            _initialized = true;
        }

        private static async Task EnsureMicrophoneAccessAsync()
        {
            try
            {
                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Audio,
                    MediaCategory = MediaCategory.Speech
                };

                using (var capture = new MediaCapture())
                {
                    await capture.InitializeAsync(settings).AsTask().ConfigureAwait(true);
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    "Permiso de microfono denegado. Configuracion > Privacidad > Microfono > Gemini Assistant.");
            }
        }

        private static SpeechRecognizer TryCreateRecognizer(string languageTag)
        {
            try
            {
                return new SpeechRecognizer(new Language(languageTag));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Listens for one utterance (no overlay UI). Timeouts are handled by SpeechRecognizer.
        /// </summary>
        public async Task<string> ListenOnceAsync(CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync().ConfigureAwait(true);

            cancellationToken.ThrowIfCancellationRequested();

            var result = await _recognizer.RecognizeAsync().AsTask().ConfigureAwait(true);

            if (result.Status == SpeechRecognitionResultStatus.UserCanceled)
            {
                throw new OperationCanceledException("Reconocimiento cancelado.");
            }

            if (result.Status == SpeechRecognitionResultStatus.TimeoutExceeded)
            {
                throw new InvalidOperationException("No se detecto voz a tiempo. Pulsa Microfono y habla de inmediato.");
            }

            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                throw new InvalidOperationException(
                    "No se entendio el audio (" + result.Status + "). Habla mas cerca del microfono.");
            }

            var text = result.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                throw new InvalidOperationException("No se detecto texto. Repite mas fuerte o instala el paquete de voz en espanol.");
            }

            return text;
        }

        public void Dispose()
        {
            _recognizer?.Dispose();
            _recognizer = null;
            _initialized = false;
        }
    }
}
