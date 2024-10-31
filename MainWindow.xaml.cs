using AutoGenerateContent.Event;
using AutoGenerateContent.ViewModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Windows;

namespace AutoGenerateContent
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly SynchronizationContext? _syncContext;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = App.AppHost.Services.GetRequiredService<MainWindowViewModel>();


            _syncContext = SynchronizationContext.Current;

            WeakReferenceMessenger.Default.Register<SearchKeyword>(this, async (r, m) =>
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                _syncContext!.Post(_ =>
                {
                    webView.CoreWebView2.NavigationCompleted += navigateGoogle;
                    webView.CoreWebView2.Navigate("https://www.google.com/");
                }, null);

                async void navigateGoogle(object sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    webView.CoreWebView2.NavigationCompleted -= navigateGoogle;

                    //await InsertPromptAndSubmit(m.Value);

                    //webView.CoreWebView2.WebMessageReceived -= AnswerReceived;
                    //webView.CoreWebView2.WebMessageReceived += AnswerReceived;
                }

            });

            WeakReferenceMessenger.Default.Register<AskChatGpt>(this, async (r, m) =>
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                _syncContext!.Post(_ =>
                {
                    webView.CoreWebView2.NavigationCompleted += navigateChatGpt;
                    webView.CoreWebView2.Navigate("https://chatgpt.com/");
                }, null);

                async void navigateChatGpt(object sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    webView.CoreWebView2.NavigationCompleted -= navigateChatGpt;

                    await InsertPromptAndSubmit(m.Value);

                    webView.CoreWebView2.WebMessageReceived -= AnswerReceived;
                    webView.CoreWebView2.WebMessageReceived += AnswerReceived;
                }

                async void AnswerReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
                {
                    tokenSource?.Cancel();
                    tokenSource = new CancellationTokenSource();
                    var rawMessage = args.TryGetWebMessageAsString();
                    if (rawMessage == "No target")
                    {
                        await ListenAnwser();
                    }
                    else
                    {
                        ProcessMessage(tokenSource.Token, rawMessage);
                    }
                }

                async void ProcessMessage(CancellationToken token, string message)
                {
                    await Task.Delay(1000);
                    if (!token.IsCancellationRequested && message.EndsWith("mini"))
                    {
                        await CloseDialogLogin();

                        var text = await webView.ExecuteScriptAsync(@"let targets = document.getElementsByClassName('group/conversation-turn');
                                                                      targets[targets.length - 1].innerText");
                        if (string.IsNullOrWhiteSpace(text) && text != "null")
                        {
                            MessageBox.Show(text);
                        }
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

        private async Task InsertPromptAndSubmit(string prompt)
        {
            await Task.Delay(100);
            await CloseDialogLogin();
            await webView.ExecuteScriptAsync($@"document.querySelector('div[id=""prompt-textarea""]').innerText='{prompt}';");

            await Task.Delay(100);
            await CloseDialogLogin();
            await webView.ExecuteScriptAsync($@"document.querySelector('button[data-testid=""send-button""]').click();");

            await Task.Delay(100);
            await CloseDialogLogin();

            await Task.Delay(1000);
            await ListenAnwser();
        }

        private async Task CloseDialogLogin()
        {
            await webView.ExecuteScriptAsync(@"Array.from(document.querySelectorAll('a')).findLast(x => x.innerText === ""Stay logged out"")?.click();");
            await Task.Delay(50);
        }
        
        private async Task ListenAnwser()
        {
            string script = $@"
                    let callback = function(mutationsList, observer) {{
                        for(let mutation of mutationsList) {{
                            if (mutation.type === 'childList') {{
                                console.log('Child list changed. New content:', mutation.target.innerText);
                                window.chrome.webview.postMessage(mutation.target.innerText);
                            }}
                            else if (mutation.type === 'characterData') {{
                                console.log('Text content changed. New text:', mutation.target.data);
                                window.chrome.webview.postMessage(mutation.target.data);
                            }}
                        }}
                    }};
                    let observer = new MutationObserver(callback);
                    setTimeout(() => {{
                        Array.from(document.querySelectorAll('a')).findLast(x => x.innerText === ""Stay logged out"")?.click();
                        let targets = document.getElementsByClassName('group/conversation-turn');
                        if (targets) {{
                            const config = {{ attributes: true, childList: true, subtree: true }};
                            for (let i = 0; i < targets.length; i++) {{
                                    observer.observe(targets[i], config);
                            }}
                            console.log('MutationObserver is now observing after 1 seconds delay.');
                        }} else {{
                            window.chrome.webview.postMessage('No target');
                        }}
                    }}, 500);
                    ";

            await webView.ExecuteScriptAsync(script);
        }
    }
}