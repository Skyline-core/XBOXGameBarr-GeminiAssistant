using System.Collections.ObjectModel;
using GeminiAssistant.Models;

namespace GeminiAssistant.Services
{
    /// <summary>
    /// Mantiene el chat si Game Bar reinicia la pagina del widget.
    /// </summary>
    public sealed class ChatSessionStore
    {
        public ObservableCollection<ChatMessageViewModel> Messages { get; } =
            new ObservableCollection<ChatMessageViewModel>();

        public PendingScreenshot PendingScreenshot { get; set; }

        private static ChatSessionStore _current;

        public static ChatSessionStore Current => _current ?? (_current = new ChatSessionStore());

        public static void Reset()
        {
            _current = new ChatSessionStore();
        }
    }
}
