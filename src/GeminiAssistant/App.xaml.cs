using System;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using GeminiAssistant.Services;
using GeminiAssistant.Widgets;

namespace GeminiAssistant
{
    sealed partial class App : Application
    {
        private XboxGameBarWidget? _chatWidget;
        private XboxGameBarWidget? _settingsWidget;

        public App()
        {
            AppLanguageService.ApplySavedOrSystemLanguage();
            InitializeComponent();
            Suspending += OnSuspending;
            UnhandledException += OnUnhandledException;
            CoreApplication.UnhandledErrorDetected += OnUnhandledErrorDetected;
        }

        private void OnUnhandledErrorDetected(object? sender, UnhandledErrorDetectedEventArgs e)
        {
            try
            {
                e.UnhandledError.Propagate();
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("UnhandledError: " + ex.Message);
            }
        }

        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            WidgetFileLog.Write("XamlException: " + WidgetExceptionFormatter.Format(e.Exception ?? new Exception(e.Message)));
            e.Handled = true;
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            if (args.Kind != ActivationKind.Protocol)
            {
                return;
            }

            var protocolArgs = args as IProtocolActivatedEventArgs;
            if (protocolArgs?.Uri?.Scheme != "ms-gamebarwidget")
            {
                return;
            }

            var widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
            if (widgetArgs == null)
            {
                return;
            }

            WidgetFileLog.Write("OnActivated launch=" + widgetArgs.IsLaunchActivation + " ext=" + widgetArgs.AppExtensionId);
            AppLanguageService.ApplySavedOrSystemLanguage();

            if (widgetArgs.IsLaunchActivation)
            {
                ActivateWidgetLaunch(widgetArgs);
                return;
            }

            ActivateWidgetRepeat(widgetArgs);
            _ = Window.Current.Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                () => RefreshOpenWidgetLocalization(widgetArgs));
        }

        private static void ActivateWidgetLaunch(XboxGameBarWidgetActivatedEventArgs widgetArgs)
        {
            var rootFrame = new Frame();
            var flowDirectionSetting = Windows.ApplicationModel.Resources.Core.ResourceContext
                .GetForCurrentView().QualifierValues["LayoutDirection"];
            rootFrame.FlowDirection = flowDirectionSetting == "RTL"
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            rootFrame.NavigationFailed += OnNavigationFailed;
            Window.Current.Content = rootFrame;

            var app = Current as App;
            if (widgetArgs.AppExtensionId == "ChatWidget")
            {
                app._chatWidget = new XboxGameBarWidget(
                    widgetArgs,
                    Window.Current.CoreWindow,
                    rootFrame);
                rootFrame.Navigate(typeof(ChatWidget), app._chatWidget);
                Window.Current.Closed += app.ChatWidgetWindow_Closed;
            }
            else if (widgetArgs.AppExtensionId == "SettingsWidget")
            {
                app._settingsWidget = new XboxGameBarWidget(
                    widgetArgs,
                    Window.Current.CoreWindow,
                    rootFrame);
                rootFrame.Navigate(typeof(SettingsWidget), app._settingsWidget);
                Window.Current.Closed += app.SettingsWidgetWindow_Closed;
            }
            else
            {
                return;
            }

            Window.Current.Activate();
        }

        private void ActivateWidgetRepeat(XboxGameBarWidgetActivatedEventArgs widgetArgs)
        {
            // Repetir activacion: no crear otro XboxGameBarWidget (patron oficial de Microsoft).
            var rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                return;
            }

            if (widgetArgs.AppExtensionId == "ChatWidget" && _chatWidget != null)
            {
                // No volver a navegar si la pagina ya esta cargada (evita reinicio visual del widget).
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(ChatWidget), _chatWidget);
                }
            }
            else if (widgetArgs.AppExtensionId == "SettingsWidget" && _settingsWidget != null)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(SettingsWidget), _settingsWidget);
                }
            }
        }

        private static void RefreshOpenWidgetLocalization(XboxGameBarWidgetActivatedEventArgs widgetArgs)
        {
            var rootFrame = Window.Current.Content as Frame;
            if (rootFrame?.Content == null)
            {
                return;
            }

            if (widgetArgs.AppExtensionId == "ChatWidget" && rootFrame.Content is ChatWidget chatWidget)
            {
                chatWidget.RefreshLocalization();
            }
            else if (widgetArgs.AppExtensionId == "SettingsWidget" && rootFrame.Content is SettingsWidget settingsWidget)
            {
                settingsWidget.RefreshLocalization();
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            // El widget vive en Game Bar (OnActivated). No abrir MainPage al implantar desde VS.
        }

        private void ChatWidgetWindow_Closed(object sender, Windows.UI.Core.CoreWindowEventArgs e)
        {
            _chatWidget = null;
            Window.Current.Closed -= ChatWidgetWindow_Closed;
        }

        private void SettingsWidgetWindow_Closed(object sender, Windows.UI.Core.CoreWindowEventArgs e)
        {
            _settingsWidget = null;
            Window.Current.Closed -= SettingsWidgetWindow_Closed;
        }

        private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            e.Handled = true;
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            // No anular _chatWidget aqui: Game Bar puede reactivar el mismo proceso (repeat activation).
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
