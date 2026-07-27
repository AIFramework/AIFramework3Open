using AI.LLM.Core.Models.Common.Messages.Content;
using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.Requests;

/// <summary>
/// Документ для реранкинга: текст, изображение либо и то и другое.
/// Изображения понимают только мультимодальные реранкеры.
/// </summary>
public class RerankDocument
{
    /// <summary>
    /// Текст документа
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Text { get; set; }

    /// <summary>
    /// Изображение: ссылка либо data:...;base64,... URI
    /// </summary>
    [JsonPropertyName("image")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Image { get; set; }

    /// <summary>
    /// Заполнено ли хоть одно поле
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrEmpty(Text) && string.IsNullOrEmpty(Image);

    public RerankDocument() { }

    public RerankDocument(string text)
    {
        Text = text;
    }

    /// <summary>
    /// Документ-изображение по ссылке или data-URI, с необязательной подписью
    /// </summary>
    public static RerankDocument FromImage(string imageUrl, string text = null) =>
        new() { Image = imageUrl, Text = text };

    /// <summary>
    /// Документ-изображение из байтов, с необязательной подписью.
    /// MIME-тип определяется по сигнатуре средствами <see cref="ImageContent"/>.
    /// </summary>
    public static RerankDocument FromImage(IEnumerable<byte> image, string text = null) =>
        new() { Image = new ImageContent(image).ImageUrl.Url, Text = text };

    /// <summary>
    /// Строка превращается в текстовый документ без лишних церемоний
    /// </summary>
    public static implicit operator RerankDocument(string text) => new(text);

    public override string ToString() => Text ?? Image ?? "";
}
