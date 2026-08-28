using System.Globalization;
using System.Text;

namespace AI.Script.Hosting;

/// <summary>Чем закончилось выполнение стадии.</summary>
public enum StageOutcome
{
    /// <summary>Стадия посчитана.</summary>
    Computed,

    /// <summary>Результат взят из кэша.</summary>
    Cached,

    /// <summary>Стадия сорвалась.</summary>
    Failed,
}

/// <summary>
/// Один вызов стадии в графе прогона.
/// </summary>
/// <remarks>
/// Узел — это вызов, а не объявление: одна и та же стадия, вызванная с разными аргументами,
/// даёт два узла. Иначе граф врал бы про то, что происходило на самом деле.
/// </remarks>
public sealed class StageNode
{
    /// <summary>Номер узла в пределах прогона.</summary>
    public int Id { get; init; }

    /// <summary>Имя стадии.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Номер вызывающего узла; <c>null</c> — вызов с верхнего уровня.</summary>
    public int? Caller { get; init; }

    /// <summary>Ключ кэша; <c>null</c>, если стадия некэшируема.</summary>
    public string? Key { get; set; }

    /// <summary>Почему стадия некэшируема; <c>null</c>, если кэш возможен.</summary>
    public string? NotCacheable { get; set; }

    /// <summary>Итог.</summary>
    public StageOutcome Outcome { get; set; }

    /// <summary>Сколько попыток потребовалось.</summary>
    public int Attempts { get; set; } = 1;

    /// <summary>Сколько времени заняло.</summary>
    public TimeSpan Elapsed { get; set; }

    /// <summary>Краткое описание результата.</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>Сообщение об отказе; <c>null</c>, если стадия отработала.</summary>
    public string? Error { get; set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        string outcome = Outcome switch
        {
            StageOutcome.Cached => "из кэша",
            StageOutcome.Failed => "отказ",
            _ => $"{Elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)} мс",
        };

        string attempts = Attempts > 1 ? $", попыток: {Attempts}" : string.Empty;

        return $"{Name} — {outcome}{attempts}";
    }
}

/// <summary>
/// Граф прогона: какие стадии вызывались, откуда и чем закончились.
/// </summary>
/// <remarks>
/// Граф выводится из фактических вызовов, а не объявляется в скрипте: отдельная конструкция
/// «объявить конвейер» дублировала бы то, что рантайм и так знает, и умела бы расходиться с
/// действительностью.
/// </remarks>
public sealed class RunGraph
{
    private readonly List<StageNode> _nodes = [];

    /// <summary>Узлы в порядке начала выполнения.</summary>
    public IReadOnlyList<StageNode> Nodes => _nodes;

    /// <summary>Есть ли в графе хоть один узел.</summary>
    public bool IsEmpty => _nodes.Count == 0;

    /// <summary>Сколько стадий взято из кэша.</summary>
    public int CachedCount
    {
        get
        {
            int count = 0;

            foreach (StageNode node in _nodes)
            {
                if (node.Outcome == StageOutcome.Cached) count++;
            }

            return count;
        }
    }

    /// <summary>Заводит узел для нового вызова стадии.</summary>
    public StageNode Add(string name, int? caller)
    {
        var node = new StageNode { Id = _nodes.Count, Name = name, Caller = caller };

        _nodes.Add(node);

        return node;
    }

    /// <summary>Отчёт для человека: список стадий с итогами.</summary>
    public string Render()
    {
        if (_nodes.Count == 0) return "Стадий не было.";

        var lines = new List<string>(_nodes.Count + 1) { "Стадии прогона:" };

        foreach (StageNode node in _nodes)
        {
            string indent = node.Caller == null ? "  " : "    ";
            string result = string.IsNullOrEmpty(node.Result) ? string.Empty : $" → {node.Result}";

            lines.Add($"{indent}{node}{result}");

            if (node.NotCacheable != null) lines.Add($"{indent}  (не кэшируется: {node.NotCacheable})");
            if (node.Error != null) lines.Add($"{indent}  отказ: {node.Error}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Граф в записи Mermaid — то, что рисует хост.
    /// </summary>
    /// <remarks>
    /// Mermaid, а не собственный формат: его рисуют и Markdown-просмотрщики, и веб-хост, и
    /// вставка в отчёт не требует ни одной строки кода на стороне вызывающего.
    /// </remarks>
    public string ToMermaid()
    {
        if (_nodes.Count == 0) return "graph TD\n    empty[\"стадий не было\"]";

        var builder = new StringBuilder("graph TD\n");

        foreach (StageNode node in _nodes)
        {
            string label = $"{node.Name}<br/>{Describe(node)}";

            _ = builder.Append("    ").Append('n').Append(node.Id)
                .Append("[\"").Append(Escape(label)).Append("\"]").Append('\n');
        }

        foreach (StageNode node in _nodes)
        {
            if (node.Caller is not int caller) continue;

            _ = builder.Append("    n").Append(caller).Append(" --> n").Append(node.Id).Append('\n');
        }

        // Класс на узел, а не цвет в подписи: так вид задаётся темой хоста, а не графом.
        foreach (StageNode node in _nodes)
        {
            string style = node.Outcome switch
            {
                StageOutcome.Cached => "cached",
                StageOutcome.Failed => "failed",
                _ => "computed",
            };

            _ = builder.Append("    class n").Append(node.Id).Append(' ').Append(style).Append('\n');
        }

        return builder.ToString().TrimEnd('\n');
    }

    private static string Describe(StageNode node) => node.Outcome switch
    {
        StageOutcome.Cached => "из кэша",
        StageOutcome.Failed => "отказ",
        _ => $"{node.Elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)} мс",
    };

    private static string Escape(string text) => text.Replace("\"", "&quot;", StringComparison.Ordinal);
}
