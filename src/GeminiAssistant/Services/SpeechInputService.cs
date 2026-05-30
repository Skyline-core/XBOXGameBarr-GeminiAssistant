using System;
using System.Threading.Tasks;
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

            _recognizer = new SpeechRecognizer();
            var constraint = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "gaming");
            _recognizer.Constraints.Add(constraint);

            var compileResult = await _recognizer.CompileConstraintsAsync();
            if (compileResult.Status != SpeechRecognitionResultStatus.Success)
            {
                throw new InvalidOperationException("No se pudo inicializar el reconocimiento de voz.");
            }

            _initialized = true;
        }

        public async Task<string> ListenOnceAsync(TimeSpan listenTimeout)
        {
            await EnsureInitializedAsync().ConfigureAwait(true);

            using (var cts = new System.Threading.CancellationTokenSource(listenTimeout))
            {
                var recognitionTask = _recognizer.RecognizeWithUIAsync();
                var completed = await Task.WhenAny(
                    recognitionTask.AsTask(),
                    Task.Delay(listenTimeout, cts.Token)).ConfigureAwait(true);

                if (completed != recognitionTask.AsTask())
                {
                    throw new TimeoutException("Tiempo de escucha agotado.");
                }

                var result = await recognitionTask.AsTask().ConfigureAwait(true);
                if (result.Status != SpeechRecognitionResultStatus.Success)
                {
                    throw new InvalidOperationException("No se entendió el audio. Estado: " + result.Status);
                }

                return result.Text?.Trim() ?? string.Empty;
            }
        }

        public void Dispose()
        {
            _recognizer?.Dispose();
            _recognizer = null;
            _initialized = false;
        }
    }
}
