using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace GeminiAssistant.Services
{
    internal static class ScreenshotImageHelper
    {
        private const int MaxBytesForApi = 220_000;
        private const int MaxJpegLongEdge = 768;

        public static async Task<byte[]> PrepareForApiAsync(byte[] jpegBytes)
        {
            if (jpegBytes == null || jpegBytes.Length == 0)
            {
                return jpegBytes;
            }

            if (jpegBytes.Length <= MaxBytesForApi)
            {
                return jpegBytes;
            }

            return await RecompressJpegAsync(jpegBytes).ConfigureAwait(true) ?? jpegBytes;
        }

        private static async Task<byte[]> RecompressJpegAsync(byte[] jpegBytes)
        {
            using (var input = new InMemoryRandomAccessStream())
            {
                await input.WriteAsync(jpegBytes.AsBuffer());
                input.Seek(0);

                var decoder = await BitmapDecoder.CreateAsync(input).AsTask().ConfigureAwait(true);
                using (var bitmap = await decoder.GetSoftwareBitmapAsync().AsTask().ConfigureAwait(true))
                using (var converted = SoftwareBitmap.Convert(
                    bitmap,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied))
                using (var output = new InMemoryRandomAccessStream())
                {
                    var encoder = await BitmapEncoder.CreateAsync(
                        BitmapEncoder.JpegEncoderId,
                        output).AsTask().ConfigureAwait(true);

                    encoder.SetSoftwareBitmap(converted);
                    encoder.IsThumbnailGenerated = false;

                    var w = converted.PixelWidth;
                    var h = converted.PixelHeight;
                    var scale = Math.Min(
                        (double)MaxJpegLongEdge / w,
                        (double)MaxJpegLongEdge / h);
                    if (scale < 1.0)
                    {
                        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Linear;
                        encoder.BitmapTransform.ScaledWidth = (uint)Math.Max(1, w * scale);
                        encoder.BitmapTransform.ScaledHeight = (uint)Math.Max(1, h * scale);
                    }

                    await encoder.FlushAsync().AsTask().ConfigureAwait(true);
                    output.Seek(0);
                    var buffer = new Windows.Storage.Streams.Buffer((uint)output.Size);
                    await output.ReadAsync(buffer, (uint)output.Size, InputStreamOptions.None);
                    return buffer.ToArray();
                }
            }
        }
    }
}
