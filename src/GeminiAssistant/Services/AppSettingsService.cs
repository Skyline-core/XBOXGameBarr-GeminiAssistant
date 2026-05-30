using Windows.Storage;

namespace GeminiAssistant.Services
{
    public static class AppSettingsService
    {
        private const string ApiKeyKey = "GeminiApiKey";

        public static string GetApiKey()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(ApiKeyKey, out var value))
            {
                return value as string ?? string.Empty;
            }

            return string.Empty;
        }

        public static void SetApiKey(string apiKey)
        {
            ApplicationData.Current.LocalSettings.Values[ApiKeyKey] = apiKey ?? string.Empty;
        }

        public static bool HasApiKey()
        {
            return !string.IsNullOrWhiteSpace(GetApiKey());
        }
    }
}
