using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using GeminiAssistant.Models;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Microsoft.Graphics.Canvas;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Captures a single frame from a user-selected window via GraphicsCapturePicker.
    /// Requires Windows 10 1903+ and Desktop Extensions for UWP (SDK reference in csproj).
    /// </summary>
    public sealed class ScreenCaptureService
    {
        public async Task<PendingScreenshot> CaptureAsync()
        {
            var picker = new GraphicsCapturePicker();
            var item = await picker.PickSingleItemAsync().AsTask().ConfigureAwait(true);
            if (item == null)
            {
                return null;
            }

            var canvasDevice = CanvasDevice.GetSharedDevice();
            var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                canvasDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                item.Size);

            var session = framePool.CreateCaptureSession(item);
            session.StartCapture();

            Direct3D11CaptureFrame frame = null;
            try
            {
                for (var attempt = 0; attempt < 30; attempt++)
                {
                    frame = framePool.TryGetNextFrame();
                    if (frame != null)
                    {
                        break;
                    }

                    await Task.Delay(50).ConfigureAwait(true);
                }

                if (frame == null)
                {
                    throw new InvalidOperationException("No se pudo obtener un fotograma de la captura.");
                }

                using (var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(canvasDevice, frame.Surface))
                {
                    var jpegBytes = await EncodeJpegAsync(bitmap).ConfigureAwait(true);
                    return new PendingScreenshot { JpegBytes = jpegBytes };
                }
            }
            finally
            {
                session?.Dispose();
                framePool?.Dispose();
                frame?.Dispose();
            }
        }

        private static async Task<byte[]> EncodeJpegAsync(CanvasBitmap bitmap)
        {
            using (var stream = new InMemoryRandomAccessStream())
            {
                await bitmap.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg, 0.85f).AsTask().ConfigureAwait(false);
                stream.Seek(0);
                var buffer = new global::Windows.Storage.Streams.Buffer((uint)stream.Size);
                await stream.ReadAsync(buffer, (uint)stream.Size, InputStreamOptions.None);
                return buffer.ToArray();
            }
        }
    }
}
