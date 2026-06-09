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
        private string _initializedLanguageTag;

        public async Task EnsureInitializedAsync()
        {
            var desiredTag = AppLanguageService.GetEffectiveLanguageTag();
            if (_initialized && string.Equals(_initializedLanguageTag, desiredTag, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_initialized)
            {
                _recognizer?.Dispose();
                _recognizer = null;
                _initialized = false;
            }

            await EnsureMicrophoneAccessAsync().ConfigureAwait(true);

            _recognizer = CreateRecognizerForAppLanguage() ?? new SpeechRecognizer();
            _recognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(8);
            _recognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(1.2);
            _recognizer.Timeouts.BabbleTimeout = TimeSpan.FromSeconds(0);

            var compileResult = await _recognizer.CompileConstraintsAsync().AsTask().ConfigureAwait(true);
            if (compileResult.Status != SpeechRecognitionResultStatus.Success)
            {
                throw new InvalidOperationException(LocalizedStrings.Error_SpeechInit);
            }

            _initialized = true;
            _initializedLanguageTag = desiredTag;
        }

        private static SpeechRecognizer CreateRecognizerForAppLanguage()
        {
            foreach (var tag in AppLanguageService.GetSpeechLanguageTags())
            {
                var recognizer = TryCreateRecognizer(tag);
                if (recognizer != null)
                {
                    return recognizer;
                }
            }

            return null;
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
                throw new InvalidOperationException(LocalizedStrings.Error_MicDenied);
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

        public async Task<string> ListenOnceAsync(CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync().ConfigureAwait(true);

            cancellationToken.ThrowIfCancellationRequested();

            var result = await _recognizer.RecognizeAsync().AsTask().ConfigureAwait(true);

            if (result.Status == SpeechRecognitionResultStatus.UserCanceled)
            {
                throw new OperationCanceledException();
            }

            if (result.Status == SpeechRecognitionResultStatus.TimeoutExceeded)
            {
                throw new InvalidOperationException(LocalizedStrings.Status_RecordingMax);
            }

            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                throw new InvalidOperationException(result.Status.ToString());
            }

            var text = result.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                throw new InvalidOperationException(LocalizedStrings.Error_SpeechInit);
            }

            return text;
        }

        public void Dispose()
        {
            _recognizer?.Dispose();
            _recognizer = null;
            _initialized = false;
            _initializedLanguageTag = null;
        }
    }
}
