using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Miniaturas JPEG para la UI. Debe ejecutarse en el hilo UI del widget.
    /// </summary>
    internal static class ScreenshotThumbnailHelper
    {
        private const int ThumbnailWidth = 160;

        public static async Task<ImageSource> CreateFromJpegAsync(byte[] jpegBytes)
        {
            if (jpegBytes == null || jpegBytes.Length == 0)
            {
                return null;
            }

            await CoreUiDispatcher.YieldToUiAsync().ConfigureAwait(true);

            try
            {
                var bitmap = new BitmapImage
                {
                    DecodePixelType = DecodePixelType.Logical,
                    DecodePixelWidth = ThumbnailWidth
                };

                using (var stream = new InMemoryRandomAccessStream())
                {
                    await stream.WriteAsync(jpegBytes.AsBuffer()).AsTask().ConfigureAwait(true);
                    stream.Seek(0);
                    await bitmap.SetSourceAsync(stream).AsTask().ConfigureAwait(true);
                }

                await CoreUiDispatcher.YieldToUiAsync().ConfigureAwait(true);
                WidgetFileLog.Write("Miniatura OK " + bitmap.PixelWidth + "x" + bitmap.PixelHeight);
                return bitmap;
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Miniatura captura: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }
    }
}
