namespace AI.LLM.Services.Embeddings.OpenRouter;

/// <summary>
/// Описание модели эмбеддингов OpenRouter
/// </summary>
/// <param name="Id">Слаг модели для поля "model" в запросе</param>
/// <param name="MaxContextTokens">Максимальный контекст в токенах</param>
/// <param name="DefaultDimensions">Размерность вектора по умолчанию (null — не задокументирована)</param>
/// <param name="SupportsDimensions">Поддерживает ли параметр "dimensions" (усечение Matryoshka)</param>
/// <param name="SupportsImages">Принимает ли изображения на вход</param>
/// <param name="PricePerMTokens">Цена за миллион входных токенов, USD</param>
public record OpenRouterEmbeddingModel(
    string Id,
    int MaxContextTokens,
    int? DefaultDimensions,
    bool SupportsDimensions,
    bool SupportsImages,
    double PricePerMTokens);

/// <summary>
/// Каталог моделей эмбеддингов, доступных через OpenRouter (на 27.07.2026).
/// Список статический — если OpenRouter добавит модель, её слаг можно передать строкой напрямую,
/// проверки просто не будут применены.
/// </summary>
public static class OpenRouterEmbeddingModels
{
    // --- Текстовые ---

    /// <summary>OpenAI text-embedding-3-small: 1536 измерений, дёшево, хороший дефолт</summary>
    public const string TextEmbedding3Small = "openai/text-embedding-3-small";

    /// <summary>OpenAI text-embedding-3-large: 3072 измерения, точнее и дороже</summary>
    public const string TextEmbedding3Large = "openai/text-embedding-3-large";

    /// <summary>Qwen3 Embedding 8B: 4096 измерений, контекст 33K, инструктивная модель</summary>
    public const string Qwen3Embedding8B = "qwen/qwen3-embedding-8b";

    /// <summary>Qwen3 Embedding 4B: 2560 измерений, контекст 33K, инструктивная модель</summary>
    public const string Qwen3Embedding4B = "qwen/qwen3-embedding-4b";

    /// <summary>BAAI bge-m3: 1024 измерения, сильная многоязычная модель</summary>
    public const string BgeM3 = "baai/bge-m3";

    /// <summary>Mistral Embed 2312: 1024 измерения</summary>
    public const string MistralEmbed = "mistralai/mistral-embed-2312";

    /// <summary>Perplexity Embed V1 0.6B: контекст 32K, самая дешёвая платная</summary>
    public const string PerplexityEmbedV1 = "perplexity/pplx-embed-v1-0.6b";

    /// <summary>Google Gemini Embedding 001: контекст 20K, гибкая размерность</summary>
    public const string GeminiEmbedding001 = "google/gemini-embedding-001";

    // --- Мультимодальные (текст + изображения в одном пространстве) ---

    /// <summary>Google Gemini Embedding 2: текст и изображения, размерность 128–3072</summary>
    public const string GeminiEmbedding2 = "google/gemini-embedding-2";

    /// <summary>NVIDIA Llama Nemotron Embed VL 1B v2: текст и изображения, контекст 131K</summary>
    public const string NemotronEmbedVL = "nvidia/llama-nemotron-embed-vl-1b-v2";

    /// <summary>Бесплатный вариант <see cref="NemotronEmbedVL"/></summary>
    public const string NemotronEmbedVLFree = "nvidia/llama-nemotron-embed-vl-1b-v2:free";

    private static readonly Dictionary<string, OpenRouterEmbeddingModel> _catalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [TextEmbedding3Small] = new(TextEmbedding3Small, 8192, 1536, true, false, 0.02),
            [TextEmbedding3Large] = new(TextEmbedding3Large, 8192, 3072, true, false, 0.13),
            [Qwen3Embedding8B] = new(Qwen3Embedding8B, 32768, 4096, true, false, 0.01),
            [Qwen3Embedding4B] = new(Qwen3Embedding4B, 32768, 2560, true, false, 0.02),
            [BgeM3] = new(BgeM3, 8192, 1024, false, false, 0.01),
            [MistralEmbed] = new(MistralEmbed, 8192, 1024, false, false, 0.10),
            [PerplexityEmbedV1] = new(PerplexityEmbedV1, 32768, null, false, false, 0.004),
            [GeminiEmbedding001] = new(GeminiEmbedding001, 20480, 3072, true, false, 0.15),
            [GeminiEmbedding2] = new(GeminiEmbedding2, 8192, 3072, true, true, 0.20),
            [NemotronEmbedVL] = new(NemotronEmbedVL, 131072, null, false, true, 0.0),
            [NemotronEmbedVLFree] = new(NemotronEmbedVLFree, 131072, null, false, true, 0.0),
        };

    /// <summary>
    /// Все известные модели каталога
    /// </summary>
    public static IReadOnlyCollection<OpenRouterEmbeddingModel> All => _catalog.Values;

    /// <summary>
    /// Модели, принимающие изображения на вход
    /// </summary>
    public static IEnumerable<OpenRouterEmbeddingModel> Multimodal => _catalog.Values.Where(m => m.SupportsImages);

    /// <summary>
    /// Описание модели по слагу; null, если модель не из каталога
    /// </summary>
    public static OpenRouterEmbeddingModel Find(string modelId) =>
        modelId != null && _catalog.TryGetValue(modelId, out var model) ? model : null;
}
