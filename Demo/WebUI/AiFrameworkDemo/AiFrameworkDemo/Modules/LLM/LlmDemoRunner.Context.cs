using AI.LLM.Clients.OpenRouter;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Services.LLM;
using AiFrameworkDemo.Core;
using System.Diagnostics;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.LLM;

public static partial class LlmDemoRunner
{
    private static string DoContext(
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp)
    {
        var apiKey  = RequireApiKey(tp);
        var model   = GetModel(p);
        var mode    = I(p, "ctxMode", 0);
        var system  = T(tp, "_system", "Ты — эксперт по C# и .NET. Отвечай кратко и по делу.");
        var message = T(tp, "_message", "Чем отличается struct от class?");

        var sb = new StringBuilder();
        sb.AppendLine("> Управление контекстом LLM");
        sb.AppendLine();
        sb.AppendLine($"  Модель: {model}");
        sb.AppendLine($"  Режим:  {mode switch { 0 => "Новый диалог", 1 => "System + User", 2 => "Многоходовой", _ => "?" }}");
        sb.AppendLine();

        var messages = BuildContextMessages(mode, system, message);

        sb.AppendLine("- Формируемый контекст");
        foreach (var msg in messages)
        {
            var role = msg.Role.ToUpperInvariant();
            var content = msg.Content?.ToString() ?? "";
            sb.AppendLine($"  [{role}] {Truncate(content, 200)}");
        }
        sb.AppendLine();

        var options = new LLMOptions
        {
            ApiKey    = apiKey,
            ModelName = model,
        };

        var settings = new GenerateSettings { MaxTokens = 512 };

        var client = new LLMWithOpenRouterClient(options);
        var sw = Stopwatch.StartNew();

        string response;
        try
        {
            response = client.SendToLLM(messages, settings)
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
        sb.AppendLine();
        sb.AppendLine("- Примечание");
        sb.AppendLine("  ContextExtention.FixContext автоматически нормализует");
        sb.AppendLine("  последовательность ролей: вставляет пустые user-сообщения");
        sb.AppendLine("  между двумя assistant, корректирует порядок tool-ответов и т.д.");

        return sb.ToString();
    }

    private static List<LLMMessage> BuildContextMessages(int mode, string system, string userMsg)
    {
        return mode switch
        {
            1 => // System + User
            [
                LLMMessage.CreateMessage(Roles.System, system),
                LLMMessage.CreateMessage(Roles.User, userMsg),
            ],

            2 => // Многоходовой диалог
            [
                LLMMessage.CreateMessage(Roles.System, system),
                LLMMessage.CreateMessage(Roles.User, "Привет! Расскажи о себе."),
                LLMMessage.CreateMessage(Roles.Assistant,
                    "Привет! Я — языковая модель, специализирующаяся на C# и .NET. " +
                    "Могу помочь с вопросами по платформе, паттернам проектирования, " +
                    "производительности и архитектуре."),
                LLMMessage.CreateMessage(Roles.User, userMsg),
            ],

            _ => // Новый диалог — только user
            [
                LLMMessage.CreateMessage(Roles.User, userMsg),
            ],
        };
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
