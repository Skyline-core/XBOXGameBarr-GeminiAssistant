using System;
using System.Collections.Generic;
using GeminiAssistant.Services;
using Microsoft.Gaming.XboxGameBar;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace GeminiAssistant.Widgets
{
    public sealed partial class SettingsWidget : Page
    {
        private sealed class LanguageOption
        {
            public string Tag { get; set; }
            public string Label { get; set; }
        }

        private XboxGameBarWidget _widget;
        private string _loadedLanguagePreference;

        public SettingsWidget()
        {
            InitializeComponent();
            Loaded += SettingsWidget_Loaded;
        }

        private void SettingsWidget_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SettingsWidget_Loaded;
            if (string.IsNullOrEmpty(TitleText.Text))
            {
                SafeInitializeUi();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _widget = e.Parameter as XboxGameBarWidget;
            if (_widget != null)
            {
                _widget.RequestedThemeChanged += Widget_RequestedThemeChanged;
            }

            ApplyTheme();
            SafeInitializeUi();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (_widget != null)
            {
                _widget.RequestedThemeChanged -= Widget_RequestedThemeChanged;
            }

            base.OnNavigatedFrom(e);
        }

        private void SafeInitializeUi()
        {
            try
            {
                AppLanguageService.ApplySavedOrSystemLanguage();
                ApplyLocalizedUi();
                LoadLanguageSelection();
                LoadCredentials();
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("SettingsWidget init: " + WidgetExceptionFormatter.Format(ex));
                TitleText.Text = "Settings";
                SaveButton.Content = "Save";
            }
        }

        private void ApplyLocalizedUi()
        {
            TitleText.Text = LocalizedStrings.Settings_Title;
            LanguageLabel.Text = LocalizedStrings.Settings_Language;
            ApiKeyLabel.Text = LocalizedStrings.Settings_ApiKeyLabel;
            ApiKeyBox.PlaceholderText = LocalizedStrings.Settings_ApiKeyPlaceholder;
            ApiKeyHintText.Text = LocalizedStrings.Settings_ApiKeyHint;
            SteamKeyLabel.Text = LocalizedStrings.Settings_SteamKeyLabel;
            SteamApiKeyBox.PlaceholderText = LocalizedStrings.Settings_SteamKeyLabel;
            SteamIdLabel.Text = LocalizedStrings.Settings_SteamIdLabel;
            SteamHintText.Text = LocalizedStrings.Settings_SteamHint;
            SaveButton.Content = LocalizedStrings.Settings_Save;
            CaptureTitleText.Text = LocalizedStrings.Settings_CaptureTitle;
            CaptureHintText.Text = LocalizedStrings.Settings_CaptureHint;
        }

        private void LoadLanguageSelection()
        {
            _loadedLanguagePreference = AppLanguageService.GetPreference();
            var options = new List<LanguageOption>
            {
                new LanguageOption { Tag = AppLanguageService.PreferenceAuto, Label = LocalizedStrings.Settings_Language_Auto },
                new LanguageOption { Tag = AppLanguageService.SpanishTag, Label = LocalizedStrings.Settings_Language_Spanish },
                new LanguageOption { Tag = AppLanguageService.EnglishTag, Label = LocalizedStrings.Settings_Language_English }
            };

            LanguageCombo.ItemsSource = options;
            LanguageCombo.DisplayMemberPath = nameof(LanguageOption.Label);
            LanguageCombo.SelectedValuePath = nameof(LanguageOption.Tag);

            var selected = _loadedLanguagePreference;
            if (string.IsNullOrWhiteSpace(selected))
            {
                selected = AppLanguageService.PreferenceAuto;
            }

            LanguageCombo.SelectedValue = selected;
        }

        private void LoadCredentials()
        {
            var existing = AppSettingsService.GetApiKey();
            if (!string.IsNullOrEmpty(existing))
            {
                ApiKeyBox.Password = existing;
            }

            var steamKey = AppSettingsService.GetSteamApiKey();
            if (!string.IsNullOrEmpty(steamKey))
            {
                SteamApiKeyBox.Password = steamKey;
            }

            var steamId = AppSettingsService.GetSteamId64();
            if (!string.IsNullOrEmpty(steamId))
            {
                SteamIdBox.Text = steamId;
            }
        }

        private void Widget_RequestedThemeChanged(XboxGameBarWidget sender, object args)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, ApplyTheme);
        }

        private void ApplyTheme()
        {
            RequestedTheme = _widget?.RequestedTheme ?? ElementTheme.Dark;
        }

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
                LoadLanguageSelection();
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Settings RefreshLocalization: " + WidgetExceptionFormatter.Format(ex));
            }
        }

        private static string ReadSelectedLanguage(ComboBox combo)
        {
            if (combo?.SelectedItem is LanguageOption option && !string.IsNullOrWhiteSpace(option.Tag))
            {
                return option.Tag;
            }

            return combo?.SelectedValue as string ?? AppLanguageService.PreferenceAuto;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var selectedLanguage = AppLanguageService.NormalizePreference(ReadSelectedLanguage(LanguageCombo));
            var languageChanged = !string.Equals(selectedLanguage, _loadedLanguagePreference, StringComparison.Ordinal);

            AppSettingsService.SetApiKey(ApiKeyBox.Password?.Trim() ?? string.Empty);
            AppSettingsService.SetSteamApiKey(SteamApiKeyBox.Password?.Trim() ?? string.Empty);
            AppSettingsService.SetSteamId64(SteamIdBox.Text?.Trim() ?? string.Empty);
            AppLanguageService.SetPreference(selectedLanguage);
            AppLanguageService.ApplySavedOrSystemLanguage();

            SavedText.Text = LocalizedStrings.Settings_Saved;
            if (languageChanged)
            {
                SavedText.Text += LocalizedStrings.Settings_SavedLanguageNote;
            }

            ApplyLocalizedUi();
            LoadLanguageSelection();

            SavedText.Visibility = Visibility.Visible;
            _loadedLanguagePreference = selectedLanguage;
        }
    }
}
