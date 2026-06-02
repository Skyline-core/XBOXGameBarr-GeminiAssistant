using System;
using System.Threading;
using System.Threading.Tasks;
using GeminiAssistant.Models;

namespace GeminiAssistant.Services
{
    public sealed class ScreenCaptureService
    {
        private static readonly SemaphoreSlim CaptureLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Captura JPEG sin tocar la UI (seguro desde hilos de fondo).
        /// </summary>
        public async Task<PendingScreenshot> CaptureAsync(GameContextInfo gameContext)
        {
            await CaptureLock.WaitAsync().ConfigureAwait(false);
            CaptureSessionClock.Begin();
            try
            {
                WidgetFileLog.Write("Captura: inicio automatico");

                await GameBarHotkeyCaptureService.TryTriggerScreenshotHotkeyAsync().ConfigureAwait(false);

                var windowTask = GameWindowScreenshotCapture.TryCaptureAsync(gameContext);
                var importTask = GameBarScreenshotImporter.TryImportWithPollingAsync(
                    gameContext?.DisplayName,
                    totalWaitMs: 5500,
                    intervalMs: 300);

                var allFinished = Task.WhenAll(windowTask, importTask);
                while (!allFinished.IsCompleted)
                {
                    if (windowTask.IsCompleted)
                    {
                        var earlyWindow = await windowTask.ConfigureAwait(false);
                        if (IsValidJpeg(earlyWindow))
                        {
                            WidgetFileLog.Write("Captura automatica ventana OK (rapida)");
                            return BuildResult(earlyWindow, "ventana-juego");
                        }
                    }

                    if (importTask.IsCompleted)
                    {
                        var earlyImport = await importTask.ConfigureAwait(false);
                        if (IsValidJpeg(earlyImport))
                        {
                            WidgetFileLog.Write("Captura automatica Game Bar OK (rapida)");
                            return BuildResult(earlyImport, "gamebar");
                        }
                    }

                    await Task.WhenAny(allFinished, Task.Delay(200)).ConfigureAwait(false);
                }

                var windowBytes = await windowTask.ConfigureAwait(false);
                if (IsValidJpeg(windowBytes))
                {
                    WidgetFileLog.Write("Captura automatica ventana OK");
                    return BuildResult(windowBytes, "ventana-juego");
                }

                var importBytes = await importTask.ConfigureAwait(false);
                if (IsValidJpeg(importBytes))
                {
                    WidgetFileLog.Write("Captura automatica Game Bar OK");
                    return BuildResult(importBytes, "gamebar");
                }

                WidgetFileLog.Write(
                    "Captura fallida seguimiento=" + (gameContext?.TrackingEnabled == true) +
                    " titulo=" + (gameContext?.DisplayName ?? "?"));
                throw new InvalidOperationException(
                    "No se pudo capturar. Comprueba que el juego este visible, que Game Bar siga el juego " +
                    "(icono de seguimiento activo) y que Windows permita captura de pantalla para esta app.");
            }
            finally
            {
                CaptureSessionClock.End();
                CaptureLock.Release();
            }
        }

        private static PendingScreenshot BuildResult(byte[] jpegBytes, string source)
        {
            return new PendingScreenshot { JpegBytes = jpegBytes, Source = source };
        }

        private static bool IsValidJpeg(byte[] jpegBytes)
        {
            return jpegBytes != null && jpegBytes.Length >= 100;
        }
    }
}
