using System.Net;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Abstractions;

namespace AI.LLM.Clients.Perplexity;

public class PerplexityModelApi : ChatLLMApi
{
    public PerplexityModelApi(string apiKey, string modelName, IStreamHandler streamSender = null, string prompt = "", IEnumerable<WebProxy> proxies = null) : base(apiKey: apiKey, modelName: modelName, prompt: prompt, streamSender: streamSender, proxies: proxies)
    {
        ApiUrl = "https://api.perplexity.ai/chat/completions";
    }
}
