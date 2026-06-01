using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Busca capturas recientes en carpetas del paquete Xbox Game Bar (cuando no estan en Videos\Captures).
    /// </summary>
    internal static class GameBarPackageCaptureImporter
    {
        private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(2);

        public static async Task<byte[]> TryImportJpegAsync()
        {
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var packagesRoot = Path.Combine(localAppData, "Packages");
                if (!Directory.Exists(packagesRoot))
                {
                    return null;
                }

                var cutoff = DateTimeOffset.UtcNow - MaxAge;
                var bestPath = (string)null;
                var bestTime = DateTimeOffset.MinValue;

                foreach (var packageDir in Directory.EnumerateDirectories(packagesRoot))
                {
                    var name = Path.GetFileName(packageDir);
                    if (name == null ||
                        name.IndexOf("XboxGamingOverlay", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    WidgetFileLog.Write("Captura paquete: " + name);
                    ScanDirectoryTree(packageDir, cutoff, ref bestPath, ref bestTime);
                }

                if (string.IsNullOrEmpty(bestPath))
                {
                    return null;
                }

                WidgetFileLog.Write("Captura paquete archivo: " + bestPath);
                var file = await StorageFile.GetFileFromPathAsync(bestPath).AsTask().ConfigureAwait(false);
                return await ClipboardScreenshotImporter.TryImportFromStorageFileAsync(file).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura paquete: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }

        private static void ScanDirectoryTree(
            string root,
            DateTimeOffset cutoff,
            ref string bestPath,
            ref DateTimeOffset bestTime)
        {
            var queue = new Queue<string>();
            queue.Enqueue(root);
            var scanned = 0;

            while (queue.Count > 0 && scanned < 120)
            {
                var dir = queue.Dequeue();
                scanned++;

                IEnumerable<string> files;
                IEnumerable<string> dirs;
                try
                {
                    files = Directory.EnumerateFiles(dir);
                    dirs = Directory.EnumerateDirectories(dir);
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                {
                    if (!IsImageFile(file))
                    {
                        continue;
                    }

                    DateTimeOffset lastWrite;
                    try
                    {
                        lastWrite = new DateTimeOffset(File.GetLastWriteTimeUtc(file));
                    }
                    catch
                    {
                        continue;
                    }

                    if (lastWrite < cutoff)
                    {
                        continue;
                    }

                    if (!CaptureSessionClock.IsRecentEnough(lastWrite))
                    {
                        continue;
                    }

                    if (lastWrite > bestTime)
                    {
                        bestTime = lastWrite;
                        bestPath = file;
                    }
                }

                foreach (var sub in dirs)
                {
                    queue.Enqueue(sub);
                }
            }
        }

        private static bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path);
            return ext != null &&
                   (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase));
        }
    }
}
