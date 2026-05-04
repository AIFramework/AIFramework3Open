using System.Net;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Abstractions;

namespace AI.LLM.Clients.DeepSeek
{
    /// <summary>
    /// Работа с API DeepSeek
    /// </summary>
    public class DeepSeekModelApi : ChatLLMApi
    {
        public DeepSeekModelApi(string apiKey, string modelName, IStreamHandler streamSender = null, string prompt = "", IEnumerable<WebProxy> proxies = null) 
            : base(apiKey: apiKey, modelName: modelName, prompt: prompt, streamSender: streamSender, proxies: proxies)
        {
            ApiUrl = "https://api.deepseek.com/chat/completions";
            StreamOptions = new();
        }
    }
}

