using CommunityToolkit.Mvvm.Messaging.Messages;

namespace AutoGenerateContent.Event
{
    public class GoNavigate : ValueChangedMessage<string>
    {
        public GoNavigate(string url) : base(url)
        {
        }
    }
    
    public class AskChatGpt : ValueChangedMessage<string>
    {
        public AskChatGpt(string prompt) : base(prompt)
        {
        }
    }

    public class SearchKeyword : ValueChangedMessage<string>
    {
        public SearchKeyword(string keyword) : base(keyword)
        {
        }
    }
}
