using AI.LLM.Core.Models.Common.Requests;
using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Providers.OpenRouter;

/// <summary>
/// Ответ эндпоинта реранкинга OpenRouter
/// </summary>
public class OpenRouterRerankResponse
{
    /// <summary>
    /// Идентификатор запроса
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Модель, обработавшая запрос
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; }

    /// <summary>
    /// Провайдер, на который ушёл запрос
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; }

    /// <summary>
    /// Результаты по убыванию релевантности (не в порядке входных документов)
    /// </summary>
    [JsonPropertyName("results")]
    public List<OpenRouterRerankResult> Results { get; set; }

    /// <summary>
    /// Потребление и стоимость запроса
    /// </summary>
    [JsonPropertyName("usage")]
    public OpenRouterRerankUsage Usage { get; set; }
}

/// <summary>
/// Оценка одного документа
/// </summary>
public class OpenRouterRerankResult
{
    /// <summary>
    /// Позиция документа во входном массиве
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>
    /// Оценка релевантности: больше — релевантнее
    /// </summary>
    [JsonPropertyName("relevance_score")]
    public double RelevanceScore { get; set; }

    /// <summary>
    /// Сам документ; возвращается не всеми провайдерами
    /// </summary>
    [JsonPropertyName("document")]
    public RerankDocument Document { get; set; }
}

/// <summary>
/// Потребление ресурсов запросом реранкинга.
/// У Cohere тарификация идёт в search units, а не в токенах, поэтому поля заполняются по-разному.
/// </summary>
public class OpenRouterRerankUsage
{
    [JsonPropertyName("total_tokens")]
    public int? TotalTokens { get; set; }

    [JsonPropertyName("search_units")]
    public int? SearchUnits { get; set; }

    [JsonPropertyName("cost")]
    public double? Cost { get; set; }
}
