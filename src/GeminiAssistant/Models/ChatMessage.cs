using Windows.UI.Xaml.Media.Imaging;

namespace GeminiAssistant.Models
{
    public enum ChatRole
    {
        User,
        Assistant,
        System
    }

    public sealed class ChatMessage
    {
        public ChatRole Role { get; set; }
        public string Text { get; set; }
        public BitmapImage Thumbnail { get; set; }
        public bool HasImage => Thumbnail != null;
        public bool IsLoading { get; set; }
    }
}
