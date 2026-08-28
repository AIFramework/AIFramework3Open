using AI.Script.Syntax;
using System.Text;

namespace AI.Script.Semantics;

/// <summary>Строгость диагностики.</summary>
public enum DiagnosticSeverity
{
    /// <summary>Замечание: на выполнение не влияет.</summary>
    Info = 0,

    /// <summary>Предупреждение: скрипт выполняется, но, вероятно, не то, что задумано.</summary>
    Warning = 1,

    /// <summary>Ошибка: скрипт не будет запущен либо прерван.</summary>
    Error = 2,
}

/// <summary>
/// Сообщение о проблеме в скрипте: код, позиция, суть и — обязательно — что делать.
/// </summary>
/// <remarks>
/// Основной автор скриптов — языковая модель, и диагностика для неё является интерфейсом
/// языка ровно в той же мере, что и синтаксис. Поэтому <see cref="Hint"/> — не украшение:
/// сообщение без подсказки не даёт исправить ошибку, а лишь сообщает о ней.
/// </remarks>
public sealed class Diagnostic
{
    /// <summary>Код вида <c>AIS1101</c>; см. приложение C в DESIGN.md.</summary>
    public string Code { get; }

    /// <summary>Строгость.</summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>Отрезок исходника, к которому относится сообщение.</summary>
    public TextSpan Span { get; }

    /// <summary>Суть проблемы одной фразой.</summary>
    public string Message { get; }

    /// <summary>Что делать: подсказка, ближайшее имя, сигнатура.</summary>
    public string? Hint { get; }

    /// <summary>Исходный текст, если он известен: нужен для печати фрагмента.</summary>
    public SourceText? Source { get; }

    /// <summary>Создаёт диагностику.</summary>
    public Diagnostic(
        string code,
        DiagnosticSeverity severity,
        TextSpan span,
        string message,
        string? hint = null,
        SourceText? source = null)
    {
        Code = code;
        Severity = severity;
        Span = span;
        Message = message;
        Hint = hint;
        Source = source;
    }

    /// <summary>Позиция начала сообщения в терминах строк и колонок.</summary>
    public LinePosition Position => Source?.GetLinePosition(Span.Start) ?? new LinePosition(1, 1);

    /// <summary>Короткая однострочная запись: <c>файл:строка:колонка КОД: сообщение</c>.</summary>
    public override string ToString()
    {
        var position = Position;
        string file = Source?.FileName ?? "script.ais";
        return $"{file}:{position.Line}:{position.Column} {Code}: {Message}";
    }

    /// <summary>
    /// Развёрнутая запись с фрагментом исходника и подчёркиванием.
    /// </summary>
    public string Render()
    {
        var builder = new StringBuilder();
        var position = Position;
        string file = Source?.FileName ?? "script.ais";
        string severity = Severity switch
        {
            DiagnosticSeverity.Error => "ошибка",
            DiagnosticSeverity.Warning => "предупреждение",
            _ => "замечание",
        };

        _ = builder.AppendLine($"{file}:{position.Line}:{position.Column}  {Code}  {severity}: {Message}");

        if (Source != null)
        {
            string lineText = Source.GetLineText(position.Line);
            string gutter = position.Line.ToString();
            string pad = new(' ', gutter.Length);

            _ = builder.AppendLine($" {gutter} | {lineText}");

            int caretOffset = Math.Max(0, position.Column - 1);
            int caretLength = Math.Max(1, Math.Min(Span.Length, Math.Max(1, lineText.Length - caretOffset)));

            _ = builder.AppendLine($" {pad} | {new string(' ', caretOffset)}{new string('^', caretLength)}");
        }

        if (!string.IsNullOrWhiteSpace(Hint))
        {
            foreach (string hintLine in Hint.Split('\n'))
                _ = builder.AppendLine($"     = {hintLine.TrimEnd()}");
        }

        return builder.ToString().TrimEnd();
    }
}
