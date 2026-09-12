#nullable enable
using AI.DataStructs.Algebraic;
using System;
using System.Linq;

namespace AI.Statistics.Distributions;

/// <summary>
/// Распределение фон Мизеса VM(μ, κ) — «нормальное на окружности» для направлений.
/// Углы в радианах; плотность периодична, поэтому 359° и 1° — соседи, а не противоположные концы шкалы.
/// </summary>
/// <remarks>
/// κ = 0 — равномерное по окружности: «направление неизвестно». При больших κ распределение
/// близко к N(μ, 1/κ), поэтому привычное «±σ» переводится в κ ≈ 1/σ² (<see cref="FromStandardDeviation"/>).
/// </remarks>
public sealed class VonMisesDist1D : SimpleDist1DBase
{
    /// <summary>Концентрация, которой ограничивается оценка при почти совпадающих направлениях</summary>
    private const double MaxKappa = 1e8;

    /// <summary>Среднее направление, радианы на [0; 2π)</summary>
    public double Mu { get; }

    /// <summary>Концентрация κ ≥ 0</summary>
    public double Kappa { get; }

    /// <summary>Создаёт распределение</summary>
    /// <param name="mu">Среднее направление, радианы</param>
    /// <param name="kappa">Концентрация κ ≥ 0</param>
    public VonMisesDist1D(double mu, double kappa)
    {
        if (!(kappa >= 0) || double.IsInfinity(kappa))
            throw new ArgumentOutOfRangeException(nameof(kappa), "Концентрация — конечное неотрицательное число");

        Mu = CircularStatistics.WrapRadians(mu);
        Kappa = kappa;
    }

    /// <inheritdoc/>
    public override double CulcProb(double x) => Math.Exp(CulcLogProb(x));

    /// <summary>Точная лог-плотность: κ·cos(x − μ) − ln(2π·I₀(κ))</summary>
    public override double CulcLogProb(double x) => (Kappa * Math.Cos(x - Mu)) - Math.Log(2.0 * Math.PI) - LogBesselI0(Kappa);

    /// <inheritdoc/>
    public override double Sample1D(Random rng) => RandomEngine.NextVonMises(rng, Mu, Kappa);

    /// <summary>
    /// Оценка по данным: среднее направление — точная оценка максимального правдоподобия,
    /// κ — приближение Беста — Фишера к обращению I₁(κ)/I₀(κ) = R
    /// </summary>
    public override IDistributionWithoutParams Refit1D(double[] data, int count)
    {
        (double direction, double length) = CircularStatistics.Resultant(new Vector(data.Take(count)), null);
        return new VonMisesDist1D(direction, KappaFromResultantLength(length));
    }

    /// <summary>Распределение по привычному «±σ»: κ = 1/σ², точно при малых σ</summary>
    /// <param name="mu">Среднее направление, радианы</param>
    /// <param name="sigma">Разброс, радианы</param>
    public static VonMisesDist1D FromStandardDeviation(double mu, double sigma)
    {
        if (!(sigma > 0))
            throw new ArgumentOutOfRangeException(nameof(sigma), "Разброс — положительное число");

        return new VonMisesDist1D(mu, Math.Min(1.0 / (sigma * sigma), MaxKappa));
    }

    /// <summary>ln I₀(x) — модифицированная функция Бесселя по Абрамовицу — Стиган 9.8.1–9.8.2, погрешность ~2e-7</summary>
    private static double LogBesselI0(double x)
    {
        if (x < 3.75)
        {
            double t = x / 3.75 * (x / 3.75);
            return Math.Log(1.0 + t * (3.5156229 + t * (3.0899424 + t * (1.2067492 + t * (0.2659732 + t * (0.0360768 + t * 0.0045813))))));
        }

        double s = 3.75 / x;
        double poly = 0.39894228 + s * (0.01328592 + s * (0.00225319 + s * (-0.00157565 + s * (0.00916281
            + s * (-0.02057706 + s * (0.02635537 + s * (-0.01647633 + s * 0.00392377)))))));
        return x - (0.5 * Math.Log(x)) + Math.Log(poly);
    }

    /// <summary>Обращение A(κ) = I₁(κ)/I₀(κ) по Бесту — Фишеру (1981)</summary>
    private static double KappaFromResultantLength(double r)
    {
        if (r <= 0)
            return 0;

        if (r < 0.53)
            return (2 * r) + (r * r * r) + (5 * Math.Pow(r, 5) / 6);

        if (r < 0.85)
            return -0.4 + (1.39 * r) + (0.43 / (1 - r));

        return r >= 1 ? MaxKappa : Math.Min(1.0 / ((r * r * r) - (4 * r * r) + (3 * r)), MaxKappa);
    }
}
