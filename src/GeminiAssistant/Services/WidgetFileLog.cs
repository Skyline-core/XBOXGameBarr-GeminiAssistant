using System;
using System.IO;
using System.Linq;
using System.Text;
using Windows.Storage;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Log en disco del paquete (sobrevive si el proceso muere sin excepcion).
    /// </summary>
    internal static class WidgetFileLog
    {
        private const string FileName = "widget-diagnostic.log";
        private const int MaxFileBytes = 64_000;

        public static void Write(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            try
            {
                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine;
                var path = GetLogPath();
                File.AppendAllText(path, line, Encoding.UTF8);
                TrimIfNeeded(path);

                var shortMsg = message.Length > 400 ? message.Substring(0, 400) : message;
                ApplicationData.Current.LocalSettings.Values["WidgetLastError"] = shortMsg;
            }
            catch
            {
            }
        }

        public static string GetRecentSummary()
        {
            try
            {
                var path = GetLogPath();
                if (!File.Exists(path))
                {
                    return null;
                }

                var lines = File.ReadAllLines(path);
                if (lines.Length == 0)
                {
                    return null;
                }

                var tail = lines.Skip(Math.Max(0, lines.Length - 4)).ToArray();
                return string.Join(" | ", tail);
            }
            catch
            {
                return null;
            }
        }

        private static string GetLogPath()
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName);
        }

        private static void TrimIfNeeded(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Length <= MaxFileBytes)
                {
                    return;
                }

                var lines = File.ReadAllLines(path);
                var keep = lines.Skip(Math.Max(0, lines.Length - 200)).ToArray();
                File.WriteAllLines(path, keep, Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
