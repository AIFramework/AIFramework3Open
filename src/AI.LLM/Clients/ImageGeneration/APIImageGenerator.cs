using AI.LLM.Clients.Base;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Core.Models.Providers.ImageGeneration;
using System.Threading;

namespace AI.LLM.Clients.ImageGeneration;

/// <summary>
/// Генерация изображений
/// </summary>
[Serializable]
public class APIImageGenerator
{
    public const string ToxicMessage = "ext";

    private static readonly HttpClient _sharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly ChatLLMApi _imageGenerativeModelApi;
    private readonly SsrfGuardOptions _ssrfGuard;

    /// <param name="llmApi">LLM-клиент для генерации изображений.</param>
    /// <param name="ssrfGuard">
    /// Настройки защиты от SSRF. По умолчанию (<see cref="SsrfGuardOptions.Default"/>)
    /// блокируются приватные IP-диапазоны. Передайте <see cref="SsrfGuardOptions.Disabled"/>
    /// для полного отключения проверок (только для изолированных dev-сред).
    /// Используйте <see cref="SsrfGuardOptions.WithHosts"/> или <see cref="SsrfGuardOptions.OpenAiOnly"/>
    /// для строгого allowlist конкретных хостов.
    /// </param>
    public APIImageGenerator(ChatLLMApi llmApi, SsrfGuardOptions ssrfGuard = null)
    {
        _imageGenerativeModelApi = llmApi;
        _ssrfGuard = ssrfGuard ?? SsrfGuardOptions.Default;
    }


    public async Task<ImageGenerationAnswer> GenerateAsync(string prompt, CancellationToken cancellationToken = default) =>
        await GenerateAsync(new LLMMessage(LLMMessage.UserRole, prompt), cancellationToken);

    public async Task<ImageGenerationAnswer> GenerateAsync(LLMMessage prompt, CancellationToken cancellationToken = default)
    {
        List<LLMMessage> context = new List<LLMMessage>() { prompt };
        return await GenerateAsync(context, cancellationToken);
    }

    public async Task<ImageGenerationAnswer> GenerateAsync(IEnumerable<LLMMessage> context, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _imageGenerativeModelApi.SendWithContextAsync(context, cancellationToken: cancellationToken);

            var firstChoice = response?.Choices?.FirstOrDefault();
            var message = firstChoice?.Message;

            if (message == null)
                return new ImageGenerationAnswer("The API response was empty or malformed.");

            if (message.Content is string contentStr && contentStr == ToxicMessage)
                return new ImageGenerationAnswer("Задача нарушает политику безопасности") { StatusOK = false };

            var imageUrl = message.Images?.FirstOrDefault()?.ImageUrl?.Url;

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                string errorContent = message.Content?.ToString() ?? "The API response was empty or malformed.";
                return new ImageGenerationAnswer(errorContent);
            }

            var cost = CostExtractor.TryExtract(response?.Usage?.Cost);

            return new ImageGenerationAnswer(ParseDataUri(imageUrl), message.Content?.ToString(), cost);
        }
        catch (Exception ex)
        {
            return new ImageGenerationAnswer(ex.Message);
        }
    }


    private byte[] ParseDataUri(string dataUri)
    {
        if (string.IsNullOrEmpty(dataUri))
            throw new ArgumentException("Data URI cannot be null or empty.", nameof(dataUri));

        int commaIndex = dataUri.IndexOf(',');
        if (commaIndex == -1 || !dataUri.Substring(0, Math.Min(dataUri.Length, commaIndex + 1)).Contains(";base64"))
        {
            // Not a data URI — treat as a regular URL, download the image
            _ssrfGuard.Validate(dataUri);
            return _sharedHttpClient.GetByteArrayAsync(dataUri).GetAwaiter().GetResult();
        }

        string base64Data = dataUri.Substring(commaIndex + 1);

        try
        {
            return Convert.FromBase64String(base64Data);
        }
        catch (FormatException ex)
        {
            throw new FormatException("Failed to convert base64 string to byte array. The data might be corrupted.", ex);
        }
    }
}
