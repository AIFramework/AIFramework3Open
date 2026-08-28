using AI.Script.Docs;
using AI.Script.Hosting;
using AI.Script.Semantics;
using System.Reflection;
using System.Text;

namespace AI.Script.Llm;

/// <summary>
/// Сборка системного промпта: правила языка плюс перечень возможностей хоста.
/// </summary>
/// <remarks>
/// Два уровня, а не один: правила языка неизменны и вшиты в сборку, перечень пространств
/// строится по фактически подключённым модулям. Зашитый список функций рано или поздно
/// разошёлся бы с хостом, и модель уверенно писала бы вызовы того, чего в этом хосте нет.
/// </remarks>
public static class ScriptPrompt
{
    private const string CardResource = "AI.Script.Llm.PromptCard.md";

    private static string? s_card;

    /// <summary>Правила языка — текст карточки без заголовка репозитория.</summary>
    public static string Card => s_card ??= LoadCard();

    /// <summary>
    /// Системный промпт для написания скрипта под этот хост.
    /// </summary>
    /// <param name="host">Хост: из него берётся перечень пространств имён.</param>
    public static string System(ScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return new StringBuilder(Card)
            .Append("\n\n## Что доступно в этом хосте\n\n")
            .Append(host.DescribeCapabilities(ManifestOptions.Index))
            .Append("\n\nВ ответе — только текст скрипта, без пояснений до и после.\n")
            .ToString();
    }

    /// <summary>
    /// Сообщение с диагностиками для следующей попытки.
    /// </summary>
    /// <remarks>
    /// Диагностики отдаются целиком, вместе с подсказками и подчёркнутой строкой: они и
    /// написаны так, чтобы по ним можно было исправить не догадываясь. Пересказывать их своими
    /// словами — значит терять именно то, что отличает их от «ошибка синтаксиса».
    /// </remarks>
    public static string Repair(string script, IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var builder = new StringBuilder("Скрипт не прошёл проверку. Вот он:\n\n");

        _ = builder.Append(script).Append("\n\nДиагностики:\n\n");

        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error) continue;

            _ = builder.Append(diagnostic.Render()).Append("\n\n");
        }

        _ = builder.Append("Исправь ровно указанное и верни скрипт целиком.");

        return builder.ToString();
    }

    /// <summary>
    /// Достаёт текст скрипта из ответа модели.
    /// </summary>
    /// <remarks>
    /// Модель обрамляет код оградами, даже когда просили этого не делать; отдавать такой текст
    /// в разбор — значит получить отказ на первой же строке из-за трёх обратных кавычек.
    /// </remarks>
    public static string ExtractScript(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return string.Empty;

        int open = answer.IndexOf("```", StringComparison.Ordinal);

        if (open < 0) return answer.Trim();

        int lineEnd = answer.IndexOf('\n', open);

        if (lineEnd < 0) return answer.Trim();

        int close = answer.IndexOf("```", lineEnd, StringComparison.Ordinal);

        return close < 0
            ? answer[(lineEnd + 1)..].Trim()
            : answer[(lineEnd + 1)..close].Trim();
    }

    private static string LoadCard()
    {
        using Stream? stream = typeof(ScriptPrompt).Assembly.GetManifestResourceStream(CardResource);

        if (stream == null) return string.Empty;

        using var reader = new StreamReader(stream, Encoding.UTF8);

        string text = reader.ReadToEnd();

        // До первой горизонтальной черты идёт объяснение, зачем нужен файл; модели оно
        // говорит о языке ровно ничего.
        int start = text.IndexOf("\n---\n", StringComparison.Ordinal);

        return start < 0 ? text : text[(start + 5)..].TrimStart();
    }
}
