using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using GeminiAssistant.Models;

namespace GeminiAssistant.Services
{
    public sealed class GeminiChatService
    {
        private const string Model = "gemini-2.5-flash";
        private const int MaxHistoryTurns = 4;
        private const int MaxOutputTokens = 2048;
        private static readonly object HistoryLock = new object();
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly List<GeminiContent> _history = new List<GeminiContent>();

        public void ClearHistory()
        {
            lock (HistoryLock)
            {
                _history.Clear();
            }
        }

        public Task<string> SendMessageAsync(
            string userText,
            GameContextInfo gameContext,
            PendingScreenshot screenshot,
            CancellationToken cancellationToken = default)
        {
            var parts = new List<GeminiPart> { new GeminiPart { Text = userText } };
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

            var parts = new List<GeminiPart>
            {
                new GeminiPart
                {
                    Text = "Escucha el audio del jugador. Transcribelo en espanol y responde como asistente de gaming. " +
                           "Si no hay voz audible, dilo claramente."
                },
                new GeminiPart
                {
                    InlineData = new GeminiInlineData
                    {
                        MimeType = "audio/wav",
                        Data = Convert.ToBase64String(wavBytes)
                    }
                }
            };

            AddScreenshotPart(parts, screenshot);
            return SendPartsAsync(parts, gameContext, cancellationToken);
        }

        private static void AddScreenshotPart(List<GeminiPart> parts, PendingScreenshot screenshot)
        {
            if (screenshot?.JpegBytes == null || screenshot.JpegBytes.Length == 0)
            {
                return;
            }

            parts.Add(new GeminiPart
            {
                InlineData = new GeminiInlineData
                {
                    MimeType = screenshot.MimeType ?? "image/jpeg",
                    Data = Convert.ToBase64String(screenshot.JpegBytes)
                }
            });
        }

        private async Task<string> SendPartsAsync(
            List<GeminiPart> parts,
            GameContextInfo gameContext,
            CancellationToken cancellationToken)
        {
            var apiKey = AppSettingsService.GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Configura tu API key en Ajustes del widget.");
            }

            List<GeminiContent> apiContents;
            lock (HistoryLock)
            {
                _history.Add(ToHistoryUserContent(parts));
                TrimHistoryLocked();
                apiContents = new List<GeminiContent>(_history);
                apiContents[apiContents.Count - 1] = new GeminiContent { Role = "user", Parts = parts };
            }

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            var body = new GeminiRequest
            {
                SystemInstruction = new GeminiContent
                {
                    Parts = new List<GeminiPart>
                    {
                        new GeminiPart
                        {
                            Text = (gameContext?.ToSystemInstruction() ?? "Eres un asistente de gaming.") +
                                   " Responde claro y directo; evita relleno innecesario."
                        }
                    }
                },
                Contents = apiContents,
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = 0.55,
                    MaxOutputTokens = MaxOutputTokens,
                    ThinkingConfig = new GeminiThinkingConfig { ThinkingBudget = 0 }
                }
            };

            var json = JsonSerializer.Serialize(body, JsonOptions);
            WidgetFileLog.Write("Gemini API in=" + json.Length + " chars");

            var (statusCode, responseText) = await GeminiUwpHttpClient.PostJsonAsync(url, json, cancellationToken)
                .ConfigureAwait(false);

            if (statusCode == 400 && body.GenerationConfig?.ThinkingConfig != null &&
                responseText != null &&
                responseText.IndexOf("thinking", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                WidgetFileLog.Write("Gemini: reintento sin thinkingConfig");
                body.GenerationConfig.ThinkingConfig = null;
                json = JsonSerializer.Serialize(body, JsonOptions);
                (statusCode, responseText) = await GeminiUwpHttpClient.PostJsonAsync(url, json, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (statusCode < 200 || statusCode >= 300)
            {
                throw new InvalidOperationException(ParseError(responseText, statusCode));
            }

            var reply = ParseReply(responseText);
            lock (HistoryLock)
            {
                _history.Add(new GeminiContent
                {
                    Role = "model",
                    Parts = new List<GeminiPart> { new GeminiPart { Text = reply } }
                });
                TrimHistoryLocked();
            }

            return reply;
        }

        private static GeminiContent ToHistoryUserContent(List<GeminiPart> parts)
        {
            var historyParts = new List<GeminiPart>();
            var hadImage = false;
            var hadAudio = false;

            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part.Text))
                {
                    historyParts.Add(new GeminiPart { Text = part.Text });
                }

                if (part.InlineData != null)
                {
                    if (part.InlineData.MimeType != null &&
                        part.InlineData.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        hadImage = true;
                    }
                    else if (part.InlineData.MimeType != null &&
                             part.InlineData.MimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                    {
                        hadAudio = true;
                    }
                }
            }

            if (hadImage)
            {
                historyParts.Add(new GeminiPart { Text = "[El jugador adjunto una captura de pantalla en este mensaje.]" });
            }

            if (hadAudio)
            {
                historyParts.Add(new GeminiPart { Text = "[El jugador envio un mensaje de voz en este turno.]" });
            }

            if (historyParts.Count == 0)
            {
                historyParts.Add(new GeminiPart { Text = "(mensaje vacio)" });
            }

            return new GeminiContent { Role = "user", Parts = historyParts };
        }

        private void TrimHistoryLocked()
        {
            while (_history.Count > MaxHistoryTurns * 2)
            {
                _history.RemoveAt(0);
            }
        }

        private static string ParseReply(string responseText)
        {
            using (var doc = JsonDocument.Parse(responseText))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    var candidate = candidates[0];
                    var text = ExtractCandidateText(candidate);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (candidate.TryGetProperty("finishReason", out var finish) &&
                            string.Equals(finish.GetString(), "MAX_TOKENS", StringComparison.OrdinalIgnoreCase))
                        {
                            WidgetFileLog.Write("Gemini: respuesta truncada por MAX_TOKENS");
                            text += "\n\n[Respuesta cortada por limite de longitud. Pide mas detalle si lo necesitas.]";
                        }

                        return text.Trim();
                    }
                }

                if (root.TryGetProperty("promptFeedback", out var feedback) &&
                    feedback.TryGetProperty("blockReason", out var blockReason))
                {
                    throw new InvalidOperationException("Respuesta bloqueada: " + blockReason.GetString());
                }
            }

            throw new InvalidOperationException("Gemini no devolvio texto en la respuesta.");
        }

        private static string ExtractCandidateText(JsonElement candidate)
        {
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts))
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textEl))
                {
                    var piece = textEl.GetString();
                    if (!string.IsNullOrEmpty(piece))
                    {
                        if (sb.Length > 0)
                        {
                            sb.AppendLine();
                        }

                        sb.Append(piece);
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        private static string ParseError(string responseText, int statusCode)
        {
            try
            {
                using (var doc = JsonDocument.Parse(responseText))
                {
                    if (doc.RootElement.TryGetProperty("error", out var error) &&
                        error.TryGetProperty("message", out var message))
                    {
                        var text = message.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            return $"Gemini API ({statusCode}): {text}";
                        }
                    }
                }
            }
            catch
            {
            }

            return $"Gemini API error ({statusCode})";
        }

        private sealed class GeminiRequest
        {
            [JsonPropertyName("system_instruction")]
            public GeminiContent SystemInstruction { get; set; }

            [JsonPropertyName("contents")]
            public List<GeminiContent> Contents { get; set; }

            [JsonPropertyName("generationConfig")]
            public GeminiGenerationConfig GenerationConfig { get; set; }
        }

        private sealed class GeminiGenerationConfig
        {
            [JsonPropertyName("temperature")]
            public double Temperature { get; set; }

            [JsonPropertyName("maxOutputTokens")]
            public int MaxOutputTokens { get; set; }

            [JsonPropertyName("thinkingConfig")]
            public GeminiThinkingConfig ThinkingConfig { get; set; }
        }

        private sealed class GeminiThinkingConfig
        {
            [JsonPropertyName("thinkingBudget")]
            public int ThinkingBudget { get; set; }
        }

        private sealed class GeminiContent
        {
            [JsonPropertyName("role")]
            public string Role { get; set; }

            [JsonPropertyName("parts")]
            public List<GeminiPart> Parts { get; set; }
        }

        private sealed class GeminiPart
        {
            [JsonPropertyName("text")]
            public string Text { get; set; }

            [JsonPropertyName("inline_data")]
            public GeminiInlineData InlineData { get; set; }
        }

        private sealed class GeminiInlineData
        {
            [JsonPropertyName("mime_type")]
            public string MimeType { get; set; }

            [JsonPropertyName("data")]
            public string Data { get; set; }
        }
    }
}
