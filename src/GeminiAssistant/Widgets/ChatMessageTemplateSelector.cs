using GeminiAssistant.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace GeminiAssistant.Widgets
{
    public sealed class ChatMessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate UserMessageTemplate { get; set; }
        public DataTemplate GeminiMessageTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item is ChatMessageViewModel vm && vm.IsUserMessage)
            {
                return UserMessageTemplate;
            }

            return GeminiMessageTemplate;
        }
    }
}
