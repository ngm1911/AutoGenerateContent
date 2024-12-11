using AutoGenerateContent.Event;
using AutoGenerateContent.ViewModel;
using CommunityToolkit.Mvvm.Messaging;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Serilog;
using Stateless;
using System.IO;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows.Controls;
using static Google.Apis.Requests.BatchRequest;

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
        Random r = new();

        public ucWebView2()
        {
            _viewModel = App.AppHost.Services.GetRequiredService<MainWindowViewModel>();

            InitializeComponent();

            _syncContext = SynchronizationContext.Current;

            WeakReferenceMessenger.Default.Register<UpdateHtml>(this, async (r, m) =>
            {
                _syncContext!.Post(_ =>
                {
                    try
                    {
                        webView.CoreWebView2.NavigateToString(m.Value);
                    }
                    catch { }
                }, null);
            });
            
            WeakReferenceMessenger.Default.Register<SearchImage>(this, async (r, m) =>
            {
                await SearchImage(m.Value.Item1, m.Value.Item2, 0, m.Token);
            });

            WeakReferenceMessenger.Default.Register<SearchKeyword>(this, (r, m) =>
            {
                SearchKeyword(m.Value, m.Token);
            });

            WeakReferenceMessenger.Default.Register<AskChatGpt>(this, (r, m) =>
            {
                AskChatGpt(m.Value, m.Token);
            });

            WeakReferenceMessenger.Default.Register<StateChanged>(this, (r, m) =>
            {
                switch (m.Value.Item1)
                {
                    case State.Start:
                        _syncContext!.Post(async _ =>
                        {
                            await ReloadProfileWebView2(m.Value.Item2.ToString()!, m.Token);
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


        private async Task ReloadProfileWebView2(string newProfile, CancellationToken token)
        {
            _profilePath = newProfile;
            if (Directory.Exists(_profilePath) == false)
            {
                Directory.CreateDirectory(_profilePath);
            }

#if DEBUG

#else
webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
#endif

            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _profilePath);
            await webView.EnsureCoreWebView2Async(environment);
            webView.CoreWebView2.Navigate("https://example.com");

            await Task.Delay(r.Next(500, 1000), token);
            await _viewModel.StateMachine.FireAsync(Trigger.Next, token);
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

        private void SearchKeyword(string keyword, CancellationToken token)
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

                    await Task.Delay(r.Next(1000, 1200), token);
                    string script = @"
                                    let links = document.querySelectorAll('a');
                                    let hrefs = Array.from(links)
                                        .map(a => a.href)
                                        .filter(href => !href.includes('google') && !href.includes('-'));
                                    hrefs;";

                    await webView.CoreWebView2.ExecuteScriptAsync(script).ContinueWith(async t =>
                    {
                        var hrefs = t.Result.Split(["[", "]", ",", "\""], StringSplitOptions.RemoveEmptyEntries).Distinct();
                        List<string> hosts = [];
                        foreach(var href in hrefs)
                        {
                            if (Uri.TryCreate(href, new UriCreationOptions(), out Uri? uri))
                            {
                                if (hosts.Any(x => x == uri.Host) == false)
                                {
                                    hosts.Add(uri.Host);
                                    _viewModel.GoogleUrls.Add(href);
                                }
                            }
                        }
                        await _viewModel.StateMachine.FireAsync(ViewModel.Trigger.Next, token);
                    });
                }
            }
        }

        private async Task SearchImage(string searchImageText, string html, int idx, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(searchImageText) == false)
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                var headings = doc.DocumentNode.SelectNodes("//h2");
                HtmlNode heading;
                if (headings?.Count > idx)
                {
                    heading = headings[idx];
                    _syncContext!.Post(_ =>
                    {
                        webView.CoreWebView2.NavigationCompleted += searchGoogle;
                        webView.CoreWebView2.Navigate($"https://www.google.com/search?q={UrlEncoder.Default.Encode(string.Format(searchImageText, heading.InnerText))}");
                    }, null);                    
                }
                else
                {
                    html = html.Replace("\\\"", "\"");
                    _viewModel.HtmlContent = html;
                    await File.WriteAllTextAsync(Path.Combine("Output", $"{Guid.NewGuid()}.html"), html, token);
                    await Task.Delay(r.Next(400, 600), token);
                    webView.CoreWebView2.NavigateToString(html);

                    await _viewModel.StateMachine.FireAsync(ViewModel.Trigger.Next, token);
                }

                async void searchGoogle(object sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    webView.CoreWebView2.NavigationCompleted -= searchGoogle;
                    webView.CoreWebView2.NavigationCompleted += navigationImageTab;

                    await Task.Delay(r.Next(400, 600), token);
                    await webView.ExecuteScriptAsync(@"Array.from(document.querySelectorAll('a')).findLast(x => x.innerText === ""Hình ảnh"" | x.innerText === ""Images"").click()");
                }
                
                async void navigationImageTab(object sender, CoreWebView2NavigationCompletedEventArgs e)
                {
                    webView.CoreWebView2.NavigationCompleted -= navigationImageTab;

                    await Task.Delay(r.Next(400, 600), token);
                    await webView.ExecuteScriptAsync(@$"Array.from(document.querySelectorAll('img[class=""YQ4gaf""]'))[{r.Next(0, 4)}].click()");

                    await Task.Delay(r.Next(800, 1200), token);
                    var imageUrl = await webView.ExecuteScriptAsync(@"Array.from(document.querySelectorAll('a[class=""YsLeY""]'))[1].firstChild[""src""]");

                    var img = doc.CreateElement("img");
                    img.SetAttributeValue("src", imageUrl.Replace("\"", ""));
                    img.SetAttributeValue("style", "width:500px;height:500px;");
                    heading.ParentNode.InsertAfter(img, heading);

                    string modifiedHtml = doc.DocumentNode.OuterHtml;
                    await SearchImage(searchImageText, modifiedHtml, idx + 1, token);
                }
            }
        }

        private void AskChatGpt(string prompt, CancellationToken token)
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

                    if (token.IsCancellationRequested == false)
                    {
                        await Task.Delay(r.Next(1500, 2000), token);
                        await InsertPromptAndSubmit(prompt, guid);
                    }
                }

                async void AnswerReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
                {
                    if ((args?.TryGetWebMessageAsString() ?? guid) == guid  && !token.IsCancellationRequested)
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
                    int retry = 3;
                TRY_AGAIN:
                    await Task.Delay(1000);
                    if (token.IsCancellationRequested == false)
                    {
                        await CloseDialogLogin();
                        await ClickButtonContinue();
                        await ClickButtonPrefer();
                        var text = await webView.ExecuteScriptAsync(@"document.getElementsByClassName('group/conversation-turn')[document.getElementsByClassName('group/conversation-turn').length - 1].innerText");
                        if (token.IsCancellationRequested == false && guid != "Finihshed")
                        {
                            if (text.EndsWith("4o mini\"")
                                || retry <= 0
                                || text.Contains("The message you submitted was too long"))
                            {
                                switch (_viewModel.StateMachine.State)
                                {
                                    case State.AskNewContent:
                                        var html = GetHtmlRegex().Matches(text.Replace("\\u003C", "<").Replace("\\n", "")).LastOrDefault();
                                        Log.Logger.Information(html?.Value);
                                        if (string.IsNullOrWhiteSpace(html?.Value) == false)
                                        {
                                            guid = "Finihshed";
                                            await Task.Delay(r.Next(200, 500));
                                            _viewModel.HtmlContent = html.Value;
                                            _viewModel.Heading = GetH2Regex().Matches(_viewModel.HtmlContent).Select(x => x.Value).ToList();
                                            if(_viewModel.Heading.Count == 1)
                                            {
                                                _viewModel.Heading = new Regex(@"<h3.*?>.+?</h3>").Matches(_viewModel.HtmlContent).Select(x => x.Value).ToList();
                                            }
                                            await _viewModel.StateMachine.FireAsync(ViewModel.Trigger.Next);
                                        }
                                        else
                                        {
                                            goto TRY_AGAIN;
                                        }
                                        break;


                                    case State.AskHeading:
                                        var htmlHeading = GetHtmlRegex().Matches(text.Replace("\\u003C", "<").Replace("\\n", "")).LastOrDefault();
                                        Log.Logger.Information(htmlHeading?.Value);
                                        if (string.IsNullOrWhiteSpace(htmlHeading?.Value) == false)
                                        {
                                            guid = "Finihshed";
                                            await Task.Delay(r.Next(200, 500));
                                            var htmlDoc = new HtmlDocument();
                                            htmlDoc.LoadHtml(htmlHeading.Value);
                                            var newHeading = htmlDoc.DocumentNode.SelectSingleNode("//body");
                                            var unwantedNodes = newHeading.SelectNodes("//header | //footer");
                                            if (unwantedNodes != null)
                                            {
                                                foreach (var node in unwantedNodes)
                                                {
                                                    node.Remove();
                                                }
                                            }

                                            var first = _viewModel.Heading.FirstOrDefault();
                                            var second = _viewModel.Heading.Skip(1).FirstOrDefault();
                                            string oldText = string.Empty;
                                            try
                                            {
                                                oldText = _viewModel.HtmlContent.Substring(_viewModel.HtmlContent.IndexOf(first) + first.Length,
                                                    ((second is null || _viewModel.HtmlContent.IndexOf(second) == -1) ? _viewModel.HtmlContent.LastIndexOf("</body>") : _viewModel.HtmlContent.IndexOf(second)) - _viewModel.HtmlContent.IndexOf(first) - first.Length);
                                            }
                                            catch
                                            {

                                            }
                                            _viewModel.HtmlContent = _viewModel.HtmlContent.Replace(oldText, newHeading.InnerHtml.Trim());
                                            WeakReferenceMessenger.Default.Send<UpdateHtml>(new(_viewModel.HtmlContent, token));

                                            _viewModel.Heading.RemoveAt(0);
                                            await _viewModel.StateMachine.FireAsync(Trigger.Loop);
                                        }
                                        else
                                        {
                                            goto TRY_AGAIN;
                                        }
                                        break;

                                    case State.AskTitle:
                                        var title = GetTitleRegex().Matches(text.Replace("\\u003C", "<").Replace("\\n", "")).LastOrDefault();
                                        Log.Logger.Information(title?.Value);
                                        if (string.IsNullOrWhiteSpace(title?.Value) == false)
                                        {
                                            guid = "Finihshed";
                                            await Task.Delay(r.Next(200, 500));
                                            var oldTitle = GetTitleRegex().Match(_viewModel.HtmlContent);
                                            var h1Title = GetH1Regex().Match(_viewModel.HtmlContent);
                                            _viewModel.HtmlContent = _viewModel.HtmlContent.Replace(oldTitle.Value, title?.Value).Replace(h1Title.Value, title?.Value.Replace("title", "h1"));
                                            await _viewModel.StateMachine.FireAsync(ViewModel.Trigger.Next);
                                        }
                                        else if (retry == 0)
                                        {
                                            await _viewModel.StateMachine.FireAsync(ViewModel.Trigger.Next);
                                        }
                                        else
                                        {
                                            goto TRY_AGAIN;
                                        }
                                        break;


                                    case State.SearchImage:
                                        var finalHtml = GetHtmlRegex().Matches(text.Replace("\\u003C", "<").Replace("\\n", "")).LastOrDefault();
                                        Log.Logger.Information(finalHtml?.Value);
                                        if (string.IsNullOrWhiteSpace(finalHtml?.Value) == false)
                                        {
                                            guid = "Finihshed";
                                            await Task.Delay(r.Next(200, 500));
                                            _viewModel.HtmlContent = finalHtml.Value;
                                            WeakReferenceMessenger.Default.Send<SearchImage>(new(new(_viewModel.Sidebar.SelectedConfig.SearchImageText, _viewModel.HtmlContent), token));
                                        }
                                        else
                                        {
                                            goto TRY_AGAIN;
                                        }
                                        break;
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
                    await webView.ExecuteScriptAsync($@"document.querySelector('div[id=""prompt-textarea""]').innerHTML='{prompt.Replace("\n", "<br>")}';");

                    await CloseDialogLogin();
                    await webView.ExecuteScriptAsync($@"document.querySelector('button[data-testid=""send-button""]').click();");

                    await CloseDialogLogin();

                    await ListenAnwser(guid);
                }

                async Task CloseDialogLogin()
                {
                    await Task.Delay(r.Next(200, 800), token);
                    await webView.ExecuteScriptAsync(@"Array.from(document.querySelectorAll('a')).findLast(x => x.innerText === ""Stay logged out"")?.click();");
                }

                async Task ClickButtonPrefer()
                {
                    await Task.Delay(r.Next(200, 800), token);
                    await webView.ExecuteScriptAsync(@"Array.from(document.querySelectorAll('button')).findLast(x => x.innerText === ""I prefer this response"").click()");
                }

                async Task ClickButtonContinue()
                {
                    await Task.Delay(r.Next(200, 800), token);
                    await webView.ExecuteScriptAsync(@"Array.from(document.querySelectorAll('button')).findLast(x => x.innerText === ""Continue generating"").click()");
                }

                async Task ClickButtonSkipLogin()
                {
                    await Task.Delay(r.Next(200, 800), token);
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

        [GeneratedRegex(@"<html.*?>.+?</html>")]
        private static partial Regex GetHtmlRegex();

        [GeneratedRegex(@"<title\b[^>]*>([^<]*)<\/title>(?!.*<\/title>)")]
        private static partial Regex GetTitleRegex();

        [GeneratedRegex(@"<h1.*?>.+?</h1>")]
        private static partial Regex GetH1Regex();

        [GeneratedRegex(@"<h2.*?>.+?</h2>")]
        private static partial Regex GetH2Regex();
    }
}
