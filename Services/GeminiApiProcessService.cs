using AutoGenerateContent.Event;
using AutoGenerateContent.Views;
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
    public partial class GeminiApiProcessService
    {
        GoogleAI googleAI;
        GenerativeModel model;
        Dictionary<string,string> files = new Dictionary<string, string>();

        public Task<string> OnAskChatGpt(string promptText, string googleContents, CancellationToken token)
        {
            return Task.FromResult(googleContents);
        }

        public async Task<string> OnAskTitle(string _title, CancellationToken token)
        {
            try
            {
                StringBuilder response = new StringBuilder();
                var responseStream = model.GenerateContentStream(_title);
                await foreach (var stream in responseStream)
                {
                    response.Append(stream.Text);
                    WeakReferenceMessenger.Default.Send<UpdateHtml>(new(response.ToString(), token));
                }

                var title = GetTitleRegex().Matches(response.ToString().Replace("\\u003C", "<").Replace("\\n", "").Replace("\n", "")).LastOrDefault();
                return title?.Value;
            }
            catch
            {

            }
            return "finish";
        }
        
        public async Task<string> OnAskHeading(string promptHeading, string heading, CancellationToken token)
        {
            int i = 1;
        Retry:
            try
            {
                string headingprompt = string.Format(promptHeading, heading);
                var answer = await model.GenerateContent(headingprompt);
                var h2New = string.Join("<br>", answer.Text.ReplaceLineEndings()
                                        .Split(Environment.NewLine)
                                        .Where(x => x.StartsWith("```") == false
                                                    && string.IsNullOrWhiteSpace(x) == false
                                                    && x.ToLower().Contains(heading.ToLower()) == false));

                await Task.Delay(3000);
                return h2New;
            }
            catch (Exception ex)
            {
                await Task.Delay(2000 * i);
                i++;
                if (i < 5)
                {
                    goto Retry;
                }
            }
            return string.Empty;
        }

        public Task OnFinish()
        {
            throw new NotImplementedException();
        }

        public Task OnIdle()
        {
            throw new NotImplementedException();
        }

        public async Task OnIntro(string intro, CancellationToken token)
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
            }
            catch(Exception ex)
            {

            }
        }

        public async Task OnReadHtmlContent(string url, CancellationToken token)
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
                    var mediaResponse = await googleAI.UploadFile(cleanedContent, cancellationToken: token);
                    files.Add(mediaResponse.Uri, mediaResponse.Name);
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                if (File.Exists(cleanedContent))
                {
                    File.Delete(cleanedContent);
                }
            }
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
            googleAI = new GoogleAI(apiKey);            
            model = googleAI.GenerativeModel();
            return Task.CompletedTask;
        }

        public async Task<string> OnAskFinalHtml(string htmlOriginal, CancellationToken token)
        {
            var cleanedContent = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.html");
            await File.WriteAllTextAsync(cleanedContent, htmlOriginal, token);
            var mediaResponse = await googleAI.UploadFile(cleanedContent, cancellationToken: token);
            Dictionary<string, string> files = new Dictionary<string, string>();
            files.Add(mediaResponse.Uri, mediaResponse.Name);

            int i = 1;
        Retry:
            try
            {
                var content = new GenerateContentRequest("Đọc file đính kèm, viết lại 1 hoàn chỉnh trang web, fix các lỗi heading, lỗi html cho tôi. Lưu ý phải giữ lại toàn bộ các nội dung của trang web, chỉ chỉnh sửa format cho đúng");
                foreach (var file in files)
                {
                    content.AddMedia(file.Key);
                }
                var answer = await model.GenerateContent(content);                
                return answer.Text;
            }
            catch (Exception ex)
            {
                await Task.Delay(2000 * i);
                i++;
                if (i < 5)
                {
                    goto Retry;
                }
            }
            finally
            {
                foreach (var file in files)
                {
                    googleAI.DeleteFile(file.Value);
                }
            }
            return string.Empty;
        }

        public async Task<string> OnAskNewContent(string promptAskNewContent, CancellationToken token)
        {
            int i = 1;
            Retry:
            try
            {
                var content = new GenerateContentRequest(promptAskNewContent);
                foreach (var file in files)
                {
                    content.AddMedia(file.Key);
                }
                var responseStream = model.GenerateContentStream(content);
                StringBuilder response = new StringBuilder();
                await foreach (var stream in responseStream)
                {
                    response.Append(stream.Text);
                    WeakReferenceMessenger.Default.Send<UpdateHtml>(new(response.ToString(), token));
                }
                return response.ToString();
            }
            catch (Exception ex)
            {
                await Task.Delay(2000 * i);
                i++;
                if (i < 5)
                {
                    goto Retry;
                }
            }
            finally
            {
                foreach (var file in files)
                {
                    googleAI.DeleteFile(file.Value);
                }
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