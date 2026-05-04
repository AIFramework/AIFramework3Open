using AI.LLM.API.LLMAPI;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Infrastructure.Extensions;
using AI.LLM.Infrastructure.Http;
using AI.LLM.Services.Prompts;
using Newtonsoft.Json;

namespace AI.LLM.Clients.Base;

public partial class ChatLLMApi
{
    /// <summary>
    /// Создает исключение для ошибки HTTP-запроса
    /// </summary>
    private async Task<Exception> CreateHttpErrorException(
        int attempt,
        HttpResponseMessage response,
        IEnumerable<LLMMessage> context,
        CancellationToken cancellationToken,
        int maxAttempts = 2)
    {
        string lastMessage = context.Any() ? (context.Last().Content?.ToString() ?? "") : "";
        string truncatedMessage = lastMessage.Substring(0, Math.Min(lastMessage.Length, 512));

        var content = (await response.Content.ReadAsStringAsync(cancellationToken) ?? "").TruncateForLogging();

        return new Exception(
            $"Attempt #{attempt + 1}/{maxAttempts}\n" +
            $"Query: {truncatedMessage}\n" +
            $"###\n" +
            $"StatusCode: {response.StatusCode}\n" +
            $"IsCancellationRequested: {cancellationToken.IsCancellationRequested}\n" +
            $"Content: {content}\n" +
            $"###");
    }

    /// <summary>
    /// Создает исключение для ошибки обработки ответа
    /// </summary>
    private async Task<Exception> CreateProcessingErrorException(
        int attempt,
        Exception innerException,
        IEnumerable<LLMMessage> context,
        SendDataLLM sendData,
        CancellationToken cancellationToken)
    {
        string sendDataJson = JsonConvert.SerializeObject(sendData);
        sendDataJson = sendDataJson.Substring(0, Math.Min(sendDataJson.Length, 512));

        string lastMessage = context.Any() ? (context.Last().Content?.ToString() ?? "") : "";
        string truncatedMessage = lastMessage.Substring(0, Math.Min(lastMessage.Length, 512));

        return new Exception(
            $"Attempt #{attempt + 1}\n" +
            $"Query: {truncatedMessage}\n" +
            $"###\n" +
            $"IsCancellationRequested: {cancellationToken.IsCancellationRequested}\n" +
            $"SendData: {sendDataJson}\n" +
            $"###",
            innerException);
    }

    /// <summary>
    /// Валидация настроек генерации
    /// </summary>
    /// <param name="generateSettings">Начальные настройки</param>
    /// <returns></returns>
    public GenerateSettings Validate(GenerateSettings generateSettings)
    {
        generateSettings ??= new();

        generateSettings.Temperature = ValidateTemperature(generateSettings.Temperature);
        generateSettings.MaxTokens = ValidateMaxTokens(generateSettings.MaxTokens);

        if (generateSettings.ReasoningSettings?.MaxTokens != null)
            generateSettings.ReasoningSettings.MaxTokens = ValidateMaxTokens(generateSettings.ReasoningSettings.MaxTokens.Value);

        return generateSettings;
    }


    /// <summary>
    /// Проверяет и нормализует значение температуры
    /// </summary>
    public static double? ValidateTemperature(double? temperature)
    {
        if (temperature == null) 
            return null;

        if (temperature > 1.5)
            return 1.5;
        if (temperature < 0.0)
            return 0.0;

        return temperature;
    }

    /// <summary>
    /// Проверяет максимальное количество токенов
    /// </summary>
    public static int? ValidateMaxTokens(int? maxTokens)
    {
        if (maxTokens == null) return null;

        return Math.Max(1, maxTokens.Value);
    }


    private void LLMApi_OnProxyError(object sender, ProxyErrorEventArgs e)
    {
        ProxyInfo($"Proxy: {e.Proxy.Address}\nError: {e.Exception}");
    }

    private void ChatLLMApi_ProxyInfo(string obj)
    {
        
    }



    #region Тестирование

    /// <summary>
    /// Метод предназначенный в первую очередь для тестирования 
    /// (он показывает, что отправляется в модель)
    /// </summary>
    /// <param name="text"></param>
    /// <param name="generateSettings"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public SendDataLLM GetSendDataAsync(string text, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default)
    {
        List<LLMMessage> context = [
            LLMMessage.CreateMessage(Roles.System, _prompt),
            LLMMessage.CreateMessage(Roles.User, text)
            ];

        generateSettings = Validate(generateSettings);

        if (context == null)
            throw new ArgumentException("Контекст не может быть null.", nameof(context));

        var sendData = new SendDataLLM(ModelName, generateSettings);
        sendData.StreamOptions = StreamOptions;
        sendData.SetMessages(context);

        // Установка провайдера если задан (для OpenRouter)
        if (PreferredProvider != null)
            sendData.Provider = PreferredProvider;

        return sendData;
    }

    #endregion
}
