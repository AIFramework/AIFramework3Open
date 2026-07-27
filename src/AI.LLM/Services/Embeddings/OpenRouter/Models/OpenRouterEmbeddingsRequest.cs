using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Core.Models.Common.Requests;
using System.Text.Json.Serialization;

namespace AI.LLM.Services.Embeddings.OpenRouter.Models;

/// <summary>
/// Тело запроса POST https://openrouter.ai/api/v1/embeddings
/// </summary>
public class OpenRouterEmbeddingsRequest
{
    /// <summary>
    /// Слаг модели, например "openai/text-embedding-3-small"
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; }

    /// <summary>
    /// Вход: массив строк либо массив мультимодальных элементов <see cref="OpenRouterEmbeddingInput"/>.
    /// Сериализуется по фактическому типу значения.
    /// </summary>
    [JsonPropertyName("input")]
    public object Input { get; set; }

    /// <summary>
    /// Размерность вектора на выходе (Matryoshka). Поддерживается не всеми моделями,
    /// см. <see cref="OpenRouterEmbeddingModels"/>.
    /// </summary>
    [JsonPropertyName("dimensions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Dimensions { get; set; }

    /// <summary>
    /// Формат кодирования вектора. OpenRouter поддерживает "float".
    /// </summary>
    [JsonPropertyName("encoding_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string EncodingFormat { get; set; } = "float";

    /// <summary>
    /// Настройки маршрутизации по провайдерам
    /// </summary>
    [JsonPropertyName("provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProviderPreference Provider { get; set; }
}

/// <summary>
/// Мультимодальный элемент входа: части контента, которые модель укладывает в ОДИН вектор.
/// В JSON выглядит как { "content": [ {"type":"text",...}, {"type":"image_url",...} ] }
/// </summary>
public class OpenRouterEmbeddingInput
{
    [JsonPropertyName("content")]
    public MessageContent Content { get; set; }

    public OpenRouterEmbeddingInput() { }

    public OpenRouterEmbeddingInput(MessageContent content)
    {
        Content = content;
    }
}
