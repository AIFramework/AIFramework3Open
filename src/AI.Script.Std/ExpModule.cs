using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Diagnostics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>exp</c>: опыт как план, повторы и журнал.
/// </summary>
/// <remarks>
/// «Сравним три подхода и две модели по пять раз» — это тридцать прогонов, таблица итогов и
/// вывод, который можно проверить. Пока такого набора нет, сравнение пишется циклом руками:
/// каждый раз по-своему, без повторов, с победителем по разнице в третьем знаке.
/// <para>
/// Библиотека, а не новый синтаксис: план — обычная таблица, испытание — обычная лямбда,
/// параллельность даёт тот же механизм, что и <c>core.map</c>. Язык от этого не растёт, а
/// проверка до запуска видит обычные вызовы.
/// </para>
/// <para>
/// Каждое испытание записывается в журнал прогона само: запись, которую надо не забыть
/// сделать, рано или поздно не делается, и повторить опыт становится нечем.
/// </para>
/// </remarks>
[ScriptModule("exp", "Опыты: сетка параметров, повторы, журнал и сравнение", Version = "0.1", Group = "данные")]
public static partial class ExpModule
{
    /// <summary>Имя колонки с номером повтора.</summary>
    public const string RepeatColumn = "repeat";

    /// <summary>Имя колонки с номером испытания в журнале.</summary>
    public const string IdColumn = "id";

    /// <summary>Больше стольких испытаний один опыт не ставит: это уже не опыт, а ошибка в плане.</summary>
    public const int MaxTrials = 1_000_000;

    [ScriptFn("grid", "План опыта: все сочетания значений параметров",
        Example = "exp.grid({ temp: [0.2, 0.7], модель: [\"a\", \"b\"] })")]
    public static ScriptTable Grid(
        IScriptContext context,
        [ScriptParam("запись «параметр → список значений»")] ScriptRecord axes)
    {
        var names = new List<string>();
        var options = new List<IReadOnlyList<ScriptValue>>();

        foreach (var axis in axes.Pairs())
        {
            names.Add(axis.Key);
            options.Add(Options(axis.Key, axis.Value));
        }

        int rows = 1;

        foreach (var values in options) rows *= values.Count;

        if (rows == 0) return ScriptTable.Empty;

        var columns = new List<ScriptColumn>(names.Count);
        int repeat = rows;

        for (int i = 0; i < names.Count; i++)
        {
            // Значение оси повторяется блоками: первая ось меняется реже всех, последняя чаще.
            // Так план читается сверху вниз как перечисление, а не как случайный порядок.
            repeat /= options[i].Count;

            var cells = new ScriptValue[rows];

            for (int row = 0; row < rows; row++) cells[row] = options[i][row / repeat % options[i].Count];

            columns.Add(ScriptColumn.Own(names[i], cells));
        }

        context.CountAllocation((long)rows * Math.Max(1, names.Count));

        return ScriptTable.Create(columns);
    }

    /// <summary>
    /// Исполняет план: каждая точка столько раз, сколько заказано повторов.
    /// </summary>
    /// <remarks>
    /// Повторы — не украшение: без них разница двух средних по одному прогону на каждую точку
    /// неотличима от разброса, а победитель объявляется по шуму. Поэтому номер повтора остаётся
    /// в таблице итогов: сравнение считается по повторам, а не по единственному числу.
    /// <para>
    /// Каждая ветвь получает собственный поток случайных чисел, выведенный из зерна прогона, —
    /// повтор всего опыта с тем же <c>seed</c> даёт ту же таблицу.
    /// </para>
    /// </remarks>
    [ScriptFn("run", "Исполняет план: испытание на каждую точку, с повторами",
        Example = "план |> exp.run(опыт, repeat: 5)")]
    public static async Task<ScriptTable> Run(
        IScriptContext context,
        [ScriptParam("план: таблица параметров")] ScriptTable plan,
        [ScriptParam("испытание: функция от записи параметров")] ScriptCallable trial,
        [ScriptParam("сколько раз повторить каждую точку; с until — не больше стольких")] int repeat = 1,
        [ScriptParam("считать испытания одновременно")] bool parallel = false,
        [ScriptParam("когда остановиться раньше: { ci_width, metric, cost, tokens }")] ScriptRecord? until = null)
    {
        if (repeat < 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "exp.run: число повторов должно быть больше нуля");

        if (plan.RowCount == 0) return ScriptTable.Empty;

        long trials = (long)plan.RowCount * repeat;

        if (trials > MaxTrials)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"exp.run: {plan.RowCount} точек по {repeat} повторов это {trials} испытаний при пределе {MaxTrials}",
                "уменьшите план либо число повторов; остановку раньше задает until");
        }

        if (context.Pilot is { } pilot) await PilotAsync(context, pilot, plan, trial, repeat).ConfigureAwait(false);

        if (until is not null) return await RunUntilAsync(context, plan, trial, repeat, parallel, until).ConfigureAwait(false);

        var points = new List<ScriptValue>((int)trials);
        var numbers = new List<int>((int)trials);

        for (int row = 0; row < plan.RowCount; row++)
        {
            for (int number = 0; number < repeat; number++)
            {
                points.Add(ScriptValue.Record(plan.Row(row)));
                numbers.Add(number);
            }
        }

        ScriptValue[] outcomes = await RoundAsync(context, trial, points, numbers, parallel).ConfigureAwait(false);

        return Outcomes(plan, points, numbers, outcomes);
    }

    /// <summary>Считает пачку испытаний, пишет их в журнал и учитывает память.</summary>
    private static async Task<ScriptValue[]> RoundAsync(
        IScriptContext context,
        ScriptCallable trial,
        IReadOnlyList<ScriptValue> points,
        IReadOnlyList<int> numbers,
        bool parallel)
    {
        var started = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();
        ExternalUsage before = context.Usage;
        ScriptValue[] outcomes = await context
            .CallEachAsync(ScriptValue.Fn(trial), points, parallel ? context.Parallelism : 1)
            .ConfigureAwait(false);

        long elapsed = clock.ElapsedMilliseconds / Math.Max(1, outcomes.Length);
        ExternalUsage after = context.Usage;
        var spent = new ExternalUsage(after.Calls - before.Calls, after.Tokens - before.Tokens, after.Cost - before.Cost);

        await WriteAsync(context, points, numbers, outcomes, started, elapsed, spent).ConfigureAwait(false);

        context.CountAllocation(outcomes.Length);

        return outcomes;
    }

    [ScriptFn("log", "Журнал опытов: испытания с параметрами, метриками и расходом",
        Example = "emit trials = exp.log()")]
    public static async Task<ScriptTable> Log(IScriptContext context)
    {
        if (context.Journal is not { } journal) return ScriptTable.Empty;

        IReadOnlyList<ExperimentEntry> entries = await journal
            .ReadAsync(context.Cancellation)
            .ConfigureAwait(false);

        return Journal(entries);
    }

    /// <summary>
    /// Повторяет записанное испытание тем же испытанием и сверяет метрики.
    /// </summary>
    /// <remarks>
    /// Не «показать запись», а именно ПОВТОРИТЬ: вопрос к опыту недельной давности всегда один
    /// — получится ли то же самое. Если звенья опыта оформлены стадиями с кэшем, повтор
    /// обходится без сети и без денег, и ответ получается сразу.
    /// </remarks>
    [ScriptFn("replay", "Повторяет испытание из журнала и сверяет метрики",
        Example = "exp.replay(\"3\", опыт).same")]
    public static async Task<ScriptRecord> Replay(
        IScriptContext context,
        [ScriptParam("номер испытания из журнала")] string id,
        [ScriptParam("испытание: та же функция, что и в опыте")] ScriptCallable trial)
    {
        if (context.Journal is not { } journal)
            throw new ScriptError(DiagnosticCodes.FunctionFailed, "exp.replay: журнал опытов хостом не подключён");

        IReadOnlyList<ExperimentEntry> entries = await journal.ReadAsync(context.Cancellation).ConfigureAwait(false);
        ExperimentEntry? found = null;

        foreach (ExperimentEntry entry in entries)
        {
            if (string.Equals(entry.Id, id, StringComparison.Ordinal)) found = entry;
        }

        if (found == null)
        {
            throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"exp.replay: испытания «{id}» нет в журнале",
                entries.Count == 0 ? "журнал пуст" : $"записано испытаний: {entries.Count}");
        }

        ScriptValue parameters = Marshaller.FromClr(found.Parameters);
        ScriptValue now = await context.CallAsync(ScriptValue.Fn(trial), parameters).ConfigureAwait(false);
        ScriptRecord fresh = Metrics(now);
        ScriptRecord was = Marshaller.FromClr(found.Metrics).AsRecord();

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>(IdColumn, ScriptValue.Str(found.Id)),
            new KeyValuePair<string, ScriptValue>("same", ScriptValue.Bool(Same(was, fresh))),
            new KeyValuePair<string, ScriptValue>("was", ScriptValue.Record(was)),
            new KeyValuePair<string, ScriptValue>("now", ScriptValue.Record(fresh)),
        ]);
    }

    /// <summary>Значения оси плана: список либо одно значение.</summary>
    private static IReadOnlyList<ScriptValue> Options(string axis, ScriptValue values)
    {
        if (values.Type != ScriptType.List) return [values];

        ScriptList list = values.AsList();

        if (list.Count == 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, $"exp.grid: у параметра «{axis}» нет значений");

        var items = new ScriptValue[list.Count];

        for (int i = 0; i < list.Count; i++) items[i] = list[i];

        return items;
    }

    /// <summary>
    /// Итог испытания записью метрик.
    /// </summary>
    /// <remarks>
    /// Одно число тоже принимается: «сколько попало в цель» — самый частый опыт, и требовать
    /// ради него записи из одного поля значит заставлять писать лишнее в каждой строке.
    /// </remarks>
    private static ScriptRecord Metrics(ScriptValue outcome)
    {
        if (outcome.Type == ScriptType.Record) return outcome.AsRecord();

        if (outcome.Type is ScriptType.Num or ScriptType.Dec or ScriptType.Bool)
            return ScriptRecord.From([new KeyValuePair<string, ScriptValue>("value", outcome)]);

        throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"exp.run: испытание вернуло {outcome.Type.ToName()}, а нужны метрики",
            "верните запись вида { опора: 0.8, стоимость: 12 } либо одно число");
    }

    private static bool Same(ScriptRecord was, ScriptRecord now)
    {
        foreach (var metric in was.Pairs())
        {
            if (!now.TryGet(metric.Key, out ScriptValue value)) return false;
            if (!metric.Value.Equals(value)) return false;
        }

        return was.Count == now.Count;
    }
}
