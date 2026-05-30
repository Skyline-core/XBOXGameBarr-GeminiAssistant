using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using GeminiAssistant.Models;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Resolves HWND(s) to capture from Game Bar app-target info and Win32 enumeration.
    /// </summary>
    internal static class GameTargetWindowResolver
    {
        private const uint GaRoot = 2;
        private const uint MonitorDefaultToNearest = 2;
        private const int MinTitleMatchScore = 40;
        private const int MinCaptureWidth = 160;
        private const int MinCaptureHeight = 90;

        private const uint ProcessQueryLimitedInformation = 0x1000;

        private static readonly uint CurrentProcessId = GetCurrentProcessId();

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
            public long Area => (long)Width * Height;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(
            IntPtr hProcess,
            int dwFlags,
            StringBuilder lpExeName,
            ref int lpdwSize);

        public static IReadOnlyList<IntPtr> ResolveCaptureWindowCandidates(GameContextInfo context, IntPtr excludeHwnd)
        {
            var ordered = new List<IntPtr>();
            var seen = new HashSet<IntPtr>();

            void Add(IntPtr hwnd)
            {
                if (hwnd != IntPtr.Zero && seen.Add(hwnd))
                {
                    ordered.Add(hwnd);
                }
            }

            if (context != null)
            {
                foreach (var hwnd in FindWindowsByAppTarget(context, excludeHwnd))
                {
                    Add(hwnd);
                }

                Add(FindWindowByDisplayName(context.DisplayName, excludeHwnd));
            }

            Add(GetForegroundWindowExcluding(excludeHwnd));
            Add(FindLargestTitledWindow(context?.DisplayName, excludeHwnd));
            Add(FindLargestCapturableWindow(excludeHwnd));

            return ordered;
        }

        public static IntPtr ResolveCaptureWindow(GameContextInfo context, IntPtr excludeHwnd)
        {
            var candidates = ResolveCaptureWindowCandidates(context, excludeHwnd);
            return candidates.Count > 0 ? candidates[0] : IntPtr.Zero;
        }

        public static IntPtr ResolveMonitorForWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        }

        public static IReadOnlyList<string> GetDisplayNameSearchTerms(string displayName)
        {
            var terms = new List<string>();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return terms;
            }

            var cleaned = displayName
                .Replace("(aplicacion)", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("(aplicaci�n)", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            void AddTerm(string term)
            {
                term = term?.Trim();
                if (string.IsNullOrEmpty(term) || term.Length < 3)
                {
                    return;
                }

                if (!terms.Exists(t => t.Equals(term, StringComparison.OrdinalIgnoreCase)))
                {
                    terms.Add(term);
                }
            }

            AddTerm(cleaned);
            AddTerm(displayName.Trim());

            var parts = cleaned.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                AddTerm(parts[i]);
            }

            return terms;
        }

        private static List<IntPtr> FindWindowsByAppTarget(GameContextInfo context, IntPtr excludeHwnd)
        {
            var terms = GetDisplayNameSearchTerms(context.DisplayName);
            var aumId = context.AumId?.Trim();
            var exeHint = ExtractProcessHint(aumId);
            var matches = new List<(IntPtr Hwnd, int Score, long Area)>();

            EnumWindows(
                (hWnd, lParam) =>
                {
                    if (!IsCapturableWindow(hWnd, excludeHwnd))
                    {
                        return true;
                    }

                    var title = GetWindowTitle(hWnd);
                    var titleScore = string.IsNullOrWhiteSpace(title) ? 0 : ScoreAgainstTerms(title, terms);

                    GetWindowThreadProcessId(hWnd, out var pid);
                    var exeScore = 0;
                    if (ProcessExecutableMatches(pid, aumId, exeHint))
                    {
                        exeScore = 95;
                    }

                    var score = Math.Max(titleScore, exeScore);
                    if (score <= 0)
                    {
                        return true;
                    }

                    GetClientRect(hWnd, out var rect);
                    matches.Add((hWnd, score, rect.Area));
                    return true;
                },
                IntPtr.Zero);

            matches.Sort((a, b) =>
            {
                var byScore = b.Score.CompareTo(a.Score);
                return byScore != 0 ? byScore : b.Area.CompareTo(a.Area);
            });

            var result = new List<IntPtr>();
            foreach (var match in matches)
            {
                result.Add(match.Hwnd);
            }

            return result;
        }

        private static bool ProcessExecutableMatches(uint processId, string aumId, string exeHint)
        {
            var imagePath = TryGetProcessImagePath(processId);
            if (string.IsNullOrEmpty(imagePath))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(aumId))
            {
                if (imagePath.Equals(aumId, StringComparison.OrdinalIgnoreCase) ||
                    imagePath.EndsWith(aumId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(exeHint))
            {
                var fileName = Path.GetFileNameWithoutExtension(imagePath);
                if (fileName.Equals(exeHint, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string TryGetProcessImagePath(uint processId)
        {
            var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var buffer = new StringBuilder(1024);
                var size = buffer.Capacity;
                if (QueryFullProcessImageName(handle, 0, buffer, ref size) && size > 0)
                {
                    return buffer.ToString(0, size);
                }
            }
            catch
            {
            }
            finally
            {
                CloseHandle(handle);
            }

            return null;
        }

        private static IntPtr FindWindowByDisplayName(string displayName, IntPtr excludeHwnd)
        {
            var terms = GetDisplayNameSearchTerms(displayName);
            if (terms.Count == 0)
            {
                return IntPtr.Zero;
            }

            var state = new SearchState
            {
                Terms = terms,
                ExcludeHwnd = excludeHwnd
            };

            var gcHandle = GCHandle.Alloc(state);
            try
            {
                EnumWindows(TitleSearchCallback, GCHandle.ToIntPtr(gcHandle));
                if (state.BestScore >= MinTitleMatchScore)
                {
                    return state.BestHwnd;
                }

                return state.BestScore > 0 ? state.BestHwnd : IntPtr.Zero;
            }
            finally
            {
                gcHandle.Free();
            }
        }

        private static IntPtr FindLargestTitledWindow(string displayName, IntPtr excludeHwnd)
        {
            var terms = GetDisplayNameSearchTerms(displayName);
            var state = new LargestWindowState
            {
                ExcludeHwnd = excludeHwnd,
                RequireTitle = true,
                TitleTerms = terms
            };

            var gcHandle = GCHandle.Alloc(state);
            try
            {
                EnumWindows(LargestWindowCallback, GCHandle.ToIntPtr(gcHandle));
                return state.BestHwnd;
            }
            finally
            {
                gcHandle.Free();
            }
        }

        private static IntPtr FindLargestCapturableWindow(IntPtr excludeHwnd)
        {
            var state = new LargestWindowState { ExcludeHwnd = excludeHwnd };
            var gcHandle = GCHandle.Alloc(state);
            try
            {
                EnumWindows(LargestWindowCallback, GCHandle.ToIntPtr(gcHandle));
                return state.BestHwnd;
            }
            finally
            {
                gcHandle.Free();
            }
        }

        public static IntPtr GetForegroundWindowExcluding(IntPtr excludeHwnd)
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            foreground = GetAncestor(foreground, GaRoot);
            return IsCapturableWindow(foreground, excludeHwnd) ? foreground : IntPtr.Zero;
        }

        private static bool LargestWindowCallback(IntPtr hWnd, IntPtr lParam)
        {
            var state = (LargestWindowState)GCHandle.FromIntPtr(lParam).Target;
            if (!IsCapturableWindow(hWnd, state.ExcludeHwnd))
            {
                return true;
            }

            var title = GetWindowTitle(hWnd);
            if (state.RequireTitle && string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            if (state.TitleTerms != null && state.TitleTerms.Count > 0 && !string.IsNullOrWhiteSpace(title))
            {
                var matchesHint = false;
                foreach (var term in state.TitleTerms)
                {
                    if (title.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchesHint = true;
                        break;
                    }
                }

                if (!matchesHint)
                {
                    return true;
                }
            }

            GetClientRect(hWnd, out var rect);
            var area = rect.Area;
            if (area > state.BestArea)
            {
                state.BestArea = area;
                state.BestHwnd = hWnd;
            }

            return true;
        }

        private static bool TitleSearchCallback(IntPtr hWnd, IntPtr lParam)
        {
            var state = (SearchState)GCHandle.FromIntPtr(lParam).Target;
            if (!IsCapturableWindow(hWnd, state.ExcludeHwnd))
            {
                return true;
            }

            var title = GetWindowTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            var score = ScoreAgainstTerms(title, state.Terms);
            if (score > state.BestScore)
            {
                state.BestScore = score;
                state.BestHwnd = hWnd;
            }

            return true;
        }

        private static int ScoreAgainstTerms(string windowTitle, IReadOnlyList<string> terms)
        {
            var best = 0;
            foreach (var term in terms)
            {
                var score = ScoreTitleMatch(windowTitle, term);
                if (score > best)
                {
                    best = score;
                }
            }

            return best;
        }

        private static string ExtractProcessHint(string aumId)
        {
            if (string.IsNullOrWhiteSpace(aumId))
            {
                return null;
            }

            aumId = aumId.Trim();
            if (aumId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return System.IO.Path.GetFileNameWithoutExtension(aumId);
            }

            var bang = aumId.IndexOf('!');
            if (bang > 0)
            {
                return aumId.Substring(0, bang);
            }

            return null;
        }

        private static bool IsCapturableWindow(IntPtr hWnd, IntPtr excludeHwnd)
        {
            if (hWnd == IntPtr.Zero || hWnd == excludeHwnd || !IsWindowVisible(hWnd) || IsIconic(hWnd))
            {
                return false;
            }

            var shell = GetShellWindow();
            if (shell != IntPtr.Zero && hWnd == shell)
            {
                return false;
            }

            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == CurrentProcessId || pid == 0)
            {
                return false;
            }

            if (IsGameBarOrWidgetTitle(GetWindowTitle(hWnd)))
            {
                return false;
            }

            GetClientRect(hWnd, out var rect);
            return rect.Width >= MinCaptureWidth && rect.Height >= MinCaptureHeight;
        }

        private static bool IsGameBarOrWidgetTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            return title.IndexOf("Xbox Game Bar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   title.IndexOf("Game Bar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   title.IndexOf("Gemini Assistant", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ScoreTitleMatch(string windowTitle, string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return 0;
            }

            if (windowTitle.Equals(term, StringComparison.OrdinalIgnoreCase))
            {
                return 100;
            }

            if (windowTitle.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 80;
            }

            if (term.IndexOf(windowTitle, StringComparison.OrdinalIgnoreCase) >= 0 &&
                windowTitle.Length > 3)
            {
                return 60;
            }

            return 0;
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            var sb = new StringBuilder(512);
            return GetWindowText(hWnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
        }

        private sealed class SearchState
        {
            public IReadOnlyList<string> Terms;
            public IntPtr ExcludeHwnd;
            public IntPtr BestHwnd;
            public int BestScore;
        }

        private sealed class LargestWindowState
        {
            public IntPtr ExcludeHwnd;
            public IntPtr BestHwnd;
            public long BestArea;
            public bool RequireTitle;
            public IReadOnlyList<string> TitleTerms;
        }

    }
}
