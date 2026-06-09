using Windows.Storage;

namespace GeminiAssistant.Services
{
    public static class AppSettingsService
    {
        private const string ApiKeyKey = "GeminiApiKey";
        private const string SteamApiKeyKey = "SteamWebApiKey";
        private const string SteamId64Key = "SteamId64";
        private const string LanguagePreferenceKey = "AppLanguagePreference";

        public static string GetApiKey()
        {
            return GetString(ApiKeyKey);
        }

        public static void SetApiKey(string apiKey)
        {
            SetString(ApiKeyKey, apiKey);
        }

        public static bool HasApiKey()
        {
            return !string.IsNullOrWhiteSpace(GetApiKey());
        }

        public static string GetSteamApiKey()
        {
            return GetString(SteamApiKeyKey);
        }

        public static void SetSteamApiKey(string apiKey)
        {
            SetString(SteamApiKeyKey, apiKey);
        }

        public static string GetSteamId64()
        {
            return GetString(SteamId64Key);
        }

        public static void SetSteamId64(string steamId64)
        {
            SetString(SteamId64Key, steamId64);
        }

        public static bool HasSteamCredentials()
        {
            return !string.IsNullOrWhiteSpace(GetSteamApiKey()) &&
                   !string.IsNullOrWhiteSpace(GetSteamId64());
        }

        public static string GetLanguagePreference()
        {
            return GetString(LanguagePreferenceKey);
        }

        public static void SetLanguagePreference(string preference)
        {
            SetString(LanguagePreferenceKey, preference);
        }

        private static string GetString(string key)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var value))
            {
                return value as string ?? string.Empty;
            }

            return string.Empty;
        }

        private static void SetString(string key, string value)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value ?? string.Empty;
        }
    }
}
