using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Web.Http;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// HTTP dedicado para Steam Web API — peticiones iguales al navegador (key en query, sin cabeceras extra).
    /// </summary>
    internal static class SteamHttpClient
    {
        private const string ApiBase = "https://api.steampowered.com";

        private static readonly object ClientLock = new object();
        private static HttpClient _client;

        public static Task<string> GetAsync(
            string endpointPath,
            IDictionary<string, string> query,
            string apiKey,
            CancellationToken cancellationToken)
        {
            return GetAsync(endpointPath, query, apiKey, requireKey: true, cancellationToken);
        }

        public static Task<string> GetPublicAsync(
            string endpointPath,
            IDictionary<string, string> query,
            CancellationToken cancellationToken)
        {
            return GetAsync(endpointPath, query, apiKey: null, requireKey: false, cancellationToken);
        }

        /// <summary>
        /// Devuelve status y cuerpo sin lanzar por HTTP 4xx/5xx (p. ej. logros con perfil restringido).
        /// </summary>
        public static async Task<(int StatusCode, string Body)> GetRawAsync(
            string endpointPath,
            IDictionary<string, string> query,
            string apiKey,
            bool requireKey,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(endpointPath))
            {
                throw new ArgumentException("Ruta de Steam vacia.");
            }

            if (requireKey && string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Falta Steam Web API Key.");
            }

            var url = BuildUrl(endpointPath, query, apiKey);
            WidgetFileLog.Write("Steam GET " + RedactKey(url, apiKey));

            using (var response = await GetClient()
                .GetAsync(new Uri(url))
                .AsTask()
                .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var statusCode = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync().AsTask().ConfigureAwait(false);
                WidgetFileLog.Write("Steam HTTP status=" + statusCode + " bytes=" + (body?.Length ?? 0));
                return (statusCode, body ?? string.Empty);
            }
        }

        private static async Task<string> GetAsync(
            string endpointPath,
            IDictionary<string, string> query,
            string apiKey,
            bool requireKey,
            CancellationToken cancellationToken)
        {
            var (statusCode, body) = await GetRawAsync(
                endpointPath,
                query,
                apiKey,
                requireKey,
                cancellationToken).ConfigureAwait(false);

            if (statusCode >= 200 && statusCode < 300 && !string.IsNullOrWhiteSpace(body))
            {
                return body;
            }

            throw new InvalidOperationException(FormatHttpError(statusCode, body));
        }

        internal static bool IsAchievementPrivacyError(string bodyOrMessage)
        {
            if (string.IsNullOrWhiteSpace(bodyOrMessage))
            {
                return false;
            }

            return bodyOrMessage.IndexOf("Profile is not public", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   bodyOrMessage.IndexOf("profile is private", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   bodyOrMessage.IndexOf("Requested profile is not public", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildUrl(string endpointPath, IDictionary<string, string> query, string apiKey)
        {
            var path = endpointPath.StartsWith("/", StringComparison.Ordinal)
                ? endpointPath
                : "/" + endpointPath;

            var builder = new StringBuilder(ApiBase);
            builder.Append(path);

            var first = !path.Contains("?", StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                builder.Append(first ? '?' : '&');
                first = false;
                builder.Append("key=").Append(Uri.EscapeDataString(apiKey));
            }

            if (query != null)
            {
                foreach (var pair in query)
                {
                    if (string.IsNullOrEmpty(pair.Key))
                    {
                        continue;
                    }

                    builder.Append(first ? '?' : '&');
                    first = false;
                    builder.Append(pair.Key).Append('=');
                    builder.Append(Uri.EscapeDataString(pair.Value ?? string.Empty));
                }
            }

            if (first)
            {
                builder.Append('?');
            }
            else
            {
                builder.Append('&');
            }

            builder.Append("format=json");
            return builder.ToString();
        }

        private static string RedactKey(string url, string apiKey)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(apiKey))
            {
                return url;
            }

            return url.Replace(apiKey, "***", StringComparison.Ordinal);
        }

        private static string FormatHttpError(int statusCode, string body)
        {
            var snippet = TrimBody(body);

            switch (statusCode)
            {
                case 401:
                    return LocalizedStrings.Get("Steam_Http401") + snippet;
                case 403:
                    if (IsAchievementPrivacyError(body))
                    {
                        return SteamPrivacyHelper.BuildAchievementsBlockedMessage(libraryAccessible: true);
                    }

                    return LocalizedStrings.Get("Steam_Http403") + snippet;
                case 429:
                    return LocalizedStrings.Get("Steam_Http429") + snippet;
                default:
                    return LocalizedStrings.Format("Steam_HttpGeneric", statusCode) + snippet;
            }
        }

        private static string TrimBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            var trimmed = body.Trim();
            if (trimmed.Length > 180)
            {
                trimmed = trimmed.Substring(0, 180) + "...";
            }

            return LocalizedStrings.Format("Steam_DetailSuffix", trimmed);
        }

        private static HttpClient GetClient()
        {
            if (_client != null)
            {
                return _client;
            }

            lock (ClientLock)
            {
                if (_client == null)
                {
                    _client = new HttpClient();
                }

                return _client;
            }
        }
    }
}
