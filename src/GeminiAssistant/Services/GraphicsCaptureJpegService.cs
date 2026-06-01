using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Security.Authorization.AppCapabilityAccess;
using Windows.Storage.Streams;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Captura programmatica (WGC). Funciona en juegos donde PrintWindow devuelve negro.
    /// Sesion de un solo frame; se libera al terminar para no interferir con Game Bar.
    /// </summary>
    internal static class GraphicsCaptureJpegService
    {
        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
        [System.Runtime.InteropServices.InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMemoryBufferByteAccess
        {
            void GetBuffer(out IntPtr buffer, out uint capacity);
        }

        private const int MaxJpegLongEdge = 768;
        private const int CaptureTimeoutMs = 2000;
        private const int MaxFramesToSample = 4;

        public static async Task<byte[]> TryCaptureWindowAsync(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            var access = await ProgrammaticCaptureHelper.EnsureCaptureAccessAsync().ConfigureAwait(false);
            if (access != AppCapabilityAccessStatus.Allowed)
            {
                WidgetFileLog.Write("WGC permiso=" + ProgrammaticCaptureHelper.DescribeAccessStatus(access));
                return null;
            }

            var item = ProgrammaticCaptureHelper.TryCreateForWindow(hwnd);
            if (item == null)
            {
                WidgetFileLog.Write("WGC item null hwnd=" + hwnd.ToInt64());
                return null;
            }

            return await CaptureItemAsync(item, "ventana " + hwnd.ToInt64(), rejectBlackFrames: true)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Intenta WGC sobre HWND sin pedir graphicsCaptureProgrammatic (solo con graphicsCapture en manifiesto).
        /// </summary>
        public static async Task<byte[]> TryCaptureWindowUncheckedAsync(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            var item = ProgrammaticCaptureHelper.TryCreateForWindow(hwnd);
            if (item == null)
            {
                return null;
            }

            return await CaptureItemAsync(item, "hwnd " + hwnd.ToInt64(), rejectBlackFrames: true)
                .ConfigureAwait(false);
        }

        public static async Task<byte[]> TryCaptureMonitorUncheckedAsync(IntPtr monitor)
        {
            if (monitor == IntPtr.Zero)
            {
                return null;
            }

            var item = ProgrammaticCaptureHelper.TryCreateForMonitor(monitor);
            if (item == null)
            {
                return null;
            }

            return await CaptureItemAsync(item, "monitor unchecked", rejectBlackFrames: false)
                .ConfigureAwait(false);
        }

        public static async Task<byte[]> TryCaptureMonitorAsync(IntPtr monitor)
        {
            if (monitor == IntPtr.Zero)
            {
                return null;
            }

            var access = await ProgrammaticCaptureHelper.EnsureCaptureAccessAsync().ConfigureAwait(false);
            if (access != AppCapabilityAccessStatus.Allowed)
            {
                return null;
            }

            var item = ProgrammaticCaptureHelper.TryCreateForMonitor(monitor);
            if (item == null)
            {
                WidgetFileLog.Write("WGC monitor item null");
                return null;
            }

            return await CaptureItemAsync(item, "monitor", rejectBlackFrames: false).ConfigureAwait(false);
        }

        public static Task<byte[]> TryCaptureGraphicsItemAsync(GraphicsCaptureItem item)
        {
            if (item == null)
            {
                return Task.FromResult<byte[]>(null);
            }

            return CaptureItemAsync(item, "picker", rejectBlackFrames: false);
        }

        private static async Task<byte[]> CaptureItemAsync(
            GraphicsCaptureItem item,
            string label,
            bool rejectBlackFrames)
        {
            Direct3D11CaptureFramePool framePool = null;
            GraphicsCaptureSession session = null;
            CaptureD3DDevice d3d = null;
            TypedEventHandler<Direct3D11CaptureFramePool, object> handler = null;
            var framesSeen = 0;

            try
            {
                d3d = CaptureD3DDevice.Create();
                var size = item.Size;
                if (size.Width < 8 || size.Height < 8)
                {
                    return null;
                }

                framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    d3d.Device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    size);

                session = framePool.CreateCaptureSession(item);
                ConfigureSession(session);

                var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                byte[] bestFallback = null;

                handler = async (sender, args) =>
                {
                    if (Interlocked.Increment(ref framesSeen) > MaxFramesToSample)
                    {
                        return;
                    }

                    try
                    {
                        using (var frame = sender.TryGetNextFrame())
                        {
                            if (frame == null)
                            {
                                return;
                            }

                            var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface)
                                .AsTask()
                                .ConfigureAwait(false);
                            using (bitmap)
                            {
                                var jpeg = await EncodeJpegAsync(bitmap).ConfigureAwait(false);
                                var mostlyBlack = rejectBlackFrames && IsMostlyBlack(bitmap);
                                if (jpeg == null || jpeg.Length < 100)
                                {
                                    return;
                                }

                                if (!mostlyBlack)
                                {
                                    WidgetFileLog.Write("WGC OK " + label + " " + size.Width + "x" + size.Height);
                                    tcs.TrySetResult(jpeg);
                                    return;
                                }

                                if (bestFallback == null)
                                {
                                    bestFallback = jpeg;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        WidgetFileLog.Write("WGC frame error: " + WidgetExceptionFormatter.Format(ex));
                        tcs.TrySetException(ex);
                    }
                };

                framePool.FrameArrived += handler;
                session.StartCapture();

                var completed = await Task.WhenAny(
                    tcs.Task,
                    Task.Delay(CaptureTimeoutMs)).ConfigureAwait(false);

                if (completed == tcs.Task)
                {
                    try
                    {
                        return await tcs.Task.ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        WidgetFileLog.Write("WGC resultado " + label + ": " + WidgetExceptionFormatter.Format(ex));
                    }
                }

                await Task.Delay(150).ConfigureAwait(false);

                if (bestFallback != null && bestFallback.Length >= 100)
                {
                    WidgetFileLog.Write("WGC fallback " + label + " (frame oscuro)");
                    return bestFallback;
                }

                WidgetFileLog.Write("WGC timeout " + label);
                return null;
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("WGC error " + label + ": " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
            finally
            {
                if (framePool != null && handler != null)
                {
                    framePool.FrameArrived -= handler;
                }

                session?.Dispose();
                framePool?.Dispose();
                d3d?.Dispose();
            }
        }

        private static void ConfigureSession(GraphicsCaptureSession session)
        {
            session.IsCursorCaptureEnabled = false;

            if (ApiInformation.IsPropertyPresent(
                    typeof(GraphicsCaptureSession).FullName,
                    nameof(GraphicsCaptureSession.IsBorderRequired)))
            {
                session.IsBorderRequired = false;
            }
        }

        private static bool IsMostlyBlack(SoftwareBitmap bitmap)
        {
            try
            {
                using (var converted = SoftwareBitmap.Convert(
                    bitmap,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied))
                {
                    var w = converted.PixelWidth;
                    var h = converted.PixelHeight;
                    if (w < 4 || h < 4)
                    {
                        return true;
                    }

                    var stepX = Math.Max(1, w / 16);
                    var stepY = Math.Max(1, h / 16);
                    var bright = 0;
                    var samples = 0;

                    using (var buffer = converted.LockBuffer(BitmapBufferAccessMode.Read))
                    using (var reference = buffer.CreateReference())
                    {
                        var access = (IMemoryBufferByteAccess)reference;
                        access.GetBuffer(out var data, out var capacity);
                        var stride = (int)buffer.GetPlaneDescription(0).Stride;

                        for (var y = 0; y < h; y += stepY)
                        {
                            for (var x = 0; x < w; x += stepX)
                            {
                                var offset = y * stride + x * 4;
                                if (offset + 2 >= capacity)
                                {
                                    continue;
                                }

                                var b = Marshal.ReadByte(data, offset);
                                var g = Marshal.ReadByte(data, offset + 1);
                                var r = Marshal.ReadByte(data, offset + 2);
                                if (r + g + b > 30)
                                {
                                    bright++;
                                }

                                samples++;
                            }
                        }
                    }

                    return samples > 0 && bright < samples / 8;
                }
            }
            catch
            {
                return false;
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
