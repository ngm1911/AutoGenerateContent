using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoGenerateContent.Interface
{
    internal interface IProcessService
    {
        Task OnIdle();
        Task OnStart(string apiKey);
        Task OnSearchKeyword();
        Task<string> OnReadHtmlContent(string url, List<string> GoogleUrls, List<string> GoogleContents, CancellationToken token);
        Task<bool> OnIntro(string intro, CancellationToken token);
        Task<string> OnAskChatGpt(string promptText, string googleContents, CancellationToken token);
        Task<string> OnSummaryContent(string promptSummary, List<string> summaryContents, CancellationToken token);
        Task<string> OnAskTitle(string intro, CancellationToken token);
        Task<bool> OnSearchImage(string searchImageText, string htmlContent, CancellationToken token);
        Task OnFinish();
    }
}
