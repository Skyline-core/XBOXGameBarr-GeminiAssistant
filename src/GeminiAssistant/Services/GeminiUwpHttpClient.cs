using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace GeminiAssistant.Services
{
    internal static class GeminiUwpHttpClient
    {
        private static readonly object ClientLock = new object();
        private static HttpClient _sharedClient;

        private static HttpClient GetClient()
        {
            if (_sharedClient != null)
            {
                return _sharedClient;
            }

            lock (ClientLock)
            {
                if (_sharedClient == null)
                {
                    _sharedClient = new HttpClient();
                }

                return _sharedClient;
            }
        }

        public static async Task<(int StatusCode, string Body)> PostJsonAsync(
            string url,
            string json,
            CancellationToken cancellationToken)
        {
            WidgetFileLog.Write("HTTP POST bytes=" + (json?.Length ?? 0));

            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL de Gemini vacia.");
            }

            if (json == null)
            {
                json = string.Empty;
            }

            using (var content = new HttpStringContent(json, UnicodeEncoding.Utf8, "application/json"))
            {
                using (var response = await GetClient()
                    .PostAsync(new Uri(url), content)
                    .AsTask()
                    .ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var body = await response.Content.ReadAsStringAsync().AsTask().ConfigureAwait(false);
                    WidgetFileLog.Write("HTTP status=" + (int)response.StatusCode + " respBytes=" + (body?.Length ?? 0));
                    return ((int)response.StatusCode, body);
                }
            }
        }
    }
}
