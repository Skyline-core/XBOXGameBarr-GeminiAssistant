using System;

using System.Collections.Specialized;

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

        private static readonly TimeSpan VoiceRecordMaxDuration = TimeSpan.FromMinutes(3);

        private XboxGameBarWidget _widget;
        private GameContextService _gameContext;
        private readonly GeminiChatService _chatService = new GeminiChatService();
        private readonly GeminiLiveService _liveService = new GeminiLiveService();
        private readonly ScreenCaptureService _captureService = new ScreenCaptureService();
        private PendingScreenshot _pendingScreenshot;
        private CancellationTokenSource _operationCts;
        private AudioRecordingSession _voiceRecordingSession;
        private bool _isVoiceRecording;
        private bool _sendInProgress;

        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);

        private readonly SolidColorBrush _darkBrush = new SolidColorBrush(Color.FromArgb(255, 27, 27, 31));

        private readonly SolidColorBrush _lightBrush = new SolidColorBrush(Color.FromArgb(255, 245, 245, 247));

        private readonly SolidColorBrush _gameTrackingOnBrush = new SolidColorBrush(Color.FromArgb(255, 129, 199, 132));

        private readonly SolidColorBrush _gameTrackingOffBrush = new SolidColorBrush(Color.FromArgb(255, 98, 91, 113));



        public ChatWidget()

        {

            InitializeComponent();

            CoreUiDispatcher.Bind(Dispatcher);

            MessagesList.ItemsSource = ChatSessionStore.Current.Messages;
            ChatSessionStore.Current.Messages.CollectionChanged += OnMessagesCollectionChanged;
            UpdateSendButtonVisibility();
            UpdateViewMode();
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

            ProfileInitialsText.Text = GetUserInitials();

            UpdateViewMode();

            WidgetFileLog.Write("ChatWidget abierto");

            _sendInProgress = false;

            SetActionButtonsEnabled(true);

            _pendingScreenshot = ChatSessionStore.Current.PendingScreenshot;
            if (_pendingScreenshot?.JpegBytes != null && _pendingScreenshot.JpegBytes.Length > 0)
            {
                _ = ShowScreenshotReadyUiAsync(_pendingScreenshot);
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

            ChatSessionStore.Current.Messages.CollectionChanged -= OnMessagesCollectionChanged;

            CancelOperation();

        }



        private void OnGameContextChanged(object sender, GameContextInfo info)

        {

            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => UpdateGameBanner(info));

        }



        private void UpdateGameBanner(GameContextInfo info)

        {

            var name = info?.DisplayName;
            var tracking = info?.TrackingEnabled == true;
            var hasGame = !string.IsNullOrWhiteSpace(name) &&
                          !name.Equals("Desconocido", StringComparison.OrdinalIgnoreCase);

            if (GameIndicatorLabel != null)
            {
                if (!tracking)
                {
                    GameIndicatorLabel.Text = "Seguimiento desactivado";
                }
                else if (hasGame)
                {
                    GameIndicatorLabel.Text = info?.IsGame == true ? "Jugando ahora" : "App en primer plano";
                }
                else
                {
                    GameIndicatorLabel.Text = "Esperando juego";
                }

                GameIndicatorText.Text = hasGame
                    ? name
                    : (tracking ? "Sin juego detectado" : "Activa seguimiento en Game Bar");
                GameTrackingDot.Fill = tracking && hasGame ? _gameTrackingOnBrush : _gameTrackingOffBrush;
            }

            if (string.IsNullOrWhiteSpace(name) || name.Equals("Desconocido", StringComparison.OrdinalIgnoreCase))
            {
                GreetingText.Text = "Hola";
                GameContextText.Text = info?.Summary ?? "Activa seguimiento del juego en Game Bar";
            }
            else
            {
                GreetingText.Text = "Hola, " + name;
                GameContextText.Text = info?.Summary ?? string.Empty;
            }

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



        private void SetVoiceRecordingUi(bool recording)
        {
            _isVoiceRecording = recording;
            CaptureButton.IsEnabled = !recording && !_sendInProgress;
            if (FabCaptureButton != null)
            {
                FabCaptureButton.IsEnabled = !recording && !_sendInProgress;
            }

            MicButton.IsEnabled = true;
            if (MicButtonIcon != null)
            {
                MicButtonIcon.Glyph = recording ? "\uE71A" : "\uE720";
            }

            ToolTipService.SetToolTip(
                MicButton,
                recording ? "Detener y enviar" : "Grabar voz");
        }

        private System.Threading.Tasks.Task SetVoiceRecordingUiAsync(bool recording, string status = null)
        {
            return RunOnUiAsync(() =>
            {
                SetVoiceRecordingUi(recording);
                if (!string.IsNullOrEmpty(status))
                {
                    ShowStatus(status);
                }
            });
        }

        private void DisposeVoiceRecordingSession()
        {
            if (_voiceRecordingSession == null)
            {
                return;
            }

            try
            {
                _voiceRecordingSession.Dispose();
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Voz liberar sesion: " + WidgetExceptionFormatter.Format(ex));
            }

            _voiceRecordingSession = null;
        }

        private void SetActionButtonsEnabled(bool enabled)

        {

            CaptureButton.IsEnabled = enabled;

            MicButton.IsEnabled = enabled;

            if (FabCaptureButton != null)
            {
                FabCaptureButton.IsEnabled = enabled;
            }

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

            if (_isVoiceRecording)

            {

                _isVoiceRecording = false;

                if (_voiceRecordingSession != null)

                {

                    try

                    {

                        _voiceRecordingSession.StopAsync().GetAwaiter().GetResult();

                    }

                    catch

                    {

                    }

                    try

                    {

                        _voiceRecordingSession.Dispose();

                    }

                    catch

                    {

                    }

                    _voiceRecordingSession = null;

                }

                _ = RunOnUiAsync(() => SetVoiceRecordingUi(false));

            }

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

                var shot = await Task.Run(async () =>
                    await _captureService.CaptureAsync(gameContextSnapshot).ConfigureAwait(false))
                    .ConfigureAwait(true);

                shot = await TryMergeClipboardCaptureOnUiAsync(shot).ConfigureAwait(true);

                await ApplyPendingScreenshotOnUiAsync(shot).ConfigureAwait(true);

                await RunOnUiAsync(() =>
                    ShowStatus("Screenshot listo. Escribe tu mensaje y pulsa Enviar o Enter."));

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



        private static async System.Threading.Tasks.Task<PendingScreenshot> TryMergeClipboardCaptureOnUiAsync(
            PendingScreenshot shot)
        {
            await CoreUiDispatcher.YieldToUiAsync().ConfigureAwait(true);

            try
            {
                var fromClipboard = await ClipboardScreenshotImporter.TryImportJpegAsync()
                    .ConfigureAwait(true);
                if (fromClipboard == null || fromClipboard.Length < 100)
                {
                    return shot;
                }

                if (shot?.JpegBytes == null || shot.JpegBytes.Length < 100 ||
                    fromClipboard.Length > shot.JpegBytes.Length)
                {
                    WidgetFileLog.Write("Captura OK portapapeles (UI)");
                    return new PendingScreenshot { JpegBytes = fromClipboard, Source = "gamebar" };
                }
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Portapapeles UI: " + WidgetExceptionFormatter.Format(ex));
            }

            return shot;
        }

        private System.Threading.Tasks.Task ApplyPendingScreenshotOnUiAsync(PendingScreenshot screenshot)
        {
            return CoreUiDispatcher.RunOnUiAsync(async () =>
            {
                if (screenshot?.JpegBytes == null || screenshot.JpegBytes.Length == 0)
                {
                    return;
                }

                var jpegBytes = screenshot.JpegBytes;
                if (jpegBytes.Length > 220_000)
                {
                    jpegBytes = await ScreenshotImageHelper.PrepareForApiAsync(jpegBytes)
                        .ConfigureAwait(true);
                }

                await CoreUiDispatcher.YieldToUiAsync().ConfigureAwait(true);

                screenshot.JpegBytes = jpegBytes;
                _pendingScreenshot = screenshot;
                ChatSessionStore.Current.PendingScreenshot = screenshot;

                try
                {
                    await PendingCaptureStore.SaveAsync(jpegBytes).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    WidgetFileLog.Write("Captura guardar archivo: " + WidgetExceptionFormatter.Format(ex));
                }

                await CoreUiDispatcher.YieldToUiAsync().ConfigureAwait(true);

                var kb = Math.Max(1, jpegBytes.Length / 1024);
                var sourceLabel = DescribeCaptureSource(screenshot.Source);
                ScreenshotPreviewLabel.Text = "Captura lista · " + kb + " KB · " + sourceLabel;
                ScreenshotPreviewPanel.Visibility = Visibility.Visible;

                var thumbnail = await ScreenshotThumbnailHelper.CreateFromJpegAsync(jpegBytes)
                    .ConfigureAwait(true);

                await CoreUiDispatcher.YieldToUiAsync().ConfigureAwait(true);

                if (thumbnail != null)
                {
                    ScreenshotPreview.Source = thumbnail;
                    ScreenshotPreviewPlaceholder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ScreenshotPreview.Source = null;
                    ScreenshotPreviewPlaceholder.Visibility = Visibility.Visible;
                }
            });
        }

        private System.Threading.Tasks.Task ShowScreenshotReadyUiAsync(PendingScreenshot screenshot)
        {
            return ApplyPendingScreenshotOnUiAsync(screenshot);
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

            ScreenshotPreview.Source = null;
            ScreenshotPreviewPlaceholder.Visibility = Visibility.Visible;
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

                await ShowScreenshotReadyUiAsync(_pendingScreenshot);

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



            if (_isVoiceRecording)

            {

                await FinishVoiceRecordingAndSendAsync();

                return;

            }



            if (_sendInProgress || !MicButton.IsEnabled)

            {

                return;

            }



            CancelOperation();

            _operationCts = new CancellationTokenSource();

            try

            {

                await SetVoiceRecordingUiAsync(
                    true,
                    "Grabando... Pulsa el microfono otra vez para detener y enviar.");

                await _liveService.BeginVoiceActivityAsync();

                _voiceRecordingSession = await CoreUiDispatcher.RunOnUiAsync(

                    () => AudioRecordingService.StartRecordingAsync(_operationCts.Token));



                _ = Task.Run(async () =>

                {

                    try

                    {

                        await Task.Delay(VoiceRecordMaxDuration, _operationCts.Token).ConfigureAwait(false);

                        if (_isVoiceRecording)

                        {

                            await CoreUiDispatcher.RunOnUiAsync(() =>

                                ShowStatus("Tiempo maximo de grabacion, enviando..."));

                            await FinishVoiceRecordingAndSendAsync().ConfigureAwait(false);

                        }

                    }

                    catch (OperationCanceledException)

                    {

                    }

                });

            }

            catch (Exception ex)

            {

                DisposeVoiceRecordingSession();

                await SetVoiceRecordingUiAsync(false);

                await SetActionButtonsEnabledAsync(true);



                try

                {

                    await _liveService.EndVoiceActivityAsync();

                }

                catch (Exception cleanupEx)

                {

                    WidgetFileLog.Write("Microfono inicio cleanup: " + WidgetExceptionFormatter.Format(cleanupEx));

                }



                await SafeReportErrorAsync("Microfono: " + WidgetExceptionFormatter.Format(ex));

            }

        }



        private async System.Threading.Tasks.Task FinishVoiceRecordingAndSendAsync()

        {

            if (!_isVoiceRecording || _voiceRecordingSession == null)

            {

                return;

            }



            _isVoiceRecording = false;

            var session = _voiceRecordingSession;

            _voiceRecordingSession = null;

            var screenshot = _pendingScreenshot;

            var gameContextSnapshot = _gameContext.Current;

            WidgetKeepAlive keepAlive = null;



            try

            {

                await SetVoiceRecordingUiAsync(false, "Procesando audio...");



                var wav = await CoreUiDispatcher.RunOnUiAsync(() => session.StopAsync());



                await CoreUiDispatcher.RunOnUiAsync(() =>

                {

                    SafeAddMessageCore("Tu (voz)", "Analizando voz", screenshot);

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

                        _operationCts?.Token ?? CancellationToken.None);



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

                try

                {

                    session?.Dispose();

                }

                catch (Exception ex)

                {

                    WidgetFileLog.Write("Voz liberar sesion: " + WidgetExceptionFormatter.Format(ex));

                }

                await SetVoiceRecordingUiAsync(false);

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

            ChatSessionStore.Current.Messages.CollectionChanged += OnMessagesCollectionChanged;

            MessagesList.ItemsSource = ChatSessionStore.Current.Messages;

            _chatService.ClearHistory();

            UpdateViewMode();

            InputBox.Text = string.Empty;

            UpdateSendButtonVisibility();

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

            var isUser = IsUserRole(roleLabel);

            var vm = new ChatMessageViewModel

            {

                RoleLabel = roleLabel ?? "?",

                Text = text ?? string.Empty,

                IsUserMessage = isUser,

                AvatarGlyph = isUser ? GetUserInitials() : "✦"

            };



            if (screenshot?.JpegBytes != null && screenshot.JpegBytes.Length > 0)

            {

                var kb = Math.Max(1, screenshot.JpegBytes.Length / 1024);

                var src = DescribeCaptureSource(screenshot.Source);

                vm.AttachmentCaption = "[Screenshot " + src + ", " + kb + " KB]";

                vm.AttachmentVisibility = Visibility.Visible;

                _ = SetMessageThumbnailAsync(vm, screenshot.JpegBytes);

            }



            ChatSessionStore.Current.Messages.Add(vm);

            ScrollMessagesToEnd();

        }



        private static async System.Threading.Tasks.Task SetMessageThumbnailAsync(
            ChatMessageViewModel message,
            byte[] jpegBytes)
        {
            var thumbnail = await ScreenshotThumbnailHelper.CreateFromJpegAsync(jpegBytes)
                .ConfigureAwait(true);
            if (thumbnail == null)
            {
                return;
            }

            await CoreUiDispatcher.YieldToUiAsync().ConfigureAwait(true);
            message.Thumbnail = thumbnail;
            message.ThumbnailVisibility = Visibility.Visible;
        }

        private static bool IsUserRole(string roleLabel)

        {

            if (string.IsNullOrEmpty(roleLabel))
            {
                return false;
            }

            return roleLabel.StartsWith("Tu", StringComparison.OrdinalIgnoreCase);

        }



        private static string GetUserInitials()

        {

            return "TU";

        }



        private void OnMessagesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)

        {

            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>

            {

                UpdateViewMode();

                ScrollMessagesToEnd();

            });

        }



        private void UpdateViewMode()

        {

            var hasChat = ChatSessionStore.Current.Messages.Count > 0;

            HomePanel.Visibility = hasChat ? Visibility.Collapsed : Visibility.Visible;

            ChatPanel.Visibility = hasChat ? Visibility.Visible : Visibility.Collapsed;

            InputBox.PlaceholderText = hasChat ? "Escribe un mensaje..." : "Pregúntame lo que quieras";

        }



        private void ScrollMessagesToEnd()

        {

            if (MessagesList.Items.Count == 0)
            {
                return;
            }

            MessagesList.UpdateLayout();

            var last = MessagesList.Items[MessagesList.Items.Count - 1];

            MessagesList.ScrollIntoView(last);

        }



        private void UpdateSendButtonVisibility()

        {

            var hasText = !string.IsNullOrWhiteSpace(InputBox?.Text);

            SendButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;

        }



        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)

        {

            UpdateSendButtonVisibility();

        }



        private void Home_Click(object sender, RoutedEventArgs e)

        {

            ClearChat_Click(sender, e);

            StatusText.Visibility = Visibility.Collapsed;

            ShowGameBarCaptureTip();

        }



        private async void MenuSettings_Click(object sender, RoutedEventArgs e)

        {

            if (_widget != null)
            {
                await _widget.ActivateSettingsAsync();
            }

        }



        private void InsertFabPrompt(string text)

        {

            InputBox.Text = text;

            InputBox.SelectionStart = text.Length;

            UpdateSendButtonVisibility();

            InputBox.Focus(FocusState.Programmatic);

        }



        private void FabAchievements_Click(object sender, RoutedEventArgs e) => InsertFabPrompt("Mostrar mis logros de Xbox");

        private void FabTips_Click(object sender, RoutedEventArgs e) => InsertFabPrompt("Dame tips para este juego");

        private void FabStats_Click(object sender, RoutedEventArgs e) => InsertFabPrompt("¿Cuál es mi estadística de juego?");



        private async void SuggestionGames_Click(object sender, RoutedEventArgs e)

        {

            await SendUserMessageAsync("Recomendaciones de juegos para mi perfil");

        }



        private async void SuggestionAchievements_Click(object sender, RoutedEventArgs e)

        {

            await SendUserMessageAsync("Mostrar mis logros de Xbox");

        }



        private async void SuggestionCapture_Click(object sender, RoutedEventArgs e)

        {

            Capture_Click(sender, e);

        }

    }

}


