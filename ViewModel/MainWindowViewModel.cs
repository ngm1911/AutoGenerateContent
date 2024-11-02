using AutoGenerateContent.Event;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.File;
using Stateless;
using System;
using System.Diagnostics.Metrics;
using System.Net.Http;
using System.Security.Policy;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows;

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

        [ObservableProperty]
        SideBarViewModel sidebar;

        public List<string> GoogleUrls = [];

        public List<string> GoogleContents = [];

        public List<string> SummaryContents = [];

        public MainWindowViewModel(SideBarViewModel sideBarViewModel)
        {
            Sidebar = sideBarViewModel;
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
                .Permit(Trigger.Next, State.ReadHtmlContent)
                .OnEntryAsync(OnSearchKeyword);

            StateMachine.Configure(State.ReadHtmlContent)
                .Permit(Trigger.Next, State.AskChatGpt)
                .OnEntryAsync(OnReadHtmlContent);

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
            await Task.Delay(1000);
            OnPropertyChanged(nameof(StateMachine));
        }

        private async Task OnStart()
        {
            GoogleUrls.Clear();
            GoogleContents.Clear();

            OnPropertyChanged(nameof(StateMachine));
            if (Auto)
            {
                await StateMachine.FireAsync(Trigger.Next);
            }
        }
        
        private async Task OnSearchKeyword()
        {   
            await Task.Delay(1000);
            OnPropertyChanged(nameof(StateMachine));
            if (Auto)
            {
                WeakReferenceMessenger.Default.Send<SearchKeyword>(new(Sidebar.SelectedConfig.SearchText));
            }
        }
        
        private async Task OnReadHtmlContent()
        {   
            OnPropertyChanged(nameof(StateMachine));
            List<Task> tasks = new();
            foreach (var url in GoogleUrls)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using (HttpClient client = new HttpClient())
                    {
                        var response = await client.GetStringAsync(url);
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
                        cleanedContent = string.Join("<br>", cleanedContent.Split([ "\r", "\n" ], StringSplitOptions.RemoveEmptyEntries));
                        cleanedContent = cleanedContent.Replace("\\", "")
                                                       .Replace("'", "")
                                                       .Replace("\"", "")
                                                       .Replace("\t", "");
                        if (string.IsNullOrWhiteSpace(cleanedContent) == false)
                        {
                            GoogleUrls.Remove(url);
                            GoogleContents.Add(cleanedContent);
                        }
                    }
                }));
            }
            await Task.WhenAll(tasks);
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
        }
        
        private async Task OnSummaryContent()
        {
            MessageBox.Show(string.Join(Environment.NewLine, SummaryContents));
            OnPropertyChanged(nameof(StateMachine));
            if (Auto)
            {
                await StateMachine.FireAsync(Trigger.Next);
            }
        }
        
        private async Task OnFinish()
        {
            await Task.Delay(1000);
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
    }

    public enum State
    {
        Start,
        Init,
        SearchKeyword,
        ReadHtmlContent,
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
