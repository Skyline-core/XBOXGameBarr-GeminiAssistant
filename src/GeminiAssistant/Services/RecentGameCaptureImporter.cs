using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Busca la captura mas reciente en las bibliotecas de usuario que expone Windows
    /// (Videos, Imagenes, etc.), sin asumir rutas fijas como Videos\Captures.
    /// </summary>
    internal static class RecentGameCaptureImporter
    {
        private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(2);

        private static readonly IReadOnlyList<string> ImageExtensions = new[]
        {
            ".png",
            ".jpg",
            ".jpeg"
        };

        public static async Task<byte[]> TryImportJpegAsync(string preferredGameName)
        {
            try
            {
                var cutoff = DateTimeOffset.UtcNow - MaxAge;
                var best = default(NewestImage);

                var systemCaptures = await SystemCapturesFolderResolver.TryGetCapturesFolderAsync()
                    .ConfigureAwait(false);
                if (systemCaptures != null)
                {
                    best = await FindNewestInFolderAsync(systemCaptures, preferredGameName, cutoff)
                        .ConfigureAwait(false);
                }

                var roots = await CollectLibraryRootsAsync().ConfigureAwait(false);
                if (roots.Count == 0 && best.File == null)
                {
                    WidgetFileLog.Write("Captura disco: sin bibliotecas del sistema");
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(preferredGameName))
                {
                    foreach (var root in roots)
                    {
                        best = PickNewer(
                            best,
                            await QueryNewestImageInLibraryAsync(root, preferredGameName, cutoff)
                                .ConfigureAwait(false));
                    }
                }

                if (best.File == null)
                {
                    foreach (var root in roots)
                    {
                        best = PickNewer(
                            best,
                            await QueryNewestImageInLibraryAsync(root, preferredSubfolderName: null, cutoff)
                                .ConfigureAwait(false));
                    }
                }

                if (best.File == null)
                {
                    WidgetFileLog.Write(
                        "Captura disco: sin PNG/JPG reciente (ultimos " + (int)MaxAge.TotalMinutes + " min)");
                    return null;
                }

                WidgetFileLog.Write(
                    "Captura disco OK: " + best.File.Path +
                    " mod=" + best.Modified.ToLocalTime().ToString("HH:mm:ss"));
                return await ClipboardScreenshotImporter.TryImportFromStorageFileAsync(best.File)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura disco: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }

        private static async Task<NewestImage> FindNewestInFolderAsync(
            StorageFolder folder,
            string preferredGameName,
            DateTimeOffset cutoff)
        {
            var best = default(NewestImage);
            if (!string.IsNullOrWhiteSpace(preferredGameName))
            {
                best = await QueryNewestImageInLibraryAsync(folder, preferredGameName, cutoff)
                    .ConfigureAwait(false);
            }

            if (best.File == null)
            {
                best = await QueryNewestImageInLibraryAsync(folder, preferredSubfolderName: null, cutoff)
                    .ConfigureAwait(false);
            }

            if (best.File == null)
            {
                best = await ScanFolderTreeManuallyAsync(folder, preferredGameName, cutoff).ConfigureAwait(false);
            }

            return best;
        }

        private static async Task<NewestImage> ScanFolderTreeManuallyAsync(
            StorageFolder folder,
            string preferredGameName,
            DateTimeOffset cutoff)
        {
            var best = default(NewestImage);
            var queue = new Queue<StorageFolder>();
            queue.Enqueue(folder);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                IReadOnlyList<IStorageItem> items;
                try
                {
                    items = await current.GetItemsAsync().AsTask().ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }

                foreach (var item in items)
                {
                    if (item is StorageFolder sub)
                    {
                        queue.Enqueue(sub);
                        continue;
                    }

                    if (item is not StorageFile file || !IsImageFile(file.Name))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(preferredGameName) &&
                        !PathMatchesGame(file.Path, preferredGameName))
                    {
                        continue;
                    }

                    BasicProperties props;
                    try
                    {
                        props = await file.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
                    }
                    catch
                    {
                        continue;
                    }

                    if (props.DateModified < cutoff)
                    {
                        continue;
                    }

                    if (!CaptureSessionClock.IsRecentEnough(props.DateModified))
                    {
                        continue;
                    }

                    best = PickNewer(best, new NewestImage(file, props.DateModified));
                }
            }

            return best;
        }

        private static async Task<List<StorageFolder>> CollectLibraryRootsAsync()
        {
            var roots = new List<StorageFolder>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void TryAdd(StorageFolder folder, string source)
            {
                if (folder == null)
                {
                    return;
                }

                var path = folder.Path ?? string.Empty;
                if (path.Length == 0 || !seenPaths.Add(path))
                {
                    return;
                }

                roots.Add(folder);
                WidgetFileLog.Write("Captura disco raiz (" + source + "): " + path);
            }

            var savedPath = CaptureFolderSettings.GetSavedFolderPath();
            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                try
                {
                    var savedFolder = await StorageFolder.GetFolderFromPathAsync(savedPath).AsTask()
                        .ConfigureAwait(false);
                    TryAdd(savedFolder, "Usuario.Guardada");
                }
                catch (Exception ex)
                {
                    WidgetFileLog.Write("Captura disco carpeta guardada: " + WidgetExceptionFormatter.Format(ex));
                }
            }

            try
            {
                TryAdd(KnownFolders.VideosLibrary, "KnownFolders.VideosLibrary");
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura disco VideosLibrary: " + WidgetExceptionFormatter.Format(ex));
            }

            try
            {
                TryAdd(KnownFolders.PicturesLibrary, "KnownFolders.PicturesLibrary");
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura disco PicturesLibrary: " + WidgetExceptionFormatter.Format(ex));
            }

            try
            {
                var videosLib = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Videos).AsTask()
                    .ConfigureAwait(false);
                TryAdd(videosLib?.SaveFolder, "StorageLibrary.Videos");
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura disco StorageLibrary.Videos: " + WidgetExceptionFormatter.Format(ex));
            }

            try
            {
                var picturesLib = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures).AsTask()
                    .ConfigureAwait(false);
                TryAdd(picturesLib?.SaveFolder, "StorageLibrary.Pictures");
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura disco StorageLibrary.Pictures: " + WidgetExceptionFormatter.Format(ex));
            }

            return roots;
        }

        private static async Task<NewestImage> QueryNewestImageInLibraryAsync(
            StorageFolder libraryRoot,
            string preferredSubfolderName,
            DateTimeOffset cutoff)
        {
            var best = default(NewestImage);
            IReadOnlyList<StorageFile> files;

            try
            {
                files = await QueryRecentImagesAsync(libraryRoot, useIndexer: true).ConfigureAwait(false);
                if (files == null || files.Count == 0)
                {
                    files = await QueryRecentImagesAsync(libraryRoot, useIndexer: false).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write(
                    "Captura disco consulta " + libraryRoot.Path + ": " + WidgetExceptionFormatter.Format(ex));
                return best;
            }

            if (files == null || files.Count == 0)
            {
                return best;
            }

            foreach (var file in files)
            {
                if (!IsImageFile(file.Name))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(preferredSubfolderName) &&
                    !PathMatchesGame(file.Path, preferredSubfolderName))
                {
                    continue;
                }

                BasicProperties props;
                try
                {
                    props = await file.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }

                if (props.DateModified < cutoff)
                {
                    continue;
                }

                if (!CaptureSessionClock.IsRecentEnough(props.DateModified))
                {
                    continue;
                }

                best = PickNewer(best, new NewestImage(file, props.DateModified));
            }

            return best;
        }

        private static async Task<IReadOnlyList<StorageFile>> QueryRecentImagesAsync(
            StorageFolder libraryRoot,
            bool useIndexer)
        {
            var options = new QueryOptions(CommonFileQuery.OrderByDate, ImageExtensions)
            {
                FolderDepth = FolderDepth.Deep,
                IndexerOption = useIndexer
                    ? IndexerOption.UseIndexerWhenAvailable
                    : IndexerOption.DoNotUseIndexer
            };

            var query = libraryRoot.CreateFileQueryWithOptions(options);
            return await query.GetFilesAsync(0, 30).AsTask().ConfigureAwait(false);
        }

        private static NewestImage PickNewer(NewestImage current, NewestImage candidate)
        {
            if (candidate.File == null)
            {
                return current;
            }

            if (current.File == null || candidate.Modified > current.Modified)
            {
                return candidate;
            }

            return current;
        }

        private static bool PathMatchesGame(string path, string gameDisplayName)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(gameDisplayName))
            {
                return false;
            }

            return path.IndexOf(gameDisplayName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsImageFile(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            var ext = System.IO.Path.GetExtension(name);
            return ext != null &&
                   (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase));
        }

        private readonly struct NewestImage
        {
            public NewestImage(StorageFile file, DateTimeOffset modified)
            {
                File = file;
                Modified = modified;
            }

            public StorageFile File { get; }
            public DateTimeOffset Modified { get; }
        }
    }
}
