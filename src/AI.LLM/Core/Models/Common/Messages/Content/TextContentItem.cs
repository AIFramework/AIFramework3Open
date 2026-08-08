using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.Messages.Content;

/// <summary>
/// Текстовая часть контента
/// </summary>
[Serializable]
public class TextContentItem : IContentItem
{
    [JsonIgnore]
    public string Type => "text";

    [JsonPropertyName("text")]
    public string Text { get; set; }

    /// <summary>
    /// Маркер кеширования части промпта. <c>null</c> — не кешировать (обычное поведение).
    /// </summary>
    /// <remarks>
    /// Нужен ради Anthropic prompt caching: длинный системный промпт, одинаковый от запроса к
    /// запросу, помечается один раз и дальше считается по цене чтения кеша, а не полного ввода.
    /// Проставляется на ПОСЛЕДНЕЙ кешируемой части — она задаёт границу префикса; помечать каждую
    /// часть незачем и дороже.
    /// </remarks>
    [JsonPropertyName("cache_control")]
    public CacheControl CacheControl { get; set; }

    public TextContentItem() { }

    public TextContentItem(string text)
    {
        Text = text;
    }
}
