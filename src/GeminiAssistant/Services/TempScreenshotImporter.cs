using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace GeminiAssistant.Services
{
    internal static class TempScreenshotImporter
    {
        private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(2);

        public static async Task<byte[]> TryImportJpegAsync()
        {
            try
            {
                var temp = Path.GetTempPath();
                if (string.IsNullOrEmpty(temp) || !Directory.Exists(temp))
                {
                    return null;
                }

                var cutoff = DateTimeOffset.UtcNow - MaxAge;
                string bestPath = null;
                var bestTime = DateTimeOffset.MinValue;

                Scan(temp, cutoff, ref bestPath, ref bestTime, maxDepth: 2);

                if (string.IsNullOrEmpty(bestPath))
                {
                    return null;
                }

                WidgetFileLog.Write("Captura TEMP: " + bestPath);
                var file = await StorageFile.GetFileFromPathAsync(bestPath).AsTask().ConfigureAwait(false);
                return await ClipboardScreenshotImporter.TryImportFromStorageFileAsync(file).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura TEMP: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }

        private static void Scan(
            string dir,
            DateTimeOffset cutoff,
            ref string bestPath,
            ref DateTimeOffset bestTime,
            int maxDepth)
        {
            if (maxDepth < 0)
            {
                return;
            }

            string[] files;
            string[] subdirs;
            try
            {
                files = Directory.GetFiles(dir);
                subdirs = maxDepth > 0 ? Directory.GetDirectories(dir) : Array.Empty<string>();
            }
            catch
            {
                return;
            }

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                if (ext == null ||
                    (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                     !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                     !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                DateTimeOffset t;
                try
                {
                    t = new DateTimeOffset(File.GetLastWriteTimeUtc(file));
                }
                catch
                {
                    continue;
                }

                if (t < cutoff || t <= bestTime)
                {
                    continue;
                }

                if (!CaptureSessionClock.IsRecentEnough(t))
                {
                    continue;
                }

                bestTime = t;
                bestPath = file;
            }

            foreach (var sub in subdirs)
            {
                Scan(sub, cutoff, ref bestPath, ref bestTime, maxDepth - 1);
            }
        }
    }
}
