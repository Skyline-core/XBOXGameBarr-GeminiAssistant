namespace GeminiAssistant.Models
{
    public sealed class PendingScreenshot
    {
        public byte[] JpegBytes { get; set; }
        public string MimeType { get; set; } = "image/jpeg";
    }
}
