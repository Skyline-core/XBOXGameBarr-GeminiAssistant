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
            try
            {
                AppLanguageService.ApplySavedOrSystemLanguage();
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("App lang init: " + ex.Message);
            }

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
                WidgetStartupUi.ShowError("Gemini Assistant", ex.Message);
            }
        }

        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            WidgetFileLog.Write("XamlException: " + WidgetExceptionFormatter.Format(e.Exception ?? new Exception(e.Message)));

            try
            {
                WidgetStartupUi.ShowError(
                    "Gemini Assistant",
                    e.Exception?.Message ?? e.Message);
                e.Handled = true;
            }
            catch
            {
                e.Handled = false;
            }
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            try
            {
                HandleActivated(args);
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("OnActivated: " + WidgetExceptionFormatter.Format(ex));
                WidgetStartupUi.ShowError("Could not open widget", ex.Message);
            }
        }

        private void HandleActivated(IActivatedEventArgs args)
        {
            if (args.Kind != ActivationKind.Protocol)
            {
                return;
            }

            var protocolArgs = args as IProtocolActivatedEventArgs;
            if (protocolArgs?.Uri == null ||
                !string.Equals(protocolArgs.Uri.Scheme, "ms-gamebarwidget", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
            if (widgetArgs == null)
            {
                WidgetFileLog.Write("OnActivated: ms-gamebarwidget without XboxGameBarWidgetActivatedEventArgs");
                WidgetStartupUi.ShowError(
                    "Could not open widget",
                    "Game Bar activation failed. Reinstall the app or restart Xbox Game Bar.");
                return;
            }

            WidgetFileLog.Write("OnActivated launch=" + widgetArgs.IsLaunchActivation + " ext=" + widgetArgs.AppExtensionId);

            try
            {
                AppLanguageService.ApplySavedOrSystemLanguage();
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("OnActivated lang: " + ex.Message);
            }

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

        private void ActivateWidgetLaunch(XboxGameBarWidgetActivatedEventArgs widgetArgs)
        {
            var app = Current as App;
            if (app == null)
            {
                WidgetStartupUi.ShowError("Could not open widget", "Application context is unavailable.");
                return;
            }

            var rootFrame = new Frame
            {
                Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 27, 27, 31))
            };
            var flowDirectionSetting = Windows.ApplicationModel.Resources.Core.ResourceContext
                .GetForCurrentView().QualifierValues["LayoutDirection"];
            rootFrame.FlowDirection = flowDirectionSetting == "RTL"
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            rootFrame.NavigationFailed += OnNavigationFailed;
            Window.Current.Content = rootFrame;

            try
            {
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
                    WidgetFileLog.Write("OnActivated unknown extension=" + widgetArgs.AppExtensionId);
                    WidgetStartupUi.ShowError(
                        "Unknown widget",
                        "Unsupported widget id: " + widgetArgs.AppExtensionId);
                    return;
                }

                Window.Current.Activate();
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("ActivateWidgetLaunch: " + WidgetExceptionFormatter.Format(ex));
                WidgetStartupUi.ShowError("Could not open widget", ex.Message);
            }
        }

        private void ActivateWidgetRepeat(XboxGameBarWidgetActivatedEventArgs widgetArgs)
        {
            var rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                ActivateWidgetLaunch(widgetArgs);
                return;
            }

            if (widgetArgs.AppExtensionId == "ChatWidget" && _chatWidget != null)
            {
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
            if (e.PrelaunchActivated)
            {
                return;
            }

            WidgetFileLog.Write("OnLaunched");
            WidgetStartupUi.ShowLaunchLanding();
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
            WidgetFileLog.Write(
                "NavigationFailed: " + e.SourcePageType?.FullName + " | " +
                WidgetExceptionFormatter.Format(e.Exception ?? new Exception("Navigation failed")));
            e.Handled = true;
            WidgetStartupUi.HandleNavigationFailed(e.SourcePageType, e.Exception);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
