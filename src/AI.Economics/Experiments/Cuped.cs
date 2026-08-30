using System;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Economics.Experiments;

/// <summary>Результат снижения дисперсии методом CUPED.</summary>
public sealed record CupedResult : IInterpretable
{
    /// <summary>Коэффициент коррекции: ковариация делить на дисперсию ковариаты.</summary>
    public double Theta { get; init; }

    /// <summary>Корреляция предэкспериментальной метрики с экспериментальной.</summary>
    public double Correlation { get; init; }

    /// <summary>Доля снижения дисперсии.</summary>
    public double VarianceReduction { get; init; }

    /// <summary>
    /// Во сколько раз эквивалентно выросла выборка. Снижение дисперсии на
    /// 30 % равносильно росту выборки примерно в 1,43 раза.
    /// </summary>
    public double EffectiveSampleGain => VarianceReduction < 1
        ? 1.0 / (1.0 - VarianceReduction)
        : double.PositiveInfinity;

    /// <summary>Эффект без коррекции.</summary>
    public double RawEffect { get; init; }

    /// <summary>Эффект после коррекции.</summary>
    public double AdjustedEffect { get; init; }

    /// <summary>Стандартная ошибка без коррекции.</summary>
    public double RawStandardError { get; init; }

    /// <summary>Стандартная ошибка после коррекции.</summary>
    public double AdjustedStandardError { get; init; }

    /// <summary>p-значение без коррекции.</summary>
    public double RawPValue { get; init; }

    /// <summary>p-значение после коррекции.</summary>
    public double AdjustedPValue { get; init; }

    /// <summary>Наблюдений в контрольной группе.</summary>
    public int ControlSize { get; init; }

    /// <summary>Наблюдений в группе воздействия.</summary>
    public int TreatmentSize { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool becameSignificant = RawPValue > 0.05 && AdjustedPValue <= 0.05;
        bool weakCovariate = Math.Abs(Correlation) < 0.3;

        return new InterpretationBuilder("CUPED: снижение дисперсии")
            .Summary($"Коррекция по предэкспериментальным данным снизила дисперсию на " +
                     $"{Fmt.Pct(VarianceReduction)}, что эквивалентно росту выборки в " +
                     $"{Fmt.Num(EffectiveSampleGain)} раза. Оценка эффекта при этом почти " +
                     $"не изменилась: {Fmt.Num(RawEffect, 4)} против {Fmt.Num(AdjustedEffect, 4)} — " +
                     $"так и должно быть, метод не сдвигает оценку, а уточняет её.")
            .Metric("Снижение дисперсии", Fmt.Pct(VarianceReduction), null,
                "чем выше корреляция с прошлым, тем больше выигрыш",
                VarianceReduction > 0.2 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Эквивалентный рост выборки", EffectiveSampleGain, "раз",
                "во столько раз пришлось бы увеличить трафик ради того же результата",
                VarianceReduction > 0.2 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Корреляция с ковариатой", Correlation, null,
                "предэкспериментальная метрика против экспериментальной",
                weakCovariate ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("p-значение до", RawPValue, null, "фиксированный горизонт, без коррекции",
                RawPValue <= 0.05 ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("p-значение после", AdjustedPValue, null, "после снижения дисперсии",
                AdjustedPValue <= 0.05 ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("Эффект", AdjustedEffect, null,
                $"95 % интервал [{Fmt.Num(AdjustedEffect - (1.96 * AdjustedStandardError), 4)}; " +
                $"{Fmt.Num(AdjustedEffect + (1.96 * AdjustedStandardError), 4)}]", MetricQuality.Neutral, 4)
            .FindingIf(becameSignificant,
                "Эффект стал значимым после коррекции. Это не подгонка: коэффициент коррекции " +
                "оценён на данных до эксперимента и не зависит от того, кто попал в какую группу.")
            .FindingIf(VarianceReduction > 0.4,
                "Выигрыш велик: метрика сильно связана с поведением тех же пользователей " +
                "до эксперимента. Такой эффект характерен для выручки и числа сессий.")
            .WarningIf(weakCovariate,
                $"Корреляция с ковариатой всего {Fmt.Num(Correlation)}: выигрыш будет небольшим. " +
                "Подберите ковариату ближе к целевой метрике — обычно это она же за прошлый период.")
            .WarningIf(Math.Abs(RawEffect - AdjustedEffect) > Math.Abs(RawEffect) * 0.5 && Math.Abs(RawEffect) > 1e-9,
                "Оценка эффекта заметно сдвинулась после коррекции. Это признак того, что " +
                "группы неслучайно различались ещё до эксперимента — проверьте рандомизацию.")
            .Warning("Ковариата обязана быть измерена до начала эксперимента. Любая метрика " +
                     "периода эксперимента сама подвержена воздействию и внесёт смещение.")
            .Recommendation("Применяйте CUPED по умолчанию во всех экспериментах, где есть " +
                            "история пользователя: метод не требует изменений в дизайне.")
            .Build();
    }
}

/// <summary>
/// CUPED: снижение дисперсии экспериментальной метрики за счёт данных,
/// собранных до эксперимента.
/// </summary>
/// <remarks>
/// <para>
/// Идея: пользователи различались между собой ещё до эксперимента, и эта
/// часть разброса не имеет отношения к тестируемому изменению. Если вычесть
/// предсказуемую по прошлому часть, оценка эффекта останется несмещённой,
/// а дисперсия упадёт.
/// </para>
/// <code>
/// Y_adj = Y - theta * (X - mean(X)),   theta = cov(Y, X) / var(X)
/// </code>
/// <para>
/// Снижение дисперсии равно квадрату корреляции между <c>Y</c> и <c>X</c>.
/// Для выручки и числа сессий корреляция с прошлым периодом обычно
/// составляет 0,5–0,7, что даёт сокращение требуемой выборки в полтора-два
/// раза бесплатно.
/// </para>
/// </remarks>
public static class Cuped
{
    /// <summary>Применяет коррекцию и сравнивает результат с обычным критерием.</summary>
    /// <param name="controlPre">Предэкспериментальная метрика контрольной группы.</param>
    /// <param name="controlPost">Экспериментальная метрика контрольной группы.</param>
    /// <param name="treatmentPre">Предэкспериментальная метрика группы воздействия.</param>
    /// <param name="treatmentPost">Экспериментальная метрика группы воздействия.</param>
    /// <returns>Эффект до и после коррекции с оценкой выигрыша.</returns>
    /// <exception cref="ArgumentNullException">Ряды не заданы.</exception>
    /// <exception cref="ArgumentException">Длины внутри группы не совпадают или данных мало.</exception>
    public static CupedResult Apply(
        Vector controlPre, Vector controlPost, Vector treatmentPre, Vector treatmentPost)
    {
        ArgumentNullException.ThrowIfNull(controlPre);
        ArgumentNullException.ThrowIfNull(controlPost);
        ArgumentNullException.ThrowIfNull(treatmentPre);
        ArgumentNullException.ThrowIfNull(treatmentPost);

        if (controlPre.Count != controlPost.Count || treatmentPre.Count != treatmentPost.Count)
            throw new ArgumentException("Внутри группы длины рядов должны совпадать.", nameof(controlPost));
        if (controlPre.Count < 5 || treatmentPre.Count < 5)
            throw new ArgumentException("Нужно минимум по пять наблюдений в группе.", nameof(controlPre));

        double[] allPre = [.. controlPre, .. treatmentPre];
        double[] allPost = [.. controlPost, .. treatmentPost];

        double preMean = allPre.Average();
        double postMean = allPost.Average();

        double covariance = 0, variance = 0, postVariance = 0;
        for (int i = 0; i < allPre.Length; i++)
        {
            double dx = allPre[i] - preMean;
            double dy = allPost[i] - postMean;
            covariance += dx * dy;
            variance += dx * dx;
            postVariance += dy * dy;
        }

        double theta = variance > 0 ? covariance / variance : 0;
        double correlation = variance > 0 && postVariance > 0
            ? covariance / Math.Sqrt(variance * postVariance)
            : 0;

        double[] controlAdjusted = Adjust(controlPost, controlPre, theta, preMean);
        double[] treatmentAdjusted = Adjust(treatmentPost, treatmentPre, theta, preMean);

        (double rawEffect, double rawSe, double rawP) = Compare([.. controlPost], [.. treatmentPost]);
        (double adjEffect, double adjSe, double adjP) = Compare(controlAdjusted, treatmentAdjusted);

        double rawVariance = Variance([.. controlPost]) + Variance([.. treatmentPost]);
        double adjVariance = Variance(controlAdjusted) + Variance(treatmentAdjusted);

        return new CupedResult
        {
            Theta = theta,
            Correlation = correlation,
            VarianceReduction = rawVariance > 0 ? 1.0 - (adjVariance / rawVariance) : 0,
            RawEffect = rawEffect,
            AdjustedEffect = adjEffect,
            RawStandardError = rawSe,
            AdjustedStandardError = adjSe,
            RawPValue = rawP,
            AdjustedPValue = adjP,
            ControlSize = controlPost.Count,
            TreatmentSize = treatmentPost.Count,
        };
    }

    private static double[] Adjust(Vector post, Vector pre, double theta, double preMean)
    {
        var result = new double[post.Count];
        for (int i = 0; i < post.Count; i++) result[i] = post[i] - (theta * (pre[i] - preMean));
        return result;
    }

    private static double Variance(double[] values)
    {
        if (values.Length < 2) return 0;
        double mean = values.Average();
        double sum = 0;
        foreach (double v in values) sum += (v - mean) * (v - mean);
        return sum / (values.Length - 1);
    }

    /// <summary>Сравнение средних двух групп по критерию Уэлча.</summary>
    private static (double Effect, double StandardError, double PValue) Compare(
        double[] control, double[] treatment)
    {
        double effect = treatment.Average() - control.Average();
        double se = Math.Sqrt((Variance(control) / control.Length) + (Variance(treatment) / treatment.Length));
        double z = se > 0 ? effect / se : 0;
        double p = 2.0 * (1.0 - EconMath.NormalCdf(Math.Abs(z)));
        return (effect, se, p);
    }
}
