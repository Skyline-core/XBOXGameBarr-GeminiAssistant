using System;
using GeminiAssistant.Widgets;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace GeminiAssistant.Services
{
    internal static class WidgetStartupUi
    {
        public static void EnsureFrame(out Frame frame)
        {
            if (Window.Current.Content is Frame existing)
            {
                frame = existing;
                return;
            }

            frame = new Frame { Background = new SolidColorBrush(Color.FromArgb(255, 27, 27, 31)) };
            Window.Current.Content = frame;
        }

        public static void ShowLaunchLanding()
        {
            EnsureFrame(out var frame);
            if (frame.Content is LaunchLandingPage)
            {
                Window.Current.Activate();
                return;
            }

            frame.Navigate(typeof(LaunchLandingPage));
            Window.Current.Activate();
        }

        public static void ShowError(string title, string details)
        {
            WidgetFileLog.Write("Startup error: " + title + " | " + details);

            try
            {
                EnsureFrame(out var frame);
                frame.Content = BuildErrorPanel(title, details);
                Window.Current.Activate();
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("ShowError failed: " + ex.Message);
            }
        }

        public static void HandleNavigationFailed(Type sourcePageType, Exception exception)
        {
            var pageName = sourcePageType?.FullName ?? "unknown page";
            var message = exception?.Message ?? "Navigation failed";
            ShowError(
                "Could not open Gemini Assistant",
                pageName + Environment.NewLine + message);
        }

        private static Grid BuildErrorPanel(string title, string details)
        {
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 225, 229)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var detailsBlock = new TextBlock
            {
                Text = details,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 147, 143, 153)),
                TextWrapping = TextWrapping.Wrap
            };

            var hintBlock = new TextBlock
            {
                Text = "Open Xbox Game Bar (Win+G) and try again. If the problem persists, reinstall the app.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 168, 199, 250)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 16, 0, 0)
            };

            return new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 27, 27, 31)),
                Padding = new Thickness(20),
                Children =
                {
                    new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Children = { titleBlock, detailsBlock, hintBlock }
                    }
                }
            };
        }
    }
}
