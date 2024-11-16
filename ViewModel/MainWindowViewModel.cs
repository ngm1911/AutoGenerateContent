using AutoGenerateContent.Event;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HtmlAgilityPack;
using Stateless;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Web;

namespace AutoGenerateContent.ViewModel
{
    public partial class MainWindowViewModel : ObservableObject
    {
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        public string WebView2Profile;
        public bool WebvViewDone = false;
        System.Timers.Timer timer = new System.Timers.Timer(TimeSpan.FromSeconds(1));
        TimeSpan span;

        [ObservableProperty]
        ObservableCollection<string> webView2List;

        [ObservableProperty]
        StateMachine<State, Trigger> stateMachine;

        [ObservableProperty]
        bool auto = true;
        
        [ObservableProperty]
        string prompt;

        [ObservableProperty]
        string time;

        [ObservableProperty]
        SideBarViewModel sidebar;

        public List<string> GoogleUrls = [];

        public List<string> GoogleContents = [];

        public List<string> SummaryContents = [];

        public MainWindowViewModel(SideBarViewModel sideBarViewModel)
        {
            Sidebar = sideBarViewModel;
            InitStateMachine();
            if (Directory.Exists(nameof(WebView2Profile)))
            {
                Directory.Delete(nameof(WebView2Profile), true);
            }
            Directory.CreateDirectory(nameof(WebView2Profile));

            timer.Elapsed += (s, e) =>
            {
                span = span.Add(TimeSpan.FromSeconds(1));
                Time = $"{span.Minutes.ToString("0#")}:{span.Seconds.ToString("0#")}";
            };
        }
       
        private void InitStateMachine()
        {
            StateMachine = new StateMachine<State, Trigger>(State.Start);

            StateMachine.Configure(State.Start)
                .Permit(Trigger.Next, State.Init)
                .OnEntryAsync(OnIdle);

            StateMachine.Configure(State.Init)
                .Permit(Trigger.Next, State.SearchKeyword)
                .OnEntry(OnStart);

            StateMachine.Configure(State.SearchKeyword)
                .Permit(Trigger.Next, State.ReadHtmlContent)
                .OnEntry(OnSearchKeyword);

            StateMachine.Configure(State.ReadHtmlContent)
                .Permit(Trigger.Next, State.Intro)
                .OnEntryAsync(() => OnReadHtmlContent(tokenSource.Token));

            StateMachine.Configure(State.Intro)
                .PermitReentry(Trigger.Loop)
                .Permit(Trigger.Next, State.AskChatGpt)
                .OnEntryAsync(OnIntro);

            StateMachine.Configure(State.AskChatGpt)
                .PermitReentry(Trigger.Loop)
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
            OnPropertyChanged(nameof(StateMachine));
            timer.Stop();
        }

        private void OnStart()
        {
            OnPropertyChanged(nameof(StateMachine));
            WeakReferenceMessenger.Default.Send<OnStart>(new(string.Empty));
            Auto = true;
            span = TimeSpan.Zero;
            timer.Start();
            GoogleUrls.Clear();
            GoogleContents.Clear();
            SummaryContents.Clear();
            WebView2List?.Clear();
            WebView2List = [Guid.NewGuid().ToString()];
        }
        
        private void OnSearchKeyword()
        {
            OnPropertyChanged(nameof(StateMachine));
            WeakReferenceMessenger.Default.Send<SearchKeyword>(new(Sidebar.SelectedConfig.SearchText));
        }
        
        private async Task OnReadHtmlContent(CancellationToken token)
        {   
            OnPropertyChanged(nameof(StateMachine));
            List<Task> tasks = new();
            foreach (var url in GoogleUrls)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using (HttpClient client = new HttpClient())
                        {
                            var response = await client.GetStringAsync(url, token);
                            var htmlDoc = new HtmlDocument();
                            htmlDoc.LoadHtml(response);
                            var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");
                            var unwantedNodes = bodyNode.SelectNodes("//header | //footer | //nav | //aside");
                            if (unwantedNodes != null)
                            {
                                foreach (var node in unwantedNodes)
                                {
                                    node.Remove();
                                }
                            }
                            string cleanedContent = HttpUtility.HtmlDecode(bodyNode.InnerText).Trim();
                            cleanedContent = cleanedContent.Replace("\t", "");
                            cleanedContent = string.Join(" ", cleanedContent.Split(" ", StringSplitOptions.RemoveEmptyEntries));
                            cleanedContent = string.Join("<br>", cleanedContent.Split(["\r", "\n"], StringSplitOptions.RemoveEmptyEntries));
                            cleanedContent = cleanedContent.Replace("\\", "")
                                                           .Replace("'", "")
                                                           .Replace("\"", "")
                                                           .Replace("\t", "");
                            if (string.IsNullOrWhiteSpace(cleanedContent) == false)
                            {
                                GoogleUrls.Remove(url);
                                if (cleanedContent.Length > 20000)
                                {
                                    GoogleContents.Add("tham khảo nguồn" + url);
                                }
                                else
                                {
                                    GoogleContents.Add(cleanedContent);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }, token));
            }
            Task.WaitAll([.. tasks], token);
            if (Auto)
            {
                await StateMachine.FireAsync(Trigger.Next);
            }
        }

        private async Task OnAskChatGpt()
        {
            OnPropertyChanged(nameof(StateMachine));
            if (GoogleContents.Count > 0)
            {
                WeakReferenceMessenger.Default.Send<AskChatGpt>(new(string.Format(Sidebar.SelectedConfig.PromptText, GoogleContents.FirstOrDefault())));
            }
            else if (Auto)
            {
                await StateMachine.FireAsync(Trigger.Next);
            }
        }
        
        private async Task OnIntro()
        {
            OnPropertyChanged(nameof(StateMachine));
            if (string.IsNullOrWhiteSpace(Sidebar.SelectedConfig.PromptIntro) == false)
            {
                WeakReferenceMessenger.Default.Send<AskChatGpt>(new(Sidebar.SelectedConfig.PromptIntro));
            }
            else if (Auto)
            {
                await StateMachine.FireAsync(Trigger.Next);
            }
        }
        
        private async Task OnSummaryContent()
        {
            OnPropertyChanged(nameof(StateMachine));
            if (string.IsNullOrWhiteSpace(Sidebar.SelectedConfig.PromptSummary) == false)
            {
                WeakReferenceMessenger.Default.Send<AskChatGpt>(new(string.Format(Sidebar.SelectedConfig.PromptSummary, SummaryContents.ToArray())));
            }
        }
        
        private async Task OnFinish()
        {
            OnPropertyChanged(nameof(StateMachine));
            if (Auto)
            {
                Auto = false;
                await StateMachine.FireAsync(Trigger.Start);
            }
        }

        [RelayCommand]
        public async Task Start()
        {
            if (StateMachine.CanFire(Trigger.Next))
            {
                await StateMachine.FireAsync(Trigger.Next);
            }
            else if (StateMachine.CanFire(Trigger.Start))
            {
                await StateMachine.FireAsync(Trigger.Start);
            }
        }

        [RelayCommand]
        public void ClearWebCache()
        {
            string newProfile = Path.Combine(nameof(WebView2Profile), Guid.NewGuid().ToString());
            WeakReferenceMessenger.Default.Send<StateChanged>(new((State.Start, newProfile)));
            WebView2Profile = newProfile;
        }
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
        Finish
    }

    public enum Trigger
    {
        Start,
        Next,
        Loop,
    }
}
