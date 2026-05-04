using AI.LLM.Clients.OpenRouter;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Services.LLM;
using AiFrameworkDemo.Core;
using System.Diagnostics;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.LLM;

public static partial class LlmDemoRunner
{
    private static string DoChat(
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp)
    {
        var apiKey  = RequireApiKey(tp);
        var model   = GetModel(p);
        var temp    = N(p, "temperature", 7) / 10.0;
        var maxTok  = I(p, "maxTokens", 512);
        var message = T(tp, "_message", "Привет! Что ты умеешь?");

        var sb = new StringBuilder();
        sb.AppendLine("> Простой чат с LLM через OpenRouter");
        sb.AppendLine();
        sb.AppendLine($"  Модель:      {model}");
        sb.AppendLine($"  Temperature: {temp:F1}");
        sb.AppendLine($"  Max tokens:  {maxTok}");
        sb.AppendLine();
        sb.AppendLine("- Запрос");
        sb.AppendLine(message);
        sb.AppendLine();

        var options = new LLMOptions
        {
            ApiKey      = apiKey,
            ModelName   = model,
            Temperature = temp,
        };

        var settings = new GenerateSettings
        {
            Temperature = temp,
            MaxTokens   = maxTok,
        };

        var client = new LLMWithOpenRouterClient(options);
        var sw = Stopwatch.StartNew();

        string response;
        try
        {
            response = client.SendToLLM(message, settings)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            sb.AppendLine($"- Ошибка API");
            sb.AppendLine(ex.Message);
            return sb.ToString();
        }

        sw.Stop();

        sb.AppendLine("- Ответ LLM");
        sb.AppendLine(response);
        sb.AppendLine();
        sb.AppendLine($"  Время ответа: {sw.ElapsedMilliseconds} мс");

        return sb.ToString();
    }
}
