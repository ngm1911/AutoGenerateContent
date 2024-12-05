using AutoGenerateContent.Event;
using AutoGenerateContent.Interface;
using CommunityToolkit.Mvvm.Messaging;
using HtmlAgilityPack;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Google;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoGenerateContent.Services
{
    public partial class GeminiApiProcessService : IProcessService
    {
        GenerativeModel model;

        public Task<string> OnAskChatGpt(string promptText, string googleContents, CancellationToken token)
        {
            return Task.FromResult(googleContents);
        }

        public async Task<string> OnAskTitle(string intro, CancellationToken token)
        {
            try
            {
                StringBuilder response = new StringBuilder();
                var responseStream = model.GenerateContentStream(intro);
                await foreach (var stream in responseStream)
                {
                    response.Append(stream.Text);
                    WeakReferenceMessenger.Default.Send<UpdateHtml>(new(response.ToString(), token));
                }

                var title = GetTitleRegex().Matches(response.ToString().Replace("\\u003C", "<").Replace("\\n", "").Replace("\n", "")).LastOrDefault();
                return title?.Value ?? "finish";
            }
            catch
            {

            }
            return "finish";
        }

        public Task OnFinish()
        {
            throw new NotImplementedException();
        }

        public Task OnIdle()
        {
            throw new NotImplementedException();
        }

        public async Task<bool> OnIntro(string intro, CancellationToken token)
        {
            StringBuilder response = new StringBuilder();
            var responseStream = model.GenerateContentStream(intro);
            await foreach (var stream in responseStream)
            {
                response.Append(stream.Text);
                WeakReferenceMessenger.Default.Send<UpdateHtml>(new(response.ToString(), token));
            }
            return true;
        }

        public Task<string> OnReadHtmlContent(string url, List<string> GoogleUrls, List<string> GoogleContents, CancellationToken token)
        {
            return Task.Run<string>(async () =>
            {
                string cleanedContent = string.Empty;
                try
                {
                    cleanedContent = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.html");
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

                        htmlDoc.Save(cleanedContent);
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
            model = new GenerativeModel() { ApiKey = apiKey };
            return Task.CompletedTask;
        }

        public async Task<string> OnSummaryContent(string promptSummary, List<string> summaryContents, CancellationToken token)
        {
            try
            {
                var content = new GenerateContentRequest(promptSummary);
                foreach (var file in summaryContents)
                {
                    try
                    {
                        var fileUploaded = await model.UploadFile(file, cancellationToken: token);
                        content.AddMedia(fileUploaded.File);
                    }
                    catch (Exception ex)
                    {
                    }
                }
                var responseStream = model.GenerateContentStream(content);
                StringBuilder response = new StringBuilder();
                await foreach (var stream in responseStream)
                {
                    response.Append(stream.Text);
                    WeakReferenceMessenger.Default.Send<UpdateHtml>(new(response.ToString(), token));
                }

                model.ListFiles().ContinueWith(t =>
                {
                    foreach (var file in t.Result.Files)
                    {
                        model.DeleteFile(file.Name);
                    }
                    
                    foreach (var file in summaryContents)
                    {
                        if (File.Exists(file))
                            File.Delete(file);
                    }
                });

                var html = GetHtmlRegex().Matches(response.ToString().Replace("\\u003C", "<").Replace("\\n", "").Replace("\n", "")).LastOrDefault();
                return html?.Value;
            }
            catch (Exception ex)
            {

            }
            return string.Empty;
        }

        [GeneratedRegex(@"<html.*?>.+?</html>")]
        private static partial Regex GetHtmlRegex();

        [GeneratedRegex(@"<title\b[^>]*>([^<]*)<\/title>(?!.*<\/title>)")]
        private static partial Regex GetTitleRegex();

        [GeneratedRegex(@"<h1\b[^>]*>(.*?)<\/h1>(?!.*<h1\b)")]
        private static partial Regex GetH1Regex();
    }
}