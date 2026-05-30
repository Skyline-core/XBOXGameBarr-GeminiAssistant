using System;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using GeminiAssistant.Widgets;

namespace GeminiAssistant
{
    sealed partial class App : Application
    {
        private XboxGameBarWidget _chatWidget;
        private XboxGameBarWidget _settingsWidget;

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
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

            if (!widgetArgs.IsLaunchActivation)
            {
                return;
            }

            var rootFrame = new Frame();
            var flowDirectionSetting = Windows.ApplicationModel.Resources.Core.ResourceContext
                .GetForCurrentView().QualifierValues["LayoutDirection"];
            rootFrame.FlowDirection = flowDirectionSetting == "RTL"
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            rootFrame.NavigationFailed += OnNavigationFailed;
            Window.Current.Content = rootFrame;

            if (widgetArgs.AppExtensionId == "ChatWidget")
            {
                _chatWidget = new XboxGameBarWidget(
                    widgetArgs,
                    Window.Current.CoreWindow,
                    rootFrame);
                rootFrame.Navigate(typeof(ChatWidget), _chatWidget);
                Window.Current.Closed += ChatWidgetWindow_Closed;
            }
            else if (widgetArgs.AppExtensionId == "SettingsWidget")
            {
                _settingsWidget = new XboxGameBarWidget(
                    widgetArgs,
                    Window.Current.CoreWindow,
                    rootFrame);
                rootFrame.Navigate(typeof(SettingsWidget), _settingsWidget);
                Window.Current.Closed += SettingsWidgetWindow_Closed;
            }
            else
            {
                return;
            }

            Window.Current.Activate();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            var rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated)
            {
                return;
            }

            if (rootFrame.Content == null)
            {
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }

            Window.Current.Activate();
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

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            _chatWidget = null;
            _settingsWidget = null;
            deferral.Complete();
        }
    }
}
