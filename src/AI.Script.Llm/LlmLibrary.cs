using AI.LLM.Core.Abstractions;
using AI.Script.Hosting;

namespace AI.Script.Llm;

/// <summary>
/// Подключение пространств <c>llm</c> и <c>search</c> к хосту.
/// </summary>
/// <remarks>
/// Отдельный метод расширения, как и у графиков: язык не должен знать, что где-то существует
/// языковая модель. Хост, которому она не нужна, не вызывает этот метод и не платит ни
/// зависимостями, ни риском случайного обращения к сети.
/// </remarks>
public static class LlmLibrary
{
    /// <summary>
    /// Подключает <c>llm</c> и <c>search</c>.
    /// </summary>
    /// <param name="host">Хост.</param>
    /// <param name="client">Клиент модели; <c>null</c> — запросы недоступны.</param>
    /// <param name="embedder">Служба эмбеддингов; <c>null</c> — доступен только словесный поиск.</param>
    /// <param name="reranker">Переранжировщик; <c>null</c> — <c>llm.rerank</c> недоступен.</param>
    /// <remarks>
    /// Подключение модуля — это «такая возможность есть», а не «этому прогону можно»: право
    /// обратиться к сети остаётся за <see cref="RunOptions.Network"/>, и по умолчанию его нет.
    /// </remarks>
    public static ScriptHost UseLlm(
        this ScriptHost host,
        ILLMClient? client = null,
        IEmbedderService? embedder = null,
        IRerankerService<string, string>? reranker = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        _ = host.Use(new LlmModule(client, embedder, reranker).ToScriptModule());
        _ = host.Use(new SearchModule(embedder).ToScriptModule());

        return host;
    }
}
