using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Captura ventanas con PrintWindow/BitBlt (sin GraphicsCaptureSession para no cerrar Game Bar).
    /// </summary>
    internal static class Win32WindowCapture
    {
        private const uint PwRenderFullContent = 0x00000002;
        private const int Srccopy = 0x00CC0020;
        private const int MaxJpegLongEdge = 768;
        private const int BiRgb = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader bmiHeader;
        }

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SmCxScreen = 0;
        private const int SmCyScreen = 1;

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(
            IntPtr hdcDest,
            int xDest,
            int yDest,
            int width,
            int height,
            IntPtr hdcSrc,
            int xSrc,
            int ySrc,
            int rop);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(
            IntPtr hdc,
            IntPtr hbmp,
            uint uStartScan,
            uint cScanLines,
            byte[] lpvBits,
            ref BitmapInfo lpbmi,
            uint uUsage);

        [ComImport]
        [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMemoryBufferByteAccess
        {
            void GetBuffer(out IntPtr buffer, out uint capacity);
        }

        public static async Task<byte[]> TryCaptureDesktopJpegAsync()
        {
            try
            {
                var width = GetSystemMetrics(SmCxScreen);
                var height = GetSystemMetrics(SmCyScreen);
                if (width < 8 || height < 8)
                {
                    return null;
                }

                if (!TryCaptureDesktop(width, height, out var pixels))
                {
                    WidgetFileLog.Write("Captura escritorio: fallo BitBlt");
                    return null;
                }

                WidgetFileLog.Write("Captura escritorio OK " + width + "x" + height);
                return await EncodeBgraToJpegAsync(pixels, width, height).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura escritorio ex: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }

        private static bool TryCaptureDesktop(int width, int height, out byte[] pixels)
        {
            pixels = null;
            IntPtr hdcScreen = IntPtr.Zero;
            IntPtr hdcMem = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOld = IntPtr.Zero;

            try
            {
                hdcScreen = GetDC(IntPtr.Zero);
                if (hdcScreen == IntPtr.Zero)
                {
                    return false;
                }

                hdcMem = CreateCompatibleDC(hdcScreen);
                hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
                if (hdcMem == IntPtr.Zero || hBitmap == IntPtr.Zero)
                {
                    return false;
                }

                hOld = SelectObject(hdcMem, hBitmap);
                if (!BitBlt(hdcMem, 0, 0, width, height, hdcScreen, 0, 0, Srccopy))
                {
                    return false;
                }

                var info = new BitmapInfo
                {
                    bmiHeader = new BitmapInfoHeader
                    {
                        biSize = Marshal.SizeOf<BitmapInfoHeader>(),
                        biWidth = width,
                        biHeight = -height,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = BiRgb
                    }
                };

                pixels = new byte[width * height * 4];
                return GetDIBits(hdcMem, hBitmap, 0, (uint)height, pixels, ref info, 0) != 0;
            }
            finally
            {
                if (hOld != IntPtr.Zero && hdcMem != IntPtr.Zero)
                {
                    SelectObject(hdcMem, hOld);
                }

                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                }

                if (hdcMem != IntPtr.Zero)
                {
                    DeleteDC(hdcMem);
                }

                if (hdcScreen != IntPtr.Zero)
                {
                    ReleaseDC(IntPtr.Zero, hdcScreen);
                }
            }
        }

        public static async Task<byte[]> TryCaptureJpegAsync(IntPtr hwnd, bool rejectMostlyBlack = true)
        {
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                byte[] pixels = null;
                int width = 0;
                int height = 0;
                string methodUsed = null;

                if (TryCapture(hwnd, useWindowRect: false, useBitBlt: false, PwRenderFullContent, out pixels, out width, out height))
                {
                    methodUsed = "PrintWindow(full)";
                }
                else if (TryCapture(hwnd, useWindowRect: false, useBitBlt: false, 0, out pixels, out width, out height))
                {
                    methodUsed = "PrintWindow(0)";
                }
                else if (TryCapture(hwnd, useWindowRect: false, useBitBlt: true, 0, out pixels, out width, out height))
                {
                    methodUsed = "BitBlt(client)";
                }
                else if (TryCapture(hwnd, useWindowRect: true, useBitBlt: false, PwRenderFullContent, out pixels, out width, out height))
                {
                    methodUsed = "PrintWindow(windowRect)";
                }
                else if (TryCapture(hwnd, useWindowRect: true, useBitBlt: true, 0, out pixels, out width, out height))
                {
                    methodUsed = "BitBlt(windowRect)";
                }

                if (pixels == null || pixels.Length < 100)
                {
                    WidgetFileLog.Write("Captura hwnd=" + hwnd.ToInt64() + " fallo en todos los metodos");
                    return null;
                }

                if (rejectMostlyBlack && IsMostlyBlackBgra(pixels, width, height))
                {
                    WidgetFileLog.Write("Captura hwnd=" + hwnd.ToInt64() + " frame negro, ignorar");
                    return null;
                }

                WidgetFileLog.Write("Captura hwnd=" + hwnd.ToInt64() + " OK " + width + "x" + height + " via " + methodUsed);
                return await EncodeBgraToJpegAsync(pixels, width, height).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura hwnd ex: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }

        private static bool TryCapture(
            IntPtr hwnd,
            bool useWindowRect,
            bool useBitBlt,
            uint printFlags,
            out byte[] pixels,
            out int width,
            out int height)
        {
            pixels = null;
            width = 0;
            height = 0;

            Rect rect;
            if (useWindowRect)
            {
                if (!GetWindowRect(hwnd, out rect))
                {
                    return false;
                }
            }
            else if (!GetClientRect(hwnd, out rect))
            {
                return false;
            }

            width = rect.Right - rect.Left;
            height = rect.Bottom - rect.Top;
            if (width < 8 || height < 8 || width > 7680 || height > 4320)
            {
                return false;
            }

            IntPtr hdcSource = IntPtr.Zero;
            IntPtr hdcMem = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOld = IntPtr.Zero;
            var releasedSourceDc = false;

            try
            {
                hdcSource = GetDC(hwnd);
                if (hdcSource == IntPtr.Zero)
                {
                    return false;
                }

                hdcMem = CreateCompatibleDC(hdcSource);
                hBitmap = CreateCompatibleBitmap(hdcSource, width, height);
                if (hdcMem == IntPtr.Zero || hBitmap == IntPtr.Zero)
                {
                    return false;
                }

                hOld = SelectObject(hdcMem, hBitmap);

                var copied = false;
                if (useBitBlt)
                {
                    copied = BitBlt(hdcMem, 0, 0, width, height, hdcSource, 0, 0, Srccopy);
                }
                else
                {
                    copied = PrintWindow(hwnd, hdcMem, printFlags);
                }

                if (!copied)
                {
                    return false;
                }

                var info = new BitmapInfo
                {
                    bmiHeader = new BitmapInfoHeader
                    {
                        biSize = Marshal.SizeOf<BitmapInfoHeader>(),
                        biWidth = width,
                        biHeight = -height,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = BiRgb
                    }
                };

                pixels = new byte[width * height * 4];
                var lines = GetDIBits(hdcMem, hBitmap, 0, (uint)height, pixels, ref info, 0);
                return lines != 0;
            }
            finally
            {
                if (hOld != IntPtr.Zero && hdcMem != IntPtr.Zero)
                {
                    SelectObject(hdcMem, hOld);
                }

                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                }

                if (hdcMem != IntPtr.Zero)
                {
                    DeleteDC(hdcMem);
                }

                if (hdcSource != IntPtr.Zero && !releasedSourceDc)
                {
                    ReleaseDC(hwnd, hdcSource);
                }
            }
        }

        private static async Task<byte[]> EncodeBgraToJpegAsync(byte[] bgra, int width, int height)
        {
            var bitmap = new SoftwareBitmap(
                BitmapPixelFormat.Bgra8,
                width,
                height,
                BitmapAlphaMode.Ignore);

            using (bitmap)
            {
                using (var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Write))
                using (var reference = buffer.CreateReference())
                {
                    var access = (IMemoryBufferByteAccess)reference;
                    access.GetBuffer(out var data, out var capacity);
                    var length = (int)Math.Min((uint)bgra.Length, capacity);
                    Marshal.Copy(bgra, 0, data, length);
                }

                return await EncodeJpegAsync(bitmap).ConfigureAwait(false);
            }
        }

        private static async Task<byte[]> EncodeJpegAsync(SoftwareBitmap softwareBitmap)
        {
            using (var stream = new InMemoryRandomAccessStream())
            {
                var encoder = await BitmapEncoder.CreateAsync(
                    BitmapEncoder.JpegEncoderId,
                    stream).AsTask().ConfigureAwait(false);

                encoder.SetSoftwareBitmap(softwareBitmap);
                encoder.IsThumbnailGenerated = false;

                var w = softwareBitmap.PixelWidth;
                var h = softwareBitmap.PixelHeight;
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

        private static bool IsMostlyBlackBgra(byte[] pixels, int width, int height)
        {
            if (pixels == null || pixels.Length < 16)
            {
                return true;
            }

            var step = Math.Max(4, (width * height) / 4000) * 4;
            var dark = 0;
            var sampled = 0;
            for (var i = 0; i < pixels.Length - 3; i += step)
            {
                sampled++;
                var b = pixels[i];
                var g = pixels[i + 1];
                var r = pixels[i + 2];
                if (r < 12 && g < 12 && b < 12)
                {
                    dark++;
                }
            }

            return sampled > 0 && dark * 100 / sampled >= 98;
        }
    }
}
