using AutoGenerateContent.Event;
using AutoGenerateContent.ViewModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Serilog.Core;
using System.IO;
using System.Text.Encodings.Web;
using System.Windows;

namespace AutoGenerateContent
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly SynchronizationContext? _syncContext;
        readonly MainWindowViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = _viewModel = App.AppHost.Services.GetRequiredService<MainWindowViewModel>();


            _syncContext = SynchronizationContext.Current;

            WeakReferenceMessenger.Default.Register<SearchKeyword>(this, async (r, m) =>
            {
                if (string.IsNullOrWhiteSpace(m.Value) == false)
                {
                    CancellationTokenSource tokenSource = new CancellationTokenSource();
                    _syncContext!.Post(_ =>
                    {
                        webView.CoreWebView2.NavigationCompleted += navigateGoogle;
                        webView.CoreWebView2.Navigate($"https://www.google.com/search?q={UrlEncoder.Default.Encode(m.Value)}");
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
                            _viewModel.GoogleUrls = hrefs.Distinct().Take(5).ToList();
                            if (_viewModel.Auto)
                            {
                                await _viewModel.StateMachine.FireAsync(ViewModel.Trigger.Next);
                            }
                        });
                    }
                }
            });

            WeakReferenceMessenger.Default.Register<AskChatGpt>(this, async (r, m) =>
            {
                if (string.IsNullOrWhiteSpace(m.Value) == false)
                {
                    CancellationTokenSource tokenSource = new CancellationTokenSource();
                    bool isFinished = false;
                    int retry = 5;

                    _syncContext!.Post(_ =>
                    {
                        isFinished = false;
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

                    async void navigateChatGpt(object sender, CoreWebView2NavigationCompletedEventArgs e)
                    {
                        webView.CoreWebView2.NavigationCompleted -= navigateChatGpt;
                        webView.CoreWebView2.WebMessageReceived -= AnswerReceived;
                        webView.CoreWebView2.WebMessageReceived += AnswerReceived;

                        await Task.Delay(1000);
                        await InsertPromptAndSubmit(m.Value, sender is not null);
                    }

                    async void AnswerReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
                    {
                        await tokenSource.CancelAsync();
                        tokenSource = new CancellationTokenSource();

                        if (isFinished)
                        {
                            webView.CoreWebView2.WebMessageReceived -= AnswerReceived;
                        }

                        var rawMessage = args.TryGetWebMessageAsString();
                        await ProcessMessage(tokenSource.Token, rawMessage);
                    }

                    async Task ProcessMessage(CancellationToken token, string message)
                    {
                        await Task.Delay(1000);
                        if (!token.IsCancellationRequested)
                        {
                            await CloseDialogLogin();
                            await webView.ExecuteScriptAsync(@"document.getElementsByClassName('group/conversation-turn')[document.getElementsByClassName('group/conversation-turn').length - 1].innerText")
                                    .ContinueWith(async t =>
                                    {
                                        if ((t.Result.EndsWith("mini\"") && !isFinished) || retry < 0)
                                        {
                                            isFinished = true;
                                            Serilog.Log.Logger.Information($"Start ProcessMessage with {_viewModel.GoogleContents.Count}");

                                            if (_viewModel.GoogleContents.Count > 0)
                                            {
                                                _viewModel.SummaryContents.Add(t.Result);
                                                _viewModel.GoogleContents.RemoveAt(0);
                                                if (_viewModel.StateMachine.CanFire(ViewModel.Trigger.Loop))
                                                {
                                                    await _viewModel.StateMachine.FireAsync(ViewModel.Trigger.Loop);
                                                }
                                            }

                                            if (_viewModel.Auto && _viewModel.GoogleContents.Count == 0)
                                            {
                                                await _viewModel.StateMachine.FireAsync(ViewModel.Trigger.Next);
                                                Serilog.Log.Logger.Information(ViewModel.Trigger.Next.ToString());
                                            }

                                            Serilog.Log.Logger.Information($"End ProcessMessage with {_viewModel.GoogleContents.Count}");
                                        }
                                        else
                                        {
                                            retry--;
                                        }
                                    });

                        }
                    }

                    async Task InsertPromptAndSubmit(string prompt, bool firstTime)
                    {
                        await Task.Delay(100);
                        await CloseDialogLogin();
                        await webView.ExecuteScriptAsync($@"document.querySelector('div[id=""prompt-textarea""]').innerHTML='{prompt}';");

                        await Task.Delay(100);
                        await CloseDialogLogin();
                        await webView.ExecuteScriptAsync($@"document.querySelector('button[data-testid=""send-button""]').click();");

                        await Task.Delay(100);
                        await CloseDialogLogin();

                        await Task.Delay(500);
                        await ListenAnwser();
                    }

                    async Task CloseDialogLogin()
                    {
                        await webView.ExecuteScriptAsync(@"Array.from(document.querySelectorAll('a')).findLast(x => x.innerText === ""Stay logged out"")?.click();");
                        await Task.Delay(50);
                    }

                    async Task ListenAnwser()
                    {
                        string script = $@"
                    const callback = function(mutationsList, observer) {{
                        for(let mutation of mutationsList) {{
                            if (mutation.type === 'childList') {{
                                console.log('Child list changed. New content:', mutation.target.innerText);
                                window.chrome.webview.postMessage('New content');
                            }}
                            else if (mutation.type === 'characterData') {{
                                console.log('Text content changed. New text:', mutation.target.data);
                                window.chrome.webview.postMessage(mutation.target.data);
                            }}
                        }}
                    }};
                    const observer = new MutationObserver(callback);
                    setTimeout(() => {{
                        Array.from(document.querySelectorAll('a')).findLast(x => x.innerText === ""Stay logged out"")?.click();
                        observer.observe(document.body, {{attributes: true, childList: true, subtree: true }});
                    }}, 500);
                    ";

                        await webView.ExecuteScriptAsync(script);
                    }
                }
            });
        }

        protected override async void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            await webView.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(
                null,
                tempPath
            ));
            webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
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
    }
}