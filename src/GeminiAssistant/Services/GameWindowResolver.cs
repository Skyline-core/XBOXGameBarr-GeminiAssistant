using System;
using System.Runtime.InteropServices;
using System.Text;
using GeminiAssistant.Models;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Localiza la ventana del juego/app que Game Bar esta siguiendo (sin atajos ni captura grafica).
    /// </summary>
    internal static class GameWindowResolver
    {
        private const int GwlExstyle = -20;
        private const int WsExToolwindow = 0x00000080;
        private const int WsExNoactivate = 0x08000000;

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static IntPtr TryResolveHwnd(GameContextInfo context)
        {
            if (context == null)
            {
                return IntPtr.Zero;
            }

            var displayName = context.DisplayName?.Trim();
            if (string.IsNullOrEmpty(displayName) || displayName.Equals("Desconocido", StringComparison.OrdinalIgnoreCase))
            {
                return context.TrackingEnabled ? TryResolveLargestGameplayWindow() : IntPtr.Zero;
            }

            IntPtr best = IntPtr.Zero;
            var bestArea = 0L;

            EnumWindows((hWnd, _) =>
            {
                if (!IsCandidateGameWindow(hWnd))
                {
                    return true;
                }

                var title = GetWindowTitle(hWnd);
                if (string.IsNullOrEmpty(title) || !TitleMatchesTarget(title, displayName))
                {
                    return true;
                }

                if (!GetWindowRect(hWnd, out var rect))
                {
                    return true;
                }

                var area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = hWnd;
                }

                return true;
            }, IntPtr.Zero);

            if (best != IntPtr.Zero)
            {
                return best;
            }

            return TryResolveLargestGameplayWindow();
        }

        /// <summary>
        /// Si el titulo no coincide, elige la ventana visible mas grande (suele ser el juego detras de Game Bar).
        /// </summary>
        public static IntPtr TryResolveLargestGameplayWindow()
        {
            IntPtr best = IntPtr.Zero;
            var bestArea = 0L;

            EnumWindows((hWnd, _) =>
            {
                if (!IsCandidateGameWindow(hWnd))
                {
                    return true;
                }

                if (!GetWindowRect(hWnd, out var rect))
                {
                    return true;
                }

                var w = rect.Right - rect.Left;
                var h = rect.Bottom - rect.Top;
                if (w < 320 || h < 240)
                {
                    return true;
                }

                var area = (long)w * h;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = hWnd;
                }

                return true;
            }, IntPtr.Zero);

            return best;
        }

        private static bool IsCandidateGameWindow(IntPtr hWnd)
        {
            if (!IsWindowVisible(hWnd) || IsIconic(hWnd))
            {
                return false;
            }

            var exStyle = GetWindowLong(hWnd, GwlExstyle);
            if ((exStyle & WsExToolwindow) != 0 || (exStyle & WsExNoactivate) != 0)
            {
                return false;
            }

            var title = GetWindowTitle(hWnd);
            if (!string.IsNullOrEmpty(title) &&
                (title.IndexOf("Xbox Game Bar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 title.IndexOf("Game Bar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 title.IndexOf("Gemini Assistant", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return false;
            }

            if (!GetWindowRect(hWnd, out var rect))
            {
                return false;
            }

            var w = rect.Right - rect.Left;
            var h = rect.Bottom - rect.Top;
            return w >= 320 && h >= 240;
        }

        private static bool TitleMatchesTarget(string windowTitle, string displayName)
        {
            if (windowTitle.Equals(displayName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (windowTitle.StartsWith(displayName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (displayName.Length >= 4 &&
                windowTitle.IndexOf(displayName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            var length = GetWindowTextLength(hWnd);
            if (length <= 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(length + 2);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }
}
