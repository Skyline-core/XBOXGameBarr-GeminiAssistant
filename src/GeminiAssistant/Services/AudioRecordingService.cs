using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Records WAV audio via MediaCapture. Works in Game Bar widgets where SpeechRecognizer often fails.
    /// </summary>
    public static class AudioRecordingService
    {
        public static Task<AudioRecordingSession> StartRecordingAsync(CancellationToken cancellationToken = default)
        {
            return AudioRecordingSession.StartAsync(cancellationToken);
        }

        /// <summary>
        /// Fixed-duration recording kept for compatibility.
        /// </summary>
        public static async Task<byte[]> RecordWavAsync(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            using (var session = await StartRecordingAsync(cancellationToken).ConfigureAwait(true))
            {
                try
                {
                    await Task.Delay(duration, cancellationToken).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    await session.StopAsync().ConfigureAwait(true);
                    throw;
                }

                return await session.StopAsync().ConfigureAwait(true);
            }
        }
    }

    public sealed class AudioRecordingSession : IDisposable
    {
        private MediaCapture _capture;
        private LowLagMediaRecording _recorder;
        private InMemoryRandomAccessStream _stream;
        private bool _isRecording;
        private CancellationTokenRegistration _cancelRegistration;

        public bool IsRecording => _isRecording;

        public static async Task<AudioRecordingSession> StartAsync(CancellationToken cancellationToken = default)
        {
            var session = new AudioRecordingSession();
            await session.InitializeAndStartAsync(cancellationToken).ConfigureAwait(true);
            return session;
        }

        private async Task InitializeAndStartAsync(CancellationToken cancellationToken)
        {
            var settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio,
                MediaCategory = MediaCategory.Speech
            };

            _capture = new MediaCapture();
            await _capture.InitializeAsync(settings).AsTask().ConfigureAwait(true);

            _stream = new InMemoryRandomAccessStream();
            var profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
            _recorder = await _capture.PrepareLowLagRecordToStreamAsync(profile, _stream).AsTask()
                .ConfigureAwait(true);

            await _recorder.StartAsync().AsTask().ConfigureAwait(true);
            _isRecording = true;

            if (cancellationToken.CanBeCanceled)
            {
                _cancelRegistration = cancellationToken.Register(() =>
                {
                    if (_isRecording)
                    {
                        _ = StopAsync();
                    }
                });
            }
        }

        public async Task<byte[]> StopAsync()
        {
            if (!_isRecording)
            {
                return Array.Empty<byte>();
            }

            _isRecording = false;

            try
            {
                try
                {
                    await _recorder.StopAsync().AsTask().ConfigureAwait(true);
                }
                catch
                {
                }

                await _recorder.FinishAsync().AsTask().ConfigureAwait(true);
            }
            finally
            {
                _recorder = null;
                _capture?.Dispose();
                _capture = null;
                if (_cancelRegistration != null)
                {
                    _cancelRegistration.Dispose();
                    _cancelRegistration = default;
                }
            }

            _stream.Seek(0);
            if (_stream.Size < 500)
            {
                throw new InvalidOperationException(
                    "No se grabo audio. Comprueba el microfono en Configuracion > Privacidad > Microfono.");
            }

            var reader = new DataReader(_stream);
            await reader.LoadAsync((uint)_stream.Size);
            var bytes = new byte[_stream.Size];
            reader.ReadBytes(bytes);
            return bytes;
        }

        public void Dispose()
        {
            if (_isRecording)
            {
                StopAsync().GetAwaiter().GetResult();
            }

            _stream?.Dispose();
        }
    }
}
