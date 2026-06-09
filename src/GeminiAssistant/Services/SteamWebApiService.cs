using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeminiAssistant.Models;

namespace GeminiAssistant.Services
{
    public sealed class SteamWebApiService
    {
        public async Task<SteamProfileReport> GetProfileReportAsync(CancellationToken cancellationToken = default)
        {
            if (!TryGetCredentials(out var apiKey, out var steamId, out var credentialsError))
            {
                return ErrorProfile(credentialsError);
            }

            try
            {
                var ownedGames = await GetOwnedGamesAsync(apiKey, steamId, cancellationToken).ConfigureAwait(false);
                if (ownedGames.Count == 0)
                {
                    var recentGames = await GetRecentlyPlayedGamesAsync(apiKey, steamId, cancellationToken)
                        .ConfigureAwait(false);
                    ownedGames = MergeGameLists(ownedGames, recentGames);
                }

                var summary = await GetPlayerSummaryAsync(apiKey, steamId, cancellationToken).ConfigureAwait(false);
                if (summary == null)
                {
                    return ErrorProfile(LocalizedStrings.Steam_ProfileNotFound);
                }

                WidgetFileLog.Write(
                    "Steam perfil visibilidad=" + summary.CommunityVisibilityState +
                    " biblioteca=" + ownedGames.Count);

                if (ownedGames.Count == 0)
                {
                    if (SteamPrivacyHelper.IsProfileHiddenFromApi(summary.CommunityVisibilityState))
                    {
                        return ErrorProfile(SteamPrivacyHelper.BuildProfileHiddenMessage(summary.CommunityVisibilityState));
                    }

                    return ErrorProfile(SteamPrivacyHelper.BuildGameDetailsHiddenMessage(summary.CommunityVisibilityState));
                }

                var totalMinutes = ownedGames.Sum(game => game.PlaytimeMinutes);
                var topGames = ownedGames
                    .OrderByDescending(game => game.PlaytimeMinutes)
                    .Take(8)
                    .ToList();

                return new SteamProfileReport
                {
                    PersonaName = summary.PersonaName,
                    ProfileUrl = summary.ProfileUrl,
                    OwnedGamesCount = ownedGames.Count,
                    TotalPlaytimeMinutes = totalMinutes,
                    TopGames = topGames
                };
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Steam perfil: " + WidgetExceptionFormatter.Format(ex));
                return ErrorProfile(LocalizedStrings.Format("Steam_QueryFailed", ex.Message));
            }
        }

        public async Task<SteamAchievementsReport> GetAchievementsReportAsync(
            string gameDisplayName,
            CancellationToken cancellationToken = default)
        {
            gameDisplayName = SteamQueryHelper.SanitizeGameDisplayName(gameDisplayName);

            if (!TryGetCredentials(out var apiKey, out var steamId, out var credentialsError))
            {
                return ErrorAchievements(credentialsError);
            }

            try
            {
                var ownedGames = await GetOwnedGamesAsync(apiKey, steamId, cancellationToken).ConfigureAwait(false);
                if (ownedGames.Count == 0)
                {
                    var recentGames = await GetRecentlyPlayedGamesAsync(apiKey, steamId, cancellationToken)
                        .ConfigureAwait(false);
                    ownedGames = MergeGameLists(ownedGames, recentGames);
                }

                var summary = await GetPlayerSummaryAsync(apiKey, steamId, cancellationToken).ConfigureAwait(false);
                if (summary == null)
                {
                    return ErrorAchievements(LocalizedStrings.Steam_ProfileNotFound);
                }

                if (ownedGames.Count == 0)
                {
                    if (SteamPrivacyHelper.IsProfileHiddenFromApi(summary.CommunityVisibilityState))
                    {
                        return ErrorAchievements(SteamPrivacyHelper.BuildProfileHiddenMessage(summary.CommunityVisibilityState));
                    }

                    return ErrorAchievements(SteamPrivacyHelper.BuildGameDetailsHiddenMessage(summary.CommunityVisibilityState));
                }

                if (string.IsNullOrWhiteSpace(gameDisplayName))
                {
                    return ErrorAchievements(LocalizedStrings.Get("Steam_NoGameSpecified"));
                }

                var match = SteamGameNameMatcher.Match(gameDisplayName, ownedGames);
                WidgetFileLog.Write(
                    "Steam resolver juego=\"" + gameDisplayName + "\" estado=" + match.Status +
                    " appid=" + (match.AppId?.ToString() ?? "null") +
                    " candidatos=" + match.Candidates.Count);

                if (match.Status == SteamGameMatchStatus.Ambiguous)
                {
                    return ClarificationAchievements(match.BuildUserMessage());
                }

                if (match.Status == SteamGameMatchStatus.NotFound)
                {
                    if (match.Candidates.Count > 0)
                    {
                        return ClarificationAchievements(match.BuildUserMessage());
                    }

                    return ErrorAchievements(LocalizedStrings.Format(
                        "Steam_GameNotFound",
                        gameDisplayName,
                        ownedGames.Count));
                }

                var appId = match.AppId.Value;
                var matchedGame = ownedGames.FirstOrDefault(game => game.AppId == appId);
                var playerAchievements = await GetPlayerAchievementsAsync(
                    apiKey,
                    steamId,
                    appId,
                    cancellationToken).ConfigureAwait(false);

                if (!playerAchievements.Success)
                {
                    WidgetFileLog.Write(
                        "Steam logros bloqueados visibility=" + summary.CommunityVisibilityState +
                        " persona=" + summary.PersonaName);

                    return ErrorAchievements(
                        SteamPrivacyHelper.DescribeAchievementAccessFailure(
                            playerAchievements.ErrorMessage,
                            ownedGames.Count > 0,
                            summary.CommunityVisibilityState,
                            summary.PersonaName,
                            steamId,
                            appId,
                            matchedGame?.Name ?? gameDisplayName));
                }

                var schema = await GetSchemaForGameAsync(apiKey, appId, cancellationToken).ConfigureAwait(false);
                var globalPercents = await GetGlobalAchievementPercentsAsync(appId, cancellationToken)
                    .ConfigureAwait(false);
                var merged = MergeAchievements(playerAchievements.Achievements, schema, globalPercents);
                var unlocked = merged.Count(entry => entry.IsUnlocked);

                return new SteamAchievementsReport
                {
                    PersonaName = summary.PersonaName,
                    GameName = matchedGame?.Name ?? playerAchievements.GameName ?? gameDisplayName,
                    AppId = appId,
                    UnlockedCount = unlocked,
                    TotalCount = merged.Count,
                    Achievements = merged
                };
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Steam logros: " + WidgetExceptionFormatter.Format(ex));
                return ErrorAchievements(LocalizedStrings.Format("Steam_QueryFailed", ex.Message));
            }
        }

        public async Task<string> TryBuildChatContextAsync(
            string userText,
            string gameBarDisplayName,
            bool trackingGameIsLikelyGame = false,
            string lastSteamGameQuery = null,
            CancellationToken cancellationToken = default)
        {
            if (!ShouldFetchSteamContext(userText, gameBarDisplayName))
            {
                return null;
            }

            var resolvedGame = SteamQueryHelper.ResolveGameNameForQuery(
                userText,
                gameBarDisplayName,
                trackingGameIsLikelyGame,
                lastSteamGameQuery);
            var intent = SteamQueryHelper.DetectIntent(userText, gameBarDisplayName);
            WidgetFileLog.Write(
                "Steam chat intent=" + intent + " juego=" + (resolvedGame ?? "?"));

            switch (intent)
            {
                case SteamQueryIntent.LastAchievement:
                {
                    var last = await GetLastUnlockedAchievementReportAsync(cancellationToken).ConfigureAwait(false);
                    return last.FormatForGemini();
                }

                case SteamQueryIntent.Profile:
                {
                    var profile = await GetProfileReportAsync(cancellationToken).ConfigureAwait(false);
                    return profile.FormatForGemini();
                }

                case SteamQueryIntent.Achievements:
                {
                    if (string.IsNullOrWhiteSpace(resolvedGame))
                    {
                        return ClarificationAchievements(LocalizedStrings.Get("Steam_NoGameInQuery")).FormatForGemini();
                    }

                    var achievements = await GetAchievementsReportAsync(resolvedGame, cancellationToken)
                        .ConfigureAwait(false);
                    return achievements.FormatForGemini();
                }

                case SteamQueryIntent.General:
                {
                    var profile = await GetProfileReportAsync(cancellationToken).ConfigureAwait(false);
                    if (profile.HasError)
                    {
                        return profile.FormatForGemini();
                    }

                    if (string.IsNullOrWhiteSpace(resolvedGame))
                    {
                        return profile.FormatForGemini();
                    }

                    var achievements = await GetAchievementsReportAsync(resolvedGame, cancellationToken)
                        .ConfigureAwait(false);
                    return profile.FormatForGemini() + "\n\n" + achievements.FormatForGemini();
                }

                default:
                    return null;
            }
        }

        private static bool ShouldFetchSteamContext(string userText, string gameDisplayName)
        {
            if (!AppSettingsService.HasSteamCredentials())
            {
                WidgetFileLog.Write("Steam omitido: sin credenciales en Ajustes");
                return false;
            }

            if (!SteamQueryHelper.ShouldFetchSteamData(userText, gameDisplayName))
            {
                WidgetFileLog.Write("Steam omitido: mensaje no detectado como consulta Steam/logros");
                return false;
            }

            return true;
        }

        public static bool IsSteamFailureContext(string steamBlock)
        {
            if (string.IsNullOrWhiteSpace(steamBlock))
            {
                return true;
            }

            if (steamBlock.StartsWith(LocalizedStrings.Steam_ClarificationPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return steamBlock.StartsWith(LocalizedStrings.Steam_ErrorPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<SteamLastAchievementReport> GetLastUnlockedAchievementReportAsync(
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCredentials(out var apiKey, out var steamId, out var credentialsError))
            {
                return ErrorLastAchievement(credentialsError);
            }

            try
            {
                var ownedGames = await GetOwnedGamesAsync(apiKey, steamId, cancellationToken).ConfigureAwait(false);
                var recentGames = await GetRecentlyPlayedGamesAsync(apiKey, steamId, cancellationToken)
                    .ConfigureAwait(false);
                var library = MergeGameLists(ownedGames, recentGames);
                var candidates = SelectAchievementScanCandidates(library);

                WidgetFileLog.Write(
                    "Steam candidatos ultimo logro=" + candidates.Count + " de biblioteca=" + library.Count);

                var summary = await GetPlayerSummaryAsync(apiKey, steamId, cancellationToken).ConfigureAwait(false);

                if (candidates.Count == 0)
                {
                    if (summary == null)
                    {
                        return ErrorLastAchievement(LocalizedStrings.Steam_ProfileNotFound);
                    }

                    if (SteamPrivacyHelper.IsProfileHiddenFromApi(summary.CommunityVisibilityState))
                    {
                        return ErrorLastAchievement(
                            SteamPrivacyHelper.BuildProfileHiddenMessage(summary.CommunityVisibilityState));
                    }

                    return ErrorLastAchievement(
                        SteamPrivacyHelper.BuildGameDetailsHiddenMessage(summary.CommunityVisibilityState));
                }

                long bestUnlockTime = 0;
                uint bestAppId = 0;
                string bestApiName = null;
                string bestGameName = null;
                var privacyBlockedCount = 0;
                var scannedWithData = 0;

                foreach (var game in candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var playerAchievements = await GetPlayerAchievementsAsync(
                        apiKey,
                        steamId,
                        game.AppId,
                        cancellationToken).ConfigureAwait(false);

                    if (!playerAchievements.Success)
                    {
                        if (SteamHttpClient.IsAchievementPrivacyError(playerAchievements.ErrorMessage) ||
                            SteamHttpClient.IsAchievementPrivacyError(playerAchievements.RawBody))
                        {
                            privacyBlockedCount++;
                        }

                        continue;
                    }

                    if (playerAchievements.Achievements.Count == 0)
                    {
                        continue;
                    }

                    scannedWithData++;
                    foreach (var achievement in playerAchievements.Achievements)
                    {
                        if (!achievement.IsUnlocked || achievement.UnlockTime <= 0)
                        {
                            continue;
                        }

                        if (achievement.UnlockTime >= bestUnlockTime)
                        {
                            bestUnlockTime = achievement.UnlockTime;
                            bestAppId = game.AppId;
                            bestApiName = achievement.ApiName;
                            bestGameName = playerAchievements.GameName ?? game.Name;
                        }
                    }
                }

                if (bestUnlockTime <= 0 || string.IsNullOrWhiteSpace(bestApiName))
                {
                    if (privacyBlockedCount > 0 && scannedWithData == 0)
                    {
                        return ErrorLastAchievement(
                            SteamPrivacyHelper.BuildAchievementBlockedChecklist(
                                summary?.PersonaName,
                                summary?.CommunityVisibilityState ?? 0,
                                steamId,
                                candidates.Count > 0 ? candidates[0].AppId : 0,
                                candidates.Count > 0 ? candidates[0].Name : null));
                    }

                    return new SteamLastAchievementReport
                    {
                        PersonaName = summary?.PersonaName,
                        HasError = false,
                        ErrorMessage =
                            "Se consultaron " + candidates.Count +
                            " juegos de tu biblioteca Steam pero no se encontraron logros desbloqueados con fecha. " +
                            "Puede que los logros de esos juegos esten ocultos o que no tengan logros."
                    };
                }

                var schema = await GetSchemaForGameAsync(apiKey, bestAppId, cancellationToken).ConfigureAwait(false);
                schema.TryGetValue(bestApiName, out var schemaEntry);

                return new SteamLastAchievementReport
                {
                    PersonaName = summary?.PersonaName ?? string.Empty,
                    GameName = bestGameName,
                    AppId = bestAppId,
                    AchievementName = schemaEntry?.DisplayName ?? bestApiName,
                    Description = schemaEntry?.Description,
                    UnlockedAt = DateTimeOffset.FromUnixTimeSeconds(bestUnlockTime)
                };
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Steam ultimo logro: " + WidgetExceptionFormatter.Format(ex));
                return ErrorLastAchievement(LocalizedStrings.Format("Steam_QueryFailed", ex.Message));
            }
        }

        private static SteamLastAchievementReport ErrorLastAchievement(string message)
        {
            return new SteamLastAchievementReport { HasError = true, ErrorMessage = message };
        }

        private static bool TryGetCredentials(out string apiKey, out string steamId, out string error)
        {
            apiKey = NormalizeApiKey(AppSettingsService.GetSteamApiKey());
            steamId = AppSettingsService.GetSteamId64()?.Trim();
            error = null;

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(steamId))
            {
                error = LocalizedStrings.Steam_MissingCredentials;
                return false;
            }

            if (apiKey.Length < 16)
            {
                error = LocalizedStrings.Steam_IncompleteApiKey;
                return false;
            }

            if (!IsValidSteamId64(steamId))
            {
                error = LocalizedStrings.Steam_InvalidSteamId;
                return false;
            }

            return true;
        }

        private static string NormalizeApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return string.Empty;
            }

            apiKey = apiKey.Trim().Trim('"', '\'');
            return apiKey;
        }

        private static bool IsValidSteamId64(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId) || steamId.Length != 17)
            {
                return false;
            }

            return steamId.StartsWith("7656119", StringComparison.Ordinal) &&
                   ulong.TryParse(steamId, NumberStyles.None, CultureInfo.InvariantCulture, out _);
        }

        private static SteamAchievementsReport ErrorAchievements(string message)
        {
            return new SteamAchievementsReport { HasError = true, ErrorMessage = message };
        }

        private static SteamAchievementsReport ClarificationAchievements(string message)
        {
            return new SteamAchievementsReport
            {
                NeedsClarification = true,
                ClarificationMessage = message
            };
        }

        private static SteamProfileReport ErrorProfile(string message)
        {
            return new SteamProfileReport { HasError = true, ErrorMessage = message };
        }

        private static async Task<SteamPlayerSummary> GetPlayerSummaryAsync(
            string apiKey,
            string steamId,
            CancellationToken cancellationToken)
        {
            var body = await SteamHttpClient.GetAsync(
                "/ISteamUser/GetPlayerSummaries/v2/",
                new Dictionary<string, string> { { "steamids", steamId } },
                apiKey,
                cancellationToken).ConfigureAwait(false);

            using (var document = JsonDocument.Parse(body))
            {
                if (!document.RootElement.TryGetProperty("response", out var response) ||
                    !response.TryGetProperty("players", out var players) ||
                    players.ValueKind != JsonValueKind.Array ||
                    players.GetArrayLength() == 0)
                {
                    return null;
                }

                var player = players[0];
                var summary = new SteamPlayerSummary
                {
                    PersonaName = ReadString(player, "personaname"),
                    ProfileUrl = ReadString(player, "profileurl"),
                    CommunityVisibilityState = ReadInt(player, "communityvisibilitystate")
                };

                WidgetFileLog.Write(
                    "Steam perfil API visibility=" + summary.CommunityVisibilityState +
                    " persona=" + summary.PersonaName);

                return summary;
            }
        }

        private static async Task<List<SteamOwnedGameEntry>> GetOwnedGamesAsync(
            string apiKey,
            string steamId,
            CancellationToken cancellationToken)
        {
            var body = await SteamHttpClient.GetAsync(
                "/IPlayerService/GetOwnedGames/v1/",
                new Dictionary<string, string>
                {
                    { "steamid", steamId },
                    { "include_appinfo", "1" },
                    { "include_played_free_games", "1" }
                },
                apiKey,
                cancellationToken).ConfigureAwait(false);

            var games = ParseOwnedGamesResponse(body);
            WidgetFileLog.Write("Steam biblioteca juegos=" + games.Count);
            return games;
        }

        private static List<SteamOwnedGameEntry> ParseOwnedGamesResponse(string body)
        {
            var games = new List<SteamOwnedGameEntry>();
            using (var document = JsonDocument.Parse(body))
            {
                if (!document.RootElement.TryGetProperty("response", out var response))
                {
                    return games;
                }

                var gameCount = ReadInt(response, "game_count");
                if (gameCount > 0)
                {
                    WidgetFileLog.Write("Steam game_count=" + gameCount);
                }

                if (!response.TryGetProperty("games", out var gamesElement) ||
                    gamesElement.ValueKind != JsonValueKind.Array)
                {
                    return games;
                }

                foreach (var game in gamesElement.EnumerateArray())
                {
                    games.Add(new SteamOwnedGameEntry
                    {
                        AppId = (uint)ReadInt(game, "appid"),
                        Name = ReadString(game, "name"),
                        PlaytimeMinutes = ReadInt(game, "playtime_forever"),
                        LastPlayedUnix = ReadInt(game, "rtime_last_played")
                    });
                }
            }

            return games;
        }

        private static async Task<List<SteamOwnedGameEntry>> GetRecentlyPlayedGamesAsync(
            string apiKey,
            string steamId,
            CancellationToken cancellationToken)
        {
            try
            {
                var body = await SteamHttpClient.GetAsync(
                    "/IPlayerService/GetRecentlyPlayedGames/v1/",
                    new Dictionary<string, string>
                    {
                        { "steamid", steamId },
                        { "count", "20" }
                    },
                    apiKey,
                    cancellationToken).ConfigureAwait(false);

                var games = new List<SteamOwnedGameEntry>();
                using (var document = JsonDocument.Parse(body))
                {
                    if (!document.RootElement.TryGetProperty("response", out var response) ||
                        !response.TryGetProperty("games", out var gamesElement) ||
                        gamesElement.ValueKind != JsonValueKind.Array)
                    {
                        return games;
                    }

                    foreach (var game in gamesElement.EnumerateArray())
                    {
                        games.Add(new SteamOwnedGameEntry
                        {
                            AppId = (uint)ReadInt(game, "appid"),
                            Name = ReadString(game, "name"),
                            PlaytimeMinutes = ReadInt(game, "playtime_forever"),
                            LastPlayedUnix = ReadInt(game, "rtime_last_played")
                        });
                    }
                }

                WidgetFileLog.Write("Steam juegos recientes=" + games.Count);
                return games;
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Steam recientes: " + WidgetExceptionFormatter.Format(ex));
                return new List<SteamOwnedGameEntry>();
            }
        }

        private static List<SteamOwnedGameEntry> SelectAchievementScanCandidates(
            IReadOnlyList<SteamOwnedGameEntry> allGames)
        {
            if (allGames == null || allGames.Count == 0)
            {
                return new List<SteamOwnedGameEntry>();
            }

            if (allGames.Count <= 30)
            {
                return allGames.ToList();
            }

            var recentlyPlayed = allGames
                .Where(game => game.LastPlayedUnix > 0)
                .OrderByDescending(game => game.LastPlayedUnix)
                .Take(20)
                .ToList();
            var mostPlayed = allGames
                .Where(game => game.PlaytimeMinutes > 0)
                .OrderByDescending(game => game.PlaytimeMinutes)
                .Take(20)
                .ToList();

            var selected = MergeGameLists(recentlyPlayed, mostPlayed);
            if (selected.Count >= 15)
            {
                return selected.Take(35).ToList();
            }

            return allGames
                .OrderByDescending(game => game.LastPlayedUnix)
                .ThenByDescending(game => game.PlaytimeMinutes)
                .Take(35)
                .ToList();
        }

        private static List<SteamOwnedGameEntry> MergeGameLists(
            IReadOnlyList<SteamOwnedGameEntry> primary,
            IReadOnlyList<SteamOwnedGameEntry> secondary)
        {
            var merged = new List<SteamOwnedGameEntry>();
            var seen = new HashSet<uint>();

            void AddRange(IReadOnlyList<SteamOwnedGameEntry> source)
            {
                if (source == null)
                {
                    return;
                }

                foreach (var game in source)
                {
                    if (game.AppId == 0 || !seen.Add(game.AppId))
                    {
                        continue;
                    }

                    merged.Add(game);
                }
            }

            AddRange(primary);
            AddRange(secondary);
            return merged;
        }

        private static async Task<SteamPlayerAchievementResponse> GetPlayerAchievementsAsync(
            string apiKey,
            string steamId,
            uint appId,
            CancellationToken cancellationToken)
        {
            var response = await FetchPlayerAchievementsAsync(
                apiKey,
                steamId,
                appId,
                useStatsForGameEndpoint: false,
                cancellationToken).ConfigureAwait(false);

            if (response.Success)
            {
                return response;
            }

            if (SteamHttpClient.IsAchievementPrivacyError(response.ErrorMessage) ||
                SteamHttpClient.IsAchievementPrivacyError(response.RawBody))
            {
                WidgetFileLog.Write("Steam logros: probando GetUserStatsForGame/v2");
                var fallback = await FetchPlayerAchievementsAsync(
                    apiKey,
                    steamId,
                    appId,
                    useStatsForGameEndpoint: true,
                    cancellationToken).ConfigureAwait(false);

                if (fallback.Success)
                {
                    return fallback;
                }
            }

            return response;
        }

        private static async Task<SteamPlayerAchievementResponse> FetchPlayerAchievementsAsync(
            string apiKey,
            string steamId,
            uint appId,
            bool useStatsForGameEndpoint,
            CancellationToken cancellationToken)
        {
            var endpoint = useStatsForGameEndpoint
                ? "/ISteamUserStats/GetUserStatsForGame/v2/"
                : "/ISteamUserStats/GetPlayerAchievements/v1/";

            var query = useStatsForGameEndpoint
                ? new Dictionary<string, string>
                {
                    { "steamid", steamId },
                    { "appid", appId.ToString(CultureInfo.InvariantCulture) }
                }
                : new Dictionary<string, string>
                {
                    { "steamid", steamId },
                    { "appid", appId.ToString(CultureInfo.InvariantCulture) },
                    { "l", "spanish" }
                };

            var (statusCode, body) = await SteamHttpClient.GetRawAsync(
                endpoint,
                query,
                apiKey,
                requireKey: true,
                cancellationToken).ConfigureAwait(false);

            var response = new SteamPlayerAchievementResponse { RawBody = body, HttpStatus = statusCode };
            if (string.IsNullOrWhiteSpace(body))
            {
                response.ErrorMessage = LocalizedStrings.Format("Steam_EmptyResponse", statusCode);
                return response;
            }

            try
            {
                using (var document = JsonDocument.Parse(body))
                {
                    if (!document.RootElement.TryGetProperty("playerstats", out var playerStats))
                    {
                        if (statusCode >= 400)
                        {
                            response.ErrorMessage = SteamHttpClient.IsAchievementPrivacyError(body)
                                ? "Profile is not public"
                                : "HTTP " + statusCode;
                        }

                        return response;
                    }

                    response.Success = ReadSuccessFlag(playerStats, "success");
                    response.GameName = ReadString(playerStats, "gameName");
                    response.ErrorMessage = ReadString(playerStats, "error");

                    if (!response.Success &&
                        string.IsNullOrWhiteSpace(response.ErrorMessage) &&
                        statusCode >= 400)
                    {
                        response.ErrorMessage = SteamHttpClient.IsAchievementPrivacyError(body)
                            ? "Profile is not public"
                            : "HTTP " + statusCode;
                    }

                    if (!playerStats.TryGetProperty("achievements", out var achievements) ||
                        achievements.ValueKind != JsonValueKind.Array)
                    {
                        return response;
                    }

                    foreach (var achievement in achievements.EnumerateArray())
                    {
                        var apiName = ReadString(achievement, "apiname");
                        if (string.IsNullOrWhiteSpace(apiName))
                        {
                            apiName = ReadString(achievement, "name");
                        }

                        response.Achievements.Add(new SteamPlayerAchievementState
                        {
                            ApiName = apiName,
                            DisplayName = ReadString(achievement, "name"),
                            Description = ReadString(achievement, "description"),
                            IsUnlocked = ReadSuccessFlag(achievement, "achieved"),
                            UnlockTime = ReadLong(achievement, "unlocktime")
                        });
                    }

                    if (!response.Success &&
                        response.Achievements.Count > 0 &&
                        string.IsNullOrWhiteSpace(response.ErrorMessage))
                    {
                        response.Success = true;
                    }
                }
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Steam parse logros: " + WidgetExceptionFormatter.Format(ex));
                response.ErrorMessage = LocalizedStrings.Steam_ParseAchievementsFailed;
            }

            return response;
        }

        private static async Task<Dictionary<string, SteamSchemaAchievement>> GetSchemaForGameAsync(
            string apiKey,
            uint appId,
            CancellationToken cancellationToken)
        {
            var schema = new Dictionary<string, SteamSchemaAchievement>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var body = await SteamHttpClient.GetAsync(
                    "/ISteamUserStats/GetSchemaForGame/v2/",
                    new Dictionary<string, string>
                    {
                        { "appid", appId.ToString(CultureInfo.InvariantCulture) },
                        { "l", "spanish" }
                    },
                    apiKey,
                    cancellationToken).ConfigureAwait(false);

                using (var document = JsonDocument.Parse(body))
                {
                    if (!document.RootElement.TryGetProperty("game", out var game) ||
                        !game.TryGetProperty("availableGameStats", out var stats) ||
                        !stats.TryGetProperty("achievements", out var achievements) ||
                        achievements.ValueKind != JsonValueKind.Array)
                    {
                        return schema;
                    }

                    foreach (var achievement in achievements.EnumerateArray())
                    {
                        var apiName = ReadString(achievement, "name");
                        if (string.IsNullOrWhiteSpace(apiName))
                        {
                            continue;
                        }

                        schema[apiName] = new SteamSchemaAchievement
                        {
                            ApiName = apiName,
                            DisplayName = ReadString(achievement, "displayName"),
                            Description = ReadString(achievement, "description")
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Steam schema: " + WidgetExceptionFormatter.Format(ex));
            }

            return schema;
        }

        private static async Task<Dictionary<string, string>> GetGlobalAchievementPercentsAsync(
            uint appId,
            CancellationToken cancellationToken)
        {
            var percents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var body = await SteamHttpClient.GetPublicAsync(
                    "/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/",
                    new Dictionary<string, string>
                    {
                        { "gameid", appId.ToString(CultureInfo.InvariantCulture) }
                    },
                    cancellationToken).ConfigureAwait(false);

                using (var document = JsonDocument.Parse(body))
                {
                    if (!document.RootElement.TryGetProperty("achievementpercentages", out var root) ||
                        !root.TryGetProperty("achievements", out var achievements) ||
                        achievements.ValueKind != JsonValueKind.Array)
                    {
                        return percents;
                    }

                    foreach (var achievement in achievements.EnumerateArray())
                    {
                        var name = ReadString(achievement, "name");
                        var percent = ReadDouble(achievement, "percent");
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        percents[name] = percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
                    }
                }
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Steam global %: " + WidgetExceptionFormatter.Format(ex));
            }

            return percents;
        }

        private static List<SteamAchievementEntry> MergeAchievements(
            IReadOnlyList<SteamPlayerAchievementState> playerAchievements,
            IReadOnlyDictionary<string, SteamSchemaAchievement> schema,
            IReadOnlyDictionary<string, string> globalPercents)
        {
            var merged = new List<SteamAchievementEntry>();
            foreach (var achievement in playerAchievements)
            {
                schema.TryGetValue(achievement.ApiName ?? string.Empty, out var schemaEntry);
                globalPercents.TryGetValue(achievement.ApiName ?? string.Empty, out var globalPercent);

                merged.Add(new SteamAchievementEntry
                {
                    ApiName = achievement.ApiName,
                    DisplayName = schemaEntry?.DisplayName ??
                                  achievement.DisplayName ??
                                  achievement.ApiName,
                    Description = schemaEntry?.Description ?? achievement.Description,
                    IsUnlocked = achievement.IsUnlocked,
                    GlobalPercent = globalPercent
                });
            }

            return merged
                .OrderBy(entry => entry.IsUnlocked)
                .ThenByDescending(entry => ParsePercent(entry.GlobalPercent))
                .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static double ParsePercent(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return -1;
            }

            var trimmed = value.Trim().TrimEnd('%');
            return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
                ? percent
                : -1;
        }

        private static long ReadLong(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return 0;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                return number;
            }

            return long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }

        private static string ReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return string.Empty;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
        }

        private static int ReadInt(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return 0;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }

        private static bool ReadSuccessFlag(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return false;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    return value.TryGetInt32(out var number) && number != 0;
                default:
                    return int.TryParse(
                        value.ToString(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsed) && parsed != 0;
            }
        }

        private static double ReadDouble(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return 0;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return number;
            }

            return double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }

        private sealed class SteamPlayerSummary
        {
            public string PersonaName { get; set; }

            public string ProfileUrl { get; set; }

            public int CommunityVisibilityState { get; set; }
        }

        private sealed class SteamPlayerAchievementResponse
        {
            public bool Success { get; set; }

            public int HttpStatus { get; set; }

            public string RawBody { get; set; }

            public string ErrorMessage { get; set; }

            public string GameName { get; set; }

            public List<SteamPlayerAchievementState> Achievements { get; } = new List<SteamPlayerAchievementState>();
        }

        private sealed class SteamPlayerAchievementState
        {
            public string ApiName { get; set; }

            public string DisplayName { get; set; }

            public string Description { get; set; }

            public bool IsUnlocked { get; set; }

            public long UnlockTime { get; set; }
        }

        private sealed class SteamSchemaAchievement
        {
            public string ApiName { get; set; }

            public string DisplayName { get; set; }

            public string Description { get; set; }
        }
    }
}
