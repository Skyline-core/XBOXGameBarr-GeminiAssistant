namespace GeminiAssistant.Models
{
    public sealed class PendingScreenshot
    {
        public byte[] JpegBytes { get; set; }
        public string MimeType { get; set; } = "image/jpeg";
        /// <summary>ventana, juego, gamebar-archivo</summary>
        public string Source { get; set; }
    }
}
