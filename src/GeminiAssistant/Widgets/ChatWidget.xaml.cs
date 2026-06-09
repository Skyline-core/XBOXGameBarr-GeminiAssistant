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
        private readonly SteamWebApiService _steamService = new SteamWebApiService();
        private PendingScreenshot _pendingScreenshot;
        private CancellationTokenSource _operationCts;
        private AudioRecordingSession _voiceRecordingSession;
        private bool _isVoiceRecording;
        private bool _sendInProgress;
        private string _lastSteamGameQuery;

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
            RefreshLocalization();

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
                ShowStatus(LocalizedStrings.Format("Status_Diagnostic", log));
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

                    ShowStatus(LocalizedStrings.Status_CapturePermission);

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
            var hasGame = !LocalizedStrings.IsUnknownGameName(name);

            if (GameIndicatorLabel != null)
            {
                if (!tracking)
                {
                    GameIndicatorLabel.Text = LocalizedStrings.Game_TrackingOff;
                }
                else if (hasGame)
                {
                    GameIndicatorLabel.Text = info?.IsGame == true
                        ? LocalizedStrings.Game_PlayingNow
                        : LocalizedStrings.Game_AppForeground;
                }
                else
                {
                    GameIndicatorLabel.Text = LocalizedStrings.Game_WaitingGame;
                }

                GameIndicatorText.Text = hasGame
                    ? name
                    : (tracking ? LocalizedStrings.Game_NoGameDetected : LocalizedStrings.Game_TrackingOffHint);
                GameTrackingDot.Fill = tracking && hasGame ? _gameTrackingOnBrush : _gameTrackingOffBrush;
            }

            if (LocalizedStrings.IsUnknownGameName(name))
            {
                GreetingText.Text = LocalizedStrings.Chat_GreetingHello;
                GameContextText.Text = info?.Summary ?? LocalizedStrings.Game_EnableTracking;
            }
            else
            {
                GreetingText.Text = LocalizedStrings.Chat_GreetingHello + ", " + name;
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

                ShowStatus(LocalizedStrings.Status_NoApiKey);

            }

        }



        private void ShowStatus(string message)

        {

            if (!CoreUiDispatcher.IsOnUiThread)

            {

                _ = CoreUiDispatcher.InvokeOnUiAsync(() => ShowStatus(message));

                return;

            }

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
                recording ? LocalizedStrings.Voice_StopSend : LocalizedStrings.Chat_TooltipMic);
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

                message = LocalizedStrings.Error_Unknown;

            }



            return CoreUiDispatcher.RunOnUiAsync(() =>

            {

                try

                {

                    ShowStatus(message);

                    SafeAddMessageCore(LocalizedStrings.Msg_System, message, null);

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



        private async System.Threading.Tasks.Task SendUserMessageAsync(string text, string geminiText = null)

        {

            text = text?.Trim();
            geminiText = geminiText?.Trim();

            var apiText = string.IsNullOrEmpty(geminiText) ? text : geminiText;

            var screenshot = _pendingScreenshot;

            if (string.IsNullOrEmpty(text) && screenshot == null)

            {

                return;

            }

            if (string.IsNullOrEmpty(text) && screenshot != null)

            {

                await ReportErrorAsync(LocalizedStrings.Error_WriteCapturePrompt);

                return;

            }



            if (!AppSettingsService.HasApiKey())

            {

                await ReportErrorAsync(LocalizedStrings.Error_MissingApiKey);

                return;

            }

            var gameContextSnapshot = _gameContext.Current;



            if (string.IsNullOrEmpty(geminiText) &&
                SteamQueryHelper.ShouldFetchSteamData(text, gameContextSnapshot?.DisplayName) &&
                !AppSettingsService.HasSteamCredentials())
            {
                await ReportErrorAsync(LocalizedStrings.Error_MissingSteam);
                return;
            }



            if (_sendInProgress)

            {

                await RunOnUiAsync(() => ShowStatus(LocalizedStrings.Status_WaitSend));

                return;

            }



            if (!await _sendGate.WaitAsync(0))

            {

                await RunOnUiAsync(() => ShowStatus(LocalizedStrings.Status_WaitSend));

                return;

            }



            CancelOperation();

            _operationCts = new CancellationTokenSource();

            WidgetFileLog.Write("Send inicio");

            WidgetKeepAlive keepAlive = null;

            try

            {

                if (string.IsNullOrEmpty(geminiText) &&
                    SteamQueryHelper.ShouldFetchSteamData(text, gameContextSnapshot?.DisplayName))
                {
                    await RunOnUiAsync(() => ShowStatus(LocalizedStrings.Status_QueryingSteam));

                    var trackingGame = gameContextSnapshot?.IsGame == true &&
                        !LocalizedStrings.IsUnknownGameName(gameContextSnapshot?.DisplayName);

                    var gameForSteam = SteamQueryHelper.ResolveGameNameForQuery(
                        text,
                        gameContextSnapshot?.DisplayName,
                        trackingGame,
                        _lastSteamGameQuery);

                    if (!string.IsNullOrWhiteSpace(gameForSteam))
                    {
                        _lastSteamGameQuery = gameForSteam;
                    }

                    var steamBlock = await _steamService.TryBuildChatContextAsync(
                        text,
                        gameContextSnapshot?.DisplayName,
                        trackingGame,
                        _lastSteamGameQuery,
                        _operationCts.Token).ConfigureAwait(false);

                    await CoreUiDispatcher.YieldToUiAsync().ConfigureAwait(true);

                    if (string.IsNullOrWhiteSpace(steamBlock))
                    {
                        await ReportErrorAsync(LocalizedStrings.Error_SteamNoResponse);
                        return;
                    }

                    if (SteamWebApiService.IsSteamFailureContext(steamBlock))
                    {
                        await ReportErrorAsync(steamBlock);
                        return;
                    }

                    apiText = text + "\n\n" + steamBlock + "\n\n" + LocalizedStrings.Gemini_SteamContextSuffix;

                    WidgetFileLog.Write("Steam chat context OK bytes=" + steamBlock.Length);
                    await RunOnUiAsync(() => ShowStatus(LocalizedStrings.Status_SteamOk));
                }

                await SetSendInProgressAsync(true, LocalizedStrings.Status_SendingGemini);



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

                    SafeAddMessageCore(LocalizedStrings.Msg_User, text, screenshot);

                    InputBox.Text = string.Empty;

                    return System.Threading.Tasks.Task.CompletedTask;

                });



                WidgetFileLog.Write("Send paso: llamada Gemini");

                var reply = await _chatService.SendMessageAsync(

                    apiText,

                    gameContextSnapshot,

                    screenshot,

                    _operationCts.Token).ConfigureAwait(false);

                WidgetFileLog.Write("Send paso: respuesta OK");

                await CoreUiDispatcher.YieldToUiAsync().ConfigureAwait(true);



                await CoreUiDispatcher.RunOnUiAsync(() =>

                {

                    ClearPendingScreenshotUi();

                    SafeAddMessageCore(LocalizedStrings.Msg_Gemini, reply, null);

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

                try

                {

                    await CoreUiDispatcher.YieldToUiAsync().ConfigureAwait(true);

                    await SetSendInProgressAsync(false).ConfigureAwait(true);

                    await RunOnUiAsync(() => WidgetFileLog.Write("Send fin")).ConfigureAwait(true);

                    await WidgetKeepAlive.ReleaseAsync(keepAlive).ConfigureAwait(true);

                    await _liveService.EndRequestActivityAsync().ConfigureAwait(true);

                }

                catch (Exception ex)

                {

                    WidgetFileLog.Write("Send cleanup: " + WidgetExceptionFormatter.Format(ex));

                }

                finally

                {

                    _sendGate.Release();

                }

            }

        }



        private async void Capture_Click(object sender, RoutedEventArgs e)

        {

            if (!AppSettingsService.HasApiKey())

            {

                await ReportErrorAsync(LocalizedStrings.Error_MissingApiKey);

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

                await SetActionBusyAsync(true, LocalizedStrings.Status_Capturing);



                await _liveService.BeginCaptureActivityAsync();

                keepAlive = await WidgetKeepAlive.BeginAsync();

                var shot = await Task.Run(async () =>
                    await _captureService.CaptureAsync(gameContextSnapshot).ConfigureAwait(false))
                    .ConfigureAwait(true);

                shot = await TryMergeClipboardCaptureOnUiAsync(shot).ConfigureAwait(true);

                await ApplyPendingScreenshotOnUiAsync(shot).ConfigureAwait(true);

                await RunOnUiAsync(() =>
                    ShowStatus(LocalizedStrings.Status_ScreenshotReady));

            }

            catch (Exception ex)

            {

                WidgetFileLog.Write("Captura error: " + WidgetExceptionFormatter.Format(ex));

                await SafeReportErrorAsync(LocalizedStrings.Format("Error_CapturePrefix", WidgetExceptionFormatter.Format(ex)));

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
                ScreenshotPreviewLabel.Text = LocalizedStrings.Format(
                    nameof(LocalizedStrings.Capture_ReadyLabel), kb, sourceLabel);
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
                return LocalizedStrings.Capture_Source_Default;
            }

            switch (source)
            {
                case "ventana":
                    return LocalizedStrings.Capture_Source_Window;
                case "juego":
                    return LocalizedStrings.Capture_Source_GameWindow;
                case "gamebar":
                case "gamebar-manual":
                case "gamebar-archivo":
                    return LocalizedStrings.Capture_Source_GameBar;
                case "ventana-juego":
                    return LocalizedStrings.Capture_Source_GameWindow;
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

            ShowStatus(LocalizedStrings.Status_CaptureTip);

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

                ShowStatus(LocalizedStrings.Status_CaptureRestored);

            }

            catch (Exception ex)

            {

                ShowStatus(LocalizedStrings.Format("Status_CaptureRestoreFailed", ex.Message));

            }

        }



        private async void Mic_Click(object sender, RoutedEventArgs e)

        {

            if (!AppSettingsService.HasApiKey())

            {

                await ReportErrorAsync(LocalizedStrings.Error_MissingApiKey);

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
                    LocalizedStrings.Status_Recording);

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

                                ShowStatus(LocalizedStrings.Status_RecordingMax));

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



                await SafeReportErrorAsync(LocalizedStrings.Format("Error_MicPrefix", WidgetExceptionFormatter.Format(ex)));

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

                await SetVoiceRecordingUiAsync(false, LocalizedStrings.Status_ProcessingAudio);



                var wav = await CoreUiDispatcher.RunOnUiAsync(() => session.StopAsync());



                await CoreUiDispatcher.RunOnUiAsync(() =>

                {

                    SafeAddMessageCore(LocalizedStrings.Msg_UserVoice, LocalizedStrings.Msg_AnalyzingVoice, screenshot);

                    ShowStatus(LocalizedStrings.Status_Transcribing);

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

                        SafeAddMessageCore(LocalizedStrings.Msg_Gemini, reply, null);

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

                await ReportErrorAsync(LocalizedStrings.Error_VoiceCancelled);

            }

            catch (Exception ex)

            {

                await SafeReportErrorAsync(LocalizedStrings.Format("Error_MicPrefix", WidgetExceptionFormatter.Format(ex)));

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

                ShowStatus(LocalizedStrings.Status_NoLog);

            }

            else

            {

                ShowStatus(LocalizedStrings.Format("Status_LogPrefix", log));

            }

        }



        private void ClearChat_Click(object sender, RoutedEventArgs e)

        {

            CancelOperation();

            _sendInProgress = false;

            ChatSessionStore.Reset();

            ChatSessionStore.Current.Messages.CollectionChanged += OnMessagesCollectionChanged;

            MessagesList.ItemsSource = ChatSessionStore.Current.Messages;

            _chatService.ClearHistory();
            _lastSteamGameQuery = null;

            UpdateViewMode();

            InputBox.Text = string.Empty;

            UpdateSendButtonVisibility();

            StatusText.Visibility = Visibility.Collapsed;

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
            await CoreUiDispatcher.RunOnUiAsync(async () =>
            {
                var thumbnail = await ScreenshotThumbnailHelper.CreateFromJpegAsync(jpegBytes)
                    .ConfigureAwait(true);
                if (thumbnail == null)
                {
                    return;
                }

                message.Thumbnail = thumbnail;
                message.ThumbnailVisibility = Visibility.Visible;
            }).ConfigureAwait(true);
        }

        private static bool IsUserRole(string roleLabel)

        {

            if (string.IsNullOrEmpty(roleLabel))
            {
                return false;
            }

            return LocalizedStrings.IsUserRole(roleLabel);

        }



        private static string GetUserInitials()

        {

            return LocalizedStrings.Chat_UserInitials;

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

            InputBox.PlaceholderText = hasChat
                ? LocalizedStrings.Chat_InputPlaceholderActive
                : LocalizedStrings.Chat_InputPlaceholder;

        }



        private void ScrollMessagesToEnd()

        {

            if (!CoreUiDispatcher.IsOnUiThread)

            {

                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, ScrollMessagesToEnd);

                return;

            }

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



        private async System.Threading.Tasks.Task SendSteamAchievementsAsync()
        {
            if (!AppSettingsService.HasApiKey())
            {
                await ReportErrorAsync(LocalizedStrings.Error_MissingApiKey);
                return;
            }

            if (!AppSettingsService.HasSteamCredentials())
            {
                await ReportErrorAsync(LocalizedStrings.Steam_MissingCredentials);
                return;
            }

            if (_sendInProgress)
            {
                await RunOnUiAsync(() => ShowStatus(LocalizedStrings.Status_WaitSend));
                return;
            }

            await RunOnUiAsync(() => ShowStatus(LocalizedStrings.Format("Status_QueryingSteamAchievements")));

            var gameName = SteamQueryHelper.SanitizeGameDisplayName(_gameContext?.Current?.DisplayName);
            SteamAchievementsReport report;
            try
            {
                report = await _steamService.GetAchievementsReportAsync(gameName).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Steam logros UI: " + WidgetExceptionFormatter.Format(ex));
                await SafeReportErrorAsync(LocalizedStrings.Format("Error_SteamPrefix", WidgetExceptionFormatter.Format(ex)));
                return;
            }

            if (report.NeedsClarification)
            {
                await ReportErrorAsync(report.ClarificationMessage);
                return;
            }

            if (report.HasError)
            {
                await ReportErrorAsync(report.ErrorMessage);
                return;
            }

            var message = report.FormatForGemini() + "\n\n" + LocalizedStrings.Prompt_SteamAchievementsGemini;

            var displayText = LocalizedStrings.Format(
                "Prompt_SteamAchievementsDisplay",
                report.GameName,
                report.UnlockedCount,
                report.TotalCount);

            await SendUserMessageAsync(displayText, message);
        }

        private async System.Threading.Tasks.Task SendSteamProfileAsync()
        {
            if (!AppSettingsService.HasApiKey())
            {
                await ReportErrorAsync(LocalizedStrings.Error_MissingApiKey);
                return;
            }

            if (!AppSettingsService.HasSteamCredentials())
            {
                await ReportErrorAsync(LocalizedStrings.Steam_MissingCredentials);
                return;
            }

            if (_sendInProgress)
            {
                await RunOnUiAsync(() => ShowStatus(LocalizedStrings.Status_WaitSend));
                return;
            }

            await RunOnUiAsync(() => ShowStatus(LocalizedStrings.Format("Status_QueryingSteamProfile")));

            SteamProfileReport report;
            try
            {
                report = await _steamService.GetProfileReportAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Steam perfil UI: " + WidgetExceptionFormatter.Format(ex));
                await SafeReportErrorAsync(LocalizedStrings.Format("Error_SteamPrefix", WidgetExceptionFormatter.Format(ex)));
                return;
            }

            if (report.HasError)
            {
                await ReportErrorAsync(report.ErrorMessage);
                return;
            }

            var message = report.FormatForGemini() + "\n\n" + LocalizedStrings.Prompt_SteamProfileGemini;

            var displayText = LocalizedStrings.Format(
                "Prompt_SteamProfileDisplay",
                report.PersonaName,
                report.OwnedGamesCount);

            await SendUserMessageAsync(displayText, message);
        }

        private void InsertFabPrompt(string text)

        {

            InputBox.Text = text;

            InputBox.SelectionStart = text.Length;

            UpdateSendButtonVisibility();

            InputBox.Focus(FocusState.Programmatic);

        }



        private async void FabAchievements_Click(object sender, RoutedEventArgs e) => await SendSteamAchievementsAsync();

        private void FabTips_Click(object sender, RoutedEventArgs e) => InsertFabPrompt(LocalizedStrings.Prompt_FabTips);

        private async void FabStats_Click(object sender, RoutedEventArgs e) => await SendSteamProfileAsync();



        private async void SuggestionGames_Click(object sender, RoutedEventArgs e)

        {

            await SendUserMessageAsync(LocalizedStrings.Prompt_GameRecommendations);

        }



        private async void SuggestionAchievements_Click(object sender, RoutedEventArgs e)
        {
            await SendSteamAchievementsAsync();
        }



        private async void SuggestionCapture_Click(object sender, RoutedEventArgs e)

        {

            Capture_Click(sender, e);

        }

    }

}


