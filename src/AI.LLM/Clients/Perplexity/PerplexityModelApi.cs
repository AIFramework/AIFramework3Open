using System.Net;
using AI.LLM.Clients.Base;

namespace AI.LLM.Clients.Perplexity;

public class PerplexityModelApi : ChatLLMApi
{
    public PerplexityModelApi(string apiKey, string modelName, string prompt = "", IEnumerable<WebProxy> proxies = null) : base(apiKey: apiKey, modelName: modelName, prompt: prompt, proxies: proxies)
    {
        ApiUrl = "https://api.perplexity.ai/chat/completions";
    }
}
