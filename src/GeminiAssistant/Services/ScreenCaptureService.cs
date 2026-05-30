using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using GeminiAssistant.Models;
using Microsoft.Gaming.XboxGameBar;
using Microsoft.Graphics.Canvas;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Security.Authorization.AppCapabilityAccess;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

namespace GeminiAssistant.Services
{
    public sealed class ScreenCaptureService
    {
        private const string ChatWidgetExtensionId = "ChatWidget";

        public async Task<PendingScreenshot> CaptureAsync(GameContextInfo gameContext, XboxGameBarWidget widget = null)
        {
            if (!GraphicsCaptureSession.IsSupported())
            {
                throw new InvalidOperationException(
                    "La captura de pantalla no esta disponible en este equipo.");
            }

            var minimizedWidget = false;
            XboxGameBarWidgetControl widgetControl = null;
            if (widget != null)
            {
                widgetControl = new XboxGameBarWidgetControl(widget);
                try
                {
                    await widgetControl.MinimizeAsync(ChatWidgetExtensionId).AsTask().ConfigureAwait(true);
                    minimizedWidget = true;
                    await Task.Delay(350).ConfigureAwait(true);
                }
                catch
                {
                }
            }

            try
            {
                return await CaptureCoreAsync(gameContext).ConfigureAwait(true);
            }
            finally
            {
                if (minimizedWidget && widgetControl != null)
                {
                    try
                    {
                        await widgetControl.RestoreAsync(ChatWidgetExtensionId).AsTask().ConfigureAwait(true);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static async Task<PendingScreenshot> CaptureCoreAsync(GameContextInfo gameContext)
        {
            WindowHandleInterop.TryGetWindowHandle(out var widgetHwnd);

            var access = await ProgrammaticCaptureHelper.RequestCaptureAccessAsync().ConfigureAwait(true);

            var candidates = GameTargetWindowResolver.ResolveCaptureWindowCandidates(gameContext, widgetHwnd);
            var item = TryCreateCaptureItemFromCandidates(candidates);

            if (item == null && gameContext != null && gameContext.IsFullscreen)
            {
                foreach (var hwnd in candidates)
                {
                    var monitor = GameTargetWindowResolver.ResolveMonitorForWindow(hwnd);
                    item = ProgrammaticCaptureHelper.TryCreateForMonitor(monitor);
                    if (item != null)
                    {
                        break;
                    }
                }
            }

            if (item == null)
            {
                item = await PickCaptureItemAsync(widgetHwnd).ConfigureAwait(true);
            }

            if (item == null)
            {
                if (access != AppCapabilityAccessStatus.Allowed)
                {
                    throw new InvalidOperationException(
                        "Sin permiso de captura (" +
                        ProgrammaticCaptureHelper.DescribeAccessStatus(access) +
                        "). Pulsa Capturar de nuevo y elige Permitir.");
                }

                var hint = candidates.Count > 0
                    ? "Se encontraron " + candidates.Count + " ventanas pero ninguna se pudo capturar. "
                    : "No se encontro la ventana del proceso. ";

                throw new InvalidOperationException(
                    hint + "Se abrira el selector: elige la ventana del juego (icono de app), no el monitor.");
            }

            return await CaptureFramesAsync(item).ConfigureAwait(true);
        }

        private static GraphicsCaptureItem TryCreateCaptureItemFromCandidates(System.Collections.Generic.IReadOnlyList<IntPtr> candidates)
        {
            foreach (var hwnd in candidates)
            {
                var item = ProgrammaticCaptureHelper.TryCreateForWindow(hwnd);
                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        private static async Task<PendingScreenshot> CaptureFramesAsync(GraphicsCaptureItem item)
        {
            if (item.Size.Width <= 0 || item.Size.Height <= 0)
            {
                throw new InvalidOperationException(
                    "El destino de captura no tiene un tamano valido.");
            }

            var canvasDevice = CanvasDevice.GetSharedDevice();
            var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                canvasDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                item.Size);

            var session = framePool.CreateCaptureSession(item);
            session.IsBorderRequired = false;
            session.StartCapture();

            await Task.Delay(200).ConfigureAwait(true);

            Direct3D11CaptureFrame frame = null;
            try
            {
                for (var attempt = 0; attempt < 60; attempt++)
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
                    throw new InvalidOperationException(
                        "No se pudo obtener un fotograma. Vuelve al juego e intenta de nuevo.");
                }

                using (var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(canvasDevice, frame.Surface))
                {
                    var jpegBytes = await EncodeJpegAsync(bitmap).ConfigureAwait(true);
                    if (jpegBytes == null || jpegBytes.Length < 100)
                    {
                        throw new InvalidOperationException("La captura salio vacia.");
                    }

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

        private static async Task<GraphicsCaptureItem> PickCaptureItemAsync(IntPtr ownerHwnd)
        {
            var picker = new GraphicsCapturePicker();
            WindowHandleInterop.TrySetOwnerWindow(picker, ownerHwnd);
            return await picker.PickSingleItemAsync().AsTask().ConfigureAwait(true);
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
