using System.Net;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Abstractions;

namespace AI.LLM.Clients.GoogleAIStudio;

public class GoogleAIStudioApi : ChatLLMApi
{
    public GoogleAIStudioApi(string apiKey, string modelName, IStreamHandler streamSender = null, string prompt = "", IEnumerable<WebProxy> proxies = null) : base(apiKey: apiKey, modelName: modelName, prompt: prompt, streamSender: streamSender, proxies: proxies)
    {
        ApiUrl = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
    }
}
