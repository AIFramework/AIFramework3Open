using AI.LLM.API.LLMAPI;
using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.Messages.Content;

[Serializable]
public class ImageContent : IContentItem
{
    [JsonIgnore]
    public string Type => "image_url";

    [JsonPropertyName("image_url")]
    public ImageUrl ImageUrl { get; set; }


    public ImageContent() { }

    public ImageContent(string imageUrl)
    {
        ImageUrl = new ImageUrl { Url = imageUrl };
    }

    public ImageContent(IEnumerable<byte> image)
    {
        byte[] bytes = image.ToArray();
        string base64 = Convert.ToBase64String(bytes);
        ImageUrl = new ImageUrl { Url = $"data:{DetectMimeType(bytes)};base64,{base64}" };
    }

    // Определяет MIME-тип изображения по сигнатуре (magic bytes); если формат не распознан — image/jpeg
    private static string DetectMimeType(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 4 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == '8')
            return "image/gif";
        if (bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
            && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P')
            return "image/webp";
        if (bytes.Length >= 2 && bytes[0] == 'B' && bytes[1] == 'M')
            return "image/bmp";
        return "image/jpeg";
    }
}
