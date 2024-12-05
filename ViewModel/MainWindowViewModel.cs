using AutoGenerateContent.Event;
using AutoGenerateContent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Stateless;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace AutoGenerateContent.ViewModel
{
    public partial class MainWindowViewModel : ObservableObject
    {
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        string WebView2Profile;
        public string HtmlContent;
        System.Timers.Timer timer = new System.Timers.Timer(TimeSpan.FromSeconds(1));
        TimeSpan span; 
        ProcessService _processService;

        [ObservableProperty]
        ObservableCollection<string> webView2List;

        [ObservableProperty]
        StateMachine<State, Trigger> stateMachine;

        [ObservableProperty]
        int loopCount;

        [ObservableProperty]
        string prompt;

        [ObservableProperty]
        string time;

        public string StatusTitle => StateMachine.State == State.Start ? string.Empty : StateMachine.State.ToString();
        public string BtnStartContent => StateMachine.State == State.Start ? "Start" : "Stop";

        [ObservableProperty]
        SideBarViewModel sidebar;

        public List<string> GoogleUrls = [];

        public List<string> GoogleContents = [];

        public List<string> SummaryContents = [];

        public MainWindowViewModel(SideBarViewModel sideBarViewModel, ProcessService processService)
        {
            Sidebar = sideBarViewModel;
            _processService = processService;

            InitStateMachine();
            if (Directory.Exists(nameof(WebView2Profile)))
            {
                Directory.Delete(nameof(WebView2Profile), true);
            }
            Directory.CreateDirectory(nameof(WebView2Profile));

            if (Directory.Exists("Output"))
            {
                Directory.Delete("Output", true);
            }
            Directory.CreateDirectory("Output");

            timer.Elapsed += (s, e) =>
            {
                span = span.Add(TimeSpan.FromSeconds(1));
                Time = $"{span.Minutes:0#}:{span.Seconds:0#}";
            };
        }
       
        private void InitStateMachine()
        {
            StateMachine = new StateMachine<State, Trigger>(State.Start);
            StateMachine.OnTransitioned(_ =>
            {
                OnPropertyChanged(nameof(StatusTitle));
                OnPropertyChanged(nameof(BtnStartContent));
            });
            StateMachine.Configure(State.Start)
                .PermitIf(Trigger.Next, State.Init, () => !tokenSource.IsCancellationRequested)
                .OnEntryAsync(OnIdle);

            StateMachine.Configure(State.Init)
                .PermitIf(Trigger.Next, State.SearchKeyword, () => !tokenSource.IsCancellationRequested)
                .OnEntryAsync(OnStart);

            StateMachine.Configure(State.SearchKeyword)
                .PermitIf(Trigger.Next, State.ReadHtmlContent, () => !tokenSource.IsCancellationRequested)
                .OnEntry(OnSearchKeyword);

            StateMachine.Configure(State.ReadHtmlContent)
                .PermitIf(Trigger.Next, State.Intro, () => !tokenSource.IsCancellationRequested)
                .OnEntryAsync(OnReadHtmlContent);

            StateMachine.Configure(State.Intro)
                .PermitReentryIf(Trigger.Loop, () => !tokenSource.IsCancellationRequested)
                .PermitIf(Trigger.Next, State.AskChatGpt, () => !tokenSource.IsCancellationRequested)
                .OnEntryAsync(OnIntro);

            StateMachine.Configure(State.AskChatGpt)
                .PermitReentryIf(Trigger.Loop, () => !tokenSource.IsCancellationRequested)
                .PermitIf(Trigger.Next, State.SummaryContent, () => !tokenSource.IsCancellationRequested)
                .OnEntryAsync(OnAskChatGpt);

            StateMachine.Configure(State.SummaryContent)
                .PermitIf(Trigger.Next, State.AskTitle, () => !tokenSource.IsCancellationRequested)
                .OnEntryAsync(OnSummaryContent);
            
            StateMachine.Configure(State.AskTitle)
                .PermitIf(Trigger.Next, State.SearchImage, () => !tokenSource.IsCancellationRequested)
                .OnEntryAsync(OnAskTitle);

            StateMachine.Configure(State.SearchImage)
                .PermitIf(Trigger.Next, State.Finish, () => !tokenSource.IsCancellationRequested)
                .OnEntryAsync(OnSearchImage);
            
            StateMachine.Configure(State.Finish)
                .PermitIf(Trigger.Start, State.Start, () => !tokenSource.IsCancellationRequested)
                .OnEntryAsync(OnFinish);

            OnPropertyChanged(nameof(StatusTitle));
            OnPropertyChanged(nameof(BtnStartContent));
        }

        private async Task OnIdle()
        {
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            if (LoopCount > 0 && !token.IsCancellationRequested)
            {
                LoopCount--;
                await StateMachine.FireAsync(Trigger.Next, token);
            }
            else
            {
                tokenSource = new CancellationTokenSource();
                timer.Stop();
            }
        }

        private async Task OnStart()
        {
            _processService.UpdateMode(Sidebar.SelectedConfig.Mode);
            await tokenSource.CancelAsync();
            tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            WeakReferenceMessenger.Default.Send<OnStart>(new(string.Empty, token));
            span = TimeSpan.Zero;
            timer.Start();
            GoogleUrls.Clear();
            GoogleContents.Clear();
            SummaryContents.Clear();
            WebView2List?.Clear();
            WebView2List = [Guid.NewGuid().ToString()];
        }
        
        private async void OnSearchKeyword()
        {
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            WeakReferenceMessenger.Default.Send<SearchKeyword>(new(Sidebar.SelectedConfig.SearchText, token));
        }
        
        private async Task OnReadHtmlContent()
        {
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            List<Task> tasks = [];
            while (GoogleContents.Count < Sidebar.SelectedConfig.NumberUrls && GoogleUrls.Count > 0)
            {
                foreach (var url in GoogleUrls.Take(5))
                {
                    tasks.Add(_processService.OnReadHtmlContent(url, GoogleUrls, GoogleContents, token).ContinueWith(t =>
                    {
                        var cleanedContent = t.Result;
                        GoogleUrls.Remove(url);
                        if (string.IsNullOrWhiteSpace(cleanedContent) == false)
                        {
                            if (cleanedContent.Length > 20000)
                            {
                                cleanedContent = $"{cleanedContent[..20000]}. Reference: {url}";
                            }
                            GoogleContents.Add(cleanedContent);
                        }
                    }));
                }
                Task.WaitAll([.. tasks], token);
            }
            GoogleContents = GoogleContents.Take(Sidebar.SelectedConfig.NumberUrls).ToList();
            await StateMachine.FireAsync(Trigger.Next, token);
        }

        private async Task OnAskChatGpt()
        {
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            if (GoogleContents.Count > 0)
            {
                string content = await _processService.OnAskChatGpt(Sidebar.SelectedConfig.PromptText, GoogleContents.FirstOrDefault(), token);
                if (string.IsNullOrWhiteSpace(content) == false)
                {
                    SummaryContents.Add(content);
                    GoogleContents.RemoveAt(0);
                    if (StateMachine.CanFire(Trigger.Loop))
                    {
                        await StateMachine.FireAsync(Trigger.Loop);
                    }
                }
            }
            else
            {
                await StateMachine.FireAsync(Trigger.Next);
            }
        }
        
        private async Task OnIntro()
        {
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            if (string.IsNullOrWhiteSpace(Sidebar.SelectedConfig.PromptIntro) == false)
            {
                bool finish = await _processService.OnIntro(Sidebar.SelectedConfig.PromptIntro, token);
                if (finish)
                {
                    await StateMachine.FireAsync(ViewModel.Trigger.Next);
                }
            }
            else
            {
                await StateMachine.FireAsync(Trigger.Next);
            }
        }
        
        private async Task OnSummaryContent()
        {
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            if (string.IsNullOrWhiteSpace(Sidebar.SelectedConfig.PromptSummary) == false)
            {
                var html = await _processService.OnSummaryContent(Sidebar.SelectedConfig.PromptSummary, SummaryContents, token);
                if (string.IsNullOrWhiteSpace(html) == false)
                {
                    HtmlContent = html;
                    await StateMachine.FireAsync(ViewModel.Trigger.Next);
                }
            }
        }

        private async Task OnAskTitle()
        {
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            if (string.IsNullOrWhiteSpace(Sidebar.SelectedConfig.PromptTitle) == false)
            {
                var title = await _processService.OnAskTitle(Sidebar.SelectedConfig.PromptTitle, token);
                if (string.IsNullOrWhiteSpace(title) == false)
                {
                    if (title != "finish")
                    {
                        var oldTitle = GetTitleRegex().Match(HtmlContent);
                        var h1Title = GetH1Regex().Match(HtmlContent);
                        HtmlContent = HtmlContent.Replace(oldTitle.Value, title).Replace(h1Title.Value, title.Replace("title", "h1"));
                    }
                    await StateMachine.FireAsync(ViewModel.Trigger.Next);
                }
            }
        }

        private async Task OnSearchImage()
        {
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            if (string.IsNullOrWhiteSpace(Sidebar.SelectedConfig.SearchImageText) == false)
            {
                var result = await _processService.OnSearchImage(Sidebar.SelectedConfig.SearchImageText, HtmlContent, token);
                if (result)
                {
                    await StateMachine.FireAsync(ViewModel.Trigger.Next);
                }
            }
        }

        private async Task OnFinish()
        {
            OnPropertyChanged(nameof(StateMachine));
            await StateMachine.FireAsync(Trigger.Start);
        }

        [RelayCommand]
        public async Task Start()
        {
            if (StateMachine.CanFire(Trigger.Start))
            {
                await StateMachine.FireAsync(Trigger.Start);
            }
            else if (StateMachine.IsInState(State.Start) == false)
            {
                await tokenSource.CancelAsync();
                await OnIdle();
                InitStateMachine();
            }
            else if (StateMachine.CanFire(Trigger.Next))
            {
                await StateMachine.FireAsync(Trigger.Next);
            }
        }

        [RelayCommand]
        public void ClearWebCache()
        {
            CancellationToken token = tokenSource.Token;
            string newProfile = Path.Combine(nameof(WebView2Profile), Guid.NewGuid().ToString());
            WeakReferenceMessenger.Default.Send<StateChanged>(new((State.Start, newProfile),token));
            WebView2Profile = newProfile;
        }


        [GeneratedRegex(@"<title\b[^>]*>([^<]*)<\/title>(?!.*<\/title>)")]
        private static partial Regex GetTitleRegex();

        [GeneratedRegex(@"<h1\b[^>]*>(.*?)<\/h1>(?!.*<h1\b)")]
        private static partial Regex GetH1Regex();
    }

    public enum State
    {
        Start,
        Init,
        SearchKeyword,
        ReadHtmlContent,
        Intro,
        AskChatGpt,
        SummaryContent,
        AskTitle,
        SearchImage,
        Finish
    }

    public enum Trigger
    {
        Start,
        Next,
        Loop,
    }
}
