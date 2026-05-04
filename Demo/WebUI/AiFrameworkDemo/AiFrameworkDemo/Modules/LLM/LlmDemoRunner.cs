using AiFrameworkDemo.Core;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.LLM;

/// <summary>Диспетчер демо-сценариев AI.LLM.</summary>
public static partial class LlmDemoRunner
{
    private static readonly string[] ModelNames =
    [
        "google/gemini-2.0-flash-001",
        "deepseek/deepseek-chat-v3-0324",
        "anthropic/claude-sonnet-4",
        "openai/gpt-4.1-mini",
    ];

    public static DemoResult Run(
        string key,
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp,
        DemoSettings s)
    {
        string txt;
        try
        {
            txt = key switch
            {
                "llm_chat"    => DoChat(p, tp),
                "llm_context" => DoContext(p, tp),
                "sk_demo"     => DoSemanticKernel(p, tp),
                "ssrf_guard"  => DoSsrfGuard(p, tp),
                _             => $"Неизвестный ключ «{key}»",
            };
        }
        catch (Exception ex)
        {
            txt = $"Ошибка: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()}";
        }

        return new DemoResult { TextOutput = txt };
    }

    private static string GetApiKey(IReadOnlyDictionary<string, string> tp) =>
        T(tp, "_apikey", "").Trim();

    private static string GetModel(IReadOnlyDictionary<string, double> p) =>
        ModelNames[Math.Clamp(I(p, "model", 0), 0, ModelNames.Length - 1)];

    private static string RequireApiKey(IReadOnlyDictionary<string, string> tp)
    {
        var key = GetApiKey(tp);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "API-ключ OpenRouter не указан.\n" +
                "Получите бесплатный ключ на https://openrouter.ai/keys " +
                "и вставьте в поле «API-ключ OpenRouter».");
        return key;
    }
}
