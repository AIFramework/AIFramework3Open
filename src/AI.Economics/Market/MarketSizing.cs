using System;

using AI.Insights;

namespace AI.Economics.Market;

/// <summary>Оценка рынка «сверху вниз»: от общего объёма к своей доле.</summary>
public sealed record TopDownInput
{
    /// <summary>Объём всего рынка по отраслевому отчёту.</summary>
    public double TotalMarketValue { get; init; }

    /// <summary>Доля целевой географии.</summary>
    public double GeographyShare { get; init; } = 1.0;

    /// <summary>Доля целевого сегмента внутри географии.</summary>
    public double SegmentShare { get; init; } = 1.0;

    /// <summary>Доля сегмента, до которой продукт вообще применим.</summary>
    public double AddressableShare { get; init; } = 1.0;

    /// <summary>Реалистично захватываемая доля на горизонте планирования.</summary>
    public double AchievableShare { get; init; } = 0.05;
}

/// <summary>Оценка рынка «снизу вверх»: от числа клиентов и чека.</summary>
public sealed record BottomUpInput
{
    /// <summary>Число потенциальных клиентов в целевой географии.</summary>
    public double TargetAccounts { get; init; }

    /// <summary>Доля клиентов, подходящих продукту по профилю.</summary>
    public double QualifiedShare { get; init; } = 1.0;

    /// <summary>Средний годовой чек на клиента.</summary>
    public double AnnualRevenuePerAccount { get; init; }

    /// <summary>Доля клиентов, до которых дотягиваются каналы продаж.</summary>
    public double ReachableShare { get; init; } = 1.0;

    /// <summary>Конверсия из достижимых в платящих.</summary>
    public double WinRate { get; init; } = 0.05;
}

/// <summary>Согласованная оценка объёма рынка.</summary>
public sealed partial record MarketSizingResult
{
    /// <summary>TAM по оценке сверху вниз.</summary>
    public double TamTopDown { get; init; }

    /// <summary>SAM по оценке сверху вниз.</summary>
    public double SamTopDown { get; init; }

    /// <summary>SOM по оценке сверху вниз.</summary>
    public double SomTopDown { get; init; }

    /// <summary>TAM по оценке снизу вверх.</summary>
    public double TamBottomUp { get; init; }

    /// <summary>SAM по оценке снизу вверх.</summary>
    public double SamBottomUp { get; init; }

    /// <summary>SOM по оценке снизу вверх.</summary>
    public double SomBottomUp { get; init; }

    /// <summary>Согласованный TAM — среднее геометрическое двух оценок.</summary>
    public double ReconciledTam { get; init; }

    /// <summary>Согласованный SAM.</summary>
    public double ReconciledSam { get; init; }

    /// <summary>Согласованный SOM.</summary>
    public double ReconciledSom { get; init; }

    /// <summary>Во сколько раз оценки TAM расходятся между собой.</summary>
    public double TamDivergence { get; init; }

    /// <summary>Во сколько раз расходятся оценки SOM.</summary>
    public double SomDivergence { get; init; }

    /// <summary>Доля рынка, которую подразумевает SOM.</summary>
    public double ImpliedMarketShare { get; init; }

    /// <summary>Словесный вывод о согласованности оценок.</summary>
    public string Verdict { get; init; } = string.Empty;
}

/// <summary>
/// Оценка TAM, SAM и SOM двумя независимыми способами с последующим
/// согласованием.
/// </summary>
/// <remarks>
/// <para>
/// Оценка сверху вниз («рынок 40 миллиардов, нам хватит одного процента»)
/// не проверяема и потому бесполезна: один процент любого рынка звучит
/// скромно и не следует ни из чего. Оценка снизу вверх («столько-то клиентов,
/// такой-то чек, такая-то конверсия») проверяема, но систематически
/// занижает — она видит только известные сегодня каналы.
/// </para>
/// <para>
/// Ценность метода не в числе, а в расхождении. Совпадение оценок в пределах
/// полутора раз означает, что модель рынка непротиворечива. Расхождение
/// в разы означает ошибку в одной из них — и её надо найти до того, как
/// оценка попадёт в презентацию инвестору.
/// </para>
/// </remarks>
public static class MarketSizing
{
    /// <summary>Считает и согласовывает две оценки рынка.</summary>
    /// <param name="topDown">Параметры оценки сверху вниз.</param>
    /// <param name="bottomUp">Параметры оценки снизу вверх.</param>
    /// <returns>Обе оценки, согласованные значения и вывод о расхождении.</returns>
    /// <exception cref="ArgumentNullException">Параметры не заданы.</exception>
    public static MarketSizingResult Estimate(TopDownInput topDown, BottomUpInput bottomUp)
    {
        ArgumentNullException.ThrowIfNull(topDown);
        ArgumentNullException.ThrowIfNull(bottomUp);

        double tamTop = topDown.TotalMarketValue * topDown.GeographyShare;
        double samTop = tamTop * topDown.SegmentShare * topDown.AddressableShare;
        double somTop = samTop * topDown.AchievableShare;

        double tamBottom = bottomUp.TargetAccounts * bottomUp.AnnualRevenuePerAccount;
        double samBottom = tamBottom * bottomUp.QualifiedShare * bottomUp.ReachableShare;
        double somBottom = samBottom * bottomUp.WinRate;

        double tamDivergence = Divergence(tamTop, tamBottom);
        double somDivergence = Divergence(somTop, somBottom);

        double reconciledTam = GeometricMean(tamTop, tamBottom);
        double reconciledSam = GeometricMean(samTop, samBottom);
        double reconciledSom = GeometricMean(somTop, somBottom);

        return new MarketSizingResult
        {
            TamTopDown = tamTop,
            SamTopDown = samTop,
            SomTopDown = somTop,
            TamBottomUp = tamBottom,
            SamBottomUp = samBottom,
            SomBottomUp = somBottom,
            ReconciledTam = reconciledTam,
            ReconciledSam = reconciledSam,
            ReconciledSom = reconciledSom,
            TamDivergence = tamDivergence,
            SomDivergence = somDivergence,
            ImpliedMarketShare = reconciledTam > 0 ? reconciledSom / reconciledTam : 0,
            Verdict = Verdict(tamDivergence),
        };
    }

    /// <summary>Отношение большей оценки к меньшей.</summary>
    private static double Divergence(double a, double b)
    {
        double lo = Math.Min(a, b);
        double hi = Math.Max(a, b);
        return lo > 0 ? hi / lo : double.PositiveInfinity;
    }

    /// <summary>
    /// Среднее геометрическое: устойчиво к тому, что оценки различаются
    /// в разы, — в отличие от среднего арифметического, которое в такой
    /// ситуации почти повторяет большую из них.
    /// </summary>
    private static double GeometricMean(double a, double b)
    {
        if (a <= 0) return b;
        if (b <= 0) return a;
        return Math.Sqrt(a * b);
    }

    private static string Verdict(double divergence) => divergence switch
    {
        <= 1.5 => "Оценки согласованы: модель рынка непротиворечива.",
        <= 3.0 => "Умеренное расхождение: проверьте долю сегмента и средний чек.",
        <= 10.0 => "Сильное расхождение: одна из оценок опирается на неверное допущение.",
        _ => "Оценки несовместимы: считайте рынок заново снизу вверх.",
    };
}
