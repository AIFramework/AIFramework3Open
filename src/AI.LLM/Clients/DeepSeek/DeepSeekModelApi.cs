using System.Net;
using AI.LLM.Clients.Base;

namespace AI.LLM.Clients.DeepSeek
{
    /// <summary>
    /// Работа с API DeepSeek
    /// </summary>
    public class DeepSeekModelApi : ChatLLMApi
    {
        public DeepSeekModelApi(string apiKey, string modelName, string prompt = "", IEnumerable<WebProxy> proxies = null)
            : base(apiKey: apiKey, modelName: modelName, prompt: prompt, proxies: proxies)
        {
            ApiUrl = "https://api.deepseek.com/chat/completions";
            StreamOptions = new();
        }
    }
}

