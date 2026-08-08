using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using AI.LLM.Clients.Base;

namespace AI.LLM.Clients.DeepSeek;

/// <summary>
/// Api для работы с DeepSeek
/// </summary>
[Serializable]
public class DeepSeekApi : ChatLLMApi
{
    public DeepSeekApi(string apiKey, string modelName, string prompt = "", IEnumerable<WebProxy> proxies = null) : base(apiKey: apiKey, modelName: modelName, prompt: prompt, proxies: proxies)
    {
        ApiUrl = "https://api.deepseek.com/v1/chat/completions";
    }
}
