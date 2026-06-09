using System;
using System.Collections.Generic;
using System.Text;
using GeminiAssistant.Services;

namespace GeminiAssistant.Models
{
    public sealed class SteamAchievementsReport
    {
        public bool HasError { get; set; }

        public bool NeedsClarification { get; set; }

        public string ErrorMessage { get; set; }

        public string ClarificationMessage { get; set; }

        public string PersonaName { get; set; }

        public string GameName { get; set; }

        public uint AppId { get; set; }

        public int UnlockedCount { get; set; }

        public int TotalCount { get; set; }

        public List<SteamAchievementEntry> Achievements { get; set; } = new List<SteamAchievementEntry>();

        public string FormatForGemini()
        {
            if (NeedsClarification)
            {
                return LocalizedStrings.Steam_ClarificationPrefix + ClarificationMessage;
            }

            if (HasError)
            {
                return LocalizedStrings.Steam_ErrorPrefix + ErrorMessage;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Datos reales de Steam Web API:");
            if (!string.IsNullOrEmpty(PersonaName))
            {
                sb.AppendLine("Perfil: " + PersonaName);
            }

            sb.AppendLine("Juego: " + GameName + " (AppID " + AppId + ")");
            sb.AppendLine("Logros: " + UnlockedCount + " / " + TotalCount);

            if (Achievements.Count == 0)
            {
                sb.AppendLine("Este juego no tiene logros en Steam o no estan disponibles.");
                return sb.ToString().Trim();
            }

            sb.AppendLine();
            sb.AppendLine("Lista de logros:");

            foreach (var achievement in Achievements)
            {
                var status = achievement.IsUnlocked ? "[DESBLOQUEADO]" : "[BLOQUEADO]";
                sb.Append(status).Append(' ').Append(achievement.DisplayName);
                if (!string.IsNullOrWhiteSpace(achievement.GlobalPercent))
                {
                    sb.Append(" (").Append(achievement.GlobalPercent).Append(" de jugadores)");
                }

                if (!string.IsNullOrWhiteSpace(achievement.Description))
                {
                    sb.Append(" — ").Append(achievement.Description);
                }

                sb.AppendLine();
            }

            return sb.ToString().Trim();
        }
    }

    public sealed class SteamAchievementEntry
    {
        public string ApiName { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public bool IsUnlocked { get; set; }

        public string GlobalPercent { get; set; }
    }

    public sealed class SteamProfileReport
    {
        public bool HasError { get; set; }

        public string ErrorMessage { get; set; }

        public string PersonaName { get; set; }

        public string ProfileUrl { get; set; }

        public int OwnedGamesCount { get; set; }

        public int TotalPlaytimeMinutes { get; set; }

        public List<SteamOwnedGameEntry> TopGames { get; set; } = new List<SteamOwnedGameEntry>();

        public string FormatForGemini()
        {
            if (HasError)
            {
                return LocalizedStrings.Steam_ErrorPrefix + ErrorMessage;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Datos reales de Steam Web API:");
            sb.AppendLine("Perfil: " + PersonaName);
            if (!string.IsNullOrWhiteSpace(ProfileUrl))
            {
                sb.AppendLine("URL: " + ProfileUrl);
            }

            sb.AppendLine("Juegos en biblioteca: " + OwnedGamesCount);
            sb.AppendLine("Tiempo total jugado (aprox.): " + (TotalPlaytimeMinutes / 60) + " horas");

            if (TopGames.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Juegos con mas horas:");
                foreach (var game in TopGames)
                {
                    sb.Append("- ").Append(game.Name)
                        .Append(": ").Append(game.PlaytimeHours).Append(" h");
                    if (game.AppId > 0)
                    {
                        sb.Append(" (AppID ").Append(game.AppId).Append(')');
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString().Trim();
        }
    }

    public sealed class SteamOwnedGameEntry
    {
        public uint AppId { get; set; }

        public string Name { get; set; }

        public int PlaytimeMinutes { get; set; }

        public int LastPlayedUnix { get; set; }

        public int PlaytimeHours => PlaytimeMinutes / 60;
    }

    public sealed class SteamLastAchievementReport
    {
        public bool HasError { get; set; }

        public string ErrorMessage { get; set; }

        public string PersonaName { get; set; }

        public string GameName { get; set; }

        public uint AppId { get; set; }

        public string AchievementName { get; set; }

        public string Description { get; set; }

        public DateTimeOffset? UnlockedAt { get; set; }

        public string FormatForGemini()
        {
            if (HasError)
            {
                return LocalizedStrings.Steam_ErrorPrefix + ErrorMessage;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Datos reales de Steam Web API:");
            if (!string.IsNullOrEmpty(PersonaName))
            {
                sb.AppendLine("Perfil: " + PersonaName);
            }

            if (string.IsNullOrWhiteSpace(AchievementName))
            {
                if (!string.IsNullOrWhiteSpace(ErrorMessage))
                {
                    sb.AppendLine(ErrorMessage);
                }
                else
                {
                    sb.AppendLine(
                        "No se encontro ningun logro desbloqueado reciente. " +
                        "Comprueba que Detalles del juego este en Publico en Steam.");
                }

                return sb.ToString().Trim();
            }

            sb.AppendLine("Ultimo logro desbloqueado encontrado:");
            sb.AppendLine("- Juego: " + GameName + " (AppID " + AppId + ")");
            sb.AppendLine("- Logro: " + AchievementName);
            if (UnlockedAt.HasValue)
            {
                sb.AppendLine("- Fecha: " + UnlockedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
            }

            if (!string.IsNullOrWhiteSpace(Description))
            {
                sb.AppendLine("- Descripcion: " + Description);
            }

            return sb.ToString().Trim();
        }
    }
}
