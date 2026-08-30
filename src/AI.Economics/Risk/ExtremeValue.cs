using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Risk;

/// <summary>Результат подгонки обобщённого распределения Парето к хвосту.</summary>
public sealed record ExtremeValueResult : IInterpretable
{
    /// <summary>Название ряда.</summary>
    public string Series { get; init; } = "убытки";

    /// <summary>Порог, выше которого превышения считаются экстремальными.</summary>
    public double Threshold { get; init; }

    /// <summary>Параметр формы: положительный означает тяжёлый хвост.</summary>
    public double Shape { get; init; }

    /// <summary>Параметр масштаба.</summary>
    public double Scale { get; init; }

    /// <summary>Число превышений порога.</summary>
    public int Exceedances { get; init; }

    /// <summary>Доля наблюдений выше порога.</summary>
    public double ExceedanceRate { get; init; }

    /// <summary>Оценки квантилей убытков по уровням доверия.</summary>
    public IReadOnlyList<(double Confidence, double ValueAtRisk, double ExpectedShortfall)> TailQuantiles { get; init; } = [];

    /// <summary>Оценка квантиля по эмпирическому распределению для сравнения.</summary>
    public IReadOnlyList<(double Confidence, double Empirical)> EmpiricalQuantiles { get; init; } = [];

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Логарифм правдоподобия подгонки.</summary>
    public double LogLikelihood { get; init; }

    /// <summary>Существует ли конечное математическое ожидание убытка.</summary>
    public bool HasFiniteMean => Shape < 1;

    /// <summary>Существует ли конечная дисперсия убытка.</summary>
    public bool HasFiniteVariance => Shape < 0.5;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool heavyTail = Shape > 0.15;

        (double Confidence, double ValueAtRisk, double ExpectedShortfall) extreme =
            TailQuantiles.OrderByDescending(q => q.Confidence).FirstOrDefault();

        (double Confidence, double Empirical) empirical =
            EmpiricalQuantiles.OrderByDescending(q => q.Confidence).FirstOrDefault();

        double gap = empirical.Empirical > 0
            ? (extreme.ValueAtRisk - empirical.Empirical) / empirical.Empirical
            : 0;

        var builder = new InterpretationBuilder($"Теория экстремальных значений: {Series}")
            .Summary($"Порог {Fmt.Pct(Threshold, 2)} превышен в {Exceedances} наблюдениях из " +
                     $"{Observations} ({Fmt.Pct(ExceedanceRate, 1)}). Параметр формы " +
                     $"{Fmt.Num(Shape, 3)} — хвост {(heavyTail ? "тяжёлый" : "умеренный")}. " +
                     $"На уровне {Fmt.Pct(extreme.Confidence, 2)} оценка потерь " +
                     $"{Fmt.Pct(extreme.ValueAtRisk, 2)} против {Fmt.Pct(empirical.Empirical, 2)} " +
                     "по эмпирическому распределению.")
            .Metric("Параметр формы", Shape, null,
                heavyTail ? "степенной хвост: экстремумы вероятнее нормальных" : "хвост близок к экспоненциальному",
                heavyTail ? MetricQuality.Warning : MetricQuality.Neutral, 3)
            .Metric("Параметр масштаба", Scale, null, "разброс превышений над порогом",
                MetricQuality.Neutral, 4)
            .Metric("Порог", Threshold, null,
                $"превышений {Exceedances} ({Fmt.Pct(ExceedanceRate, 1)} наблюдений)",
                Exceedances >= 50 ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("Конечное среднее убытка", HasFiniteMean ? "да" : "нет", null,
                HasFiniteMean
                    ? "ожидаемые потери в хвосте определены"
                    : "параметр формы не меньше единицы: среднее не существует",
                HasFiniteMean ? MetricQuality.Good : MetricQuality.Critical)
            .Metric("Конечная дисперсия", HasFiniteVariance ? "да" : "нет", null,
                HasFiniteVariance ? "дисперсия убытка определена" : "дисперсия бесконечна",
                HasFiniteVariance ? MetricQuality.Good : MetricQuality.Warning);

        foreach ((double confidence, double var, double shortfall) in TailQuantiles)
        {
            builder.Metric($"Уровень {Fmt.Pct(confidence, 2)}", var, null,
                $"ожидаемые потери в хвосте {Fmt.Pct(shortfall, 2)}", MetricQuality.Unknown, 4);
        }

        return builder
            .Finding("Метод превышений порога описывает только хвост, а не всё распределение. " +
                     "Это его сила: для оценки редких событий форма центра распределения " +
                     "не важна и лишь мешает.")
            .FindingIf(heavyTail,
                $"Параметр формы {Fmt.Num(Shape, 3)} положителен: хвост степенной. " +
                "Это означает, что убытки не имеют естественного потолка, и любая " +
                "модель с нормальными хвостами занижает риск тем сильнее, " +
                "чем дальше в хвост мы смотрим.")
            .FindingIf(Math.Abs(gap) > 0.1,
                $"На крайнем уровне оценка по теории экстремальных значений отличается " +
                $"от эмпирической на {Fmt.Pct(gap, 0)}. Эмпирический квантиль там опирается " +
                "на единицы наблюдений и потому ненадёжен — в этом и смысл параметрической " +
                "аппроксимации хвоста.")
            .WarningIf(Exceedances < 50,
                $"Превышений порога всего {Exceedances}. Оценка параметров по такой " +
                "выборке неустойчива; понизьте порог или увеличьте историю.")
            .WarningIf(!HasFiniteMean,
                $"Параметр формы {Fmt.Num(Shape, 3)} не меньше единицы: математическое " +
                "ожидание убытка бесконечно. Ожидаемые потери в хвосте в этом случае " +
                "не определены, и опираться можно только на квантили.")
            .WarningIf(ExceedanceRate > 0.15,
                $"Порог отсекает {Fmt.Pct(ExceedanceRate, 0)} наблюдений. Асимптотическое " +
                "обоснование метода требует, чтобы превышения были редкими: " +
                "при слишком низком пороге в выборку попадает центр распределения.")
            .Warning("Выбор порога — компромисс между смещением и дисперсией, и он " +
                     "существенно влияет на результат. Устойчивость оценки к сдвигу " +
                     "порога нужно проверять отдельно.")
            .Recommendation("Стройте график среднего превышения над порогом: линейный " +
                            "участок указывает область, где обобщённое распределение " +
                            "Парето применимо.")
            .Recommendation("Используйте метод для уровней доверия выше 99%: на более " +
                            "низких эмпирический квантиль надёжнее и не требует " +
                            "предпосылок о форме хвоста.")
            .Build();
    }
}

/// <summary>
/// Теория экстремальных значений: подгонка хвоста распределения убытков.
/// </summary>
/// <remarks>
/// <para>
/// Для редких событий важна не форма распределения в целом, а поведение его
/// хвоста. Теорема Пикандса — Балкемы — де Хаана утверждает, что превышения
/// над достаточно высоким порогом асимптотически описываются обобщённым
/// распределением Парето независимо от исходного распределения:
/// </para>
/// <code>
/// P(X - u &lt;= y | X &gt; u) = 1 - (1 + xi * y / beta)^(-1/xi)
/// </code>
/// <para>
/// Параметр формы определяет тяжесть хвоста. При нулевом значении хвост
/// экспоненциальный, при положительном — степенной: убытки не имеют
/// естественного потолка. При значении не меньше единицы среднее убытка
/// бесконечно, и ожидаемые потери в хвосте теряют смысл.
/// </para>
/// <para>
/// Квантили за пределами наблюдённых данных получаются экстраполяцией:
/// </para>
/// <code>
/// VaR_a = u + (beta / xi) * ((n / N_u * (1 - a))^(-xi) - 1)
/// ES_a  = (VaR_a + beta - xi * u) / (1 - xi)
/// </code>
/// <para>
/// Это единственный корректный способ оценить убыток уровня 99,9% по нескольким
/// сотням наблюдений: эмпирический квантиль там опирается на единичные точки.
/// </para>
/// </remarks>
public static class ExtremeValue
{
    /// <summary>Подгоняет обобщённое распределение Парето к хвосту убытков.</summary>
    /// <param name="returns">Ряд доходностей; убытками считаются отрицательные значения.</param>
    /// <param name="thresholdQuantile">Квантиль убытков, задающий порог.</param>
    /// <param name="confidenceLevels">Уровни доверия для оценки квантилей.</param>
    /// <param name="series">Название ряда.</param>
    /// <returns>Параметры хвоста и оценки квантилей убытков.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    /// <exception cref="ArgumentException">Наблюдений недостаточно или порог задан неверно.</exception>
    public static ExtremeValueResult Fit(
        Vector returns, double thresholdQuantile = 0.95,
        IReadOnlyList<double>? confidenceLevels = null, string series = "убытки")
    {
        ArgumentNullException.ThrowIfNull(returns);

        if (returns.Count < 100)
            throw new ArgumentException("Нужно минимум сто наблюдений.", nameof(returns));
        if (thresholdQuantile is <= 0.5 or >= 0.999)
            throw new ArgumentException("Квантиль порога должен лежать между 0,5 и 0,999.", nameof(thresholdQuantile));

        // Работаем с убытками как с положительными величинами
        double[] losses = [.. returns.Select(r => -r).OrderBy(v => v)];
        double threshold = EconMath.Quantile(losses, thresholdQuantile);

        double[] excesses = [.. losses.Where(l => l > threshold).Select(l => l - threshold)];

        if (excesses.Length < 10)
            throw new ArgumentException("Превышений порога слишком мало.", nameof(thresholdQuantile));

        (double shape, double scale, double logLikelihood) = FitGeneralizedPareto(excesses);

        int n = losses.Length;
        double rate = (double)excesses.Length / n;

        IReadOnlyList<double> levels = confidenceLevels is { Count: > 0 }
            ? confidenceLevels
            : [0.99, 0.995, 0.999];

        var quantiles = new List<(double, double, double)>(levels.Count);
        var empirical = new List<(double, double)>(levels.Count);

        foreach (double level in levels.OrderBy(l => l))
        {
            double var = TailQuantile(threshold, shape, scale, rate, level);
            double shortfall = shape < 1
                ? (var + scale - (shape * threshold)) / (1 - shape)
                : double.PositiveInfinity;

            quantiles.Add((level, var, double.IsFinite(shortfall) ? shortfall : var * 2));
            empirical.Add((level, EconMath.Quantile(losses, level)));
        }

        return new ExtremeValueResult
        {
            Series = series,
            Threshold = threshold,
            Shape = shape,
            Scale = scale,
            Exceedances = excesses.Length,
            ExceedanceRate = rate,
            TailQuantiles = quantiles,
            EmpiricalQuantiles = empirical,
            Observations = n,
            LogLikelihood = logLikelihood,
        };
    }

    /// <summary>Квантиль убытка по подогнанному хвосту.</summary>
    /// <param name="threshold">Порог.</param>
    /// <param name="shape">Параметр формы.</param>
    /// <param name="scale">Параметр масштаба.</param>
    /// <param name="exceedanceRate">Доля наблюдений выше порога.</param>
    /// <param name="confidence">Уровень доверия.</param>
    /// <returns>Оценка квантиля убытка.</returns>
    public static double TailQuantile(
        double threshold, double shape, double scale, double exceedanceRate, double confidence)
    {
        double ratio = (1 - confidence) / Math.Max(exceedanceRate, 1e-12);

        return Math.Abs(shape) < 1e-8
            ? threshold - (scale * Math.Log(ratio))
            : threshold + (scale / shape * (Math.Pow(ratio, -shape) - 1));
    }

    /// <summary>График среднего превышения над порогом.</summary>
    /// <param name="returns">Ряд доходностей.</param>
    /// <param name="points">Число точек графика.</param>
    /// <returns>Пары «порог — среднее превышение»: линейный участок указывает область применимости.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    public static IReadOnlyList<(double Threshold, double MeanExcess)> MeanExcessPlot(
        Vector returns, int points = 30)
    {
        ArgumentNullException.ThrowIfNull(returns);

        double[] losses = [.. returns.Select(r => -r).OrderBy(v => v)];
        var plot = new List<(double, double)>(points);

        for (int i = 0; i < points; i++)
        {
            double quantile = 0.5 + (i * 0.45 / Math.Max(1, points - 1));
            double threshold = EconMath.Quantile(losses, quantile);

            var excesses = losses.Where(l => l > threshold).Select(l => l - threshold).ToList();
            if (excesses.Count < 5) continue;

            plot.Add((threshold, excesses.Average()));
        }

        return plot;
    }

    /// <summary>Оценивает параметры обобщённого распределения Парето методом максимального правдоподобия.</summary>
    private static (double Shape, double Scale, double LogLikelihood) FitGeneralizedPareto(double[] excesses)
    {
        double mean = excesses.Average();
        double variance = excesses.Sum(e => (e - mean) * (e - mean)) / Math.Max(1, excesses.Length - 1);

        // Метод моментов даёт начальное приближение
        double startShape = variance > 0 ? 0.5 * (1 - (mean * mean / variance)) : 0.1;
        double startScale = Math.Max(mean * (1 - startShape), 1e-8);

        double Negative(double[] p)
        {
            double shape = p[0];
            double scale = Math.Exp(Math.Clamp(p[1], -30, 10));
            double total = 0;

            foreach (double excess in excesses)
            {
                if (Math.Abs(shape) < 1e-8)
                {
                    total += -Math.Log(scale) - (excess / scale);
                    continue;
                }

                double term = 1 + (shape * excess / scale);
                if (term <= 0) return double.MaxValue;

                total += -Math.Log(scale) - ((1 + (1 / shape)) * Math.Log(term));
            }

            return double.IsFinite(total) ? -total : double.MaxValue;
        }

        double[] estimate = NelderMead.Minimize(
            Negative, [Math.Clamp(startShape, -0.4, 0.9), Math.Log(startScale)], 3000);

        double fittedShape = Math.Clamp(estimate[0], -0.49, 1.5);
        double fittedScale = Math.Exp(Math.Clamp(estimate[1], -30, 10));

        return (fittedShape, fittedScale, -Negative(estimate));
    }
}
