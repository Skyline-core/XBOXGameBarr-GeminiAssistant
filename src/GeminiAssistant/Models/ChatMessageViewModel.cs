using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace GeminiAssistant.Models
{
    public sealed class ChatMessageViewModel : INotifyPropertyChanged
    {
        private ImageSource _thumbnail;
        private Visibility _thumbnailVisibility = Visibility.Collapsed;

        public string RoleLabel { get; set; }
        public string Text { get; set; }
        public Visibility AttachmentVisibility { get; set; } = Visibility.Collapsed;
        public string AttachmentCaption { get; set; }

        public bool IsUserMessage { get; set; }

        public string AvatarGlyph { get; set; } = "✦";

        public Visibility ThumbnailVisibility
        {
            get => _thumbnailVisibility;
            set
            {
                if (_thumbnailVisibility == value)
                {
                    return;
                }

                _thumbnailVisibility = value;
                OnPropertyChanged();
            }
        }

        public ImageSource Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (ReferenceEquals(_thumbnail, value))
                {
                    return;
                }

                _thumbnail = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
