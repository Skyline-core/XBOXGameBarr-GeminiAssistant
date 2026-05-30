using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using GeminiAssistant.Models;
using GeminiAssistant.Services;
using Microsoft.Gaming.XboxGameBar;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace GeminiAssistant.Widgets
{
    public sealed partial class ChatWidget : Page
    {
        private XboxGameBarWidget _widget;
        private GameContextService _gameContext;
        private readonly GeminiChatService _chatService = new GeminiChatService();
        private readonly GeminiLiveService _liveService = new GeminiLiveService();
        private readonly ScreenCaptureService _captureService = new ScreenCaptureService();
        private readonly ObservableCollection<ChatMessageViewModel> _messages = new ObservableCollection<ChatMessageViewModel>();
        private PendingScreenshot _pendingScreenshot;
        private CancellationTokenSource _operationCts;
        private readonly SolidColorBrush _darkBrush = new SolidColorBrush(Color.FromArgb(255, 26, 26, 46));
        private readonly SolidColorBrush _lightBrush = new SolidColorBrush(Color.FromArgb(255, 219, 219, 219));

        public ChatWidget()
        {
            InitializeComponent();
            MessagesList.ItemsSource = _messages;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _widget = e.Parameter as XboxGameBarWidget;
            if (_widget == null)
            {
                return;
            }

            _widget.SettingsClicked += Widget_SettingsClicked;
            _widget.RequestedThemeChanged += Widget_RequestedThemeChanged;
            _widget.SettingsSupported = true;

            _liveService.AttachWidget(_widget);
            _gameContext = new GameContextService(_widget);
            _gameContext.ContextChanged += OnGameContextChanged;
            ApplyTheme();
            UpdateGameBanner(_gameContext.Current);
            ShowStatusIfNoApiKey();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (_widget != null)
            {
                _widget.SettingsClicked -= Widget_SettingsClicked;
                _widget.RequestedThemeChanged -= Widget_RequestedThemeChanged;
            }

            _gameContext?.Dispose();
            _gameContext = null;
            _liveService.Dispose();
            CancelOperation();
        }

        private void OnGameContextChanged(object sender, GameContextInfo info)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => UpdateGameBanner(info));
        }

        private void UpdateGameBanner(GameContextInfo info)
        {
            GameContextText.Text = info?.Summary ?? "Sin informacion";
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
            RootGrid.Background = _widget.RequestedTheme == ElementTheme.Dark ? _darkBrush : _lightBrush;
        }

        private async void Widget_SettingsClicked(XboxGameBarWidget sender, object args)
        {
            await _widget.ActivateSettingsAsync();
        }

        private void ShowStatusIfNoApiKey()
        {
            if (!AppSettingsService.HasApiKey())
            {
                StatusText.Text = "Configura tu API key en Ajustes (engranaje en Game Bar).";
                StatusText.Visibility = Visibility.Visible;
            }
        }

        private void SetBusy(bool busy, string status = null)
        {
            CaptureButton.IsEnabled = !busy;
            MicButton.IsEnabled = !busy;
            InputBox.IsEnabled = !busy;
            if (!string.IsNullOrEmpty(status))
            {
                StatusText.Text = status;
                StatusText.Visibility = Visibility.Visible;
            }
            else if (AppSettingsService.HasApiKey())
            {
                StatusText.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelOperation()
        {
            _operationCts?.Cancel();
            _operationCts?.Dispose();
            _operationCts = null;
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            await SendUserMessageAsync(InputBox.Text);
        }

        private async void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                await SendUserMessageAsync(InputBox.Text);
            }
        }

        private async System.Threading.Tasks.Task SendUserMessageAsync(string text)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (!AppSettingsService.HasApiKey())
            {
                StatusText.Text = "Falta la API key. Abre Ajustes del widget.";
                StatusText.Visibility = Visibility.Visible;
                return;
            }

            CancelOperation();
            _operationCts = new CancellationTokenSource();
            var screenshot = _pendingScreenshot;
            await AddMessageAsync("Tu", text, screenshot);
            InputBox.Text = string.Empty;
            ClearPendingScreenshotUi();

            SetBusy(true, "Gemini esta pensando...");
            try
            {
                var reply = await _chatService.SendMessageAsync(
                    text,
                    _gameContext.Current,
                    screenshot,
                    _operationCts.Token);
                await AddMessageAsync("Gemini", reply, null);
            }
            catch (Exception ex)
            {
                await AddMessageAsync("Sistema", ex.Message, null);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void Capture_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true, "Selecciona la ventana del juego...");
            try
            {
                var shot = await _captureService.CaptureAsync();
                if (shot == null)
                {
                    StatusText.Text = "Captura cancelada.";
                    StatusText.Visibility = Visibility.Visible;
                    return;
                }

                _pendingScreenshot = shot;
                await ShowScreenshotPreviewAsync(shot.JpegBytes);
                StatusText.Text = "Captura adjunta. Escribe tu pregunta y envia.";
                StatusText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
                StatusText.Visibility = Visibility.Visible;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async System.Threading.Tasks.Task ShowScreenshotPreviewAsync(byte[] jpegBytes)
        {
            var image = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                await stream.WriteAsync(jpegBytes.AsBuffer());
                stream.Seek(0);
                image.SetSource(stream);
            }

            ScreenshotPreview.Source = image;
            ScreenshotPreviewPanel.Visibility = Visibility.Visible;
        }

        private void RemoveScreenshot_Click(object sender, RoutedEventArgs e)
        {
            ClearPendingScreenshotUi();
            StatusText.Visibility = Visibility.Collapsed;
        }

        private void ClearPendingScreenshotUi()
        {
            _pendingScreenshot = null;
            ScreenshotPreview.Source = null;
            ScreenshotPreviewPanel.Visibility = Visibility.Collapsed;
        }

        private async void Mic_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSettingsService.HasApiKey())
            {
                StatusText.Text = "Falta la API key. Abre Ajustes del widget.";
                StatusText.Visibility = Visibility.Visible;
                return;
            }

            CancelOperation();
            _operationCts = new CancellationTokenSource();
            SetBusy(true, "Escuchando... Habla ahora.");
            try
            {
                var screenshot = _pendingScreenshot;
                var reply = await _liveService.ListenAndAskGeminiAsync(
                    _gameContext.Current,
                    screenshot,
                    _operationCts.Token);

                var spoken = string.IsNullOrWhiteSpace(_liveService.LastTranscript)
                    ? "(voz)"
                    : _liveService.LastTranscript;
                await AddMessageAsync("Tu (voz)", spoken, screenshot);
                await AddMessageAsync("Gemini", reply, null);
                ClearPendingScreenshotUi();
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
                StatusText.Visibility = Visibility.Visible;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ClearChat_Click(object sender, RoutedEventArgs e)
        {
            _messages.Clear();
            _chatService.ClearHistory();
        }

        private async System.Threading.Tasks.Task AddMessageAsync(string roleLabel, string text, PendingScreenshot screenshot)
        {
            var vm = new ChatMessageViewModel
            {
                RoleLabel = roleLabel,
                Text = text
            };

            if (screenshot?.JpegBytes != null)
            {
                vm.Thumbnail = await LoadBitmapAsync(screenshot.JpegBytes);
                vm.ThumbnailVisibility = Visibility.Visible;
            }

            _messages.Add(vm);
            MessagesList.UpdateLayout();
            if (_messages.Count > 0)
            {
                MessagesList.ScrollIntoView(_messages[_messages.Count - 1]);
            }
        }

        private static async System.Threading.Tasks.Task<BitmapImage> LoadBitmapAsync(byte[] bytes)
        {
            var image = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                await stream.WriteAsync(bytes.AsBuffer());
                stream.Seek(0);
                image.SetSource(stream);
            }

            return image;
        }
    }
}
