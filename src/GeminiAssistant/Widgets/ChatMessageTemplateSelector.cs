using GeminiAssistant.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace GeminiAssistant.Widgets
{
    public sealed partial class ChatMessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate UserMessageTemplate { get; set; }
        public DataTemplate GeminiMessageTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return SelectTemplateCore(item, null);
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is ChatMessageViewModel vm && vm.IsUserMessage)
            {
                return UserMessageTemplate;
            }

            return GeminiMessageTemplate;
        }
    }
}
