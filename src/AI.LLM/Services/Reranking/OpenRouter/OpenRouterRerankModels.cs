namespace AI.LLM.Services.Reranking.OpenRouter;

/// <summary>
/// Описание модели реранкинга OpenRouter
/// </summary>
/// <param name="Id">Слаг модели для поля "model" в запросе</param>
/// <param name="MaxContextTokens">Максимальный контекст в токенах</param>
/// <param name="SupportsImages">Принимает ли изображения в документах</param>
public record OpenRouterRerankModel(string Id, int MaxContextTokens, bool SupportsImages);

/// <summary>
/// Каталог моделей реранкинга, доступных через OpenRouter (на 24.08.2026).
/// Слаг можно передать и строкой напрямую — тогда проверки просто не применяются.
/// </summary>
public static class OpenRouterRerankModels
{
    /// <summary>Cohere Rerank 4 Pro: контекст 33K, максимальное качество</summary>
    public const string CohereRerank4Pro = "cohere/rerank-4-pro";

    /// <summary>Cohere Rerank 4 Fast: контекст 33K, компромисс качества и скорости</summary>
    public const string CohereRerank4Fast = "cohere/rerank-4-fast";

    /// <summary>Cohere Rerank v3.5: контекст 4K, проверенная временем модель</summary>
    public const string CohereRerankV35 = "cohere/rerank-v3.5";

    /// <summary>NVIDIA Llama Nemotron Rerank VL 1B v2: оценивает изображения документов, контекст 10K</summary>
    public const string NemotronRerankVL = "nvidia/llama-nemotron-rerank-vl-1b-v2";

    /// <summary>Бесплатный вариант <see cref="NemotronRerankVL"/></summary>
    public const string NemotronRerankVLFree = "nvidia/llama-nemotron-rerank-vl-1b-v2:free";

    /// <summary>Voyage rerank-2.5: контекст 32K на пару, тарификация за токены, следование инструкции</summary>
    public const string VoyageRerank25 = "voyageai/rerank-2.5";

    /// <summary>Облегчённый вариант <see cref="VoyageRerank25"/>: вчетверо дешевле</summary>
    public const string VoyageRerank25Lite = "voyageai/rerank-2.5-lite";

    /// <summary>Qwen3 Reranker 8B: контекст 41K, 100+ языков; 8B — это латентность LLM, а не кросс-энкодера</summary>
    public const string Qwen3Reranker8B = "qwen/qwen3-reranker-8b";

    private static readonly Dictionary<string, OpenRouterRerankModel> _catalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [CohereRerank4Pro] = new(CohereRerank4Pro, 33792, false),
            [CohereRerank4Fast] = new(CohereRerank4Fast, 33792, false),
            [CohereRerankV35] = new(CohereRerankV35, 4096, false),
            [NemotronRerankVL] = new(NemotronRerankVL, 10240, true),
            [NemotronRerankVLFree] = new(NemotronRerankVLFree, 10240, true),
            [VoyageRerank25] = new(VoyageRerank25, 32000, false),
            [VoyageRerank25Lite] = new(VoyageRerank25Lite, 32000, false),
            [Qwen3Reranker8B] = new(Qwen3Reranker8B, 40960, false),
        };

    /// <summary>
    /// Все известные модели каталога
    /// </summary>
    public static IReadOnlyCollection<OpenRouterRerankModel> All => _catalog.Values;

    /// <summary>
    /// Модели, принимающие изображения в документах
    /// </summary>
    public static IEnumerable<OpenRouterRerankModel> Multimodal => _catalog.Values.Where(m => m.SupportsImages);

    /// <summary>
    /// Описание модели по слагу; null, если модель не из каталога
    /// </summary>
    public static OpenRouterRerankModel Find(string modelId) =>
        modelId != null && _catalog.TryGetValue(modelId, out var model) ? model : null;
}
