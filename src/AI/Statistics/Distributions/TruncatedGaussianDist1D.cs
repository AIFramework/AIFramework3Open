#nullable enable
using System;

namespace AI.Statistics.Distributions;

/// <summary>
/// Нормальное N(μ, σ²), усечённое до [Lower; Upper]. Для величин, которые не бывают отрицательными
/// или ограничены по смыслу: масса, расстояние, скорость.
/// </summary>
/// <remarks>
/// Нормировка считается через логарифм хвоста нормального распределения с относительной точностью ~1e-7
/// на всей оси. Поэтому плотность верна и далеко в хвосте, где 1 − Φ(x) через обычную функцию
/// распределения теряет все значащие цифры.
/// </remarks>
public sealed class TruncatedGaussianDist1D : SimpleDist1DBase
{
    /// <summary>ln(Φ(β) − Φ(α)) — доля исходного нормального, попавшая в интервал</summary>
    private readonly double _logMass;

    /// <summary>Среднее исходного нормального</summary>
    public double Mu { get; }

    /// <summary>Стандартное отклонение исходного нормального</summary>
    public double Sigma { get; }

    /// <summary>Нижняя граница; может быть −∞</summary>
    public double Lower { get; }

    /// <summary>Верхняя граница; может быть +∞</summary>
    public double Upper { get; }

    /// <summary>Математическое ожидание усечённого распределения: μ + σ·(φ(α) − φ(β)) / (Φ(β) − Φ(α))</summary>
    public double Mean => Mu + (Sigma * (Math.Exp(LogPhi(Alpha) - _logMass) - Math.Exp(LogPhi(Beta) - _logMass)));

    private double Alpha => (Lower - Mu) / Sigma;

    private double Beta => (Upper - Mu) / Sigma;

    /// <summary>Создаёт распределение</summary>
    /// <param name="mu">Среднее исходного нормального</param>
    /// <param name="sigma">Стандартное отклонение исходного нормального</param>
    /// <param name="lower">Нижняя граница; допускается −∞</param>
    /// <param name="upper">Верхняя граница; допускается +∞</param>
    public TruncatedGaussianDist1D(double mu, double sigma, double lower, double upper = double.PositiveInfinity)
    {
        if (double.IsNaN(mu) || double.IsInfinity(mu))
            throw new ArgumentOutOfRangeException(nameof(mu), "Среднее — конечное число");

        if (!(sigma > 0) || double.IsInfinity(sigma))
            throw new ArgumentOutOfRangeException(nameof(sigma), "Отклонение — конечное положительное число");

        if (!(lower < upper))
            throw new ArgumentException("Нижняя граница должна быть меньше верхней", nameof(lower));

        Mu = mu;
        Sigma = sigma;
        Lower = lower;
        Upper = upper;
        _logMass = LogMass(Alpha, Beta);

        if (double.IsNegativeInfinity(_logMass) || double.IsNaN(_logMass))
            throw new ArgumentException("Интервал лежит так далеко в хвосте, что его вероятность неотличима от нуля", nameof(lower));
    }

    /// <inheritdoc/>
    public override double CulcProb(double x) => x < Lower || x > Upper ? 0 : Math.Exp(CulcLogProb(x));

    /// <summary>
    /// Точная лог-плотность на интервале: −z²/2 − ln(σ·√(2π)) − ln(Φ(β) − Φ(α));
    /// вне интервала — <see cref="SimpleDist1DBase.LogProbFloor"/>
    /// </summary>
    public override double CulcLogProb(double x)
    {
        if (x < Lower || x > Upper)
            return LogProbFloor;

        double z = (x - Mu) / Sigma;
        return (-0.5 * z * z) - (0.5 * Math.Log(2.0 * Math.PI)) - Math.Log(Sigma) - _logMass;
    }

    /// <inheritdoc/>
    public override double Sample1D(Random rng) => RandomEngine.NextTruncatedGaussian(rng, Mu, Sigma, Lower, Upper);

    /// <summary>
    /// Оценка по данным с сохранением границ: μ и σ — выборочные среднее и отклонение. Это приближение:
    /// при сильном усечении оно смещает μ к середине интервала и занижает σ
    /// </summary>
    public override IDistributionWithoutParams Refit1D(double[] data, int count)
    {
        double sum = 0;
        for (int i = 0; i < count; i++)
            sum += data[i];
        double mean = sum / count;

        double squares = 0;
        for (int i = 0; i < count; i++)
            squares += (data[i] - mean) * (data[i] - mean);

        return new TruncatedGaussianDist1D(mean, Math.Max(Math.Sqrt(squares / count), 1e-6), Lower, Upper);
    }

    private static double LogPhi(double z) =>
        double.IsInfinity(z) ? double.NegativeInfinity : (-0.5 * z * z) - (0.5 * Math.Log(2.0 * Math.PI));

    /// <summary>ln(Φ(b) − Φ(a)) без потери точности в хвостах</summary>
    private static double LogMass(double a, double b)
    {
        // Правый хвост: Q(a) − Q(b) = Q(a)·(1 − Q(b)/Q(a))
        if (a >= 0)
            return LogUpperTail(a) + LogOneMinusExp(LogUpperTail(b) - LogUpperTail(a));

        // Левый хвост — зеркально
        if (b <= 0)
            return LogUpperTail(-b) + LogOneMinusExp(LogUpperTail(-a) - LogUpperTail(-b));

        // Интервал накрывает ноль: оба отброшенных хвоста не больше половины, вычитание устойчиво
        return Math.Log(1.0 - Math.Exp(LogUpperTail(-a)) - Math.Exp(LogUpperTail(b)));
    }

    /// <summary>
    /// ln Q(x), Q(x) = 1 − Φ(x): erfc по Numerical Recipes (чебышёвская аппроксимация, относительная
    /// погрешность меньше 1.2e-7 на всей оси), сразу в логарифме — без исчезновения порядка далеко в хвосте
    /// </summary>
    private static double LogUpperTail(double x)
    {
        if (double.IsPositiveInfinity(x))
            return double.NegativeInfinity;

        if (double.IsNegativeInfinity(x))
            return 0.0;

        double z = Math.Abs(x) / Math.Sqrt(2.0);
        double t = 1.0 / (1.0 + (0.5 * z));
        double poly = 1.00002368 + t * (0.37409196 + t * (0.09678418 + t * (-0.18628806 + t * (0.27886807
            + t * (-1.13520398 + t * (1.48851587 + t * (-0.82215223 + t * 0.17087277)))))));
        double logErfc = Math.Log(t) - (z * z) - 1.26551223 + (t * poly);

        return x >= 0 ? Math.Log(0.5) + logErfc : Math.Log(1.0 - (0.5 * Math.Exp(logErfc)));
    }

    /// <summary>ln(1 − eᵈ) при d ≤ 0 без потери точности у нуля (приём Мехлера)</summary>
    private static double LogOneMinusExp(double d)
    {
        if (d > -0.6931471805599453)
            return Math.Log(-(Math.Abs(d) < 1e-5 ? d + (0.5 * d * d) : Math.Exp(d) - 1.0));

        return Math.Log(1.0 - Math.Exp(d));
    }
}
