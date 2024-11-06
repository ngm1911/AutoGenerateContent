using CommunityToolkit.Mvvm.Messaging.Messages;

namespace AutoGenerateContent.Event
{
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

    public class SummaryHighLight : ValueChangedMessage<string>
    {
        public SummaryHighLight(string keyword) : base(keyword)
        {
        }
    }
}
