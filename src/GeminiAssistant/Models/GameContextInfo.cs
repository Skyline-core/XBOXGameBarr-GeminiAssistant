namespace GeminiAssistant.Models
{
    public sealed class GameContextInfo
    {
        public bool TrackingEnabled { get; set; }
        public string DisplayName { get; set; } = "Desconocido";
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
                    return "Seguimiento desactivado en Game Bar";
                }

                var gameLabel = IsGame ? "juego" : "aplicación";
                var fs = IsFullscreen ? ", pantalla completa" : "";
                var title = string.IsNullOrEmpty(TitleId) ? "" : $", titleId={TitleId}";
                return $"{DisplayName} ({gameLabel}{fs}{title})";
            }
        }

        public string ToSystemInstruction()
        {
            if (!TrackingEnabled)
            {
                return "Eres un asistente de gaming en Xbox Game Bar. El seguimiento del juego activo está desactivado; da consejos generales si no conoces el título.";
            }

            return "Eres un asistente de gaming en Xbox Game Bar. " +
                   $"El jugador está en: {DisplayName}. " +
                   $"Es juego: {IsGame}. Pantalla completa: {IsFullscreen}. " +
                   $"AumId: {AumId}. TitleId: {TitleId}. " +
                   "Da consejos, estrategias y respuestas relevantes a ese título. Responde en el idioma del usuario.";
        }
    }
}
