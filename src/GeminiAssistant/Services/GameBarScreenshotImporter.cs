using System;
using System.Threading.Tasks;

namespace GeminiAssistant.Services
{
    internal static class GameBarScreenshotImporter
    {
        public static async Task<byte[]> TryImportWithPollingAsync(
            string preferredGameName,
            int totalWaitMs,
            int intervalMs)
        {
            var attempts = Math.Max(1, totalWaitMs / Math.Max(200, intervalMs));
            WidgetFileLog.Write("Captura Game Bar polling " + attempts + " x " + intervalMs + "ms");

            for (var i = 0; i < attempts; i++)
            {
                if (i > 0)
                {
                    await Task.Delay(intervalMs).ConfigureAwait(false);
                }

                var jpeg = await TryImportOnceAsync(preferredGameName).ConfigureAwait(false);
                if (IsValid(jpeg))
                {
                    return jpeg;
                }
            }

            WidgetFileLog.Write("Captura Game Bar polling sin resultado");
            return null;
        }

        private static async Task<byte[]> TryImportOnceAsync(string preferredGameName)
        {
            var fromFolder = await RecentGameCaptureImporter.TryImportJpegAsync(preferredGameName)
                .ConfigureAwait(false);
            if (IsValid(fromFolder))
            {
                WidgetFileLog.Write("Captura OK carpeta");
                return fromFolder;
            }

            var fromPackage = await GameBarPackageCaptureImporter.TryImportJpegAsync().ConfigureAwait(false);
            if (IsValid(fromPackage))
            {
                WidgetFileLog.Write("Captura OK paquete Game Bar");
                return fromPackage;
            }

            var fromTemp = await TempScreenshotImporter.TryImportJpegAsync().ConfigureAwait(false);
            if (IsValid(fromTemp))
            {
                WidgetFileLog.Write("Captura OK TEMP");
                return fromTemp;
            }

            // Portapapeles solo desde el hilo UI (ver ChatWidget.TryMergeClipboardCaptureOnUiAsync).
            return null;
        }

        private static bool IsValid(byte[] jpegBytes)
        {
            return jpegBytes != null && jpegBytes.Length >= 100;
        }
    }
}
