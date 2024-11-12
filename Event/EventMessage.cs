using AutoGenerateContent.ViewModel;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Stateless.Graph;

namespace AutoGenerateContent.Event
{
    public class AskChatGpt : ValueChangedMessage<string>
    {
        public AskChatGpt(string prompt) : base(prompt)
        {
        }
    }
    
    public class StateChanged : ValueChangedMessage<(ViewModel.State, object)>
    {
        public StateChanged((ViewModel.State, object) a) : base(a)
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
