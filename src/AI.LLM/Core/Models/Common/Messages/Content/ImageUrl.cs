using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.Messages.Content;

public class ImageUrl
{
    [JsonPropertyName("url")]
    public string Url { get; set; }
}
