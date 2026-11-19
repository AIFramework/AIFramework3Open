using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.Responses;


/// <summary>
/// Represents token usage information for a given request or session.
/// </summary>
[Serializable]
public class Usage
{
    /// <summary>
    /// Gets or sets the number of tokens used in the prompt.
    /// </summary>
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of tokens used by the model's completion.
    /// </summary>
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Gets or sets the total number of tokens used in the session.
    /// </summary>
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; set; }

    /// <summary>
    /// Сколько токенов промпта провайдер взял из кеша.
    /// </summary>
    /// <remarks>
    /// Считаются они по отдельной, заметно меньшей цене, поэтому тому, кто пересчитывает расход в
    /// деньги, без этого числа кешируемые промпты выходят дороже, чем есть на самом деле.
    /// </remarks>
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; set; }

    [JsonPropertyName("cost")]
    public object Cost { get; set; }
}

public static class CostExtractor
{
    public static decimal? TryExtract(object cost)
    {
        // 1. Проверяем на null в самом начале
        if (cost == null)
        {
            return null;
        }

        if (cost is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            // Имя свойства в JSON - "total_cost", а не "TotalCost".
            if (jsonElement.TryGetProperty("total_cost", out var totalCostProperty))
            {
                // Пытаемся получить значение как decimal
                if (totalCostProperty.TryGetDecimal(out decimal decimalValue))
                {
                    return decimalValue;
                }
            }
        }

        // 2. Число CLR берем как есть: клиенты кладут в Cost готовую сумму.
        switch (cost)
        {
            case decimal value: return value;
            case double value: return double.IsFinite(value) ? (decimal)value : null;
            case float value: return float.IsFinite(value) ? (decimal)value : null;
            case int value: return value;
            case long value: return value;
        }

        // 3. Остальное разбираем строкой. Строку делаем и читаем в одной, инвариантной культуре:
        // ToString() текущей культуры под ru-RU пишет «0,5», а инвариантный разбор принимает
        // запятую за разделитель тысяч, и сумма вырастает в 10 раз и больше.
        var text = cost is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : cost.ToString();

        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
        {
            return result;
        }

        // 4. Если ничего не подошло, возвращаем null
        return null;
    }
}

