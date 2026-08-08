using System.Net;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Infrastructure.Http;

namespace AI.LLM.Clients.OpenRouter
{
    /// <summary>
    /// Работа с API OpenRouter
    /// </summary>
    public class OpenRouterModelApi : ChatLLMApi
    {
        public OpenRouterModelApi(string apiKey, string modelName, string prompt = "", IEnumerable<WebProxy> proxies = null)
            : base(apiKey: apiKey, modelName: modelName, prompt: prompt, proxies: proxies)
        {
            ApiUrl = "https://openrouter.ai/api/v1/chat/completions";
            StreamOptions = new();
        }

        /// <summary>
        /// Работа с API OpenRouter поверх готового http-клиента: ключи, маршруты и повторы —
        /// на стороне вызывающего (см. конструктор <see cref="ChatLLMApi"/> с <see cref="IWebAPIClient"/>).
        /// </summary>
        public OpenRouterModelApi(IWebAPIClient webApi, string modelName, string prompt = "")
            : base(webApi: webApi, modelName: modelName, prompt: prompt)
        {
            ApiUrl = "https://openrouter.ai/api/v1/chat/completions";
            StreamOptions = new();
        }

        /// <summary>
        /// Отключаем валидацию контекста для OpenRouter (огромный контекст у Gemma 3 27B)
        /// Возвращаем 1 чтобы проверка tokensCount < MaxLLMTokens всегда проходила
        /// </summary>
        public override async Task<int> TokenizeAsync(IEnumerable<LLMMessage> messages, CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(1);
        }
    }
}
