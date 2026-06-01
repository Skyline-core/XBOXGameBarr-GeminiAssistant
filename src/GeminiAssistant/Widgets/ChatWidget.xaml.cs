using System;

using System.Collections.ObjectModel;

using System.Runtime.InteropServices.WindowsRuntime;

using System.Threading;

using System.Threading.Tasks;

using Windows.UI.Core;

using GeminiAssistant.Models;

using Windows.System;

using GeminiAssistant.Services;

using Microsoft.Gaming.XboxGameBar;

using Windows.UI;

using Windows.UI.Xaml;

using Windows.UI.Xaml.Controls;

using Windows.UI.Xaml.Input;

using Windows.UI.Xaml.Media;

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

        private PendingScreenshot _pendingScreenshot;

        private CancellationTokenSource _operationCts;

        private bool _sendInProgress;

        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);

        private readonly SolidColorBrush _darkBrush = new SolidColorBrush(Color.FromArgb(255, 26, 26, 46));

        private readonly SolidColorBrush _lightBrush = new SolidColorBrush(Color.FromArgb(255, 219, 219, 219));



        public ChatWidget()

        {

            InitializeComponent();

            MessagesList.ItemsSource = ChatSessionStore.Current.Messages;

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

            CoreUiDispatcher.Bind(Dispatcher);

            _gameContext = new GameContextService(_widget, Dispatcher);

            _gameContext.ContextChanged += OnGameContextChanged;

            ApplyTheme();

            UpdateGameBanner(_gameContext.Current);

            WidgetFileLog.Write("ChatWidget abierto");

            _sendInProgress = false;

            SetActionButtonsEnabled(true);

            _pendingScreenshot = ChatSessionStore.Current.PendingScreenshot;
            if (_pendingScreenshot?.JpegBytes != null && _pendingScreenshot.JpegBytes.Length > 0)
            {
                ShowScreenshotReadyUi(_pendingScreenshot.JpegBytes.Length, _pendingScreenshot.Source);
            }

            var log = WidgetDiagnostics.GetLastError();
            if (!string.IsNullOrEmpty(log))
            {
                ShowStatus("Diagnostico: " + log);
            }
            else
            {
                ShowStatusIfNoApiKey();
            }

            if (PendingCaptureStore.HasPending())

            {

                _ = RestorePendingCaptureAsync();

            }

            else

            {

                ShowGameBarCaptureTip();

            }



            _ = EnsureCapturePermissionAsync();

        }



        private async System.Threading.Tasks.Task EnsureCapturePermissionAsync()

        {

            try

            {

                var status = await ProgrammaticCaptureHelper.EnsureCaptureAccessAsync();

                WidgetFileLog.Write("Permiso captura al abrir: " + ProgrammaticCaptureHelper.DescribeAccessStatus(status));

                if (status == Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus.DeniedByUser)

                {

                    ShowStatus("Captura: activa permiso de captura de pantalla en Ajustes de Windows.");

                }

            }

            catch (Exception ex)

            {

                WidgetFileLog.Write("Permiso captura: " + WidgetExceptionFormatter.Format(ex));

            }

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



        private void SetActionButtonsEnabled(bool enabled)

        {

            CaptureButton.IsEnabled = enabled;

            MicButton.IsEnabled = enabled;

        }



        private System.Threading.Tasks.Task SetActionButtonsEnabledAsync(bool enabled)

        {

            return CoreUiDispatcher.RunOnUiAsync(() =>

            {

                SetActionButtonsEnabled(enabled);

                return System.Threading.Tasks.Task.CompletedTask;

            });

        }



        private void SetSendInProgress(bool inProgress, string status = null)

        {

            _sendInProgress = inProgress;

            if (!string.IsNullOrEmpty(status))

            {

                ShowStatus(status);

            }

        }



        private System.Threading.Tasks.Task SetSendInProgressAsync(bool inProgress, string status = null)

        {

            return RunOnUiAsync(() => SetSendInProgress(inProgress, status));

        }



        private System.Threading.Tasks.Task SetActionBusyAsync(bool busy, string status = null)

        {

            return RunOnUiAsync(() =>

            {

                SetActionButtonsEnabled(!busy);

                if (!string.IsNullOrEmpty(status))

                {

                    ShowStatus(status);

                }

            });

        }



        /// <summary>Marshaling al hilo UI del widget (Game Bar no tiene SynchronizationContext).</summary>

        private Task RunOnUiAsync(Action action)

        {

            return CoreUiDispatcher.RunOnUiAsync(action ?? (() => { }));

        }



        private async System.Threading.Tasks.Task ReportErrorAsync(string message)

        {

            await SafeReportErrorAsync(message);

        }



        private System.Threading.Tasks.Task SafeReportErrorAsync(string message)

        {

            if (string.IsNullOrWhiteSpace(message))

            {

                message = "Error desconocido (el widget pudo cerrarse durante la peticion).";

            }



            return CoreUiDispatcher.RunOnUiAsync(() =>

            {

                try

                {

                    ShowStatus(message);

                    SafeAddMessageCore("Sistema", message, null);

                }

                catch (Exception ex)

                {

                    WidgetFileLog.Write("SafeReportError fallo: " + WidgetExceptionFormatter.Format(ex));

                }

                return System.Threading.Tasks.Task.CompletedTask;

            });

        }



        private async void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)

        {

            if (e.Key != VirtualKey.Enter)

            {

                return;

            }



            e.Handled = true;

            if (_sendInProgress)

            {

                return;

            }



            try

            {

                await SendUserMessageAsync(InputBox.Text);

            }

            catch (Exception ex)

            {

                WidgetFileLog.Write("InputBox_Enter crash: " + WidgetExceptionFormatter.Format(ex));

                await SafeReportErrorAsync(WidgetExceptionFormatter.Format(ex));

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

            try

            {

                await SendUserMessageAsync(InputBox.Text);

            }

            catch (Exception ex)

            {

                WidgetFileLog.Write("Send_Click crash: " + WidgetExceptionFormatter.Format(ex));

                await SafeReportErrorAsync(WidgetExceptionFormatter.Format(ex));

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



            if (_sendInProgress)

            {

                await RunOnUiAsync(() => ShowStatus("Espera a que termine el envio anterior."));

                return;

            }



            if (!await _sendGate.WaitAsync(0))

            {

                await RunOnUiAsync(() => ShowStatus("Espera a que termine el envio anterior."));

                return;

            }



            CancelOperation();

            _operationCts = new CancellationTokenSource();

            WidgetFileLog.Write("Send inicio");

            var gameContextSnapshot = _gameContext.Current;

            WidgetKeepAlive keepAlive = null;

            try

            {

                await SetSendInProgressAsync(true, "Enviando a Gemini...");



                WidgetFileLog.Write("Send paso: activity");

                await _liveService.BeginRequestActivityAsync();

                WidgetFileLog.Write("Send paso: keepalive");

                keepAlive = await WidgetKeepAlive.BeginAsync();



                if (screenshot?.JpegBytes != null && screenshot.JpegBytes.Length > 220_000)

                {

                    WidgetFileLog.Write("Send paso: comprimir captura");

                    var prepared = await CoreUiDispatcher.RunOnUiAsync(

                        () => ScreenshotImageHelper.PrepareForApiAsync(screenshot.JpegBytes));

                    screenshot = new PendingScreenshot { JpegBytes = prepared, Source = screenshot.Source };

                }



                WidgetFileLog.Write("Send paso: mensaje usuario");

                await CoreUiDispatcher.RunOnUiAsync(() =>

                {

                    SafeAddMessageCore("Tu", text, screenshot);

                    InputBox.Text = string.Empty;

                    return System.Threading.Tasks.Task.CompletedTask;

                });



                WidgetFileLog.Write("Send paso: llamada Gemini");

                var reply = await _chatService.SendMessageAsync(

                    text,

                    gameContextSnapshot,

                    screenshot,

                    _operationCts.Token);

                WidgetFileLog.Write("Send paso: respuesta OK");



                await CoreUiDispatcher.RunOnUiAsync(() =>

                {

                    ClearPendingScreenshotUi();

                    SafeAddMessageCore("Gemini", reply, null);

                    StatusText.Visibility = Visibility.Collapsed;

                    return System.Threading.Tasks.Task.CompletedTask;

                });

            }

            catch (Exception ex)

            {

                var detail = WidgetExceptionFormatter.Format(ex);

                WidgetFileLog.Write("Send error: " + detail);

                await SafeReportErrorAsync(detail);

            }

            finally

            {

                _sendGate.Release();



                try

                {

                    await SetSendInProgressAsync(false);

                    await RunOnUiAsync(() => WidgetFileLog.Write("Send fin"));

                    await WidgetKeepAlive.ReleaseAsync(keepAlive);

                    await _liveService.EndRequestActivityAsync();

                }

                catch (Exception ex)

                {

                    WidgetFileLog.Write("Send cleanup: " + WidgetExceptionFormatter.Format(ex));

                }

            }

        }



        private async void Capture_Click(object sender, RoutedEventArgs e)

        {

            if (!AppSettingsService.HasApiKey())

            {

                await ReportErrorAsync("Falta la API key. Abre Ajustes del widget.");

                return;

            }



            if (!CaptureButton.IsEnabled)

            {

                return;

            }



            WidgetFileLog.Write("Captura inicio");

            var gameContextSnapshot = _gameContext.Current;

            WidgetKeepAlive keepAlive = null;

            try

            {

                await SetActionBusyAsync(true, "Capturando pantalla del juego...");



                await _liveService.BeginCaptureActivityAsync();

                keepAlive = await WidgetKeepAlive.BeginAsync();

                var shot = await _captureService.CaptureAsync(gameContextSnapshot);



                await CoreUiDispatcher.RunOnUiAsync(() =>

                {

                    _pendingScreenshot = shot;

                    ChatSessionStore.Current.PendingScreenshot = shot;

                    ShowScreenshotReadyUi(shot.JpegBytes.Length, shot.Source);

                    ShowStatus("Screenshot listo. Escribe tu mensaje y pulsa Enviar o Enter.");

                    return System.Threading.Tasks.Task.CompletedTask;

                });

            }

            catch (Exception ex)

            {

                WidgetFileLog.Write("Captura error: " + WidgetExceptionFormatter.Format(ex));

                await SafeReportErrorAsync("Captura: " + WidgetExceptionFormatter.Format(ex));

            }

            finally

            {

                await SetActionButtonsEnabledAsync(true);

                WidgetFileLog.Write("Captura fin");



                try

                {

                    await WidgetKeepAlive.ReleaseAsync(keepAlive);

                    await _liveService.EndCaptureActivityAsync();

                }

                catch (Exception ex)

                {

                    WidgetFileLog.Write("Captura cleanup: " + WidgetExceptionFormatter.Format(ex));

                }

            }

        }



        private void ShowScreenshotReadyUi(int jpegByteCount, string source = null)

        {

            var kb = Math.Max(1, jpegByteCount / 1024);

            var sourceLabel = DescribeCaptureSource(source);

            ScreenshotPreviewLabel.Text = "Screenshot listo (" + kb + " KB, " + sourceLabel + "). Pulsa Enviar.";

            ScreenshotPreviewPanel.Visibility = Visibility.Visible;

        }



        private static string DescribeCaptureSource(string source)

        {

            if (string.IsNullOrEmpty(source))
            {
                return "captura";
            }

            switch (source)
            {
                case "ventana":
                    return "ventana elegida";
                case "juego":
                    return "ventana del juego";
                case "gamebar":
                case "gamebar-manual":
                case "gamebar-archivo":
                    return "Game Bar";
                case "ventana-juego":
                    return "ventana del juego";
                default:
                    return source;
            }
        }



        private void RemoveScreenshot_Click(object sender, RoutedEventArgs e)

        {

            ClearPendingScreenshotUi();

            StatusText.Visibility = Visibility.Collapsed;

        }



        private void ClearPendingScreenshotUi()

        {

            _pendingScreenshot = null;

            ChatSessionStore.Current.PendingScreenshot = null;

            _ = PendingCaptureStore.ClearAsync();

            ScreenshotPreviewPanel.Visibility = Visibility.Collapsed;

        }



        private void ShowGameBarCaptureTip()

        {

            ShowStatus(

                "Capturar hace screenshot del juego automaticamente. Enter envia.");

        }



        private async System.Threading.Tasks.Task RestorePendingCaptureAsync()

        {

            if (!PendingCaptureStore.HasPending())

            {

                return;

            }



            try

            {

                var bytes = await PendingCaptureStore.LoadAsync().ConfigureAwait(true);

                if (bytes == null || bytes.Length == 0)

                {

                    return;

                }



                _pendingScreenshot = new PendingScreenshot { JpegBytes = bytes };

                ChatSessionStore.Current.PendingScreenshot = _pendingScreenshot;

                ShowScreenshotReadyUi(bytes.Length);

                ShowStatus("Captura recuperada. Escribe tu mensaje y pulsa Enviar.");

            }

            catch (Exception ex)

            {

                ShowStatus("No se pudo restaurar la captura: " + ex.Message);

            }

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



            if (!MicButton.IsEnabled)

            {

                return;

            }



            var gameContextSnapshot = _gameContext.Current;

            WidgetKeepAlive keepAlive = null;

            try

            {

                await SetActionBusyAsync(true, "Grabando 6 s... Habla AHORA (no hace falta el cuadro de texto).");



                await _liveService.BeginVoiceActivityAsync();

                var wav = await CoreUiDispatcher.RunOnUiAsync(

                    () => AudioRecordingService.RecordWavAsync(VoiceRecordDuration, _operationCts.Token));



                await CoreUiDispatcher.RunOnUiAsync(() =>

                {

                    SafeAddMessageCore("Tu (voz)", "Audio grabado, enviando a Gemini...", screenshot);

                    ShowStatus("Gemini transcribe tu voz...");

                    return System.Threading.Tasks.Task.CompletedTask;

                });



                await _liveService.BeginRequestActivityAsync();

                try

                {

                    keepAlive = await WidgetKeepAlive.BeginAsync();

                    var reply = await _chatService.SendVoiceMessageAsync(

                        wav,

                        gameContextSnapshot,

                        screenshot,

                        _operationCts.Token);



                    await CoreUiDispatcher.RunOnUiAsync(() =>

                    {

                        ClearPendingScreenshotUi();

                        SafeAddMessageCore("Gemini", reply, null);

                        StatusText.Visibility = Visibility.Collapsed;

                        return System.Threading.Tasks.Task.CompletedTask;

                    });

                }

                finally

                {

                    await WidgetKeepAlive.ReleaseAsync(keepAlive);

                    await _liveService.EndRequestActivityAsync();

                }

            }

            catch (OperationCanceledException)

            {

                await ReportErrorAsync("Grabacion de voz cancelada.");

            }

            catch (Exception ex)

            {

                await SafeReportErrorAsync("Microfono: " + WidgetExceptionFormatter.Format(ex));

            }

            finally

            {

                await SetActionButtonsEnabledAsync(true);



                try

                {

                    await _liveService.EndVoiceActivityAsync();

                }

                catch (Exception ex)

                {

                    WidgetFileLog.Write("Microfono cleanup voz: " + WidgetExceptionFormatter.Format(ex));

                }

            }

        }



        private void ShowLog_Click(object sender, RoutedEventArgs e)

        {

            var log = WidgetDiagnostics.GetLastError();

            if (string.IsNullOrEmpty(log))

            {

                ShowStatus("Sin entradas en widget-diagnostic.log");

            }

            else

            {

                ShowStatus("Log: " + log);

            }

        }



        private void ClearChat_Click(object sender, RoutedEventArgs e)

        {

            ChatSessionStore.Reset();

            MessagesList.ItemsSource = ChatSessionStore.Current.Messages;

            _chatService.ClearHistory();

        }



        private System.Threading.Tasks.Task AddMessageAsync(string roleLabel, string text, PendingScreenshot screenshot)

        {

            return CoreUiDispatcher.RunOnUiAsync(() =>

            {

                SafeAddMessageCore(roleLabel, text, screenshot);

                return System.Threading.Tasks.Task.CompletedTask;

            });

        }



        private void SafeAddMessageCore(string roleLabel, string text, PendingScreenshot screenshot)

        {

            try

            {

                AddMessageCore(roleLabel, text, screenshot);

            }

            catch (Exception ex)

            {

                WidgetFileLog.Write("AddMessage: " + WidgetExceptionFormatter.Format(ex));

                throw;

            }

        }



        private void AddMessageCore(string roleLabel, string text, PendingScreenshot screenshot)

        {

            var vm = new ChatMessageViewModel

            {

                RoleLabel = roleLabel ?? "?",

                Text = text ?? string.Empty

            };



            if (screenshot?.JpegBytes != null && screenshot.JpegBytes.Length > 0)

            {

                var kb = Math.Max(1, screenshot.JpegBytes.Length / 1024);

                var src = DescribeCaptureSource(screenshot.Source);

                vm.AttachmentCaption = "[Screenshot " + src + ", " + kb + " KB]";

                vm.AttachmentVisibility = Visibility.Visible;

            }



            ChatSessionStore.Current.Messages.Add(vm);

        }

    }

}


