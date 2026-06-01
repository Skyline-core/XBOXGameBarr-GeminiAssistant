using Windows.Storage;

namespace GeminiAssistant.Services
{
    internal static class CaptureFolderSettings
    {
        private const string LastFolderPathKey = "LastCaptureFolderPath";

        public static string GetSavedFolderPath()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return values.TryGetValue(LastFolderPathKey, out var raw) ? raw as string : null;
        }

        public static void SaveFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            ApplicationData.Current.LocalSettings.Values[LastFolderPathKey] = folderPath.Trim();
            WidgetFileLog.Write("Captura carpeta guardada: " + folderPath);
        }
    }
}
