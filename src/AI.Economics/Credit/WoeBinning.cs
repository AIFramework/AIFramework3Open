using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;

namespace AI.Economics.Credit;

/// <summary>Интервал значений признака со статистикой дефолтов.</summary>
public sealed record ScoreBin
{
    /// <summary>Нижняя граница интервала, включительно.</summary>
    public double Lower { get; init; }

    /// <summary>Верхняя граница интервала, исключительно.</summary>
    public double Upper { get; init; }

    /// <summary>Число наблюдений в интервале.</summary>
    public int Total { get; init; }

    /// <summary>Число дефолтов.</summary>
    public int Bad { get; init; }

    /// <summary>Число исправных заёмщиков.</summary>
    public int Good => Total - Bad;

    /// <summary>Доля дефолтов внутри интервала.</summary>
    public double BadRate => Total > 0 ? (double)Bad / Total : 0;

    /// <summary>Доля интервала в выборке.</summary>
    public double Share { get; init; }

    /// <summary>
    /// Вес доказательства: логарифм отношения доли исправных к доле дефолтных.
    /// Положительное значение означает, что интервал безопаснее среднего.
    /// </summary>
    public double Woe { get; init; }

    /// <summary>Вклад интервала в информационную ценность признака.</summary>
    public double IvContribution { get; init; }

    /// <summary>Человекочитаемое обозначение интервала.</summary>
    public string Label { get; init; } = string.Empty;
}

/// <summary>Результат биннинга одного признака.</summary>
public sealed record VariableBinning : IInterpretable
{
    /// <summary>Название признака.</summary>
    public string Variable { get; init; } = string.Empty;

    /// <summary>Интервалы по возрастанию значения признака.</summary>
    public IReadOnlyList<ScoreBin> Bins { get; init; } = [];

    /// <summary>
    /// Информационная ценность признака: суммарный вклад интервалов.
    /// </summary>
    public double InformationValue { get; init; }

    /// <summary>Монотонна ли доля дефолтов по интервалам.</summary>
    public bool IsMonotone { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Общая доля дефолтов в выборке.</summary>
    public double OverallBadRate { get; init; }

    /// <summary>
    /// Словесная оценка предсказательной силы по общепринятой шкале.
    /// </summary>
    public string Predictiveness => InformationValue switch
    {
        < 0.02 => "не предсказывает",
        < 0.1 => "слабая",
        < 0.3 => "средняя",
        < 0.5 => "сильная",
        _ => "подозрительно высокая",
    };

    /// <summary>Вес доказательства для конкретного значения признака.</summary>
    /// <param name="value">Значение признака.</param>
    /// <returns>Вес интервала, в который попадает значение.</returns>
    public double Transform(double value)
    {
        foreach (ScoreBin bin in Bins)
            if (value >= bin.Lower && value < bin.Upper) return bin.Woe;

        return Bins.Count > 0 ? Bins[^1].Woe : 0;
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        ScoreBin? riskiest = Bins.OrderByDescending(b => b.BadRate).FirstOrDefault();
        ScoreBin? safest = Bins.OrderBy(b => b.BadRate).FirstOrDefault();
        bool suspicious = InformationValue >= 0.5;
        bool useless = InformationValue < 0.02;

        double lift = safest is not null && safest.BadRate > 1e-9 && riskiest is not null
            ? riskiest.BadRate / safest.BadRate
            : double.NaN;

        var builder = new InterpretationBuilder($"Биннинг признака «{Variable}»")
            .Summary($"Информационная ценность {Fmt.Num(InformationValue, 3)} — предсказательная сила " +
                     $"{Predictiveness}. Признак разбит на {Bins.Count} интервалов, доля дефолтов " +
                     $"меняется от {Fmt.Pct(safest?.BadRate ?? 0, 2)} до {Fmt.Pct(riskiest?.BadRate ?? 0, 2)} " +
                     $"при среднем уровне {Fmt.Pct(OverallBadRate, 2)}.")
            .Metric("Информационная ценность", InformationValue, null,
                "0,1-0,3 — средняя сила, свыше 0,5 — повод искать утечку целевой переменной",
                suspicious ? MetricQuality.Critical
                    : useless ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Metric("Интервалов", Bins.Count, null, "после объединения мелких и немонотонных",
                MetricQuality.Neutral, 0)
            .Metric("Монотонность", IsMonotone ? "да" : "нет", null,
                "монотонная связь устойчивее переносится на новые данные",
                IsMonotone ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Разброс риска", double.IsNaN(lift) ? "не определён" : Fmt.Num(lift) + "x", null,
                "во сколько раз худший интервал рискованнее лучшего")
            .Metric("Наблюдений", Observations, null, null, MetricQuality.Unknown, 0);

        foreach (ScoreBin bin in Bins)
        {
            builder.Metric(bin.Label, Fmt.Pct(bin.BadRate, 2), null,
                $"доля {Fmt.Pct(bin.Share)}, вес {Fmt.Num(bin.Woe, 3)}",
                MetricQuality.Unknown);
        }

        return builder
            .Finding("Вес доказательства переводит любой признак в единую шкалу логарифма шансов, " +
                     "поэтому категориальные и числовые переменные попадают в модель одинаково " +
                     "и без нормировки.")
            .FindingIf(IsMonotone,
                "Связь монотонна: риск последовательно меняется от интервала к интервалу. " +
                "Такая структура устойчивее переносится на новые заявки.")
            .FindingIf(!useless && !suspicious,
                $"Признак стоит включить в модель: его информационная ценность " +
                $"{Fmt.Num(InformationValue, 3)} выше порога отбора 0,02.")
            .WarningIf(suspicious,
                $"Информационная ценность {Fmt.Num(InformationValue, 3)} подозрительно высока. " +
                "Чаще всего это утечка: признак содержит информацию, недоступную в момент " +
                "принятия решения — например, факт просрочки.")
            .WarningIf(useless,
                "Признак практически не разделяет заёмщиков. Включение таких переменных " +
                "усложняет модель, не улучшая её.")
            .WarningIf(Bins.Any(b => b.Bad == 0 || b.Bad == b.Total),
                "В одном из интервалов нет дефолтов либо все наблюдения дефолтные. " +
                "Вес доказательства рассчитан со сглаживанием, но такой интервал ненадёжен.")
            .Warning("Границы интервалов подобраны на этой выборке. При переносе модели " +
                     "на другую популяцию их устойчивость надо проверять индексом стабильности.")
            .Recommendation("Отбирайте признаки по информационной ценности выше 0,02, но " +
                            "проверяйте всё, что выше 0,5, на утечку целевой переменной.")
            .Build();
    }
}

/// <summary>
/// Биннинг признаков с расчётом веса доказательства и информационной ценности.
/// </summary>
/// <remarks>
/// <para>
/// Вес доказательства переводит признак в шкалу логарифма шансов:
/// </para>
/// <code>
/// WoE_i = ln( (good_i / Good) / (bad_i / Bad) )
/// </code>
/// <para>
/// Информационная ценность суммирует вклад интервалов и служит мерой
/// предсказательной силы признака:
/// </para>
/// <code>
/// IV = sum_i ( good_i/Good - bad_i/Bad ) * WoE_i
/// </code>
/// <para>
/// Практическая ценность преобразования в трёх вещах. Оно приводит признаки
/// любой природы к одной шкале, делает связь с целевой переменной линейной
/// в логарифме шансов и — главное — переносит нелинейность из модели в
/// биннинг, где её видно глазами и можно проверить на здравый смысл.
/// </para>
/// <para>
/// Монотонность добивается объединением соседних интервалов, нарушающих
/// порядок. Это сознательное ограничение гибкости: немонотонная связь
/// «риск падает, потом растёт, потом снова падает» почти всегда оказывается
/// шумом выборки и не воспроизводится на новых заявках.
/// </para>
/// </remarks>
public static class WoeBinning
{
    /// <summary>Сглаживание при нулевом числе наблюдений в категории.</summary>
    private const double Smoothing = 0.5;

    /// <summary>Строит биннинг одного числового признака.</summary>
    /// <param name="variable">Название признака.</param>
    /// <param name="values">Значения признака.</param>
    /// <param name="defaults">Признак дефолта по каждому наблюдению.</param>
    /// <param name="maxBins">Максимальное число интервалов.</param>
    /// <param name="minShare">Минимальная доля наблюдений в интервале.</param>
    /// <param name="enforceMonotonic">Добиваться ли монотонности доли дефолтов.</param>
    /// <returns>Интервалы с весами и информационная ценность.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Длины не совпадают или мало наблюдений.</exception>
    public static VariableBinning Fit(
        string variable, Vector values, IReadOnlyList<bool> defaults,
        int maxBins = 6, double minShare = 0.05, bool enforceMonotonic = true)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(defaults);

        int n = values.Count;
        if (n != defaults.Count)
            throw new ArgumentException("Длины признака и целевой переменной должны совпадать.", nameof(defaults));
        if (n < 50)
            throw new ArgumentException("Нужно минимум 50 наблюдений.", nameof(values));

        int totalBad = defaults.Count(d => d);
        if (totalBad == 0 || totalBad == n)
            throw new ArgumentException("В выборке должны быть и дефолты, и исправные заёмщики.", nameof(defaults));

        var sorted = Enumerable.Range(0, n)
            .Select(i => (Value: values[i], Bad: defaults[i]))
            .OrderBy(p => p.Value)
            .ToList();

        int minCount = Math.Max((int)Math.Ceiling(minShare * n), 5);

        // Предварительное разбиение по квантилям: дальше интервалы только
        // укрупняются, поэтому начинать надо с мелкого шага. Дробить мельче
        // минимального размера интервала бессмысленно — всё равно сольётся
        int preBins = Math.Max(2, Math.Min(Math.Max(maxBins * 4, 20), n / minCount));
        List<List<(double Value, bool Bad)>> groups = SplitByQuantiles(sorted, preBins);

        MergeSmall(groups, minCount);

        if (enforceMonotonic) MergeNonMonotonic(groups);
        while (groups.Count > maxBins) MergeClosest(groups);

        MergeSmall(groups, minCount);

        var bins = new List<ScoreBin>(groups.Count);
        double iv = 0;
        double lowerBound = double.NegativeInfinity;

        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            int bad = group.Count(p => p.Bad);
            int good = group.Count - bad;

            double badShare = (bad + Smoothing) / (totalBad + (Smoothing * groups.Count));
            double goodShare = (good + Smoothing) / (n - totalBad + (Smoothing * groups.Count));

            double woe = Math.Log(goodShare / badShare);
            double contribution = (goodShare - badShare) * woe;
            iv += contribution;

            double upperBound = g == groups.Count - 1 ? double.PositiveInfinity : groups[g + 1][0].Value;

            bins.Add(new ScoreBin
            {
                Lower = lowerBound,
                Upper = upperBound,
                Total = group.Count,
                Bad = bad,
                Share = (double)group.Count / n,
                Woe = woe,
                IvContribution = contribution,
                Label = FormatRange(lowerBound, upperBound),
            });

            lowerBound = upperBound;
        }

        return new VariableBinning
        {
            Variable = variable,
            Bins = bins,
            InformationValue = iv,
            IsMonotone = IsMonotone(bins),
            Observations = n,
            OverallBadRate = (double)totalBad / n,
        };
    }

    /// <summary>Строит биннинг всех признаков обучающей выборки.</summary>
    /// <param name="variableNames">Названия признаков.</param>
    /// <param name="values">Матрица «наблюдения x признаки».</param>
    /// <param name="defaults">Признак дефолта.</param>
    /// <param name="maxBins">Максимальное число интервалов.</param>
    /// <param name="minShare">Минимальная доля наблюдений в интервале.</param>
    /// <returns>Биннинги по убыванию информационной ценности.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static IReadOnlyList<VariableBinning> FitAll(
        IReadOnlyList<string> variableNames, Matrix values, IReadOnlyList<bool> defaults,
        int maxBins = 6, double minShare = 0.05)
    {
        ArgumentNullException.ThrowIfNull(variableNames);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(defaults);

        var result = new List<VariableBinning>(values.Width);

        for (int j = 0; j < values.Width; j++)
        {
            var column = new Vector(values.Height);
            for (int i = 0; i < values.Height; i++) column[i] = values[i, j];

            string name = j < variableNames.Count ? variableNames[j] : $"x{j + 1}";
            result.Add(Fit(name, column, defaults, maxBins, minShare));
        }

        return [.. result.OrderByDescending(b => b.InformationValue)];
    }

    /// <summary>Разбивает отсортированную выборку на интервалы равного наполнения.</summary>
    /// <remarks>
    /// Граница интервала сдвигается вправо до конца серии одинаковых значений:
    /// одно и то же значение признака не может попасть в разные интервалы,
    /// иначе граница окажется внутри плотного пика распределения.
    /// </remarks>
    private static List<List<(double Value, bool Bad)>> SplitByQuantiles(
        List<(double Value, bool Bad)> sorted, int parts)
    {
        var groups = new List<List<(double Value, bool Bad)>>();
        int n = sorted.Count;
        int start = 0;

        for (int p = 1; p <= parts && start < n; p++)
        {
            int end = p == parts ? n : p * n / parts;
            if (end <= start) continue;

            while (end < n && sorted[end].Value == sorted[end - 1].Value) end++;

            groups.Add(sorted.GetRange(start, end - start));
            start = end;
        }

        if (start < n)
        {
            if (groups.Count > 0) groups[^1].AddRange(sorted.GetRange(start, n - start));
            else groups.Add(sorted.GetRange(start, n - start));
        }

        return groups.Count > 0 ? groups : [sorted];
    }

    /// <summary>Укрупняет интервалы, в которых слишком мало наблюдений.</summary>
    /// <remarks>
    /// Объединять начинаем с самого мелкого интервала и присоединяем его к
    /// менее наполненному соседу. Обход слева направо с присоединением к
    /// предыдущему интервалу приводит к вырождению: первый интервал растёт,
    /// вбирая все последующие, и от разбиения остаются два интервала.
    /// </remarks>
    private static void MergeSmall(List<List<(double Value, bool Bad)>> groups, int minCount)
    {
        while (groups.Count > 2)
        {
            int worst = -1;

            for (int i = 0; i < groups.Count; i++)
            {
                bool tooSmall = groups[i].Count < minCount;
                bool degenerate = !groups[i].Any(p => p.Bad) || !groups[i].Any(p => !p.Bad);

                if (!tooSmall && !degenerate) continue;
                if (worst < 0 || groups[i].Count < groups[worst].Count) worst = i;
            }

            if (worst < 0) return;

            int target = worst == 0 ? 1
                : worst == groups.Count - 1 ? worst - 1
                : groups[worst - 1].Count <= groups[worst + 1].Count ? worst - 1 : worst + 1;

            int low = Math.Min(worst, target), high = Math.Max(worst, target);

            groups[low].AddRange(groups[high]);
            groups[low].Sort((a, b) => a.Value.CompareTo(b.Value));
            groups.RemoveAt(high);
        }
    }

    /// <summary>
    /// Объединяет соседние интервалы, нарушающие монотонность доли дефолтов.
    /// </summary>
    /// <remarks>
    /// Направление монотонности определяется по знаку корреляции между
    /// номером интервала и долей дефолтов в нём: навязывать заранее выбранное
    /// направление нельзя, оно зависит от смысла признака.
    /// </remarks>
    private static void MergeNonMonotonic(List<List<(double Value, bool Bad)>> groups)
    {
        if (groups.Count < 3) return;

        double[] rates = [.. groups.Select(g => g.Count(p => p.Bad) / (double)g.Count)];
        bool increasing = rates[^1] >= rates[0];

        bool merged = true;
        while (merged && groups.Count > 2)
        {
            merged = false;
            rates = [.. groups.Select(g => g.Count(p => p.Bad) / (double)g.Count)];

            for (int i = 1; i < groups.Count; i++)
            {
                bool violates = increasing ? rates[i] < rates[i - 1] : rates[i] > rates[i - 1];
                if (!violates) continue;

                groups[i - 1].AddRange(groups[i]);
                groups[i - 1].Sort((a, b) => a.Value.CompareTo(b.Value));
                groups.RemoveAt(i);
                merged = true;
                break;
            }
        }
    }

    /// <summary>Объединяет пару соседних интервалов с наименьшим различием риска.</summary>
    private static void MergeClosest(List<List<(double Value, bool Bad)>> groups)
    {
        if (groups.Count < 2) return;

        int best = 0;
        double smallest = double.PositiveInfinity;

        for (int i = 1; i < groups.Count; i++)
        {
            double left = groups[i - 1].Count(p => p.Bad) / (double)groups[i - 1].Count;
            double right = groups[i].Count(p => p.Bad) / (double)groups[i].Count;
            double difference = Math.Abs(left - right);

            if (difference < smallest) { smallest = difference; best = i; }
        }

        groups[best - 1].AddRange(groups[best]);
        groups[best - 1].Sort((a, b) => a.Value.CompareTo(b.Value));
        groups.RemoveAt(best);
    }

    private static bool IsMonotone(IReadOnlyList<ScoreBin> bins)
    {
        if (bins.Count < 3) return true;

        bool increasing = true, decreasing = true;
        for (int i = 1; i < bins.Count; i++)
        {
            if (bins[i].BadRate < bins[i - 1].BadRate - 1e-12) increasing = false;
            if (bins[i].BadRate > bins[i - 1].BadRate + 1e-12) decreasing = false;
        }

        return increasing || decreasing;
    }

    private static string FormatRange(double lower, double upper)
    {
        string left = double.IsNegativeInfinity(lower) ? "(-inf" : $"[{Fmt.Num(lower, 2)}";
        string right = double.IsPositiveInfinity(upper) ? "+inf)" : $"{Fmt.Num(upper, 2)})";
        return $"{left}; {right}";
    }
}
