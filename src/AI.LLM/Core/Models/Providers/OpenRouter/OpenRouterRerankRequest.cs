using AI.LLM.Core.Models.Common.Requests;
using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Providers.OpenRouter;

/// <summary>
/// Тело запроса POST https://openrouter.ai/api/v1/rerank
/// </summary>
public class OpenRouterRerankRequest
{
    /// <summary>
    /// Слаг модели реранкера, например "cohere/rerank-v3.5"
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; }

    /// <summary>
    /// Запрос, относительно которого ранжируются документы
    /// </summary>
    [JsonPropertyName("query")]
    public string Query { get; set; }

    /// <summary>
    /// Документы: массив строк либо массив объектов <see cref="RerankDocument"/>.
    /// Сериализуется по фактическому типу значения.
    /// </summary>
    [JsonPropertyName("documents")]
    public object Documents { get; set; }

    /// <summary>
    /// Сколько лучших документов вернуть. null — вернуть все.
    /// </summary>
    [JsonPropertyName("top_n")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TopN { get; set; }

    /// <summary>
    /// Настройки маршрутизации по провайдерам
    /// </summary>
    [JsonPropertyName("provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProviderPreference Provider { get; set; }
}
