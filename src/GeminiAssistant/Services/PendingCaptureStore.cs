using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage;

namespace GeminiAssistant.Services
{
    internal static class PendingCaptureStore
    {
        public const string FileName = "pending_capture.jpg";
        private const string TempFileName = "pending_capture.tmp";
        private const string ReadyKey = "PendingCaptureReady";
        private const string LengthKey = "PendingCaptureLength";

        public static bool HasPending()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return values.ContainsKey(ReadyKey) &&
                   values[ReadyKey] is bool ready &&
                   ready &&
                   values.ContainsKey(LengthKey) &&
                   values[LengthKey] is int length &&
                   length >= 100;
        }

        public static async Task SaveAsync(byte[] jpegBytes)
        {
            if (jpegBytes == null || jpegBytes.Length == 0)
            {
                return;
            }

            var settings = ApplicationData.Current.LocalSettings.Values;
            settings[ReadyKey] = false;
            settings.Remove(LengthKey);

            var folder = ApplicationData.Current.LocalFolder;
            var tempFile = await folder
                .CreateFileAsync(TempFileName, CreationCollisionOption.ReplaceExisting)
                .AsTask()
                .ConfigureAwait(true);
            await FileIO.WriteBytesAsync(tempFile, jpegBytes).AsTask().ConfigureAwait(true);

            var destFile = await folder
                .CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting)
                .AsTask()
                .ConfigureAwait(true);
            await tempFile.CopyAndReplaceAsync(destFile).AsTask().ConfigureAwait(true);
            await tempFile.DeleteAsync().AsTask().ConfigureAwait(true);

            settings[ReadyKey] = true;
            settings[LengthKey] = jpegBytes.Length;
        }

        public static async Task<byte[]> LoadAsync()
        {
            if (!HasPending())
            {
                return null;
            }

            try
            {
                var file = await ApplicationData.Current.LocalFolder
                    .GetFileAsync(FileName)
                    .AsTask()
                    .ConfigureAwait(true);
                var buffer = await FileIO.ReadBufferAsync(file).AsTask().ConfigureAwait(true);
                var bytes = buffer.ToArray();
                if (bytes.Length < 100)
                {
                    await ClearAsync().ConfigureAwait(true);
                    return null;
                }

                return bytes;
            }
            catch
            {
                ClearFlag();
                return null;
            }
        }

        public static async Task ClearAsync()
        {
            ClearFlag();
            var folder = ApplicationData.Current.LocalFolder;
            await TryDeleteFileAsync(folder, FileName).ConfigureAwait(true);
            await TryDeleteFileAsync(folder, TempFileName).ConfigureAwait(true);
        }

        private static async Task TryDeleteFileAsync(StorageFolder folder, string name)
        {
            try
            {
                var file = await folder.GetFileAsync(name).AsTask().ConfigureAwait(true);
                await file.DeleteAsync().AsTask().ConfigureAwait(true);
            }
            catch
            {
            }
        }

        private static void ClearFlag()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            values.Remove(ReadyKey);
            values.Remove(LengthKey);
        }
    }
}
