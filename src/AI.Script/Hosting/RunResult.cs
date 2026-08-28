using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Hosting;

/// <summary>Артефакт прогона: то, на что пользователь смотрит.</summary>
public sealed class ScriptArtifact
{
    /// <summary>Вид артефакта: <c>value</c>, <c>table</c>, <c>plot</c>, <c>image</c>.</summary>
    public string Kind { get; init; } = "value";

    /// <summary>Заголовок.</summary>
    public string? Title { get; init; }

    /// <summary>Текстовое представление.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Значение как объект C#.</summary>
    public object? Value { get; init; }
}

/// <summary>Счётчики прогона.</summary>
public sealed class RunStats
{
    /// <summary>Сделано шагов интерпретатора.</summary>
    public int Steps { get; init; }

    /// <summary>Выделено элементов данных.</summary>
    public long Allocations { get; init; }

    /// <summary>Сколько вызовов функций сделано.</summary>
    public int Calls { get; init; }

    /// <summary>Сколько стадий выполнено.</summary>
    public int Stages { get; init; }

    /// <summary>Сколько стадий взято из кэша.</summary>
    public int CachedStages { get; init; }

    /// <summary>Сколько платных внешних вызовов сделано.</summary>
    public int ExternalCalls { get; init; }

    /// <summary>Сколько токенов израсходовано.</summary>
    public long ExternalTokens { get; init; }

    /// <summary>Сколько потрачено в единицах биллинга.</summary>
    public decimal ExternalCost { get; init; }

    /// <summary>Затраченное время.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <inheritdoc/>
    public override string ToString()
    {
        string stages = Stages == 0
            ? string.Empty
            : $", стадий: {Stages} (из кэша: {CachedStages})";

        string external = ExternalCalls == 0
            ? string.Empty
            : $", внешних вызовов: {ExternalCalls} (токенов: {ExternalTokens}, стоимость: {ExternalCost})";

        return $"шагов: {Steps}, вызовов: {Calls}{stages}{external}, элементов: {Allocations}, " +
            $"время: {Elapsed.TotalMilliseconds:F0} мс";
    }
}

/// <summary>Итог проверки скрипта без запуска.</summary>
public sealed class CheckResult
{
    /// <summary>Прошла ли проверка без ошибок.</summary>
    public bool Success { get; init; }

    /// <summary>Все сообщения, отсортированные по позиции.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    /// <summary>Развёрнутый отчёт для человека и модели.</summary>
    public string Render()
    {
        if (Diagnostics.Count == 0) return "Проверка пройдена: замечаний нет.";

        var lines = new List<string>(Diagnostics.Count);
        foreach (Diagnostic diagnostic in Diagnostics) lines.Add(diagnostic.Render());

        return string.Join("\n\n", lines);
    }
}

/// <summary>
/// Итог прогона.
/// </summary>
/// <remarks>
/// Успех, транскрипт и причина отказа разнесены по разным полям намеренно. Пока причина
/// лежала внутри транскрипта (как в прежнем вычислителе), вызывающий отличал сорвавшийся
/// скрипт от удачного только разбором текста — и инструмент чата докладывал об успехе, положив
/// внутрь сообщение об ошибке.
/// </remarks>
public sealed class RunResult
{
    /// <summary>Отработал ли скрипт до конца.</summary>
    public bool Success { get; init; }

    /// <summary>Диагностика: ошибки разбора, проверки и исполнения.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    /// <summary>Напечатанное скриптом, включая напечатанное до срыва.</summary>
    public IReadOnlyList<string> Transcript { get; init; } = [];

    /// <summary>Именованные результаты (<c>emit</c>) как объекты C#.</summary>
    public IReadOnlyDictionary<string, object?> Emitted { get; init; } = new Dictionary<string, object?>();

    /// <summary>Артефакты (<c>show</c>).</summary>
    public IReadOnlyList<ScriptArtifact> Artifacts { get; init; } = [];

    /// <summary>Счётчики.</summary>
    public RunStats Stats { get; init; } = new();

    /// <summary>
    /// Граф прогона: какие стадии вызывались, откуда и чем закончились.
    /// </summary>
    /// <remarks>
    /// Пуст, если стадий в скрипте нет. Граф отдаётся и при отказе: узел сорвавшейся стадии —
    /// это ровно то место, с которого начинают разбираться.
    /// </remarks>
    public RunGraph Graph { get; init; } = new();

    /// <summary>Первая ошибка либо <c>null</c>.</summary>
    public Diagnostic? Error
    {
        get
        {
            foreach (Diagnostic diagnostic in Diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error) return diagnostic;
            }

            return null;
        }
    }

    /// <summary>Отчёт для человека: транскрипт, результаты и причина отказа.</summary>
    public string Render()
    {
        var lines = new List<string>();

        foreach (string line in Transcript) lines.Add(line);

        if (Emitted.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Результаты:");

            foreach (var pair in Emitted)
                lines.Add($"  {pair.Key} = {ScriptFormatter.Format(Marshalled(pair.Value))}");
        }

        foreach (Diagnostic diagnostic in Diagnostics)
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error) continue;

            lines.Add(string.Empty);
            lines.Add(diagnostic.Render());
        }

        return string.Join("\n", lines);
    }

    private static ScriptValue Marshalled(object? value) => Binding.Marshaller.FromClr(value);
}
