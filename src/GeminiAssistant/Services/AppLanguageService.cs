using System;
using System.Globalization;
using GeminiAssistant.Properties;
using Windows.Globalization;
using Windows.System.UserProfile;

namespace GeminiAssistant.Services
{
    public static class AppLanguageService
    {
        public const string PreferenceAuto = "auto";
        public const string SpanishTag = "es-ES";
        public const string EnglishTag = "en-US";

        public static CultureInfo CurrentCulture { get; private set; } = new CultureInfo(SpanishTag);

        public static string GetPreference()
        {
            var value = AppSettingsService.GetLanguagePreference();
            return NormalizePreference(string.IsNullOrWhiteSpace(value) ? PreferenceAuto : value);
        }

        public static string NormalizePreference(string preference)
        {
            if (string.IsNullOrWhiteSpace(preference) ||
                string.Equals(preference, PreferenceAuto, StringComparison.OrdinalIgnoreCase))
            {
                return PreferenceAuto;
            }

            if (preference.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                return EnglishTag;
            }

            if (preference.StartsWith("es", StringComparison.OrdinalIgnoreCase))
            {
                return SpanishTag;
            }

            return PreferenceAuto;
        }

        public static void SetPreference(string preference)
        {
            AppSettingsService.SetLanguagePreference(NormalizePreference(preference ?? PreferenceAuto));
        }

        public static string GetEffectiveLanguageTag()
        {
            var preference = GetPreference();
            if (preference == SpanishTag || preference == EnglishTag)
            {
                return preference;
            }

            return ResolveFromSystemLanguage();
        }

        public static void ApplySavedOrSystemLanguage()
        {
            ApplyForPreference(GetPreference());
        }

        public static void ApplyLanguageTag(string languageTag)
        {
            var normalized = NormalizePreference(languageTag);
            if (normalized == PreferenceAuto)
            {
                ApplyForPreference(PreferenceAuto);
                return;
            }

            ApplyForPreference(normalized);
        }

        private static void ApplyForPreference(string preference)
        {
            preference = NormalizePreference(preference);
            var isAuto = preference == PreferenceAuto;
            var effectiveTag = isAuto ? ResolveFromSystemLanguage() : preference;
            var culture = CultureInfo.GetCultureInfo(effectiveTag);
            var languageOverride = isAuto ? string.Empty : effectiveTag;

            var cultureChanged = CurrentCulture == null ||
                !string.Equals(CurrentCulture.Name, culture.Name, StringComparison.OrdinalIgnoreCase);
            var overrideChanged = !string.Equals(
                ApplicationLanguages.PrimaryLanguageOverride ?? string.Empty,
                languageOverride,
                StringComparison.OrdinalIgnoreCase);

            if (!cultureChanged && !overrideChanged)
            {
                return;
            }

            ApplicationLanguages.PrimaryLanguageOverride = languageOverride;
            CurrentCulture = culture;
            Resources.Culture = CurrentCulture;
            WidgetFileLog.Write(
                "Idioma aplicado: pref=" + preference +
                " efectivo=" + effectiveTag +
                " override=" + (string.IsNullOrEmpty(languageOverride) ? "(sistema)" : languageOverride));
        }

        public static bool IsEnglish()
        {
            var tag = CurrentCulture?.Name ?? GetEffectiveLanguageTag();
            return tag.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetGeminiResponseLanguageName()
        {
            return IsEnglish() ? "English" : "Spanish";
        }

        public static string[] GetSpeechLanguageTags()
        {
            if (IsEnglish())
            {
                return new[] { EnglishTag, "en-GB" };
            }

            return new[] { SpanishTag, "es-MX" };
        }

        private static string ResolveFromSystemLanguage()
        {
            var systemTag = GetPrimarySystemLanguageTag();
            if (systemTag.StartsWith("es", StringComparison.OrdinalIgnoreCase))
            {
                return SpanishTag;
            }

            return EnglishTag;
        }

        private static string GetPrimarySystemLanguageTag()
        {
            try
            {
                var userLanguages = GlobalizationPreferences.Languages;
                if (userLanguages != null && userLanguages.Count > 0 && !string.IsNullOrWhiteSpace(userLanguages[0]))
                {
                    return userLanguages[0];
                }
            }
            catch
            {
            }

            try
            {
                var previousOverride = ApplicationLanguages.PrimaryLanguageOverride;
                ApplicationLanguages.PrimaryLanguageOverride = string.Empty;

                var languages = ApplicationLanguages.Languages;
                if (languages != null && languages.Count > 0 && !string.IsNullOrWhiteSpace(languages[0]))
                {
                    var tag = languages[0];
                    ApplicationLanguages.PrimaryLanguageOverride = previousOverride ?? string.Empty;
                    return tag;
                }

                ApplicationLanguages.PrimaryLanguageOverride = previousOverride ?? string.Empty;
            }
            catch
            {
            }

            return EnglishTag;
        }
    }
}
