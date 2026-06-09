using GeminiAssistant.Services;

namespace GeminiAssistant.Models
{
    public sealed class GameContextInfo
    {
        public bool TrackingEnabled { get; set; }
        public string DisplayName { get; set; } = "";
        public string AumId { get; set; } = "";
        public string TitleId { get; set; } = "";
        public bool IsGame { get; set; }
        public bool IsFullscreen { get; set; }

        public string Summary
        {
            get
            {
                if (!TrackingEnabled)
                {
                    return LocalizedStrings.Ctx_TrackingDisabled;
                }

                var gameLabel = IsGame ? LocalizedStrings.Ctx_GameLabel : LocalizedStrings.Ctx_AppLabel;
                var fs = IsFullscreen ? LocalizedStrings.Ctx_FullscreenSuffix : "";
                var title = string.IsNullOrEmpty(TitleId) ? "" : $", titleId={TitleId}";
                var name = string.IsNullOrWhiteSpace(DisplayName) ? LocalizedStrings.Game_Unknown : DisplayName;
                return $"{name} ({gameLabel}{fs}{title})";
            }
        }

        public string ToSystemInstruction()
        {
            if (!TrackingEnabled)
            {
                return LocalizedStrings.Ctx_UnknownInstruction;
            }

            var name = string.IsNullOrWhiteSpace(DisplayName) ? LocalizedStrings.Game_Unknown : DisplayName;
            return string.Format(
                LocalizedStrings.Ctx_GameInstruction,
                name,
                IsGame,
                IsFullscreen,
                AumId,
                TitleId);
        }
    }
}
