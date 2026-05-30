using GeminiAssistant.Services;
using Microsoft.Gaming.XboxGameBar;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace GeminiAssistant.Widgets
{
    public sealed partial class SettingsWidget : Page
    {
        private XboxGameBarWidget _widget;

        public SettingsWidget()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _widget = e.Parameter as XboxGameBarWidget;
            if (_widget != null)
            {
                _widget.RequestedThemeChanged += Widget_RequestedThemeChanged;
                ApplyTheme();
            }
            var existing = AppSettingsService.GetApiKey();
            if (!string.IsNullOrEmpty(existing))
            {
                ApiKeyBox.Password = existing;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (_widget != null)
            {
                _widget.RequestedThemeChanged -= Widget_RequestedThemeChanged;
            }

            base.OnNavigatedFrom(e);
        }

        private void Widget_RequestedThemeChanged(XboxGameBarWidget sender, object args)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, ApplyTheme);
        }

        private void ApplyTheme()
        {
            if (_widget == null)
            {
                return;
            }

            RequestedTheme = _widget.RequestedTheme;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            AppSettingsService.SetApiKey(ApiKeyBox.Password?.Trim() ?? string.Empty);
            SavedText.Text = "Guardado.";
            SavedText.Visibility = Visibility.Visible;
        }
    }
}
