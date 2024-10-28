using AutoGenerateContent.Event;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Stateless;

namespace AutoGenerateContent.ViewModel
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        StateMachine<State, Trigger> stateMachine;

        [ObservableProperty]
        bool auto;
        
        [ObservableProperty]
        string prompt;

        public MainWindowViewModel() 
        {
            InitStateMachine();
        }
       
        private void InitStateMachine()
        {
            StateMachine = new StateMachine<State, Trigger>(State.Start);

            StateMachine.Configure(State.Start)
                .Permit(Trigger.Next, State.Init)
                .OnEntryAsync(OnIdle);

            StateMachine.Configure(State.Init)
                .Permit(Trigger.Next, State.SearchKeyword)
                .OnEntryAsync(OnStart);

            StateMachine.Configure(State.SearchKeyword)
                .Permit(Trigger.Next, State.AskChatGpt)
                .OnEntryAsync(OnSearchKeyword);

            StateMachine.Configure(State.AskChatGpt)
                .Permit(Trigger.Next, State.SummaryContent)
                .OnEntryAsync(OnAskChatGpt);

            StateMachine.Configure(State.SummaryContent)
                .Permit(Trigger.Next, State.Finish)
                .OnEntryAsync(OnSummaryContent);

            StateMachine.Configure(State.Finish)
                .Permit(Trigger.Start, State.Start)
                .OnEntryAsync(OnFinish);
        }

        private async Task OnIdle()
        {
            await Task.Delay(1000);
            OnPropertyChanged(nameof(StateMachine));
        }

        private async Task OnStart()
        {
            await Task.Delay(1000);
            OnPropertyChanged(nameof(StateMachine));
            if (Auto) 
                await StateMachine.FireAsync(Trigger.Next);
        }
        
        private async Task OnSearchKeyword()
        {   
            await Task.Delay(1000);
            OnPropertyChanged(nameof(StateMachine));
            if (Auto) 
                await StateMachine.FireAsync(Trigger.Next);
        }
        
        private async Task OnAskChatGpt()
        {
            OnPropertyChanged(nameof(StateMachine));
            WeakReferenceMessenger.Default.Send<AskChatGpt>(new(Prompt));
        }
        
        private async Task OnSummaryContent()
        {
            await Task.Delay(1000);
            OnPropertyChanged(nameof(StateMachine));
            if (Auto) 
                await StateMachine.FireAsync(Trigger.Next);
        }
        
        private async Task OnFinish()
        {
            await Task.Delay(1000);
            OnPropertyChanged(nameof(StateMachine));
            if (Auto) 
                await StateMachine.FireAsync(Trigger.Start);
        }

        [RelayCommand]
        public async Task Start()
        {
            if(StateMachine.CanFire(Trigger.Next))
                await StateMachine.FireAsync(Trigger.Next);
            else if (StateMachine.CanFire(Trigger.Start))
                await StateMachine.FireAsync(Trigger.Start);
        }
    }

    public enum State
    {
        Start,
        Init,
        SearchKeyword,
        AskChatGpt,
        SummaryContent,
        Finish
    }

    public enum Trigger
    {
        Start,
        Next
    }
}
