using GeminiAssistant.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace GeminiAssistant.Widgets
{
    public sealed partial class LaunchLandingPage : Page
    {
        public LaunchLandingPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            try
            {
                AppLanguageService.ApplySavedOrSystemLanguage();
            }
            catch (System.Exception ex)
            {
                WidgetFileLog.Write("LaunchLanding lang: " + ex.Message);
            }

            ApplyLocalizedText();
        }

        private void ApplyLocalizedText()
        {
            TitleText.Text = LocalizedStrings.Msg_Gemini;

            if (AppLanguageService.IsEnglish())
            {
                BodyText.Text =
                    "This app works inside Xbox Game Bar while you play or use other apps.";
                HintText.Text =
                    "Press Win+G to open Game Bar, then select Gemini Assistant from the widget bar.";
                return;
            }

            BodyText.Text =
                "Esta aplicacion funciona dentro de Xbox Game Bar mientras juegas o usas otras apps.";
            HintText.Text =
                "Pulsa Win+G para abrir Game Bar y selecciona Gemini Assistant en la barra de widgets.";
        }
    }
}
