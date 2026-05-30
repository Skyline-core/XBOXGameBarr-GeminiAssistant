using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeminiAssistant.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GeminiAssistant.Services
{
    public sealed class GeminiChatService
    {
        private const string Model = "gemini-2.5-flash";
        private static readonly HttpClient HttpClient = new HttpClient();

        private readonly List<GeminiContent> _history = new List<GeminiContent>();

        public void ClearHistory()
        {
            _history.Clear();
        }

        public Task<string> SendMessageAsync(
            string userText,
            GameContextInfo gameContext,
            PendingScreenshot screenshot,
            CancellationToken cancellationToken = default)
        {
            var parts = new List<object> { new { text = userText } };
            AddScreenshotPart(parts, screenshot);
            return SendPartsAsync(parts, gameContext, cancellationToken);
        }

        public Task<string> SendVoiceMessageAsync(
            byte[] wavBytes,
            GameContextInfo gameContext,
            PendingScreenshot screenshot,
            CancellationToken cancellationToken = default)
        {
            if (wavBytes == null || wavBytes.Length == 0)
            {
                throw new InvalidOperationException("El audio grabado esta vacio.");
            }

            var parts = new List<object>
            {
                new
                {
                    text = "Escucha el audio del jugador. Transcribelo en espanol y responde como asistente de gaming. " +
                           "Si no hay voz audible, dilo claramente."
                },
                new
                {
                    inline_data = new
                    {
                        mime_type = "audio/wav",
                        data = Convert.ToBase64String(wavBytes)
                    }
                }
            };

            AddScreenshotPart(parts, screenshot);
            return SendPartsAsync(parts, gameContext, cancellationToken);
        }

        private static void AddScreenshotPart(List<object> parts, PendingScreenshot screenshot)
        {
            if (screenshot?.JpegBytes != null && screenshot.JpegBytes.Length > 0)
            {
                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = screenshot.MimeType ?? "image/jpeg",
                        data = Convert.ToBase64String(screenshot.JpegBytes)
                    }
                });
            }
        }

        private async Task<string> SendPartsAsync(
            List<object> parts,
            GameContextInfo gameContext,
            CancellationToken cancellationToken)
        {
            var apiKey = AppSettingsService.GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Configura tu API key en Ajustes del widget.");
            }

            _history.Add(new GeminiContent { role = "user", parts = parts });

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            var body = new
            {
                system_instruction = new
                {
                    parts = new[]
                    {
                        new { text = gameContext?.ToSystemInstruction() ?? "Eres un asistente de gaming." }
                    }
                },
                contents = _history,
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 1024
                }
            };

            var json = JsonConvert.SerializeObject(body);
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using (var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException(ParseError(responseText, response.StatusCode));
                    }

                    var reply = ParseReply(responseText);
                    _history.Add(new GeminiContent
                    {
                        role = "model",
                        parts = new List<object> { new { text = reply } }
                    });

                    return reply;
                }
            }
        }

        private static string ParseReply(string responseText)
        {
            var root = JObject.Parse(responseText);
            var text = root["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                var blockReason = root["promptFeedback"]?["blockReason"]?.ToString();
                if (!string.IsNullOrEmpty(blockReason))
                {
                    throw new InvalidOperationException("Respuesta bloqueada: " + blockReason);
                }

                throw new InvalidOperationException("Gemini no devolvi� texto en la respuesta.");
            }

            return text.Trim();
        }

        private static string ParseError(string responseText, System.Net.HttpStatusCode statusCode)
        {
            try
            {
                var message = JObject.Parse(responseText)["error"]?["message"]?.ToString();
                if (!string.IsNullOrEmpty(message))
                {
                    return $"Gemini API ({(int)statusCode}): {message}";
                }
            }
            catch
            {
                // ignore parse errors
            }

            return $"Gemini API error ({(int)statusCode})";
        }

        private sealed class GeminiContent
        {
            [JsonProperty("role")]
            public string role { get; set; }

            [JsonProperty("parts")]
            public List<object> parts { get; set; }
        }
    }
}
