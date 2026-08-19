using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.LLM.Clients.Tavily.Models;

/// <summary>
/// Картинка из ответа Tavily.
/// </summary>
/// <remarks>
/// В <c>/extract</c> и в <c>/search</c> без описаний картинка приходит СТРОКОЙ (адрес), а при
/// <c>include_image_descriptions=true</c> — объектом <c>{url, description}</c>. Оба вида читает
/// <see cref="TavilyImageConverter"/>, поэтому вызывающему коду разница не видна.
/// </remarks>
[JsonConverter(typeof(TavilyImageConverter))]
public class TavilyImage
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }
}

/// <summary>Читает <see cref="TavilyImage"/> и из строки, и из объекта; пишет объектом.</summary>
public class TavilyImageConverter : JsonConverter<TavilyImage>
{
    public override TavilyImage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new TavilyImage { Url = reader.GetString() };

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        var image = new TavilyImage();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var name = reader.GetString();
            reader.Read();

            if (string.Equals(name, "url", StringComparison.OrdinalIgnoreCase))
                image.Url = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            else if (string.Equals(name, "description", StringComparison.OrdinalIgnoreCase))
                image.Description = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            else
                reader.Skip();
        }

        return image;
    }

    public override void Write(Utf8JsonWriter writer, TavilyImage value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("url", value.Url);
        if (!string.IsNullOrEmpty(value.Description))
            writer.WriteString("description", value.Description);
        writer.WriteEndObject();
    }
}
