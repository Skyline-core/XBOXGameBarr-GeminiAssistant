using Windows.Storage;

namespace GeminiAssistant.Services
{
    internal static class WidgetDiagnostics
    {
        private const string LastErrorKey = "WidgetLastError";

        public static void SetLastError(string message)
        {
            WidgetFileLog.Write(message);
        }

        public static string GetLastError()
        {
            var fromFile = WidgetFileLog.GetRecentSummary();
            if (!string.IsNullOrEmpty(fromFile))
            {
                return fromFile;
            }

            return ApplicationData.Current.LocalSettings.Values[LastErrorKey] as string;
        }
    }
}
