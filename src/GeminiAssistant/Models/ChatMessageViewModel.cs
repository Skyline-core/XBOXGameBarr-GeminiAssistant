using Windows.UI.Xaml;

namespace GeminiAssistant.Models
{
    public sealed class ChatMessageViewModel
    {
        public string RoleLabel { get; set; }
        public string Text { get; set; }
        public Visibility AttachmentVisibility { get; set; } = Visibility.Collapsed;
        public string AttachmentCaption { get; set; }
    }
}
