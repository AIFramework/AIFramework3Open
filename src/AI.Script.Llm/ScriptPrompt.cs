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

    /// <summary>Начало заметки о модулях: <c>&lt;!-- ns: doc xls --&gt;</c>.</summary>
    private const string NoteMarker = "<!-- ns:";

    private static string? s_text;

    /// <summary>Правила языка — ядро карточки без заголовка репозитория и без заметок о модулях.</summary>
    public static string Card => Core(Text);

    /// <summary>
    /// Карточка для этого хоста: ядро и заметки о тех модулях, что в нём есть. Заметка о модуле,
    /// которого нет, заставила бы модель планировать вызовы, которые откажут.
    /// </summary>
    public static string CardFor(ScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var card = new StringBuilder(Card);

        foreach ((IReadOnlyList<string> namespaces, string note) in Notes(Text))
        {
            if (namespaces.Any(host.Registry.HasNamespace)) card.Append("\n\n").Append(note);
        }

        return card.ToString();
    }

    /// <summary>
    /// Системный промпт для написания скрипта под этот хост.
    /// </summary>
    /// <param name="host">Хост: из него берётся перечень пространств имён.</param>
    public static string System(ScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return new StringBuilder(CardFor(host))
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

        // Переводы строк приводятся к одному виду: карточка уходит в промпт как есть, и
        // рядом с ней не должно быть двух разных промптов в зависимости от того, чем её
        // разметил чекаут.
        string text = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);

        // До первой горизонтальной черты идёт объяснение, зачем нужен файл; модели оно
        // говорит о языке ровно ничего.
        int start = text.IndexOf("\n---\n", StringComparison.Ordinal);

        return start < 0 ? text : text[(start + 5)..].TrimStart();
    }

    /// <summary>Текст карточки после заголовка: ядро и заметки о модулях.</summary>
    private static string Text => s_text ??= LoadCard();

    private static string Core(string text)
    {
        int notes = text.IndexOf(NoteMarker, StringComparison.Ordinal);

        return (notes < 0 ? text : text[..notes]).TrimEnd();
    }

    /// <summary>Заметки о модулях: пространства из метки и текст до следующей метки.</summary>
    private static IEnumerable<(IReadOnlyList<string> Namespaces, string Note)> Notes(string text)
    {
        int at = text.IndexOf(NoteMarker, StringComparison.Ordinal);

        while (at >= 0)
        {
            int close = text.IndexOf("-->", at, StringComparison.Ordinal);

            if (close < 0) yield break;

            string[] namespaces = text[(at + NoteMarker.Length)..close].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int next = text.IndexOf(NoteMarker, close, StringComparison.Ordinal);
            string note = (next < 0 ? text[(close + 3)..] : text[(close + 3)..next]).Trim();

            yield return (namespaces, note);
            at = next;
        }
    }
}
