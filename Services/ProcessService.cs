using AutoGenerateContent.Interface;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoGenerateContent.Services
{
    public enum ProcessMode
    {
        ChatGptWeb,
        GeminiApi
    }

    public class ProcessService(IServiceProvider serviceProvider) : IProcessService
    {
        ProcessMode? _mode;
        IProcessService _chatGptWeb;
        IProcessService _geminiApi;
        IProcessService _processService
        {
            get
            {
                return _mode switch
                {
                    ProcessMode.GeminiApi => _geminiApi ??= serviceProvider.GetRequiredService<GeminiApiProcessService>(),

                    _ => _chatGptWeb ??= serviceProvider.GetRequiredService<ChatGptWebProcessService>(),
                };
            }
        }

        public void UpdateMode(ProcessMode? mode)
        {
            _mode = mode;
        }

        public Task<string> OnAskChatGpt(string promptText, string googleContents, CancellationToken token) => _processService.OnAskChatGpt(promptText, googleContents, token);

        public Task<string> OnAskTitle(string intro, CancellationToken token) => _processService.OnAskTitle(intro, token);

        public Task OnFinish() => _processService.OnFinish();

        public Task OnIdle() => _processService.OnIdle();

        public Task<bool> OnIntro(string intro, CancellationToken token) => _processService.OnIntro(intro, token);

        public Task<string> OnReadHtmlContent(string url, List<string> GoogleUrls, List<string> GoogleContents, CancellationToken token) => _processService.OnReadHtmlContent(url,  GoogleUrls, GoogleContents, token);

        public Task<bool> OnSearchImage(string searchImageText, string htmlContent, CancellationToken token) => _processService.OnSearchImage(searchImageText, htmlContent, token);

        public Task OnSearchKeyword() => _processService.OnSearchKeyword();

        public Task OnStart(string apiKey = null) => _processService.OnStart(apiKey);

        public Task<string> OnSummaryContent(string promptSummary, List<string> summaryContents, CancellationToken token) => _processService.OnSummaryContent(promptSummary, summaryContents, token);
    }
}