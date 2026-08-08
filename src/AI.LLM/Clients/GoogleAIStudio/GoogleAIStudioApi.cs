using System.Net;
using AI.LLM.Clients.Base;

namespace AI.LLM.Clients.GoogleAIStudio;

public class GoogleAIStudioApi : ChatLLMApi
{
    public GoogleAIStudioApi(string apiKey, string modelName, string prompt = "", IEnumerable<WebProxy> proxies = null) : base(apiKey: apiKey, modelName: modelName, prompt: prompt, proxies: proxies)
    {
        ApiUrl = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
    }
}
