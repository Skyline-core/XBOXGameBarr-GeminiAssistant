using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeminiAssistant.Models;

namespace GeminiAssistant.Services
{
    internal enum SteamGameMatchStatus
    {
        NotFound,
        Unique,
        Ambiguous
    }

    internal sealed class SteamGameMatchCandidate
    {
        public uint AppId { get; set; }
        public string Name { get; set; }
        public int Score { get; set; }
    }

    internal sealed class SteamGameMatchResult
    {
        public SteamGameMatchStatus Status { get; set; }
        public uint? AppId { get; set; }
        public string MatchedName { get; set; }
        public string Query { get; set; }
        public List<SteamGameMatchCandidate> Candidates { get; set; } = new List<SteamGameMatchCandidate>();

        public string BuildUserMessage()
        {
            if (Status == SteamGameMatchStatus.Ambiguous)
            {
                return BuildAmbiguousMessage();
            }

            if (Status == SteamGameMatchStatus.NotFound)
            {
                return BuildNotFoundMessage();
            }

            return null;
        }

        private string BuildAmbiguousMessage()
        {
            var sb = new StringBuilder();
            sb.AppendLine(LocalizedStrings.Format("Steam_GameAmbiguous", Query));
            foreach (var candidate in Candidates.Take(6))
            {
                sb.AppendLine(LocalizedStrings.Format("Steam_GameMatchLine", candidate.Name));
            }

            sb.Append(LocalizedStrings.Get("Steam_GameAmbiguousAsk"));
            return sb.ToString().Trim();
        }

        private string BuildNotFoundMessage()
        {
            if (Candidates.Count == 0)
            {
                return LocalizedStrings.Format("Steam_GameNotFound", Query, "?");
            }

            var sb = new StringBuilder();
            sb.AppendLine(LocalizedStrings.Format("Steam_GameNotFoundFuzzy", Query));
            foreach (var candidate in Candidates.Take(5))
            {
                sb.AppendLine(LocalizedStrings.Format("Steam_GameMatchLine", candidate.Name));
            }

            sb.Append(LocalizedStrings.Get("Steam_GameNotFoundAsk"));
            return sb.ToString().Trim();
        }
    }

    internal static class SteamGameNameMatcher
    {
        private const int AutoPickMinScore = 72;
        private const int AmbiguityScoreGap = 12;
        private const int MinSuggestionScore = 30;

        public static SteamGameMatchResult Match(string query, IReadOnlyList<SteamOwnedGameEntry> ownedGames)
        {
            query = SteamQueryHelper.SanitizeGameDisplayName(query);
            var result = new SteamGameMatchResult { Query = query ?? string.Empty };

            if (ownedGames == null || ownedGames.Count == 0 || string.IsNullOrWhiteSpace(query))
            {
                result.Status = SteamGameMatchStatus.NotFound;
                return result;
            }

            var ranked = ownedGames
                .Select(game => new SteamGameMatchCandidate
                {
                    AppId = game.AppId,
                    Name = game.Name,
                    Score = ScoreGame(query, game.Name, game.PlaytimeMinutes)
                })
                .Where(candidate => candidate.Score >= MinSuggestionScore)
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.Name?.Length ?? 0)
                .ToList();

            result.Candidates = ranked;

            if (ranked.Count == 0)
            {
                result.Status = SteamGameMatchStatus.NotFound;
                return result;
            }

            var best = ranked[0];
            var secondScore = ranked.Count > 1 ? ranked[1].Score : 0;

            if (best.Score >= AutoPickMinScore && best.Score - secondScore >= AmbiguityScoreGap)
            {
                result.Status = SteamGameMatchStatus.Unique;
                result.AppId = best.AppId;
                result.MatchedName = best.Name;
                return result;
            }

            if (ranked.Count >= 2 && best.Score >= MinSuggestionScore && best.Score - secondScore < AmbiguityScoreGap)
            {
                result.Status = SteamGameMatchStatus.Ambiguous;
                result.Candidates = ranked.Take(6).ToList();
                return result;
            }

            if (best.Score >= AutoPickMinScore)
            {
                result.Status = SteamGameMatchStatus.Unique;
                result.AppId = best.AppId;
                result.MatchedName = best.Name;
                return result;
            }

            result.Status = SteamGameMatchStatus.NotFound;
            result.Candidates = ranked.Take(5).ToList();
            return result;
        }

        internal static int ScoreGame(string query, string gameName, int playtimeMinutes = 0)
        {
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(gameName))
            {
                return 0;
            }

            var normalizedQuery = NormalizeGameName(query);
            var normalizedGame = NormalizeGameName(gameName);
            var compactQuery = CompactGameName(normalizedQuery);
            var compactGame = CompactGameName(normalizedGame);

            if (string.IsNullOrEmpty(normalizedQuery) || string.IsNullOrEmpty(normalizedGame))
            {
                return 0;
            }

            if (normalizedQuery == normalizedGame || compactQuery == compactGame)
            {
                return 100;
            }

            var score = 0;

            if (normalizedGame.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                score = Math.Max(score, 78 + Math.Min(12, normalizedQuery.Length * 2));
            }
            else if (normalizedQuery.Contains(normalizedGame, StringComparison.Ordinal))
            {
                score = Math.Max(score, 70 + Math.Min(10, normalizedGame.Length * 2));
            }

            if (compactGame.Contains(compactQuery, StringComparison.Ordinal) ||
                compactQuery.Contains(compactGame, StringComparison.Ordinal))
            {
                score = Math.Max(score, 75);
            }

            var queryWords = SplitWords(normalizedQuery);
            var gameWords = SplitWords(normalizedGame);
            if (queryWords.Length > 0 && gameWords.Length > 0)
            {
                var matchedWords = 0;
                foreach (var queryWord in queryWords)
                {
                    if (queryWord.Length < 2)
                    {
                        continue;
                    }

                    foreach (var gameWord in gameWords)
                    {
                        if (gameWord == queryWord ||
                            gameWord.StartsWith(queryWord, StringComparison.Ordinal) ||
                            queryWord.StartsWith(gameWord, StringComparison.Ordinal))
                        {
                            matchedWords++;
                            break;
                        }
                    }
                }

                if (matchedWords > 0)
                {
                    var wordScore = 35 + (matchedWords * 18);
                    if (matchedWords == queryWords.Length)
                    {
                        wordScore += 15;
                    }

                    score = Math.Max(score, wordScore);
                }
            }

            var distanceScore = SimilarityPercent(compactQuery, compactGame);
            score = Math.Max(score, (int)(distanceScore * 0.45));

            if (playtimeMinutes > 0)
            {
                score += Math.Min(5, playtimeMinutes / 600);
            }

            return Math.Min(100, score);
        }

        internal static string NormalizeGameName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var normalized = name.Trim().ToLowerInvariant();
            foreach (var ch in new[] { "™", "®", "©", "’", "'", "\"", "«", "»" })
            {
                normalized = normalized.Replace(ch, string.Empty, StringComparison.Ordinal);
            }

            normalized = normalized
                .Replace(":", " ", StringComparison.Ordinal)
                .Replace("-", " ", StringComparison.Ordinal)
                .Replace("_", " ", StringComparison.Ordinal)
                .Replace("/", " ", StringComparison.Ordinal);

            while (normalized.Contains("  ", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            }

            return normalized.Trim();
        }

        private static string CompactGameName(string normalizedName)
        {
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return string.Empty;
            }

            return normalizedName.Replace(" ", string.Empty, StringComparison.Ordinal);
        }

        private static string[] SplitWords(string normalizedName)
        {
            return normalizedName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static int SimilarityPercent(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return 0;
            }

            if (a == b)
            {
                return 100;
            }

            var distance = LevenshteinDistance(a, b);
            var maxLen = Math.Max(a.Length, b.Length);
            if (maxLen == 0)
            {
                return 100;
            }

            return (int)Math.Round((1.0 - (double)distance / maxLen) * 100.0);
        }

        private static int LevenshteinDistance(string a, string b)
        {
            var n = a.Length;
            var m = b.Length;
            var d = new int[n + 1, m + 1];

            for (var i = 0; i <= n; i++)
            {
                d[i, 0] = i;
            }

            for (var j = 0; j <= m; j++)
            {
                d[0, j] = j;
            }

            for (var i = 1; i <= n; i++)
            {
                for (var j = 1; j <= m; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }
    }
}
