using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Сравнение вариантов опыта и тракт со сменными звеньями.
/// </summary>
/// <remarks>
/// Победителя объявляют не по разнице средних: на пяти повторах разница в третьем знаке — это
/// разброс, а не преимущество. Поэтому у сравнения есть интервал, размер эффекта, поправка на
/// число сравнений и признак, доказана ли разница вообще.
/// <para>
/// Итог — ДАННЫЕ: числа и признак. Как о них рассказать человеку — методика, и она живёт там,
/// где живут остальные правила продукта, а не в языке.
/// </para>
/// </remarks>
public static partial class ExpModule
{
    /// <summary>Сколько выборок берёт бутстреп: меньше тысячи интервал заметно пляшет.</summary>
    public const int BootstrapSamples = 2000;

    /// <summary>Уровень доверия по умолчанию.</summary>
    public const double DefaultConfidence = 0.95;

    [ScriptFn("compare", "Сравнивает варианты по метрике: средние, интервалы, значимость",
        Example = "итоги |> exp.compare(metric: \"опора\", by: \"temp\")")]
    public static ScriptRecord Compare(
        IScriptContext context,
        [ScriptParam("таблица итогов опыта")] ScriptTable outcomes,
        [ScriptParam("колонка с метрикой")] string metric,
        [ScriptParam("колонка, по которой сравнивают варианты")] string by,
        [ScriptParam("уровень доверия")] double confidence = DefaultConfidence,
        [ScriptParam("больше — лучше")] bool bigger = true)
    {
        ScriptColumn values = outcomes.Column(metric);
        ScriptColumn groups = outcomes.Column(by);

        if (confidence is <= 0 or >= 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "exp.compare: уровень доверия лежит строго между 0 и 1");

        var samples = Samples(groups, values);

        if (samples.Count < 2)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"exp.compare: по колонке «{by}» получился один вариант, сравнивать не с чем",
                "в плане опыта должно быть хотя бы два значения этого параметра");
        }

        var random = new Random(context.Seed);
        var summaries = new List<Summary>(samples.Count);

        foreach (var sample in samples)
        {
            (double low, double high) = Interval(sample.Value, confidence, random);

            summaries.Add(new Summary(sample.Key, sample.Value, Mean(sample.Value), low, high));
        }

        summaries.Sort((left, right) => bigger
            ? right.Mean.CompareTo(left.Mean)
            : left.Mean.CompareTo(right.Mean));

        Summary best = summaries[0];
        Summary second = summaries[1];

        // Поправка на число сравнений: чем больше вариантов, тем выше шанс, что «лучший»
        // оказался лучшим случайно. Уровень доверия поднимается, а не остаётся прежним.
        int pairs = summaries.Count - 1;
        double corrected = 1 - ((1 - confidence) / pairs);
        (double lower, double upper) = Difference(best.Values, second.Values, corrected, random);
        bool proven = bigger ? lower > 0 : upper < 0;

        context.CountAllocation(summaries.Count * 4L);

        return ScriptRecord.From(
        [
            Field("metric", ScriptValue.Str(metric)),
            Field("by", ScriptValue.Str(by)),
            Field("best", ScriptValue.Str(best.Name)),
            Field("significant", ScriptValue.Bool(proven)),
            Field("difference", ScriptValue.Num(best.Mean - second.Mean)),
            Field("lower", ScriptValue.Num(lower)),
            Field("upper", ScriptValue.Num(upper)),
            Field("effect", ScriptValue.Num(Effect(best.Values, second.Values))),
            Field("comparisons", ScriptValue.Num(pairs)),
            Field("level", ScriptValue.Num(corrected)),
            Field("groups", ScriptValue.Table(Groups(summaries))),
        ]);
    }

    /// <summary>
    /// Варианты тракта с подменой одного звена.
    /// </summary>
    /// <remarks>
    /// Тракт — запись «имя звена → функция», а не список: подменять звено надо по имени, а у
    /// элемента списка имени нет. Из того же имени берётся и колонка в итогах опыта, поэтому в
    /// таблице сразу видно, какой вариант чем отличался.
    /// </remarks>
    [ScriptFn("variants", "Варианты тракта: одно звено заменяется по очереди",
        Example = "exp.variants(тракт, swap: { очистка: [мягкая, жёсткая] })")]
    public static ScriptList Variants(
        IScriptContext context,
        [ScriptParam("тракт: запись «звено → функция»")] ScriptRecord tract,
        [ScriptParam("чем заменять: звено → список замен")] ScriptRecord swap)
    {
        var variants = new List<ScriptRecord> { tract };

        foreach (var link in swap.Pairs())
        {
            if (!tract.Has(link.Key))
            {
                throw new ScriptError(
                    DiagnosticCodes.UnknownArgument,
                    $"exp.variants: в тракте нет звена «{link.Key}»",
                    $"звенья: {string.Join(", ", tract.Keys)}");
            }

            var grown = new List<ScriptRecord>(variants.Count);

            foreach (ScriptRecord variant in variants)
            {
                foreach (ScriptValue replacement in Options(link.Key, link.Value))
                    grown.Add(variant.With(link.Key, replacement));
            }

            variants = grown;
        }

        context.CountAllocation(variants.Count);

        return ScriptList.From(variants.Select(ScriptValue.Record));
    }

    /// <summary>
    /// Прогоняет вход через звенья тракта по порядку.
    /// </summary>
    /// <remarks>
    /// Порядок — тот, в котором звенья записаны: запись помнит порядок полей, и тракт читается
    /// сверху вниз как список шагов. Звено, оформленное стадией с кэшем, при смене соседа
    /// считается заново только если изменился его собственный вход, — на этом и держится
    /// дешёвое сравнение вариантов.
    /// </remarks>
    [ScriptFn("pipe", "Прогоняет вход через звенья тракта по порядку",
        Example = "exp.pipe(вариант, input: таблица)")]
    public static async Task<ScriptValue> Pipe(
        IScriptContext context,
        [ScriptParam("тракт: запись «звено → функция»")] ScriptRecord tract,
        [ScriptParam("вход первого звена")] ScriptValue input)
    {
        ScriptValue value = input;

        foreach (var link in tract.Pairs())
        {
            if (link.Value.Type != ScriptType.Fn)
            {
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"exp.pipe: звено «{link.Key}» — это {link.Value.Type.ToName()}, а нужна функция");
            }

            value = await context.CallAsync(link.Value, value).ConfigureAwait(false);
        }

        return value;
    }

    private static KeyValuePair<string, ScriptValue> Field(string name, ScriptValue value) => new(name, value);

    /// <summary>Значения метрики по вариантам; пропуски отбрасываются.</summary>
    private static List<KeyValuePair<string, double[]>> Samples(ScriptColumn groups, ScriptColumn values)
    {
        var order = new List<string>();
        var byName = new Dictionary<string, List<double>>(StringComparer.Ordinal);

        for (int i = 0; i < groups.Count; i++)
        {
            if (TableModule.IsMissing(values[i])) continue;

            string name = TableModule.Label(groups[i]);

            if (!byName.TryGetValue(name, out List<double>? sample))
            {
                sample = [];
                byName[name] = sample;
                order.Add(name);
            }

            sample.Add(TableModule.Numeric(values[i]));
        }

        var samples = new List<KeyValuePair<string, double[]>>(order.Count);

        foreach (string name in order) samples.Add(new KeyValuePair<string, double[]>(name, [.. byName[name]]));

        return samples;
    }

    private static ScriptTable Groups(IReadOnlyList<Summary> summaries) => ScriptTable.Create(
    [
        ScriptColumn.Own("value", [.. summaries.Select(s => ScriptValue.Str(s.Name))]),
        ScriptColumn.Own("n", [.. summaries.Select(s => ScriptValue.Num(s.Values.Length))]),
        ScriptColumn.Own("mean", [.. summaries.Select(s => ScriptValue.Num(s.Mean))]),
        ScriptColumn.Own("lower", [.. summaries.Select(s => ScriptValue.Num(s.Lower))]),
        ScriptColumn.Own("upper", [.. summaries.Select(s => ScriptValue.Num(s.Upper))]),
    ]);

    private static double Mean(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return double.NaN;

        double sum = 0;

        foreach (double value in values) sum += value;

        return sum / values.Count;
    }

    /// <summary>
    /// Доверительный интервал среднего бутстрепом.
    /// </summary>
    /// <remarks>
    /// Бутстреп, а не формула нормального приближения: метрики опыта бывают долями и счётчиками,
    /// у которых распределение далеко от нормального, а повторов пять — десять. Формула на таких
    /// данных даёт интервал уже настоящего, то есть уверенность, которой нет.
    /// </remarks>
    private static (double Low, double High) Interval(double[] sample, double confidence, Random random)
    {
        if (sample.Length < 2) return (double.NaN, double.NaN);

        var means = new double[BootstrapSamples];

        for (int i = 0; i < BootstrapSamples; i++) means[i] = Mean(Resample(sample, random));

        Array.Sort(means);

        return (Quantile(means, (1 - confidence) / 2), Quantile(means, 1 - ((1 - confidence) / 2)));
    }

    /// <summary>Интервал разницы средних: по нему и решают, доказана ли разница.</summary>
    private static (double Lower, double Upper) Difference(
        double[] best, double[] second, double confidence, Random random)
    {
        if (best.Length < 2 || second.Length < 2) return (double.NaN, double.NaN);

        var differences = new double[BootstrapSamples];

        for (int i = 0; i < BootstrapSamples; i++)
            differences[i] = Mean(Resample(best, random)) - Mean(Resample(second, random));

        Array.Sort(differences);

        return (Quantile(differences, (1 - confidence) / 2), Quantile(differences, 1 - ((1 - confidence) / 2)));
    }

    private static double[] Resample(double[] sample, Random random)
    {
        var drawn = new double[sample.Length];

        for (int i = 0; i < drawn.Length; i++) drawn[i] = sample[random.Next(sample.Length)];

        return drawn;
    }

    private static double Quantile(double[] sorted, double level)
    {
        if (sorted.Length == 0) return double.NaN;

        int index = (int)Math.Round(level * (sorted.Length - 1), MidpointRounding.AwayFromZero);

        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    /// <summary>Размер эффекта Коэна: во сколько разброса укладывается разница средних.</summary>
    private static double Effect(double[] best, double[] second)
    {
        if (best.Length < 2 || second.Length < 2) return double.NaN;

        double spread = Math.Sqrt(((Variance(best) * (best.Length - 1)) + (Variance(second) * (second.Length - 1)))
            / (best.Length + second.Length - 2));

        return spread > 0 ? (Mean(best) - Mean(second)) / spread : double.NaN;
    }

    private static double Variance(double[] sample)
    {
        double mean = Mean(sample);
        double sum = 0;

        foreach (double value in sample) sum += (value - mean) * (value - mean);

        return sum / (sample.Length - 1);
    }

    /// <summary>Сводка по одному варианту.</summary>
    private readonly record struct Summary(string Name, double[] Values, double Mean, double Lower, double Upper);
}
