using GeminiAssistant.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace GeminiAssistant.Widgets
{
    public sealed partial class ChatWidget
    {
        public void RefreshLocalization()
        {
            if (!CoreUiDispatcher.IsOnUiThread)
            {
                _ = CoreUiDispatcher.RunOnUiAsync(RefreshLocalization);
                return;
            }

            try
            {
                AppLanguageService.ApplySavedOrSystemLanguage();
                ApplyLocalizedUi();
                ProfileInitialsText.Text = GetUserInitials();
                UpdateGameBanner(_gameContext?.Current);
                UpdateViewMode();
                RefreshStatusText();
            }
            catch (System.Exception ex)
            {
                WidgetFileLog.Write("RefreshLocalization: " + WidgetExceptionFormatter.Format(ex));
            }
        }

        private void RefreshStatusText()
        {
            if (StatusText == null || StatusText.Visibility != Visibility.Visible)
            {
                return;
            }

            var log = WidgetDiagnostics.GetLastError();
            if (!string.IsNullOrEmpty(log))
            {
                ShowStatus(LocalizedStrings.Format("Status_Diagnostic", log));
                return;
            }

            if (!AppSettingsService.HasApiKey())
            {
                ShowStatus(LocalizedStrings.Status_NoApiKey);
                return;
            }

            if (!PendingCaptureStore.HasPending() && (_pendingScreenshot?.JpegBytes == null || _pendingScreenshot.JpegBytes.Length == 0))
            {
                ShowStatus(LocalizedStrings.Status_CaptureTip);
            }
        }

        private void ApplyLocalizedUi()
        {
            ProfileInitialsText.Text = LocalizedStrings.Chat_ProfileInitials;
            TopTitleText.Text = LocalizedStrings.Msg_Gemini;
            ToolTipService.SetToolTip(HomeButton, LocalizedStrings.Chat_TooltipHome);
            ToolTipService.SetToolTip(MenuButton, LocalizedStrings.Chat_TooltipMore);
            if (MenuSettingsItem != null)
            {
                MenuSettingsItem.Text = LocalizedStrings.Chat_MenuSettings;
            }

            if (MenuClearItem != null)
            {
                MenuClearItem.Text = LocalizedStrings.Chat_MenuClear;
            }

            if (MenuLogItem != null)
            {
                MenuLogItem.Text = LocalizedStrings.Chat_MenuLog;
            }

            GameIndicatorLabel.Text = LocalizedStrings.Chat_GameActive;
            if (string.IsNullOrWhiteSpace(GameIndicatorText.Text) ||
                GameIndicatorText.Text.StartsWith("Detect", System.StringComparison.OrdinalIgnoreCase) ||
                GameIndicatorText.Text.StartsWith("Detectando", System.StringComparison.OrdinalIgnoreCase))
            {
                GameIndicatorText.Text = LocalizedStrings.Chat_DetectingGame;
            }

            if (HomeGeminiTitleText != null)
            {
                HomeGeminiTitleText.Text = LocalizedStrings.Msg_Gemini;
            }

            if (GreetingHelpText != null)
            {
                GreetingHelpText.Text = LocalizedStrings.Chat_GreetingHelpPart1 + "\n" + LocalizedStrings.Chat_GreetingHelpPart2;
            }

            SuggestionGamesTitle.Text = LocalizedStrings.Chat_SuggestionGamesTitle;
            SuggestionGamesSub.Text = LocalizedStrings.Chat_SuggestionGamesSub;
            SuggestionSteamTitle.Text = LocalizedStrings.Chat_SuggestionSteamTitle;
            SuggestionSteamSub.Text = LocalizedStrings.Chat_SuggestionSteamSub;
            SuggestionCaptureTitle.Text = LocalizedStrings.Chat_SuggestionCaptureTitle;
            SuggestionCaptureSub.Text = LocalizedStrings.Chat_SuggestionCaptureSub;

            FabCaptureText.Text = LocalizedStrings.Chat_FabCapture;
            FabAchievementsText.Text = LocalizedStrings.Chat_FabAchievements;
            FabTipsText.Text = LocalizedStrings.Chat_FabTips;
            FabStatsText.Text = LocalizedStrings.Chat_FabStats;

            InputBox.PlaceholderText = LocalizedStrings.Chat_InputPlaceholder;
            ToolTipService.SetToolTip(RemoveScreenshotButton, LocalizedStrings.Chat_TooltipRemoveCapture);
            ToolTipService.SetToolTip(CaptureButton, LocalizedStrings.Chat_TooltipCapture);
            ToolTipService.SetToolTip(MicButton, LocalizedStrings.Chat_TooltipMic);
            ToolTipService.SetToolTip(SendButton, LocalizedStrings.Chat_TooltipSend);
        }
    }
}
