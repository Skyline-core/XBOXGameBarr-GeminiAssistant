using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Importa la captura que Game Bar guarda con Win+Alt+Impr Pant (portapapeles).
    /// No usa PrintWindow ni GraphicsCapture, asi Win+G no se cierra.
    /// </summary>
    internal static class ClipboardScreenshotImporter
    {
        private const int MaxJpegLongEdge = 1280;

        public static async Task<byte[]> TryImportJpegAsync()
        {
            try
            {
                var view = Clipboard.GetContent();
                if (view == null)
                {
                    WidgetFileLog.Write("Portapapeles: vacio");
                    return null;
                }

                var formats = view.AvailableFormats?.ToArray() ?? Array.Empty<string>();
                if (formats.Length > 0)
                {
                    WidgetFileLog.Write("Portapapeles formatos: " + string.Join(", ", formats));
                }

                if (view.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await view.GetStorageItemsAsync().AsTask().ConfigureAwait(true);
                    foreach (var item in items)
                    {
                        if (item is StorageFile file)
                        {
                            var fromFile = await TryImportFromStorageFileAsync(file).ConfigureAwait(true);
                            if (fromFile != null && fromFile.Length >= 100)
                            {
                                WidgetFileLog.Write("Portapapeles: archivo " + file.Name);
                                return fromFile;
                            }
                        }
                    }
                }

                if (view.Contains(StandardDataFormats.Text))
                {
                    var text = (await view.GetTextAsync().AsTask().ConfigureAwait(true))?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        text = text.Trim('"');
                        var fromPath = await TryImportFromPathTextAsync(text).ConfigureAwait(true);
                        if (fromPath != null && fromPath.Length >= 100)
                        {
                            WidgetFileLog.Write("Portapapeles: ruta texto");
                            return fromPath;
                        }
                    }
                }

                if (!view.Contains(StandardDataFormats.Bitmap))
                {
                    return null;
                }

                var bitmapRef = await view.GetBitmapAsync().AsTask().ConfigureAwait(true);
                using (var stream = await bitmapRef.OpenReadAsync().AsTask().ConfigureAwait(true))
                {
                    return await DecodeStreamToJpegAsync(stream).ConfigureAwait(true);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                WidgetFileLog.Write("Portapapeles acceso denegado: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Portapapeles: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }

        private static async Task<byte[]> TryImportFromPathTextAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var ext = System.IO.Path.GetExtension(path);
            if (ext == null ||
                (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                 !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                 !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path).AsTask().ConfigureAwait(false);
                return await TryImportFromStorageFileAsync(file).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Portapapeles ruta: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }

        public static async Task<byte[]> TryImportFromStorageFileAsync(StorageFile file)
        {
            if (file == null)
            {
                return null;
            }

            var name = file.Name ?? string.Empty;
            var ext = System.IO.Path.GetExtension(name);
            if (ext == null ||
                (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                 !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                 !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            try
            {
                using (var stream = await file.OpenReadAsync().AsTask().ConfigureAwait(false))
                {
                    return await DecodeStreamToJpegAsync(stream).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Archivo captura " + name + ": " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }

        private static async Task<byte[]> DecodeStreamToJpegAsync(IRandomAccessStream stream)
        {
            var decoder = await BitmapDecoder.CreateAsync(stream).AsTask().ConfigureAwait(false);
            using (var softwareBitmap = await decoder.GetSoftwareBitmapAsync().AsTask().ConfigureAwait(false))
            {
                return await EncodeJpegAsync(softwareBitmap).ConfigureAwait(false);
            }
        }

        private static async Task<byte[]> EncodeJpegAsync(SoftwareBitmap softwareBitmap)
        {
            using (var converted = SoftwareBitmap.Convert(
                softwareBitmap,
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied))
            using (var stream = new InMemoryRandomAccessStream())
            {
                var encoder = await BitmapEncoder.CreateAsync(
                    BitmapEncoder.JpegEncoderId,
                    stream).AsTask().ConfigureAwait(false);

                encoder.SetSoftwareBitmap(converted);
                encoder.IsThumbnailGenerated = false;

                var w = converted.PixelWidth;
                var h = converted.PixelHeight;
                if (w > MaxJpegLongEdge || h > MaxJpegLongEdge)
                {
                    var scale = Math.Min(
                        (double)MaxJpegLongEdge / w,
                        (double)MaxJpegLongEdge / h);
                    encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Linear;
                    encoder.BitmapTransform.ScaledWidth = (uint)Math.Max(1, w * scale);
                    encoder.BitmapTransform.ScaledHeight = (uint)Math.Max(1, h * scale);
                }

                await encoder.FlushAsync().AsTask().ConfigureAwait(false);

                stream.Seek(0);
                var buffer = new Windows.Storage.Streams.Buffer((uint)stream.Size);
                await stream.ReadAsync(buffer, (uint)stream.Size, InputStreamOptions.None);
                return buffer.ToArray();
            }
        }
    }
}
