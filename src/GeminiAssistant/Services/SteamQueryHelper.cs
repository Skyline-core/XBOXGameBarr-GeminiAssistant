using System;
using System.Text.RegularExpressions;

namespace GeminiAssistant.Services
{
    internal enum SteamQueryIntent
    {
        None,
        LastAchievement,
        Profile,
        Achievements,
        General
    }

    internal static class SteamQueryHelper
    {
        public static bool ShouldFetchSteamData(string text, string gameBarDisplayName = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!AppSettingsService.HasSteamCredentials())
            {
                return false;
            }

            var normalized = NormalizeQueryText(text);
            if (IsSteamRelated(normalized) || MentionsAchievements(normalized))
            {
                return true;
            }

            if (IsGameContextRefreshRequest(normalized) &&
                !string.IsNullOrWhiteSpace(gameBarDisplayName))
            {
                return true;
            }

            if (ContainsAny(normalized, "falt", "missing", "cuales me", "cuáles me") &&
                (!string.IsNullOrWhiteSpace(TryExtractGameName(text)) ||
                 !string.IsNullOrWhiteSpace(gameBarDisplayName)))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(gameBarDisplayName) &&
                   LooksLikeAchievementQuestion(normalized);
        }

        public static bool IsGameContextRefreshRequest(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var normalized = NormalizeQueryText(text);
            return ContainsAny(
                normalized,
                "cambie de juego",
                "cambié de juego",
                "cambie el juego",
                "cambié el juego",
                "nuevo juego",
                "ya cambie",
                "ya cambié",
                "juego diferente",
                "otro juego",
                "listo ya",
                "ya la tienes en tus ajustes",
                "ya la tienes en ajustes",
                "ya esta en ajustes",
                "ya está en ajustes",
                "ya tienes la api",
                "ya tienes el steamid");
        }

        public static string SanitizeGameDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            name = name.Trim();
            name = Regex.Replace(name, @"\s+v?\d+\.\d+(\.\d+)*(\.\d+)*\s*$", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"\s+\(\d+\)\s*$", string.Empty);

            var colonIdx = name.IndexOf(':');
            if (colonIdx > 0 && colonIdx < name.Length - 1)
            {
                var beforeColon = name.Substring(0, colonIdx).Trim();
                if (beforeColon.Length >= 3)
                {
                    name = beforeColon;
                }
            }

            return name.Trim();
        }

        public static bool IsSteamRelated(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var lower = NormalizeQueryText(text);
            return ContainsAny(
                lower,
                "steam",
                "logro",
                "logros",
                "achievement",
                "achievements",
                "trofeo",
                "trofeos",
                "biblioteca steam",
                "perfil steam",
                "cuenta steam");
        }

        public static string NormalizeQueryText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var lower = text.ToLowerInvariant();
            lower = Regex.Replace(lower, @"\blogoros\b", "logros");
            lower = Regex.Replace(lower, @"\blogoro\b", "logro");
            lower = Regex.Replace(lower, @"\bstema\b", "steam");
            lower = Regex.Replace(lower, @"\bstem\b", "steam");
            lower = Regex.Replace(lower, @"\bachivement", "achievement");
            lower = Regex.Replace(lower, @"\bachivements", "achievements");
            lower = Regex.Replace(lower, @"\bstrandig\b", "stranding");
            return lower;
        }

        public static string TryExtractGameName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var lower = NormalizeQueryText(text);
            var markers = new[]
            {
                "cuales son los que me faltan de ",
                "cuáles son los que me faltan de ",
                "cuales me faltan de ",
                "cuáles me faltan de ",
                "que me faltan de ",
                "qué me faltan de ",
                "me faltan de ",
                "me faltan en ",
                "faltan de ",
                "faltan en ",
                "logros en ",
                "logros de ",
                "logro en ",
                "logro de ",
                "achievements in ",
                "achievement in ",
                "achievements for ",
                "achievement for "
            };

            foreach (var marker in markers)
            {
                var idx = lower.IndexOf(marker, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    return CleanGameName(text.Substring(idx + marker.Length), lower.Substring(idx + marker.Length));
                }
            }

            var deMatch = Regex.Match(
                lower,
                @"(?:faltan|faltanme|missing|conseguir|desbloq).*\bde\s+(.+)$",
                RegexOptions.IgnoreCase);
            if (deMatch.Success)
            {
                var start = deMatch.Groups[1].Index;
                var length = deMatch.Groups[1].Length;
                return CleanGameName(text.Substring(start, length), deMatch.Groups[1].Value);
            }

            var enIdx = lower.LastIndexOf(" en ", StringComparison.Ordinal);
            if (enIdx >= 0 && (MentionsAchievements(lower) || ContainsAny(lower, "falt")))
            {
                return CleanGameName(text.Substring(enIdx + 4), lower.Substring(enIdx + 4));
            }

            var inIdx = lower.LastIndexOf(" in ", StringComparison.Ordinal);
            if (inIdx >= 0 && (MentionsAchievements(lower) || ContainsAny(lower, "falt", "missing")))
            {
                return CleanGameName(text.Substring(inIdx + 4), lower.Substring(inIdx + 4));
            }

            return null;
        }

        public static string ResolveGameNameForQuery(
            string userText,
            string gameBarDisplayName,
            bool trackingGameIsLikelyGame = true,
            string lastSteamGameQuery = null)
        {
            var sanitizedBar = SanitizeGameDisplayName(gameBarDisplayName);
            var normalized = NormalizeQueryText(userText);

            var fromQuery = TryExtractGameName(userText) ?? TryExtractQuotedGameName(userText);
            if (!string.IsNullOrWhiteSpace(fromQuery) && !RefersToCurrentGame(NormalizeQueryText(fromQuery)))
            {
                return SanitizeGameDisplayName(fromQuery);
            }

            if (IsSimilarityOrLibrarySearch(normalized) && !string.IsNullOrWhiteSpace(lastSteamGameQuery))
            {
                return SanitizeGameDisplayName(lastSteamGameQuery);
            }

            if (RefersToCurrentGame(normalized) || IsGameContextRefreshRequest(normalized))
            {
                return trackingGameIsLikelyGame ? sanitizedBar : null;
            }

            if (trackingGameIsLikelyGame &&
                !string.IsNullOrWhiteSpace(sanitizedBar) &&
                !LocalizedStrings.IsUnknownGameName(sanitizedBar) &&
                (MentionsAchievements(normalized) || LooksLikeAchievementQuestion(normalized)))
            {
                return sanitizedBar;
            }

            if (!string.IsNullOrWhiteSpace(fromQuery))
            {
                return SanitizeGameDisplayName(fromQuery);
            }

            return null;
        }

        public static SteamQueryIntent DetectIntent(string text, string gameBarDisplayName = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return SteamQueryIntent.None;
            }

            var lower = NormalizeQueryText(text);
            if (ContainsAny(
                lower,
                "ultimo logro",
                "último logro",
                "ultima conquista",
                "última conquista",
                "logro mas reciente",
                "logro más reciente",
                "logro reciente",
                "last achievement",
                "recent achievement"))
            {
                return SteamQueryIntent.LastAchievement;
            }

            if (ContainsAny(
                lower,
                "perfil",
                "biblioteca",
                "mis juegos",
                "juegos tengo",
                "horas jugadas",
                "estadistica",
                "estadística",
                "stats",
                "cuantos juegos",
                "cuántos juegos"))
            {
                return SteamQueryIntent.Profile;
            }

            if (MentionsAchievements(lower) ||
                LooksLikeAchievementQuestion(lower) ||
                (IsGameContextRefreshRequest(lower) && !string.IsNullOrWhiteSpace(gameBarDisplayName)))
            {
                return SteamQueryIntent.Achievements;
            }

            if (lower.Contains("steam", StringComparison.Ordinal))
            {
                return SteamQueryIntent.General;
            }

            return SteamQueryIntent.None;
        }

        private static bool IsSimilarityOrLibrarySearch(string normalizedLower)
        {
            return ContainsAny(
                normalizedLower,
                "parecido",
                "parecida",
                "similar",
                "se parece",
                "algo como",
                "algo parecido",
                "busca algo",
                "buscar algo",
                "en mi biblioteca",
                "like",
                "similar to",
                "something like",
                "look for similar",
                "find similar");
        }

        private static string TryExtractQuotedGameName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = Regex.Match(text, @"[""«]([^""»]+)[""»]");
            if (!match.Success || match.Groups[1].Value.Trim().Length < 2)
            {
                return null;
            }

            return SanitizeGameDisplayName(match.Groups[1].Value);
        }

        private static bool RefersToCurrentGame(string normalizedLower)
        {
            return ContainsAny(
                normalizedLower,
                "este juego",
                "this game",
                "el juego actual",
                "juego actual",
                "jugando ahora",
                "en el que estoy",
                "el que estoy jugando");
        }

        private static bool LooksLikeAchievementQuestion(string normalizedLower)
        {
            if (ContainsAny(
                normalizedLower,
                "logro",
                "logros",
                "achievement",
                "achievements",
                "trofeo",
                "trofeos",
                "conquista",
                "conquistas",
                "logor"))
            {
                return ContainsAny(
                    normalizedLower,
                    "falt",
                    "missing",
                    "cuales",
                    "cuáles",
                    "que me",
                    "qué me",
                    "desbloq",
                    "unlock",
                    "consegu",
                    "complet",
                    "lista");
            }

            return ContainsAny(
                normalizedLower,
                "me faltan",
                "faltan de ",
                "cuales me faltan",
                "cuáles me faltan",
                "cuales son los que me faltan",
                "cuáles son los que me faltan",
                "what am i missing",
                "which ones am i missing",
                "which achievements");
        }

        private static bool MentionsAchievements(string lower)
        {
            return ContainsAny(
                lower,
                "logro",
                "logros",
                "achievement",
                "achievements",
                "trofeo",
                "trofeos",
                "conquista",
                "conquistas",
                "logor");
        }

        private static string CleanGameName(string value, string normalizedTail = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim().TrimEnd('?', '.', '!', ' ');
            if (value.Length < 2)
            {
                return null;
            }

            var check = normalizedTail ?? NormalizeQueryText(value);
            if (check.Contains("steam", StringComparison.Ordinal) ||
                RefersToCurrentGame(check) ||
                IsGameContextRefreshRequest(check))
            {
                return null;
            }

            if (check.StartsWith("steam ", StringComparison.Ordinal) ||
                check.StartsWith("steam de ", StringComparison.Ordinal))
            {
                return null;
            }

            return SanitizeGameDisplayName(value);
        }

        private static bool ContainsAny(string haystack, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if (haystack.Contains(needle, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
