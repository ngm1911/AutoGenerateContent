using AutoGenerateContent.Event;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoGenerateContent.Services
{
    public class ProcessService(GeminiApiProcessService _geminiApi, ChatGptWebProcessService _chatGptWeb)
    {
        public Task OnAskForHtml(string promptText, CancellationToken token)
        {
            string newContent = string.Format("Follow this context, update to html web page format: {0}", promptText);
            WeakReferenceMessenger.Default.Send<AskChatGpt>(new(newContent, token));
            return Task.CompletedTask;
        }

        public Task<string> OnAskTitle(string intro, CancellationToken token) => _geminiApi.OnAskTitle(intro, token);
        public Task<string> OnAskHeading(string promptHeading, string heading, CancellationToken token) => _geminiApi.OnAskHeading(promptHeading, heading, token);

        public Task OnFinish() => _geminiApi.OnFinish();

        public Task OnIdle() => _geminiApi.OnIdle();

        public Task OnIntro(string intro, CancellationToken token)
        {
            return _geminiApi.OnIntro(intro, token);
        }

        public Task OnReadHtmlContent(string url, CancellationToken token)
        {
            return _geminiApi.OnReadHtmlContent(url, token);
        }

        public Task<bool> OnSearchImage(string searchImageText, string htmlContent, CancellationToken token) => _chatGptWeb.OnSearchImage(searchImageText, htmlContent, token);

        public Task OnStart(string apiKey = null) => _geminiApi.OnStart(apiKey);

        public async Task<string> OnAskNewContent(string promptAskNewContent, CancellationToken token)
        {
            return await _geminiApi.OnAskNewContent(promptAskNewContent, token);
        }

        public async Task<string> OnAskFinalHtml(string htmlOriginal, CancellationToken token)
        {
            return await _geminiApi.OnAskFinalHtml(htmlOriginal, token);
        }
    }
}