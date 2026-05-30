using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace GeminiAssistant
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var package = Package.Current;
            PackageInfoText.Text =
                "Paquete instalado: " + package.Id.Name +
                "\nVersión: " + package.Id.Version +
                "\nPFN: " + package.Id.FamilyName;
        }
    }
}
