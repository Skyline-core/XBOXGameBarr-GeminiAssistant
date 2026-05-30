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

        private static readonly TimeSpan VoiceRecordDuration = TimeSpan.FromSeconds(6);



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

                ShowStatus("Configura tu API key en Ajustes (engranaje en Game Bar).");

            }

        }



        private void ShowStatus(string message)

        {

            StatusText.Text = message;

            StatusText.Visibility = Visibility.Visible;

        }



        private void SetBusy(bool busy, string status = null)

        {

            CaptureButton.IsEnabled = !busy;

            MicButton.IsEnabled = !busy;

            InputBox.IsEnabled = !busy;

            if (!string.IsNullOrEmpty(status))

            {

                ShowStatus(status);

            }

        }



        private async System.Threading.Tasks.Task ReportErrorAsync(string message)

        {

            ShowStatus(message);

            await AddMessageAsync("Sistema", message, null);

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

            var screenshot = _pendingScreenshot;

            if (string.IsNullOrEmpty(text) && screenshot == null)

            {

                return;

            }

            if (string.IsNullOrEmpty(text) && screenshot != null)

            {

                await ReportErrorAsync("Escribe que quieres saber sobre la captura y pulsa Enviar.");

                return;

            }



            if (!AppSettingsService.HasApiKey())

            {

                await ReportErrorAsync("Falta la API key. Abre Ajustes del widget.");

                return;

            }



            CancelOperation();

            _operationCts = new CancellationTokenSource();

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

                StatusText.Visibility = Visibility.Collapsed;

            }

            catch (Exception ex)

            {

                await ReportErrorAsync(ex.Message);

            }

            finally

            {

                SetBusy(false);

            }

        }



        private async void Capture_Click(object sender, RoutedEventArgs e)

        {

            if (!AppSettingsService.HasApiKey())

            {

                await ReportErrorAsync("Falta la API key. Abre Ajustes del widget.");

                return;

            }



            SetBusy(true, "Capturando ventana del juego...");

            try

            {

                _gameContext.Refresh();

                var shot = await _captureService.CaptureAsync(_gameContext.Current, _widget).ConfigureAwait(true);



                _pendingScreenshot = shot;

                await ShowScreenshotPreviewAsync(shot.JpegBytes);

                ShowStatus("Captura lista. Escribe tu mensaje y pulsa Enviar.");

            }

            catch (Exception ex)

            {

                var detail = ex.InnerException != null
                    ? ex.Message + " (" + ex.InnerException.Message + ")"
                    : ex.Message;
                await ReportErrorAsync("Captura: " + detail);

            }

            finally

            {

                SetBusy(false);

            }

        }



        private async System.Threading.Tasks.Task ShowScreenshotPreviewAsync(byte[] jpegBytes)

        {

            var image = new BitmapImage { DecodePixelWidth = 160 };

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

                await ReportErrorAsync("Falta la API key. Abre Ajustes del widget.");

                return;

            }



            CancelOperation();

            _operationCts = new CancellationTokenSource();

            var screenshot = _pendingScreenshot;



            _liveService.BeginVoiceActivity();

            SetBusy(true, "Grabando 6 s... Habla AHORA (no hace falta el cuadro de texto).");

            try

            {

                var wav = await AudioRecordingService.RecordWavAsync(

                    VoiceRecordDuration,

                    _operationCts.Token).ConfigureAwait(true);



                await AddMessageAsync("Tu (voz)", "Audio grabado, enviando a Gemini...", screenshot);

                ShowStatus("Gemini transcribe tu voz...");



                var reply = await _chatService.SendVoiceMessageAsync(

                    wav,

                    _gameContext.Current,

                    screenshot,

                    _operationCts.Token).ConfigureAwait(true);



                ClearPendingScreenshotUi();

                await AddMessageAsync("Gemini", reply, null);

                StatusText.Visibility = Visibility.Collapsed;

            }

            catch (OperationCanceledException)

            {

                await ReportErrorAsync("Grabacion de voz cancelada.");

            }

            catch (Exception ex)

            {

                await ReportErrorAsync("Microfono: " + ex.Message);

            }

            finally

            {

                _liveService.EndVoiceActivity();

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

            var image = new BitmapImage { DecodePixelWidth = 320 };

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


