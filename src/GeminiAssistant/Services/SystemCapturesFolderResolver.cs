using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Win32;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Carpeta Captures que Windows asigna a Game Bar (shell conocido + registro del usuario).
    /// </summary>
    internal static class SystemCapturesFolderResolver
    {
        private static readonly Guid CapturesKnownFolderId = new Guid("EDC0FE71-98D8-4F4A-B920-C8DC133CB165");

        private const string ShellFoldersKey =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders";

        private const string ShellFoldersValueName = "{EDC0FE71-98D8-4F4A-B920-C8DC133CB165}";

        public static async Task<StorageFolder> TryGetCapturesFolderAsync()
        {
            var path = TryGetCapturesPathFromRegistry();
            if (string.IsNullOrWhiteSpace(path))
            {
                path = TryGetCapturesPathFromShell();
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                var fromPath = await TryOpenFolderPathAsync(path, "registro/shell").ConfigureAwait(false);
                if (fromPath != null)
                {
                    return fromPath;
                }
            }

            try
            {
                var videos = KnownFolders.VideosLibrary;
                var captures = await videos.GetFolderAsync("Captures").AsTask().ConfigureAwait(false);
                WidgetFileLog.Write("Captura Carpeta Sistema: " + captures.Path + " (Videos\\Captures)");
                return captures;
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura Carpeta Sistema Videos\\Captures: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }

        private static async Task<StorageFolder> TryOpenFolderPathAsync(string path, string source)
        {
            try
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(path).AsTask().ConfigureAwait(false);
                WidgetFileLog.Write("Captura Carpeta Sistema (" + source + "): " + folder.Path);
                return folder;
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura Carpeta Sistema fallo " + path + ": " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }

        private static string TryGetCapturesPathFromRegistry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(ShellFoldersKey))
                {
                    var raw = key?.GetValue(ShellFoldersValueName) as string;
                    return ExpandPath(raw);
                }
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura registro: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
        }

        private static string TryGetCapturesPathFromShell()
        {
            IntPtr pszPath = IntPtr.Zero;
            try
            {
                var hr = SHGetKnownFolderPath(CapturesKnownFolderId, 0, IntPtr.Zero, out pszPath);
                if (hr != 0 || pszPath == IntPtr.Zero)
                {
                    return null;
                }

                return Marshal.PtrToStringUni(pszPath);
            }
            catch (Exception ex)
            {
                WidgetFileLog.Write("Captura SHGetKnownFolderPath: " + WidgetExceptionFormatter.Format(ex));
                return null;
            }
            finally
            {
                if (pszPath != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pszPath);
                }
            }
        }

        private static string ExpandPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return Environment.ExpandEnvironmentVariables(path.Trim());
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
            uint dwFlags,
            IntPtr hToken,
            out IntPtr ppszPath);
    }
}
