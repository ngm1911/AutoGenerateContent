using AutoGenerateContent.Event;
using CommunityToolkit.Mvvm.Messaging;
using HtmlAgilityPack;
using System.Net.Http;
using System.Web;

namespace AutoGenerateContent.Services
{
    public class ChatGptWebProcessService
    {
        public Task<string> OnAskChatGpt(string promptText, string googleContents, CancellationToken token)
        {   
            WeakReferenceMessenger.Default.Send<AskChatGpt>(new(string.Format(promptText, googleContents), token));
            return Task.FromResult(string.Empty);
        }

        public Task<string> OnAskTitle(string title, CancellationToken token)
        {
            WeakReferenceMessenger.Default.Send<AskChatGpt>(new(title, token));
            return Task.FromResult(string.Empty);
        }
        
        public Task<string> OnAskHeading(string promptHeading, string heading, CancellationToken token)
        {
            WeakReferenceMessenger.Default.Send<AskChatGpt>(new(heading, token));
            return Task.FromResult(string.Empty);
        }

        public Task OnFinish()
        {
            throw new NotImplementedException();
        }

        public Task OnIdle()
        {
            throw new NotImplementedException();
        }

        public Task<bool> OnIntro(string intro, CancellationToken token)
        {
            WeakReferenceMessenger.Default.Send<AskChatGpt>(new(intro, token));
            return Task.FromResult(false);
        }

        public Task<string> OnReadHtmlContent(string url, List<string> GoogleUrls, List<string> GoogleContents, CancellationToken token)
        {
            return Task.Run<string>(async () =>
            {
                string cleanedContent = string.Empty;
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        var response = await client.GetStringAsync(url, token);
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(response);
                        var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");
                        var unwantedNodes = bodyNode.SelectNodes("//header | //footer | //nav | //aside | //div[contains(@class, 'ad')]");
                        if (unwantedNodes != null)
                        {
                            foreach (var node in unwantedNodes)
                            {
                                node.Remove();
                            }
                        }
                        cleanedContent = HttpUtility.HtmlDecode(bodyNode.InnerText).Trim();
                        cleanedContent = cleanedContent.Replace("\t", "");
                        cleanedContent = string.Join(" ", cleanedContent.Split(" ", StringSplitOptions.RemoveEmptyEntries));
                        cleanedContent = string.Join("<br>", cleanedContent.Split(["\r", "\n"], StringSplitOptions.RemoveEmptyEntries));
                        cleanedContent = cleanedContent.Replace("\\", "")
                                                       .Replace("'", "")
                                                       .Replace("\"", "")
                                                       .Replace("\t", "");
                    }
                }
                catch (Exception ex)
                {

                }
                return cleanedContent;
            });
        }

        public Task<bool> OnSearchImage(string searchImageText, string htmlContent, CancellationToken token)
        {
            WeakReferenceMessenger.Default.Send<SearchImage>(new(new(searchImageText, htmlContent), token));
            return Task.FromResult(false);
        }

        public Task OnSearchKeyword()
        {
            throw new NotImplementedException();
        }

        public Task OnStart(string apiKey)
        {
            return Task.CompletedTask;
        }

        public Task<string> OnSummaryContent(string promptSummary, List<string> summaryContents, CancellationToken token)
        {
            WeakReferenceMessenger.Default.Send<AskChatGpt>(new(promptSummary, token));
            return Task.FromResult(string.Empty);
        }
    }
}
