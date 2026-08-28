using System;
using System.Collections.Generic;
using System.Linq;
using AI.Economics.Insights;
using AI.Statistics;

namespace AI.Economics.Statements;

/// <summary>Разрез анализа по закону Бенфорда.</summary>
public enum BenfordScope
{
    /// <summary>Первая значащая цифра, девять групп.</summary>
    FirstDigit,

    /// <summary>Первые две значащие цифры, девяносто групп.</summary>
    FirstTwoDigits,
}

/// <summary>Наблюдённая и ожидаемая частота одной цифровой группы.</summary>
/// <param name="Digit">Цифра или пара цифр.</param>
/// <param name="Observed">Наблюдённое число значений.</param>
/// <param name="Expected">Ожидаемое число значений по закону Бенфорда.</param>
/// <param name="ObservedShare">Наблюдённая доля.</param>
/// <param name="ExpectedShare">Ожидаемая доля.</param>
/// <param name="ZScore">Z-статистика отклонения доли.</param>
public sealed record BenfordDigit(
    int Digit, int Observed, double Expected,
    double ObservedShare, double ExpectedShare, double ZScore)
{
    /// <summary>Абсолютное отклонение доли от ожидаемой.</summary>
    public double AbsoluteDeviation => Math.Abs(ObservedShare - ExpectedShare);

    /// <summary>Значимо ли отклонение на уровне 5%.</summary>
    public bool IsSignificant => ZScore > 1.96;
}

/// <summary>Результат проверки массива чисел на закон Бенфорда.</summary>
public sealed record BenfordResult : IInterpretable
{
    /// <summary>Название проверяемого набора данных.</summary>
    public string Dataset { get; init; } = string.Empty;

    /// <summary>Разрез анализа.</summary>
    public BenfordScope Scope { get; init; }

    /// <summary>Частоты по цифровым группам.</summary>
    public IReadOnlyList<BenfordDigit> Digits { get; init; } = [];

    /// <summary>Число значений, вошедших в анализ.</summary>
    public int SampleSize { get; init; }

    /// <summary>Число значений, отброшенных как непригодные.</summary>
    public int Excluded { get; init; }

    /// <summary>Статистика хи-квадрат.</summary>
    public double ChiSquare { get; init; }

    /// <summary>Уровень значимости отклонения от закона Бенфорда.</summary>
    public double PValue { get; init; }

    /// <summary>Среднее абсолютное отклонение долей.</summary>
    public double MeanAbsoluteDeviation { get; init; }

    /// <summary>Словесная оценка соответствия закону.</summary>
    public string Conformity { get; init; } = string.Empty;

    /// <summary>Цифровые группы со значимым отклонением.</summary>
    public IReadOnlyList<BenfordDigit> Suspicious =>
        [.. Digits.Where(d => d.IsSignificant).OrderByDescending(d => d.ZScore)];

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        BenfordDigit? excess = Digits
            .OrderByDescending(d => d.ObservedShare - d.ExpectedShare)
            .FirstOrDefault();

        BenfordDigit? shortage = Digits
            .OrderBy(d => d.ObservedShare - d.ExpectedShare)
            .FirstOrDefault();

        bool conforms = PValue >= 0.05;
        string scopeName = Scope == BenfordScope.FirstDigit ? "первой цифре" : "первым двум цифрам";

        var builder = new InterpretationBuilder($"Закон Бенфорда: {Dataset}")
            .Summary($"Проверено {SampleSize} значений по {scopeName}. " +
                     $"Хи-квадрат {Fmt.Num(ChiSquare, 1)}, уровень значимости {Fmt.Num(PValue, 4)}, " +
                     $"среднее абсолютное отклонение {Fmt.Num(MeanAbsoluteDeviation, 5)} — " +
                     $"{Conformity}. Значимо отклоняются {Suspicious.Count} цифровых групп.")
            .Metric("Значений в анализе", SampleSize, null,
                $"отброшено {Excluded} непригодных",
                SampleSize >= 300 ? MetricQuality.Good : MetricQuality.Warning, 0)
            .Metric("Хи-квадрат", ChiSquare, null,
                $"уровень значимости {Fmt.Num(PValue, 4)}",
                conforms ? MetricQuality.Good : MetricQuality.Critical, 2)
            .Metric("Среднее абсолютное отклонение", MeanAbsoluteDeviation, null, Conformity,
                MeanAbsoluteDeviation < 0.006 ? MetricQuality.Good
                    : MeanAbsoluteDeviation < 0.015 ? MetricQuality.Warning : MetricQuality.Critical, 5)
            .Metric("Подозрительных групп", Suspicious.Count, null,
                "цифры со значимым отклонением доли",
                Suspicious.Count == 0 ? MetricQuality.Good
                    : Suspicious.Count <= 2 ? MetricQuality.Warning : MetricQuality.Critical, 0);

        foreach (BenfordDigit digit in Digits)
        {
            builder.Metric($"Цифра {digit.Digit}", digit.ObservedShare, null,
                $"ожидалось {Fmt.Pct(digit.ExpectedShare, 2)}, наблюдений {digit.Observed}, " +
                $"Z = {Fmt.Num(digit.ZScore, 2)}",
                digit.IsSignificant ? MetricQuality.Warning : MetricQuality.Good, 4);
        }

        return builder
            .FindingIf(excess is not null,
                $"Чаще всего сверх ожидания встречается группа {excess?.Digit}: " +
                $"{Fmt.Pct(excess?.ObservedShare ?? 0, 2)} против ожидаемых " +
                $"{Fmt.Pct(excess?.ExpectedShare ?? 0, 2)}.")
            .FindingIf(shortage is not null,
                $"Наибольший недобор у группы {shortage?.Digit}: " +
                $"{Fmt.Pct(shortage?.ObservedShare ?? 0, 2)} против " +
                $"{Fmt.Pct(shortage?.ExpectedShare ?? 0, 2)}.")
            .Finding("Закон Бенфорда выполняется для величин, охватывающих несколько порядков " +
                     "и возникающих как произведение многих факторов — суммы платежей, " +
                     "выручка по контрагентам, стоимость запасов. Придуманные вручную числа " +
                     "этому распределению почти никогда не подчиняются.")
            .FindingIf(!conforms,
                "Отклонение статистически значимо. Типичные причины помимо злоупотреблений: " +
                "округление сумм, лимиты согласования, тарифная сетка и любые ограничения " +
                "на величину платежа.")
            .WarningIf(SampleSize < 300,
                $"Всего {SampleSize} значений. Для первой цифры это минимально допустимый объём, " +
                "а для первых двух цифр — заведомо недостаточный: тест теряет мощность.")
            .WarningIf(!conforms,
                "Несоответствие закону не доказывает мошенничество. Это указание на то, " +
                "какие именно группы транзакций стоит проверить вручную.")
            .Warning("Тест применим не ко всем данным. Номера счетов, суммы с жёстким " +
                     "верхним пределом, тарифы и цены из прайс-листа не подчиняются закону " +
                     "Бенфорда изначально, и отклонение по ним ничего не означает.")
            .Recommendation("Начинайте проверку с групп, у которых максимальная Z-статистика, " +
                            "и сопоставляйте их с порогами согласования: превышение вблизи " +
                            "лимита — самый частый сценарий дробления платежей.")
            .Recommendation("Прогоняйте тест отдельно по подразделениям и подотчётным лицам: " +
                            "на уровне всей компании локальное искажение растворяется в объёме.")
            .Build();
    }
}

/// <summary>
/// Проверка массивов чисел на соответствие закону Бенфорда.
/// </summary>
/// <remarks>
/// <para>
/// Закон Бенфорда утверждает, что в наборах чисел, охватывающих несколько
/// порядков, первая значащая цифра распределена неравномерно:
/// </para>
/// <code>
/// P(d) = log10(1 + 1 / d),  d = 1..9
/// P(dd) = log10(1 + 1 / dd), dd = 10..99
/// </code>
/// <para>
/// Единица встречается в 30,1% случаев, девятка — в 4,6%. Человек, придумывающий
/// суммы, распределяет первые цифры почти равномерно, поэтому отклонение от
/// закона служит фильтром для отбора транзакций на ручную проверку.
/// </para>
/// <para>
/// Соответствие оценивается тремя способами: критерием хи-квадрат, средним
/// абсолютным отклонением долей (оно не зависит от объёма выборки, в отличие
/// от хи-квадрат) и Z-статистикой по каждой цифровой группе. Последняя и
/// указывает, какие именно суммы проверять.
/// </para>
/// </remarks>
public static class BenfordAnalysis
{
    /// <summary>Проверяет числа на соответствие закону Бенфорда.</summary>
    /// <param name="values">Проверяемые величины: суммы платежей, остатки, объёмы.</param>
    /// <param name="scope">Разрез анализа: первая цифра или первые две.</param>
    /// <param name="dataset">Название набора данных для отчёта.</param>
    /// <returns>Частоты, статистики согласия и список подозрительных групп.</returns>
    /// <exception cref="ArgumentNullException">Значения не заданы.</exception>
    /// <exception cref="ArgumentException">После отбраковки не осталось пригодных значений.</exception>
    public static BenfordResult Analyze(
        IReadOnlyList<double> values,
        BenfordScope scope = BenfordScope.FirstDigit,
        string dataset = "транзакции")
    {
        ArgumentNullException.ThrowIfNull(values);

        int first = scope == BenfordScope.FirstDigit ? 1 : 10;
        int last = scope == BenfordScope.FirstDigit ? 9 : 99;
        int groups = last - first + 1;

        var counts = new int[groups];
        int used = 0, excluded = 0;

        foreach (double value in values)
        {
            int digit = LeadingDigits(value, scope);

            if (digit < first)
            {
                excluded++;
                continue;
            }

            counts[digit - first]++;
            used++;
        }

        if (used == 0)
            throw new ArgumentException("Не осталось значений, пригодных для анализа.", nameof(values));

        var expectedShares = new double[groups];
        var expectedCounts = new double[groups];
        var observedCounts = new double[groups];
        var digits = new List<BenfordDigit>(groups);

        for (int i = 0; i < groups; i++)
        {
            int digit = first + i;
            double share = Math.Log10(1.0 + (1.0 / digit));

            expectedShares[i] = share;
            expectedCounts[i] = share * used;
            observedCounts[i] = counts[i];

            double observedShare = (double)counts[i] / used;
            double standardError = Math.Sqrt(share * (1 - share) / used);
            double continuity = 1.0 / (2.0 * used);
            double deviation = Math.Abs(observedShare - share);
            double z = standardError > 0 ? Math.Max(0, deviation - continuity) / standardError : 0;

            digits.Add(new BenfordDigit(digit, counts[i], expectedCounts[i], observedShare, share, z));
        }

        double mad = digits.Average(d => d.AbsoluteDeviation);

        StatInference.TestResult test = StatInference.PearsonChiSquareGoodnessOfFit(
            observedCounts, expectedCounts, 0);

        return new BenfordResult
        {
            Dataset = dataset,
            Scope = scope,
            Digits = digits,
            SampleSize = used,
            Excluded = excluded,
            ChiSquare = test.Statistic,
            PValue = test.PValue,
            MeanAbsoluteDeviation = mad,
            Conformity = Conformity(mad, scope),
        };
    }

    /// <summary>Первая значащая цифра или пара цифр числа.</summary>
    /// <param name="value">Число.</param>
    /// <param name="scope">Требуемый разрез.</param>
    /// <returns>Цифровая группа или ноль, если число непригодно для анализа.</returns>
    public static int LeadingDigits(double value, BenfordScope scope)
    {
        double magnitude = Math.Abs(value);

        if (magnitude < 1e-12 || double.IsNaN(magnitude) || double.IsInfinity(magnitude))
            return 0;

        while (magnitude < 1) magnitude *= 10;
        while (magnitude >= 10) magnitude /= 10;

        return scope == BenfordScope.FirstDigit
            ? (int)magnitude
            : (int)(magnitude * 10);
    }

    /// <summary>Словесная оценка соответствия по среднему абсолютному отклонению.</summary>
    /// <remarks>Пороги приведены по шкале Нигрини, принятой в аудиторской практике.</remarks>
    private static string Conformity(double mad, BenfordScope scope)
    {
        (double close, double acceptable, double marginal) = scope == BenfordScope.FirstDigit
            ? (0.006, 0.012, 0.015)
            : (0.0012, 0.0018, 0.0022);

        return mad < close ? "близкое соответствие закону"
            : mad < acceptable ? "приемлемое соответствие"
            : mad < marginal ? "пограничное соответствие"
            : "несоответствие закону Бенфорда";
    }
}
