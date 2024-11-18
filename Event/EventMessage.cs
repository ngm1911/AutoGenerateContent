using CommunityToolkit.Mvvm.Messaging.Messages;

namespace AutoGenerateContent.Event
{
    public class AskChatGpt : ValueChangedMessage<string>
    {
        public readonly CancellationToken Token;
        public AskChatGpt(string prompt, CancellationToken token) : base(prompt) => this.Token = token;
    }
    
    public class StateChanged : ValueChangedMessage<(ViewModel.State, object)>
    {
        public readonly CancellationToken Token;
        public StateChanged((ViewModel.State, object) a, CancellationToken token) : base(a) => this.Token = token;
    }

    public class SearchKeyword : ValueChangedMessage<string>
    {
        public readonly CancellationToken Token;
        public SearchKeyword(string keyword, CancellationToken token) : base(keyword) => this.Token = token;
    }
    
    public class SearchImage : ValueChangedMessage<(string, string)>
    {
        public readonly CancellationToken Token;
        public SearchImage((string, string) a, CancellationToken token) : base(a) => this.Token = token;
    }

    public class SummaryHighLight : ValueChangedMessage<string>
    {
        public readonly CancellationToken Token;
        public SummaryHighLight(string keyword, CancellationToken token) : base(keyword) => this.Token = token;
    }

    public class OnStart : ValueChangedMessage<string>
    {
        public readonly CancellationToken Token;
        public OnStart(string keyword, CancellationToken token) : base(keyword) => this.Token = token;
    }
}
