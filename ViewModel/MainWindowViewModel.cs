using AutoGenerateContent.Event;
using AutoGenerateContent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HtmlAgilityPack;
using Stateless;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using static Google.Apis.Requests.BatchRequest;
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

        public List<string> Heading = [];

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
                .PermitIf(Trigger.Next, State.AskNewContent, () => !tokenSource.IsCancellationRequested)
                .OnEntryAsync(OnIntro);

            StateMachine.Configure(State.AskNewContent)
                .PermitIf(Trigger.Next, State.AskHeading, () => !tokenSource.IsCancellationRequested)
                .OnEntryAsync(OnAskNewContent);

            StateMachine.Configure(State.AskHeading)
                .PermitReentryIf(Trigger.Loop, () => !tokenSource.IsCancellationRequested)
                .PermitIf(Trigger.Next, State.AskTitle, () => !tokenSource.IsCancellationRequested)
                .OnEntryAsync(OnAskHeading);

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
            foreach (var url in GoogleUrls.Take(5))
            {
                tasks.Add(_processService.OnReadHtmlContent(url, token));
            }
            Task.WaitAll([.. tasks], token);
            await StateMachine.FireAsync(Trigger.Next, token);
        }

        private async Task OnIntro()
        {
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            if (string.IsNullOrWhiteSpace(Sidebar.SelectedConfig.PromptIntro) == false)
            {
                await _processService.OnIntro(Sidebar.SelectedConfig.PromptIntro, token);
                await StateMachine.FireAsync(ViewModel.Trigger.Next);
            }
            else
            {
                await StateMachine.FireAsync(Trigger.Next);
            }
        }
        
        private async Task OnAskNewContent()
        {
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            if (string.IsNullOrWhiteSpace(Sidebar.SelectedConfig.PromptAskNewContent) == false)
            {
                var html = await _processService.OnAskNewContent(Sidebar.SelectedConfig.PromptAskNewContent, token);
                if (string.IsNullOrWhiteSpace(html) == false)
                {
                    HtmlContent = html;
                    await _processService.OnAskForHtml(HtmlContent, token);
                }
                else
                {
                    MessageBox.Show("Error from Gemini, try later");
                }
            }
        }

        private async Task OnAskHeading()
        {
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            if (string.IsNullOrWhiteSpace(Sidebar.SelectedConfig.PromptHeading) == false)
            {
                if (Heading.Count > 0)
                {
                    var newHeading = await _processService.OnAskHeading(Sidebar.SelectedConfig.PromptHeading, Heading.FirstOrDefault(), token);
                    if (string.IsNullOrWhiteSpace(newHeading) == false)
                    {
                        await _processService.OnAskForHtml(newHeading, token);
                    }
                }
                else
                {
                    await StateMachine.FireAsync(Trigger.Next);
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
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(title);
                    if (htmlDoc.ParseErrors.Count() > 0)
                    {
                        await _processService.OnAskForHtml(title, token);
                        return;
                    }
                    else
                    {
                        var oldTitle = GetTitleRegex().Match(HtmlContent);
                        var h1Title = GetH1Regex().Match(HtmlContent);
                        HtmlContent = HtmlContent.Replace(oldTitle.Value, title).Replace(h1Title.Value, title.Replace("title", "h1"));
                        await StateMachine.FireAsync(ViewModel.Trigger.Next);
                    }
                }
                else
                {
                    await StateMachine.FireAsync(Trigger.Next);
                }
            }
        }

        private async Task OnSearchImage()
        {
            CancellationToken token = tokenSource.Token;
            OnPropertyChanged(nameof(StateMachine));
            if (string.IsNullOrWhiteSpace(Sidebar.SelectedConfig.SearchImageText) == false)
            {
                //var newHtmlContent = await _processService.OnAskFinalHtml(HtmlContent, token);
                //var htmlDoc = new HtmlDocument();
                //htmlDoc.LoadHtml(newHtmlContent);
                //if (htmlDoc.ParseErrors.Count() > 0)
                {
                    string prompt = "Đọc html, viết lại 1 hoàn chỉnh trang web, fix các lỗi heading, lỗi html cho tôi. Lưu ý phải giữ lại toàn bộ các nội dung của trang web, chỉ chỉnh sửa format cho đúng: {0}";
                    await _processService.OnAskForHtml(string.Format(prompt, HtmlContent), token);
                    return;
                }
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
        AskNewContent,
        AskTitle,
        AskHeading,
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
