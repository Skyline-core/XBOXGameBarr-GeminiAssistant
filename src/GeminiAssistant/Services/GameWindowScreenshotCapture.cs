using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeminiAssistant.Models;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Screenshot directo de la ventana/monitor del juego (WGC + PrintWindow + escritorio).
    /// </summary>
    internal static class GameWindowScreenshotCapture
    {
        public static async Task<byte[]> TryCaptureAsync(GameContextInfo gameContext)
        {
            AppCapabilityAccessStatus access = AppCapabilityAccessStatus.NotDeclaredByApp;
            if (ProgrammaticCaptureHelper.IsProgrammaticCaptureDeclared())
            {
                access = await ProgrammaticCaptureHelper.EnsureCaptureAccessAsync().ConfigureAwait(false);
            }

            WindowHandleInterop.TryGetWindowHandle(out var excludeHwnd);
            var candidates = BuildCandidateList(gameContext, excludeHwnd);

            var tryCount = Math.Min(candidates.Count, 6);
            WidgetFileLog.Write("Captura ventana candidatos=" + candidates.Count + " probar=" + tryCount);

            for (var i = 0; i < tryCount; i++)
            {
                var hwnd = candidates[i];
                var isPrimary = i == 0;

                var win32 = await Win32WindowCapture.TryCaptureJpegAsync(hwnd, rejectMostlyBlack: true)
                    .ConfigureAwait(false);
                if (IsValid(win32))
                {
                    WidgetFileLog.Write("Captura ventana PrintWindow OK " + hwnd.ToInt64());
                    return win32;
                }

                var wgc = await GraphicsCaptureJpegService.TryCaptureWindowUncheckedAsync(hwnd)
                    .ConfigureAwait(false);
                if (IsValid(wgc))
                {
                    WidgetFileLog.Write("Captura ventana WGC OK " + hwnd.ToInt64());
                    return wgc;
                }

                if (access == AppCapabilityAccessStatus.Allowed)
                {
                    wgc = await GraphicsCaptureJpegService.TryCaptureWindowAsync(hwnd).ConfigureAwait(false);
                    if (IsValid(wgc))
                    {
                        WidgetFileLog.Write("Captura ventana WGC (permiso) OK " + hwnd.ToInt64());
                        return wgc;
                    }
                }

                if (isPrimary)
                {
                    var monitor = GameTargetWindowResolver.ResolveMonitorForWindow(hwnd);
                    var monitorJpeg = await TryCaptureMonitorAsync(monitor, access).ConfigureAwait(false);
                    if (IsValid(monitorJpeg))
                    {
                        WidgetFileLog.Write("Captura monitor del juego OK");
                        return monitorJpeg;
                    }
                }
            }

            var largest = candidates.Count > 0 ? candidates[0] : IntPtr.Zero;
            if (largest == IntPtr.Zero)
            {
                largest = GameWindowResolver.TryResolveLargestGameplayWindow();
            }

            if (largest != IntPtr.Zero)
            {
                var monitor = GameTargetWindowResolver.ResolveMonitorForWindow(largest);
                var monitorJpeg = await TryCaptureMonitorAsync(monitor, access).ConfigureAwait(false);
                if (IsValid(monitorJpeg))
                {
                    return monitorJpeg;
                }
            }

            foreach (var hwnd in candidates)
            {
                var relaxed = await Win32WindowCapture.TryCaptureJpegAsync(hwnd, rejectMostlyBlack: false)
                    .ConfigureAwait(false);
                if (IsValid(relaxed))
                {
                    WidgetFileLog.Write("Captura ventana PrintWindow (relajada) OK " + hwnd.ToInt64());
                    return relaxed;
                }
            }

            var desktop = await Win32WindowCapture.TryCaptureDesktopJpegAsync().ConfigureAwait(false);
            if (IsValid(desktop))
            {
                WidgetFileLog.Write("Captura escritorio OK");
                return desktop;
            }

            WidgetFileLog.Write("Captura ventana: ningun metodo devolvio imagen");
            return null;
        }

        private static List<IntPtr> BuildCandidateList(GameContextInfo gameContext, IntPtr excludeHwnd)
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

            foreach (var hwnd in GameTargetWindowResolver.ResolveCaptureWindowCandidates(gameContext, excludeHwnd))
            {
                Add(hwnd);
            }

            foreach (var hwnd in GameTargetWindowResolver.ResolveBorderlessLargeWindows(excludeHwnd))
            {
                Add(hwnd);
            }

            var tracked = GameWindowResolver.TryResolveHwnd(gameContext);
            Add(tracked);

            var largest = GameWindowResolver.TryResolveLargestGameplayWindow();
            Add(largest);

            var foreground = GameTargetWindowResolver.GetForegroundWindowExcluding(excludeHwnd);
            Add(foreground);

            return ordered;
        }

        private static async Task<byte[]> TryCaptureMonitorAsync(
            IntPtr monitor,
            AppCapabilityAccessStatus access)
        {
            if (monitor == IntPtr.Zero)
            {
                return null;
            }

            var jpeg = await GraphicsCaptureJpegService.TryCaptureMonitorUncheckedAsync(monitor)
                .ConfigureAwait(false);
            if (IsValid(jpeg))
            {
                return jpeg;
            }

            if (access == AppCapabilityAccessStatus.Allowed)
            {
                return await GraphicsCaptureJpegService.TryCaptureMonitorAsync(monitor).ConfigureAwait(false);
            }

            return null;
        }

        private static bool IsValid(byte[] jpegBytes)
        {
            return jpegBytes != null && jpegBytes.Length >= 100;
        }
    }
}
