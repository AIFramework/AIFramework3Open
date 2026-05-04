using AI.LLM.Clients.OpenRouter;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.ToolCalling;
using AI.LLM.Integration.SemanticKernel.Extensions;
using AI.LLM.Services.LLM;
using AiFrameworkDemo.Core;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.LLM;

public static partial class LlmDemoRunner
{
    private static string DoSemanticKernel(
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp)
    {
        var apiKey  = RequireApiKey(tp);
        var model   = GetModel(p);
        var mode    = I(p, "skMode", 0);
        var message = T(tp, "_message", "Какая сейчас погода в Москве?");

        return mode switch
        {
            0 => DoSkChat(apiKey, model, message),
            1 => DoSkFunctionCalling(apiKey, model, message),
            2 => DoSkPluginChain(apiKey, model, message),
            _ => "Неизвестный режим SK",
        };
    }

    #region SK: ChatCompletion

    private static string DoSkChat(string apiKey, string model, string message)
    {
        var sb = new StringBuilder();
        sb.AppendLine("> Semantic Kernel — ChatCompletion");
        sb.AppendLine();
        sb.AppendLine($"  Модель: {model}");
        sb.AppendLine();

        var kernel = Kernel.CreateBuilder()
            .AddSharpGPTChatCompletion(
                apiKey: apiKey,
                modelName: model,
                apiUrl: "https://openrouter.ai/api/v1/chat/completions",
                systemPrompt: "Ты — полезный ассистент. Отвечай на русском языке.")
            .Build();

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage(message);

        sb.AppendLine("- Запрос через SK");
        sb.AppendLine($"  [USER] {message}");
        sb.AppendLine();

        var sw = Stopwatch.StartNew();

        ChatMessageContent response;
        try
        {
            response = chatService.GetChatMessageContentsAsync(history)
                .GetAwaiter().GetResult()
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            sb.AppendLine($"- Ошибка");
            sb.AppendLine(ex.Message);
            return sb.ToString();
        }

        sw.Stop();

        sb.AppendLine("- Ответ SK");
        sb.AppendLine(response?.Content ?? "(пустой ответ)");
        sb.AppendLine();
        sb.AppendLine($"  Время: {sw.ElapsedMilliseconds} мс");
        sb.AppendLine();
        sb.AppendLine("- Код интеграции");
        sb.AppendLine("  var kernel = Kernel.CreateBuilder()");
        sb.AppendLine("      .AddSharpGPTChatCompletion(apiKey, model,");
        sb.AppendLine("          apiUrl: \"https://openrouter.ai/api/v1/chat/completions\")");
        sb.AppendLine("      .Build();");
        sb.AppendLine("  var chat = kernel.GetRequiredService<IChatCompletionService>();");
        sb.AppendLine("  var result = await chat.GetChatMessageContentsAsync(history);");

        return sb.ToString();
    }

    #endregion

    #region SK: Function Calling

    private static string DoSkFunctionCalling(string apiKey, string model, string message)
    {
        var sb = new StringBuilder();
        sb.AppendLine("> Semantic Kernel — Function Calling");
        sb.AppendLine();
        sb.AppendLine($"  Модель: {model}");
        sb.AppendLine();

        sb.AppendLine("- Определение tool");
        sb.AppendLine("  Имя:       get_weather");
        sb.AppendLine("  Описание:  Получает текущую погоду в городе");
        sb.AppendLine("  Параметры: { \"city\": string }");
        sb.AppendLine();

        var weatherTool = ToolDefinition.Create(
            "get_weather",
            "Получает текущую погоду в указанном городе",
            """{"type":"object","properties":{"city":{"type":"string","description":"Название города"}},"required":["city"]}""");

        var client = new OpenRouterModelApi(apiKey, model);
        var settings = new GenerateSettings
        {
            MaxTokens = 512,
            Tools = [weatherTool],
        };

        var messages = new List<LLMMessage>
        {
            LLMMessage.CreateMessage(Roles.System,
                "Ты — ассистент с доступом к функции get_weather. " +
                "Используй её для ответов о погоде."),
            LLMMessage.CreateMessage(Roles.User, message),
        };

        sb.AppendLine("- Запрос к модели (с tools)");
        sb.AppendLine($"  [USER] {message}");
        sb.AppendLine();

        var sw = Stopwatch.StartNew();

        var response = client.SendWithContextAsync(messages, settings)
            .GetAwaiter().GetResult();

        sw.Stop();
        var choice = response?.Choices?.FirstOrDefault();
        var assistantMsg = choice?.Message;

        if (assistantMsg?.ToolCalls is { Count: > 0 } toolCalls)
        {
            sb.AppendLine("- Модель вызвала функцию");
            foreach (var tc in toolCalls)
            {
                sb.AppendLine($"  Tool Call ID: {tc.Id}");
                sb.AppendLine($"  Функция:     {tc.Function?.Name}");
                sb.AppendLine($"  Аргументы:   {tc.Function?.Arguments}");
                sb.AppendLine();
            }

            // Эмулируем ответ функции
            var firstCall = toolCalls[0];
            string city = "Москва";
            try
            {
                using var doc = JsonDocument.Parse(firstCall.Function?.Arguments ?? "{}");
                if (doc.RootElement.TryGetProperty("city", out var c))
                    city = c.GetString() ?? city;
            }
            catch { }

            var weatherResult = $"Погода в {city}: +18°C, переменная облачность, ветер 5 м/с";

            sb.AppendLine("- Ответ функции (эмуляция)");
            sb.AppendLine($"  {weatherResult}");
            sb.AppendLine();

            // Отправляем tool result обратно
            messages.Add(new LLMMessage
            {
                Role = LLMMessage.AssistantRole,
                Content = assistantMsg.Content,
                ToolCalls = assistantMsg.ToolCalls,
            });
            messages.Add(LLMMessage.CreateToolResult(firstCall.Id, weatherResult));

            var finalSettings = new GenerateSettings { MaxTokens = 512 };
            var finalResponse = client.SendWithContextTextAsync(messages, finalSettings)
                .GetAwaiter().GetResult();

            sb.AppendLine("- Финальный ответ модели");
            sb.AppendLine(finalResponse);
        }
        else
        {
            sb.AppendLine("- Ответ модели (без tool call)");
            sb.AppendLine(assistantMsg?.Content?.ToString() ?? "(пусто)");
        }

        sb.AppendLine();
        sb.AppendLine($"  Время: {sw.ElapsedMilliseconds} мс");

        return sb.ToString();
    }

    #endregion

    #region SK: Plugin Chain

    private static string DoSkPluginChain(string apiKey, string model, string message)
    {
        var sb = new StringBuilder();
        sb.AppendLine("> Semantic Kernel — Цепочка плагинов");
        sb.AppendLine();
        sb.AppendLine($"  Модель: {model}");
        sb.AppendLine();
        sb.AppendLine("- Регистрация плагинов");

        var kernel = Kernel.CreateBuilder()
            .AddSharpGPTChatCompletion(
                apiKey: apiKey,
                modelName: model,
                apiUrl: "https://openrouter.ai/api/v1/chat/completions")
            .Build();

        kernel.ImportPluginFromType<WeatherPlugin>();
        kernel.ImportPluginFromType<MathPlugin>();

        foreach (var plugin in kernel.Plugins)
        {
            sb.AppendLine($"  Плагин: {plugin.Name}");
            foreach (var fn in plugin)
                sb.AppendLine($"    - {fn.Name}: {fn.Description}");
        }
        sb.AppendLine();

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory(
            "Ты — ассистент с доступом к плагинам. " +
            "Используй их когда нужно. Отвечай на русском.");
        history.AddUserMessage(message);

        sb.AppendLine("- Запрос через SK с плагинами");
        sb.AppendLine($"  [USER] {message}");
        sb.AppendLine();

        var execSettings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };

        var sw = Stopwatch.StartNew();

        ChatMessageContent response;
        try
        {
            response = chatService
                .GetChatMessageContentsAsync(history, execSettings, kernel)
                .GetAwaiter().GetResult()
                .LastOrDefault();
        }
        catch (Exception ex)
        {
            sb.AppendLine($"- Ошибка");
            sb.AppendLine(ex.Message);
            return sb.ToString();
        }

        sw.Stop();

        sb.AppendLine("- Ответ SK");
        sb.AppendLine(response?.Content ?? "(пустой ответ)");
        sb.AppendLine();
        sb.AppendLine($"  Время: {sw.ElapsedMilliseconds} мс");
        sb.AppendLine();
        sb.AppendLine("- Код плагина");
        sb.AppendLine("  public class WeatherPlugin {");
        sb.AppendLine("      [KernelFunction, Description(\"Получает погоду\")]");
        sb.AppendLine("      public string GetWeather(string city) => ...");
        sb.AppendLine("  }");
        sb.AppendLine("  kernel.ImportPluginFromType<WeatherPlugin>();");

        return sb.ToString();
    }

    #endregion

    #region Демо-плагины для SK

    private sealed class WeatherPlugin
    {
        [KernelFunction("get_weather")]
        [Description("Получает текущую погоду в указанном городе")]
        public string GetWeather(
            [Description("Название города")] string city)
        {
            // Эмуляция — в реальном приложении здесь был бы HTTP-запрос
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Москва"]          = "+18°C, переменная облачность",
                ["Санкт-Петербург"] = "+14°C, дождь",
                ["Новосибирск"]     = "+12°C, ясно",
                ["Лондон"]          = "+16°C, туман",
                ["Нью-Йорк"]       = "+22°C, солнечно",
            };

            return data.TryGetValue(city, out var w)
                ? $"Погода в {city}: {w}"
                : $"Погода в {city}: +15°C, облачно (данные недоступны, показан прогноз)";
        }
    }

    private sealed class MathPlugin
    {
        [KernelFunction("calculate")]
        [Description("Вычисляет математическое выражение (сложение, умножение и т.д.)")]
        public string Calculate(
            [Description("Первое число")] double a,
            [Description("Второе число")] double b,
            [Description("Операция: add, sub, mul, div")] string op)
        {
            var result = op.ToLower() switch
            {
                "add" => a + b,
                "sub" => a - b,
                "mul" => a * b,
                "div" => b != 0 ? a / b : double.NaN,
                _     => double.NaN,
            };
            return $"{a} {op} {b} = {result}";
        }
    }

    #endregion
}
