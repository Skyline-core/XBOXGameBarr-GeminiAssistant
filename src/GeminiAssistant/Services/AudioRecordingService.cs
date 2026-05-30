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
        public static async Task<byte[]> RecordWavAsync(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            var settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio,
                MediaCategory = MediaCategory.Speech
            };

            var capture = new MediaCapture();
            await capture.InitializeAsync(settings).AsTask().ConfigureAwait(true);

            var stream = new InMemoryRandomAccessStream();
            var profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
            var recorder = await capture.PrepareLowLagRecordToStreamAsync(profile, stream).AsTask()
                .ConfigureAwait(true);

            try
            {
                await recorder.StartAsync().AsTask().ConfigureAwait(true);
                await Task.Delay(duration, cancellationToken).ConfigureAwait(true);
            }
            finally
            {
                try
                {
                    await recorder.StopAsync().AsTask().ConfigureAwait(true);
                }
                catch
                {
                    // ignore if already stopped
                }

                await recorder.FinishAsync().AsTask().ConfigureAwait(true);
                capture.Dispose();
            }

            stream.Seek(0);
            if (stream.Size < 500)
            {
                throw new InvalidOperationException(
                    "No se grabo audio. Comprueba el microfono en Configuracion > Privacidad > Microfono.");
            }

            var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[stream.Size];
            reader.ReadBytes(bytes);
            return bytes;
        }
    }
}
