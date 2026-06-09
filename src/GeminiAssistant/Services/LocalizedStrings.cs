using System;
using System.Globalization;
using GeminiAssistant.Properties;

namespace GeminiAssistant.Services
{
    internal static class LocalizedStrings
    {
        private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo(AppLanguageService.SpanishTag);

        public static string Format(string key, params object[] args)
        {
            var format = Get(key);
            return args == null || args.Length == 0 ? format : string.Format(format, args);
        }

        public static string Get(string key)
        {
            if (AppLanguageService.IsEnglish())
            {
                var english = EnglishResources.ResourceManager.GetString(key);
                if (!string.IsNullOrEmpty(english))
                {
                    return english;
                }
            }

            var spanish = Resources.ResourceManager.GetString(key, SpanishCulture);
            return string.IsNullOrEmpty(spanish) ? key : spanish;
        }

        public static string Settings_Title => Get(nameof(Settings_Title));
        public static string Settings_Language => Get(nameof(Settings_Language));
        public static string Settings_Language_Auto => Get(nameof(Settings_Language_Auto));
        public static string Settings_Language_Spanish => Get(nameof(Settings_Language_Spanish));
        public static string Settings_Language_English => Get(nameof(Settings_Language_English));
        public static string Settings_ApiKeyLabel => Get(nameof(Settings_ApiKeyLabel));
        public static string Settings_ApiKeyPlaceholder => Get(nameof(Settings_ApiKeyPlaceholder));
        public static string Settings_ApiKeyHint => Get(nameof(Settings_ApiKeyHint));
        public static string Settings_SteamKeyLabel => Get(nameof(Settings_SteamKeyLabel));
        public static string Settings_SteamIdLabel => Get(nameof(Settings_SteamIdLabel));
        public static string Settings_SteamHint => Get(nameof(Settings_SteamHint));
        public static string Settings_Save => Get(nameof(Settings_Save));
        public static string Settings_Saved => Get(nameof(Settings_Saved));
        public static string Settings_SavedLanguageNote => Get(nameof(Settings_SavedLanguageNote));
        public static string Settings_CaptureTitle => Get(nameof(Settings_CaptureTitle));
        public static string Settings_CaptureHint => Get(nameof(Settings_CaptureHint));

        public static string Chat_ProfileInitials => Get(nameof(Chat_ProfileInitials));
        public static string Chat_TooltipHome => Get(nameof(Chat_TooltipHome));
        public static string Chat_TooltipMore => Get(nameof(Chat_TooltipMore));
        public static string Chat_MenuSettings => Get(nameof(Chat_MenuSettings));
        public static string Chat_MenuClear => Get(nameof(Chat_MenuClear));
        public static string Chat_MenuLog => Get(nameof(Chat_MenuLog));
        public static string Chat_GameActive => Get(nameof(Chat_GameActive));
        public static string Chat_DetectingGame => Get(nameof(Chat_DetectingGame));
        public static string Chat_GreetingHello => Get(nameof(Chat_GreetingHello));
        public static string Chat_GreetingHelpPart1 => Get(nameof(Chat_GreetingHelpPart1));
        public static string Chat_GreetingHelpPart2 => Get(nameof(Chat_GreetingHelpPart2));
        public static string Chat_SuggestionGamesTitle => Get(nameof(Chat_SuggestionGamesTitle));
        public static string Chat_SuggestionGamesSub => Get(nameof(Chat_SuggestionGamesSub));
        public static string Chat_SuggestionSteamTitle => Get(nameof(Chat_SuggestionSteamTitle));
        public static string Chat_SuggestionSteamSub => Get(nameof(Chat_SuggestionSteamSub));
        public static string Chat_SuggestionCaptureTitle => Get(nameof(Chat_SuggestionCaptureTitle));
        public static string Chat_SuggestionCaptureSub => Get(nameof(Chat_SuggestionCaptureSub));
        public static string Chat_FabCapture => Get(nameof(Chat_FabCapture));
        public static string Chat_FabAchievements => Get(nameof(Chat_FabAchievements));
        public static string Chat_FabTips => Get(nameof(Chat_FabTips));
        public static string Chat_FabStats => Get(nameof(Chat_FabStats));
        public static string Chat_InputPlaceholder => Get(nameof(Chat_InputPlaceholder));
        public static string Chat_TooltipRemoveCapture => Get(nameof(Chat_TooltipRemoveCapture));
        public static string Chat_TooltipCapture => Get(nameof(Chat_TooltipCapture));
        public static string Chat_TooltipMic => Get(nameof(Chat_TooltipMic));
        public static string Chat_TooltipSend => Get(nameof(Chat_TooltipSend));

        public static string Game_TrackingOff => Get(nameof(Game_TrackingOff));
        public static string Game_TrackingOffHint => Get(nameof(Game_TrackingOffHint));
        public static string Game_PlayingNow => Get(nameof(Game_PlayingNow));
        public static string Game_AppForeground => Get(nameof(Game_AppForeground));
        public static string Game_WaitingGame => Get(nameof(Game_WaitingGame));
        public static string Game_NoGameDetected => Get(nameof(Game_NoGameDetected));
        public static string Game_EnableTracking => Get(nameof(Game_EnableTracking));
        public static string Game_Unknown => Get(nameof(Game_Unknown));

        public static string Ctx_TrackingDisabled => Get(nameof(Ctx_TrackingDisabled));
        public static string Ctx_GameLabel => Get(nameof(Ctx_GameLabel));
        public static string Ctx_AppLabel => Get(nameof(Ctx_AppLabel));
        public static string Ctx_FullscreenSuffix => Get(nameof(Ctx_FullscreenSuffix));
        public static string Ctx_UnknownInstruction => Get(nameof(Ctx_UnknownInstruction));
        public static string Ctx_GameInstruction => Get(nameof(Ctx_GameInstruction));

        public static string Status_NoApiKey => Get(nameof(Status_NoApiKey));
        public static string Status_CapturePermission => Get(nameof(Status_CapturePermission));
        public static string Status_WaitSend => Get(nameof(Status_WaitSend));
        public static string Status_QueryingSteam => Get(nameof(Status_QueryingSteam));
        public static string Status_SteamOk => Get(nameof(Status_SteamOk));
        public static string Status_SendingGemini => Get(nameof(Status_SendingGemini));
        public static string Status_Capturing => Get(nameof(Status_Capturing));
        public static string Status_ScreenshotReady => Get(nameof(Status_ScreenshotReady));
        public static string Status_CaptureRestored => Get(nameof(Status_CaptureRestored));
        public static string Status_CaptureTip => Get(nameof(Status_CaptureTip));
        public static string Status_Recording => Get(nameof(Status_Recording));
        public static string Status_RecordingMax => Get(nameof(Status_RecordingMax));
        public static string Status_ProcessingAudio => Get(nameof(Status_ProcessingAudio));
        public static string Status_Transcribing => Get(nameof(Status_Transcribing));
        public static string Status_NoLog => Get(nameof(Status_NoLog));

        public static string Error_Unknown => Get(nameof(Error_Unknown));
        public static string Error_WriteCapturePrompt => Get(nameof(Error_WriteCapturePrompt));
        public static string Error_MissingApiKey => Get(nameof(Error_MissingApiKey));
        public static string Error_MissingSteam => Get(nameof(Error_MissingSteam));
        public static string Error_MicDenied => Get(nameof(Error_MicDenied));
        public static string Error_SpeechInit => Get(nameof(Error_SpeechInit));

        public static string Msg_User => Get(nameof(Msg_User));
        public static string Msg_UserVoice => Get(nameof(Msg_UserVoice));
        public static string Msg_Gemini => Get(nameof(Msg_Gemini));
        public static string Msg_System => Get(nameof(Msg_System));
        public static string Msg_AnalyzingVoice => Get(nameof(Msg_AnalyzingVoice));

        public static string Voice_StopSend => Get(nameof(Voice_StopSend));

        public static string Capture_ReadyLabel => Get(nameof(Capture_ReadyLabel));
        public static string Capture_Source_Default => Get(nameof(Capture_Source_Default));
        public static string Capture_Source_Window => Get(nameof(Capture_Source_Window));
        public static string Capture_Source_GameWindow => Get(nameof(Capture_Source_GameWindow));
        public static string Capture_Source_GameBar => Get(nameof(Capture_Source_GameBar));

        public static string Gemini_SteamConfigured => Get(nameof(Gemini_SteamConfigured));
        public static string Gemini_SteamContextSuffix => Get(nameof(Gemini_SteamContextSuffix));
        public static string Gemini_RespondClearly => Get(nameof(Gemini_RespondClearly));
        public static string Gemini_VoiceInstruction => Get(nameof(Gemini_VoiceInstruction));
        public static string Gemini_EmptyAudio => Get(nameof(Gemini_EmptyAudio));
        public static string Gemini_ConfigApiKey => Get(nameof(Gemini_ConfigApiKey));
        public static string Gemini_NoTextResponse => Get(nameof(Gemini_NoTextResponse));
        public static string Gemini_ResponseTruncated => Get(nameof(Gemini_ResponseTruncated));

        public static string Chat_InputPlaceholderActive => Get(nameof(Chat_InputPlaceholderActive));
        public static string Chat_UserInitials => Get(nameof(Chat_UserInitials));

        public static string Error_SteamNoResponse => Get(nameof(Error_SteamNoResponse));
        public static string Error_VoiceCancelled => Get(nameof(Error_VoiceCancelled));
        public static string Prompt_SteamAchievementsGemini => Get(nameof(Prompt_SteamAchievementsGemini));
        public static string Prompt_SteamProfileGemini => Get(nameof(Prompt_SteamProfileGemini));
        public static string Prompt_FabTips => Get(nameof(Prompt_FabTips));
        public static string Prompt_GameRecommendations => Get(nameof(Prompt_GameRecommendations));

        public static string Steam_ProfileNotFound => Get(nameof(Steam_ProfileNotFound));
        public static string Steam_MissingCredentials => Get(nameof(Steam_MissingCredentials));
        public static string Steam_IncompleteApiKey => Get(nameof(Steam_IncompleteApiKey));
        public static string Steam_InvalidSteamId => Get(nameof(Steam_InvalidSteamId));
        public static string Steam_ParseAchievementsFailed => Get(nameof(Steam_ParseAchievementsFailed));
        public static string Steam_AchievementsBlocked => Get(nameof(Steam_AchievementsBlocked));
        public static string Steam_ErrorPrefix => Get(nameof(Steam_ErrorPrefix));
        public static string Steam_ClarificationPrefix => Get(nameof(Steam_ClarificationPrefix));

        public static bool IsUserRole(string roleLabel)
        {
            if (string.IsNullOrEmpty(roleLabel))
            {
                return false;
            }

            return roleLabel.StartsWith(Msg_User, StringComparison.OrdinalIgnoreCase) ||
                   roleLabel.StartsWith(Msg_UserVoice, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsUnknownGameName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            return name.Equals(Game_Unknown, StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Desconocido", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
        }
    }
}
