using AI.LLM.Clients.OpenRouter.ContextFormat.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.LLM.Clients.OpenRouter.ContextFormat;

// Класс для чтения файла
public class ContextReader
{
    public static OpenRouterContext ReadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return ReadFromJson(json);
    }

    public static OpenRouterContext ReadFromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Deserialize<OpenRouterContext>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize OpenRouterContext: result is null.");
    }

    public static async Task<OpenRouterContext> ReadFromFileAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        return await JsonSerializer.DeserializeAsync<OpenRouterContext>(stream, options)
            ?? throw new InvalidOperationException("Failed to deserialize OpenRouterContext: result is null.");
    }
}
