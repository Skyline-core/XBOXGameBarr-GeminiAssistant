using System.Text;
using GeminiAssistant.Models;

namespace GeminiAssistant.Services
{
    internal static class GeminiSystemInstruction
    {
        public static string Build(GameContextInfo gameContext)
        {
            var sb = new StringBuilder(
                gameContext?.ToSystemInstruction() ?? LocalizedStrings.Ctx_UnknownInstruction);

            if (AppSettingsService.HasSteamCredentials())
            {
                sb.Append(LocalizedStrings.Gemini_SteamConfigured);
            }

            sb.Append(LocalizedStrings.Gemini_RespondClearly);
            return sb.ToString();
        }
    }
}
