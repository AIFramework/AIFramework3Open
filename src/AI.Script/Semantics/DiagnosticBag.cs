using AI.Script.Syntax;
using System.Collections;

namespace AI.Script.Semantics;

/// <summary>
/// Накопитель диагностик одного разбора или прогона.
/// </summary>
public sealed class DiagnosticBag : IReadOnlyCollection<Diagnostic>
{
    private readonly List<Diagnostic> _items = [];
    private readonly SourceText? _source;

    /// <summary>Создаёт накопитель, привязанный к исходному тексту.</summary>
    public DiagnosticBag(SourceText? source = null) => _source = source;

    /// <summary>Число накопленных сообщений.</summary>
    public int Count => _items.Count;

    /// <summary>Есть ли хотя бы одна ошибка.</summary>
    public bool HasErrors => _items.Exists(item => item.Severity == DiagnosticSeverity.Error);

    /// <summary>Добавляет ошибку.</summary>
    public void Error(string code, TextSpan span, string message, string? hint = null) =>
        Add(new Diagnostic(code, DiagnosticSeverity.Error, span, message, hint, _source));

    /// <summary>Добавляет предупреждение.</summary>
    public void Warning(string code, TextSpan span, string message, string? hint = null) =>
        Add(new Diagnostic(code, DiagnosticSeverity.Warning, span, message, hint, _source));

    /// <summary>Добавляет замечание.</summary>
    public void Info(string code, TextSpan span, string message, string? hint = null) =>
        Add(new Diagnostic(code, DiagnosticSeverity.Info, span, message, hint, _source));

    /// <summary>Добавляет готовую диагностику.</summary>
    public void Add(Diagnostic diagnostic) => _items.Add(diagnostic);

    /// <summary>Отдаёт накопленное, отсортированное по позиции.</summary>
    public IReadOnlyList<Diagnostic> ToList()
    {
        var sorted = new List<Diagnostic>(_items);
        sorted.Sort((left, right) => left.Span.Start.CompareTo(right.Span.Start));
        return sorted;
    }

    /// <inheritdoc/>
    public IEnumerator<Diagnostic> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
