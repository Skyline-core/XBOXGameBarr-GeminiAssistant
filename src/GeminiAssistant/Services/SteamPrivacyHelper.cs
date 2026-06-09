namespace GeminiAssistant.Services
{
    internal static class SteamPrivacyHelper
    {
        /// <summary>
        /// Sin autenticacion OAuth, Steam Web API solo distingue visible (3) vs no visible (1).
        /// </summary>
        public static bool IsProfilePublicToApi(int communityVisibilityState)
        {
            return communityVisibilityState == 3;
        }

        public static bool IsProfileHiddenFromApi(int communityVisibilityState)
        {
            return communityVisibilityState == 1 || communityVisibilityState == 2;
        }

        public static string DescribeVisibilityState(int communityVisibilityState)
        {
            switch (communityVisibilityState)
            {
                case 1:
                    return LocalizedStrings.Get("Steam_Visibility_Private");
                case 2:
                    return LocalizedStrings.Get("Steam_Visibility_Friends");
                case 3:
                    return LocalizedStrings.Get("Steam_Visibility_Public");
                case 4:
                    return LocalizedStrings.Get("Steam_Visibility_Registered");
                case 5:
                    return LocalizedStrings.Get("Steam_Visibility_Extended");
                default:
                    return LocalizedStrings.Format("Steam_Visibility_Unknown", communityVisibilityState);
            }
        }

        public static string BuildProfileHiddenMessage(int communityVisibilityState)
        {
            return LocalizedStrings.Format(
                "Steam_ProfileHidden",
                DescribeVisibilityState(communityVisibilityState));
        }

        public static string BuildGameDetailsHiddenMessage(int communityVisibilityState)
        {
            return LocalizedStrings.Format(
                "Steam_GameDetailsHidden",
                DescribeVisibilityState(communityVisibilityState));
        }

        /// <summary>
        /// Steam devuelve "Profile is not public" en logros aunque GetOwnedGames funcione.
        /// </summary>
        public static string BuildAchievementsBlockedMessage(bool libraryAccessible)
        {
            if (libraryAccessible)
            {
                return LocalizedStrings.Steam_AchievementsBlocked;
            }

            return BuildProfileHiddenMessage(1);
        }

        public static string BuildAchievementBlockedChecklist(
            string personaName,
            int communityVisibilityState,
            string steamId64,
            uint appId,
            string gameName)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(BuildAchievementsBlockedMessage(libraryAccessible: true));
            sb.AppendLine();
            sb.AppendLine(LocalizedStrings.Get("Steam_Checklist_Header"));

            if (!string.IsNullOrWhiteSpace(personaName))
            {
                sb.Append(LocalizedStrings.Format("Steam_Checklist_ProfileQueried", personaName)).AppendLine();
            }

            sb.Append(LocalizedStrings.Format(
                "Steam_Checklist_VisibilityLine",
                communityVisibilityState,
                DescribeVisibilityState(communityVisibilityState))).AppendLine();

            if (!IsProfilePublicToApi(communityVisibilityState))
            {
                sb.AppendLine(LocalizedStrings.Get("Steam_Checklist_NotPublicNote"));
            }

            sb.AppendLine();
            sb.AppendLine(LocalizedStrings.Get("Steam_Checklist_AlsoCheck"));
            sb.AppendLine(LocalizedStrings.Get("Steam_Checklist_Item1"));
            sb.AppendLine(LocalizedStrings.Get("Steam_Checklist_Item2"));
            sb.AppendLine(LocalizedStrings.Get("Steam_Checklist_Item3"));

            if (!string.IsNullOrWhiteSpace(steamId64) && appId > 0)
            {
                sb.Append("   steamcommunity.com/profiles/")
                    .Append(steamId64)
                    .Append("/stats/")
                    .Append(appId)
                    .AppendLine("/");
            }

            if (!string.IsNullOrWhiteSpace(gameName))
            {
                sb.Append(LocalizedStrings.Format("Steam_Checklist_GameLine", gameName)).AppendLine();
            }

            sb.AppendLine(LocalizedStrings.Get("Steam_Checklist_Item4"));
            sb.AppendLine(LocalizedStrings.Get("Steam_Checklist_Item5"));
            sb.AppendLine(LocalizedStrings.Get("Steam_Checklist_Item6"));

            return sb.ToString().Trim();
        }

        public static string DescribeAchievementAccessFailure(
            string steamError,
            bool libraryAccessible,
            int communityVisibilityState,
            string personaName = null,
            string steamId64 = null,
            uint appId = 0,
            string gameName = null)
        {
            if (SteamHttpClient.IsAchievementPrivacyError(steamError))
            {
                if (libraryAccessible)
                {
                    return BuildAchievementBlockedChecklist(
                        personaName,
                        communityVisibilityState,
                        steamId64,
                        appId,
                        gameName);
                }

                return BuildAchievementsBlockedMessage(false);
            }

            if (!string.IsNullOrWhiteSpace(steamError))
            {
                return steamError;
            }

            if (IsProfileHiddenFromApi(communityVisibilityState))
            {
                return BuildProfileHiddenMessage(communityVisibilityState);
            }

            return BuildGameDetailsHiddenMessage(communityVisibilityState);
        }
    }
}
