using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.Messages.Content;

/// <summary>
/// Маркер кеширования части промпта (Anthropic prompt caching, проксируется агрегаторами).
/// </summary>
/// <remarks>
/// Ставится на последнюю часть кешируемого префикса — обычно на системный промпт. Провайдер
/// запоминает префикс и на следующих запросах считает его по цене чтения кеша.
/// </remarks>
[Serializable]
public class CacheControl
{
    /// <summary>Готовый маркер обычного (короткоживущего) кеша — тип у него всегда один.</summary>
    public static CacheControl Ephemeral => new();

    /// <summary>Тип кеша; поддерживается <c>ephemeral</c>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ephemeral";
}
