using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace GeminiAssistant.Models
{
    public sealed class ChatMessageViewModel
    {
        public string RoleLabel { get; set; }
        public string Text { get; set; }
        public BitmapImage Thumbnail { get; set; }
        public Visibility ThumbnailVisibility { get; set; } = Visibility.Collapsed;
    }
}
