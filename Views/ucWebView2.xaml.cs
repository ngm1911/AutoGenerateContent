﻿using AutoGenerateContent.Event;
using AutoGenerateContent.ViewModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Serilog;
using Stateless;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace AutoGenerateContent.Views
{
    /// <summary>
    /// Interaction logic for ucWebView2.xaml
    /// </summary>
    public partial class ucWebView2 : UserControl
    {
        readonly MainWindowViewModel _viewModel;
        readonly SynchronizationContext? _syncContext;
        string _profilePath;

        public ucWebView2()
        {
            _viewModel = App.AppHost.Services.GetRequiredService<MainWindowViewModel>();

            InitializeComponent();

            _syncContext = SynchronizationContext.Current;

            WeakReferenceMessenger.Default.Register<SearchKeyword>(this, (r, m) => SearchKeyword(m.Value));

            WeakReferenceMessenger.Default.Register<AskChatGpt>(this, (r, m) => AskChatGpt(m.Value));

            WeakReferenceMessenger.Default.Register<StateChanged>(this, (r, m) =>
            {
                switch (m.Value.Item1)
                {
                    case State.Start:
                        _syncContext!.Post(async _ =>
                        {
                            await ReloadProfileWebView2(m.Value.Item2.ToString()!);
                        }, null);
                        break;
                }
            });

            WeakReferenceMessenger.Default.Register<OnStart>(this, (r, m) =>
            {
                WeakReferenceMessenger.Default.UnregisterAll(this);
                webView.Dispose();
                Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(t =>
                {
                    if (Directory.Exists(_profilePath))
                    {
                        Directory.Delete(_profilePath, true);
                    }
                });
            });

            _viewModel.ClearWebCacheCommand.Execute(null);
        }


        private async Task ReloadProfileWebView2(string newProfile)
        {
            _profilePath = newProfile;
            if (Directory.Exists(_profilePath) == false)
            {
                Directory.CreateDirectory(_profilePath);
            }

            //webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
            //webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;

            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _profilePath);
            await webView.EnsureCoreWebView2Async(environment);
            webView.CoreWebView2.Navigate("https://example.com");

            if (_viewModel.Auto)
            {
                await Task.Delay(1000);
                await _viewModel.StateMachine.FireAsync(Trigger.Next);
            }
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
        }

        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            webView.AllowDrop = false;
            webView.AllowExternalDrop = false;
            webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
            webView.CoreWebView2.Settings.IsPinchZoomEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
        }

        private void SearchKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword) == false)
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                _syncContext!.Post(_ =>
                {
                    webView.CoreWebView2.NavigationCompleted += navigateGoogle;
                    webView.CoreWebView2.Navigate($"https://www.google.com/search?q={UrlEncoder.Default.Encode(keyword)}");
                }, null);

                async void navigateGoogle(object sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    webView.CoreWebView2.NavigationCompleted -= navigateGoogle;

                    await Task.Delay(1000);
                    string script = @"
                                    let links = document.querySelectorAll('a');
                                    let hrefs = Array.from(links)
                                        .map(a => a.href)
                                        .filter(href => !href.includes('google'));
                                    hrefs;";

                    await webView.CoreWebView2.ExecuteScriptAsync(script).ContinueWith(async t =>
                    {
                        var hrefs = t.Result.Split(["[", "]", ",", "\""], StringSplitOptions.RemoveEmptyEntries);
                        _viewModel.GoogleUrls = hrefs.Distinct().Take(_viewModel.Sidebar.SelectedConfig.NumberUrls).ToList();
                        if (_viewModel.Auto)
                        {
                            await _viewModel.StateMachine.FireAsync(ViewModel.Trigger.Next);
                        }
                    });
                }
            }
        }

        private void AskChatGpt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt) == false)
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                string guid = Guid.NewGuid().ToString();
                string lastText = string.Empty;

                _syncContext!.Post(_ =>
                {
                    if (webView.CoreWebView2.Source.StartsWith("https://chatgpt.com") == false)
                    {
                        webView.CoreWebView2.NavigationCompleted += navigateChatGpt;
                        webView.CoreWebView2.Navigate("https://chatgpt.com/");
                    }
                    else
                    {
                        navigateChatGpt(null, null);
                    }
                }, null);

                async void navigateChatGpt(object? sender, CoreWebView2NavigationCompletedEventArgs? e)
                {
                    webView.CoreWebView2.NavigationCompleted -= navigateChatGpt;
                    webView.CoreWebView2.WebMessageReceived -= AnswerReceived;
                    webView.CoreWebView2.WebMessageReceived += AnswerReceived;

                    await Task.Delay(1000);
                    await InsertPromptAndSubmit(prompt, guid);
                }

                async void AnswerReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
                {
                    if ((args?.TryGetWebMessageAsString() ?? guid) == guid)
                    {
                        await tokenSource.CancelAsync();
                        tokenSource = new CancellationTokenSource();

                        await ProcessMessage(tokenSource.Token);
                    }
                    else
                    {
                        webView.CoreWebView2.WebMessageReceived -= AnswerReceived;
                    }
                }

                async Task ProcessMessage(CancellationToken token)
                {
                    int retry = 5;
                TRY_AGAIN:
                    await Task.Delay(1000);
                    if (token.IsCancellationRequested == false)
                    {
                        await CloseDialogLogin();
                        await ClickButtonContinue();
                        var text = await webView.ExecuteScriptAsync(@"document.getElementsByClassName('group/conversation-turn')[document.getElementsByClassName('group/conversation-turn').length - 1].innerText");
                        if (token.IsCancellationRequested == false && guid != "Finihshed")
                        {
                            if (text.Contains("I prefer this response"))
                            {
                                await ClickButtonPrefer();
                            }

                            if (text.EndsWith("4o mini\"")
                                || retry <= 0
                                || text.Contains("The message you submitted was too long"))
                            {
                                if (_viewModel.Auto)
                                {
                                    switch (_viewModel.StateMachine.State)
                                    {
                                        case State.AskChatGpt:
                                            guid = "Finihshed";
                                            if (_viewModel.GoogleContents.Count > 0)
                                            {
                                                if (retry > 0)
                                                {
                                                    _viewModel.SummaryContents.Add(text);
                                                }
                                                _viewModel.GoogleContents.RemoveAt(0);
                                            }
                                            if (_viewModel.StateMachine.CanFire(ViewModel.Trigger.Loop))
                                            {
                                                await _viewModel.StateMachine.FireAsync(ViewModel.Trigger.Loop);
                                            }
                                            break;

                                        case State.Intro:
                                            guid = "Finihshed";
                                            await _viewModel.StateMachine.FireAsync(ViewModel.Trigger.Next);
                                            break;

                                        case State.SummaryContent:
                                            var html = GetHtmlRegex().Match(text.Replace("\\u003C", "<").Replace("\\n", ""));
                                            Log.Logger.Information(html.Value);
                                            if (string.IsNullOrWhiteSpace(html.Value) == false)
                                            {
                                                guid = "Finihshed";
                                                await Task.Delay(1000);
                                                webView.NavigateToString(html.Value);
                                                await _viewModel.StateMachine.FireAsync(ViewModel.Trigger.Next);
                                            }
                                            else
                                            {
                                                goto TRY_AGAIN;
                                            }
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                if (lastText == text)
                                {
                                    retry--;
                                }
                                lastText = text;
                                goto TRY_AGAIN;
                            }
                        }
                    }
                }

                async Task InsertPromptAndSubmit(string prompt, string guid)
                {
                    await ClickButtonSkipLogin();
                    await CloseDialogLogin();
                    await webView.ExecuteScriptAsync($@"document.querySelector('div[id=""prompt-textarea""]').innerHTML='{prompt}';");

                    await CloseDialogLogin();
                    await webView.ExecuteScriptAsync($@"document.querySelector('button[data-testid=""send-button""]').click();");

                    await CloseDialogLogin();

                    await ListenAnwser(guid);
                }

                async Task CloseDialogLogin()
                {
                    await Task.Delay(500);
                    await webView.ExecuteScriptAsync(@"Array.from(document.querySelectorAll('a')).findLast(x => x.innerText === ""Stay logged out"")?.click();");
                }

                async Task ClickButtonPrefer()
                {
                    await Task.Delay(500);
                    await webView.ExecuteScriptAsync(@"Array.from(document.querySelectorAll('button')).findLast(x => x.innerText === ""I prefer this response"").click()");
                }

                async Task ClickButtonContinue()
                {
                    await Task.Delay(500);
                    await webView.ExecuteScriptAsync(@"Array.from(document.querySelectorAll('button')).findLast(x => x.innerText === ""Continue generating"").click()");
                }

                async Task ClickButtonSkipLogin()
                {
                    await Task.Delay(500);
                    await webView.ExecuteScriptAsync(@"Array.from(document.querySelectorAll('button')).findLast(x => x.innerText === ""Try it first"").click()");
                }

                async Task ListenAnwser(string guid)
                {
                    string script = $@"
                    const callback = function(mutationsList, observer) {{
                        for(let mutation of mutationsList) {{
                            if (mutation.type === 'childList') {{
                                window.chrome.webview.postMessage('{guid}');
                            }}
                            else if (mutation.type === 'characterData') {{
                                window.chrome.webview.postMessage(mutation.target.data);
                            }}
                        }}
                    }};
                    const observer = new MutationObserver(callback);
                    setTimeout(() => {{
                        Array.from(document.querySelectorAll('a')).findLast(x => x.innerText === ""Stay logged out"")?.click();
                        observer.observe(document.body, {{attributes: true, childList: true, subtree: true }});
                    }}, 100);
                    ";

                    await webView.ExecuteScriptAsync(script);
                    AnswerReceived(null, null);
                }
            }
        }

        [GeneratedRegex(@"<html.*?>.*?</html>")]
        private static partial Regex GetHtmlRegex();
    }
}