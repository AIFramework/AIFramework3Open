using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;

namespace AI.Script.Std;

/// <summary>
/// Таблица итогов опыта и запись в журнал.
/// </summary>
/// <remarks>
/// Итог опыта — таблица «параметры плюс метрики», а не сводка: сводку по ней можно посчитать
/// как угодно и сколько угодно раз, а из сводки исходные повторы уже не достать.
/// </remarks>
public static partial class ExpModule
{
    /// <summary>Таблица итогов: колонки плана, номер повтора и метрики.</summary>
    private static ScriptTable Outcomes(
        ScriptTable plan,
        IReadOnlyList<ScriptValue> points,
        IReadOnlyList<int> numbers,
        IReadOnlyList<ScriptValue> outcomes)
    {
        var metrics = new List<ScriptRecord>(outcomes.Count);

        foreach (ScriptValue outcome in outcomes) metrics.Add(Metrics(outcome));

        var columns = new List<ScriptColumn>(plan.ColumnCount + 1);

        foreach (ScriptColumn column in plan.Columns)
        {
            var cells = new ScriptValue[points.Count];

            for (int i = 0; i < points.Count; i++)
                cells[i] = points[i].AsRecord().TryGet(column.Name, out ScriptValue cell) ? cell : ScriptValue.None;

            columns.Add(ScriptColumn.Own(column.Name, cells));
        }

        var repeats = new ScriptValue[numbers.Count];

        for (int i = 0; i < numbers.Count; i++) repeats[i] = ScriptValue.Num(numbers[i]);

        columns.Add(ScriptColumn.Own(RepeatColumn, repeats));

        foreach (string name in Names(metrics))
        {
            var cells = new ScriptValue[metrics.Count];

            for (int i = 0; i < metrics.Count; i++)
                cells[i] = metrics[i].TryGet(name, out ScriptValue value) ? value : ScriptValue.None;

            columns.Add(ScriptColumn.Own(Unique(columns, name), cells));
        }

        return ScriptTable.Create(columns);
    }

    /// <summary>
    /// Имена метрик в порядке первого появления.
    /// </summary>
    /// <remarks>
    /// Испытание вправе вернуть в одном случае больше метрик, чем в другом (например, добавить
    /// причину отказа): колонка появляется от первого упоминания, а там, где метрики не было,
    /// остаётся пропуск — не ноль.
    /// </remarks>
    private static IReadOnlyList<string> Names(IReadOnlyList<ScriptRecord> metrics)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (ScriptRecord record in metrics)
        {
            foreach (var metric in record.Pairs())
            {
                if (seen.Add(metric.Key)) names.Add(metric.Key);
            }
        }

        return names;
    }

    /// <summary>Имя колонки, не совпадающее с уже занятыми: параметр и метрика бывают тёзками.</summary>
    private static string Unique(IReadOnlyList<ScriptColumn> columns, string name)
    {
        string result = name;

        while (columns.Any(column => string.Equals(column.Name, result, StringComparison.Ordinal))) result += "_";

        return result;
    }

    /// <summary>Записывает испытания в журнал прогона.</summary>
    /// <remarks>
    /// Расход пишется общий на испытание, а не по вызовам: скрипт видит только суммарный
    /// счётчик прогона, и делить его между ветвями значило бы выдумывать числа.
    /// </remarks>
    private static async Task WriteAsync(
        IScriptContext context,
        IReadOnlyList<ScriptValue> points,
        IReadOnlyList<int> numbers,
        IReadOnlyList<ScriptValue> outcomes,
        DateTimeOffset started,
        long elapsed,
        ExternalUsage spent)
    {
        if (context.Journal is not { } journal) return;

        // Номер берется из журнала и тут же занимается записью: два опыта, пишущие в один журнал
        // одновременно, иначе прочли бы одно и то же наибольшее значение и получили одинаковые номера.
        await journal.ExclusiveAsync(
            () => AppendTrialsAsync(context, journal, points, numbers, outcomes, started, elapsed, spent),
            context.Cancellation).ConfigureAwait(false);
    }

    private static async Task AppendTrialsAsync(
        IScriptContext context,
        IExperimentJournal journal,
        IReadOnlyList<ScriptValue> points,
        IReadOnlyList<int> numbers,
        IReadOnlyList<ScriptValue> outcomes,
        DateTimeOffset started,
        long elapsed,
        ExternalUsage spent)
    {
        IReadOnlyList<ExperimentEntry> before = await journal.ReadAsync(context.Cancellation).ConfigureAwait(false);

        // Следующий номер больше наибольшего записанного, а не число записей: журнал обрезается
        // по пределу, и номер по счету повторил бы номер старого испытания, а exp.replay по нему
        // повторил бы не то.
        long next = before.Select(entry => long.TryParse(entry.Id, out long id) ? id : -1).DefaultIfEmpty(-1).Max() + 1;
        long tokens = outcomes.Count == 0 ? 0 : spent.Tokens / outcomes.Count;
        decimal cost = outcomes.Count == 0 ? 0 : spent.Cost / outcomes.Count;

        for (int i = 0; i < outcomes.Count; i++)
        {
            var entry = new ExperimentEntry(
                (next + i).ToString(System.Globalization.CultureInfo.InvariantCulture),
                context.ScriptDigest,
                context.Seed,
                Plain(points[i].AsRecord(), numbers[i]),
                Plain(Metrics(outcomes[i]), null),
                started,
                elapsed,
                tokens,
                cost);

            await journal.AppendAsync(entry, context.Cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>Запись языка обычным словарём: журнал не должен знать типы языка.</summary>
    private static IReadOnlyDictionary<string, object?> Plain(ScriptRecord record, int? repeat)
    {
        var plain = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var field in record.Pairs()) plain[field.Key] = Marshaller.Unwrap(field.Value);

        if (repeat is int number) plain[RepeatColumn] = (double)number;

        return plain;
    }

    /// <summary>Журнал таблицей: номер, параметры, метрики и расход.</summary>
    private static ScriptTable Journal(IReadOnlyList<ExperimentEntry> entries)
    {
        if (entries.Count == 0) return ScriptTable.Empty;

        var columns = new List<ScriptColumn>
        {
            ScriptColumn.Own(IdColumn, [.. entries.Select(entry => ScriptValue.Str(entry.Id))]),
            ScriptColumn.Own("script", [.. entries.Select(entry => ScriptValue.Str(entry.Script))]),
            ScriptColumn.Own("seed", [.. entries.Select(entry => ScriptValue.Num(entry.Seed))]),
            ScriptColumn.Own("started", [.. entries.Select(entry => ScriptValue.Date(entry.Started.UtcDateTime))]),
            ScriptColumn.Own("ms", [.. entries.Select(entry => ScriptValue.Num(entry.ElapsedMs))]),
            ScriptColumn.Own("tokens", [.. entries.Select(entry => ScriptValue.Num(entry.Tokens))]),
            ScriptColumn.Own("cost", [.. entries.Select(entry => ScriptValue.Dec(entry.Cost))]),
        };

        foreach (string name in Keys(entries, entry => entry.Parameters))
            columns.Add(Column(columns, name, entries, entry => entry.Parameters));

        foreach (string name in Keys(entries, entry => entry.Metrics))
            columns.Add(Column(columns, name, entries, entry => entry.Metrics));

        return ScriptTable.Create(columns);
    }

    private static IReadOnlyList<string> Keys(
        IReadOnlyList<ExperimentEntry> entries,
        Func<ExperimentEntry, IReadOnlyDictionary<string, object?>> part)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (ExperimentEntry entry in entries)
        {
            foreach (string key in part(entry).Keys)
            {
                if (seen.Add(key)) names.Add(key);
            }
        }

        return names;
    }

    private static ScriptColumn Column(
        IReadOnlyList<ScriptColumn> taken,
        string name,
        IReadOnlyList<ExperimentEntry> entries,
        Func<ExperimentEntry, IReadOnlyDictionary<string, object?>> part)
    {
        var cells = new ScriptValue[entries.Count];

        for (int i = 0; i < entries.Count; i++)
        {
            cells[i] = part(entries[i]).TryGetValue(name, out object? value)
                ? Marshaller.FromClr(value)
                : ScriptValue.None;
        }

        return ScriptColumn.Own(Unique(taken, name), cells);
    }
}
