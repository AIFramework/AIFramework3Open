using AI.LLM.Clients.ImageGeneration;
using AiFrameworkDemo.Core;
using System.Net;
using System.Security;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.LLM;

public static partial class LlmDemoRunner
{
    // Тестовые URL для демонстрации работы фильтра
    private static readonly (string Url, string Comment)[] TestUrls =
    [
        ("https://oaidalleapiprodscus.blob.core.windows.net/private/img.png", "Хост OpenAI DALL-E"),
        ("https://cdn.openai.com/images/example.jpg",                         "CDN OpenAI"),
        ("https://images.example.com/photo.webp",                             "Сторонний CDN"),
        ("http://localhost/admin",                                             "Loopback (localhost)"),
        ("http://127.0.0.1:8080/secret",                                      "Loopback (127.0.0.1)"),
        ("http://192.168.1.1/router",                                         "Приватная сеть 192.168.x"),
        ("http://10.0.0.5/internal",                                          "Приватная сеть 10.x"),
        ("http://172.20.0.3/service",                                         "Приватная сеть 172.16-31.x"),
        ("http://169.254.169.254/latest/meta-data/",                          "Cloud metadata (AWS/Azure)"),
        ("file:///etc/passwd",                                                "Схема file://"),
        ("ftp://files.example.com/data",                                      "Схема ftp://"),
    ];

    private static string DoSsrfGuard(
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp)
    {
        var mode      = I(p, "ssrfMode", 0);
        var customUrl = T(tp, "_customUrl", "").Trim();

        var guard = mode switch
        {
            0 => SsrfGuardOptions.Default,
            1 => SsrfGuardOptions.OpenAiOnly,
            2 => new SsrfGuardOptions
            {
                AllowedHosts   = ["images.example.com", "cdn.mycompany.com"],
                AllowedSchemes = ["https"],
            },
            3 => SsrfGuardOptions.Disabled,
            _ => SsrfGuardOptions.Default,
        };

        var sb = new StringBuilder();
        sb.AppendLine("> SSRF Guard — проверка URL изображений");
        sb.AppendLine();
        sb.AppendLine($"  Режим: {DescribeMode(mode)}");
        sb.AppendLine($"  Проверка включена: {guard.Enabled}");
        sb.AppendLine($"  Блокировка приватных IP: {guard.BlockPrivateRanges}");
        sb.AppendLine($"  Разрешённые схемы: {string.Join(", ", guard.AllowedSchemes)}");

        if (guard.AllowedHosts.Count > 0)
            sb.AppendLine($"  Разрешённые хосты: {string.Join(", ", guard.AllowedHosts)}");
        else
            sb.AppendLine("  Разрешённые хосты: (все публичные)");

        sb.AppendLine();
        sb.AppendLine("- Пример создания APIImageGenerator");
        sb.AppendLine();
        sb.AppendLine(BuildConstructorExample(mode));
        sb.AppendLine();
        sb.AppendLine("- Проверка тестовых URL");
        sb.AppendLine();

        // Заголовок таблицы
        sb.AppendLine($"  {"Результат",-10} {"URL",-60} Пояснение");
        sb.AppendLine($"  {new string('-', 10)} {new string('-', 60)} {new string('-', 20)}");

        foreach (var (url, comment) in TestUrls)
            AppendUrlCheck(sb, guard, url, comment);

        // Пользовательский URL
        if (!string.IsNullOrEmpty(customUrl))
        {
            sb.AppendLine();
            sb.AppendLine("- Ваш URL");
            AppendUrlCheck(sb, guard, customUrl, "введён вручную");
        }

        sb.AppendLine();
        sb.AppendLine("- Как работает защита");
        sb.AppendLine();
        sb.AppendLine("  Когда LLM возвращает URL вместо base64 data-URI,");
        sb.AppendLine("  APIImageGenerator вызывает SsrfGuardOptions.Validate(url)");
        sb.AppendLine("  ДО выполнения HTTP-запроса. При нарушении политики");
        sb.AppendLine("  выбрасывается SecurityException — запрос не совершается.");

        return sb.ToString();
    }

    private static void AppendUrlCheck(StringBuilder sb, SsrfGuardOptions guard, string url, string comment)
    {
        string status;
        string detail;

        try
        {
            guard.Validate(url);
            status = "[PASS]";
            detail = "";
        }
        catch (SecurityException ex)
        {
            status = "[BLOCK]";
            // Оставляем только первое предложение из сообщения об ошибке
            var msg = ex.Message;
            var dot = msg.IndexOf('.');
            detail = dot > 0 ? msg[..dot] : msg;
        }
        catch (Exception ex)
        {
            status = "[ERROR]";
            detail = ex.Message;
        }

        var displayUrl = url.Length > 58 ? url[..55] + "…" : url;
        sb.AppendLine($"  {status,-10} {displayUrl,-60} {comment}");

        if (!string.IsNullOrEmpty(detail))
            sb.AppendLine($"  {"",10}   -> {detail}");
    }

    private static string DescribeMode(int mode) => mode switch
    {
        0 => "Default (блокировка приватных IP, хост не ограничен)",
        1 => "OpenAiOnly (только хосты OpenAI DALL-E)",
        2 => "CustomHosts (только images.example.com, cdn.mycompany.com, только HTTPS)",
        3 => "Disabled (проверка отключена)",
        _ => "Default",
    };

    private static string BuildConstructorExample(int mode) => mode switch
    {
        0 =>
            """
              // По умолчанию — блокирует приватные IP, хост не ограничен
              var generator = new APIImageGenerator(llmApi);
              // или явно:
              var generator = new APIImageGenerator(llmApi, SsrfGuardOptions.Default);
            """,
        1 =>
            """
              // Только хосты OpenAI DALL-E
              var generator = new APIImageGenerator(llmApi, SsrfGuardOptions.OpenAiOnly);
            """,
        2 =>
            """
              // Произвольный список хостов, только HTTPS
              var generator = new APIImageGenerator(llmApi, new SsrfGuardOptions
              {
                  AllowedHosts   = ["images.example.com", "cdn.mycompany.com"],
                  AllowedSchemes = ["https"],
              });
              // или кратко:
              var generator = new APIImageGenerator(llmApi,
                  SsrfGuardOptions.WithHosts("images.example.com", "cdn.mycompany.com"));
            """,
        3 =>
            """
              // Проверка отключена — только для изолированных dev-сред!
              var generator = new APIImageGenerator(llmApi, SsrfGuardOptions.Disabled);
            """,
        _ => "",
    };
}
