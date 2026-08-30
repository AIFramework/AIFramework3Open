using System;
using AI.DataStructs.Algebraic;
using AI.Econometrics.Numerics;

using AI.Insights;

namespace AI.Economics.Market;

/// <summary>
/// Модель диффузии Басса: прогноз проникновения продукта на рынок.
/// </summary>
/// <remarks>
/// <para>
/// Модель делит покупателей на две группы. Новаторы принимают продукт
/// независимо от других с интенсивностью <c>p</c>; имитаторы — под влиянием
/// уже принявших, с интенсивностью, пропорциональной их доле, коэффициент
/// <c>q</c>. Отсюда S-образная кривая: медленный старт, взрывной рост,
/// насыщение.
/// </para>
/// <para>
/// Практическая ценность для стартапа — не сама кривая, а два следствия из
/// подогнанных параметров. Во-первых, момент пика продаж
/// <c>t* = ln(q/p)/(p+q)</c>: после него объём новых клиентов падает, даже
/// если маркетинг не меняется. Во-вторых, потолок <c>m</c>: если он
/// оказывается заметно меньше заявленного SAM, значит, гипотеза о рынке
/// не подтверждается фактическими продажами.
/// </para>
/// </remarks>
public sealed partial class BassDiffusion
{
    /// <summary>Потенциал рынка <c>m</c> — предельное число принявших.</summary>
    public double MarketPotential { get; private set; }

    /// <summary>Коэффициент инновации <c>p</c>: типичные значения 0,01–0,03.</summary>
    public double Innovation { get; private set; }

    /// <summary>Коэффициент имитации <c>q</c>: типичные значения 0,3–0,5.</summary>
    public double Imitation { get; private set; }

    /// <summary>Коэффициент детерминации подгонки по накопленным принявшим.</summary>
    public double RSquared { get; private set; }

    /// <summary>Момент пика новых принявших, в периодах от старта.</summary>
    public double PeakTime => Innovation > 0 && Imitation > Innovation
        ? Math.Log(Imitation / Innovation) / (Innovation + Imitation)
        : 0;

    /// <summary>Число новых принявших в момент пика.</summary>
    public double PeakAdopters => Imitation > 0
        ? MarketPotential * Math.Pow(Innovation + Imitation, 2) / (4.0 * Imitation)
        : 0;

    /// <summary>Задаёт параметры модели напрямую, без подгонки.</summary>
    /// <param name="marketPotential">Потенциал рынка.</param>
    /// <param name="innovation">Коэффициент инновации.</param>
    /// <param name="imitation">Коэффициент имитации.</param>
    public void SetParameters(double marketPotential, double innovation, double imitation)
    {
        MarketPotential = marketPotential;
        Innovation = innovation;
        Imitation = imitation;
    }

    /// <summary>
    /// Подгоняет параметры по накопленному числу принявших.
    /// </summary>
    /// <param name="cumulativeAdopters">
    /// Накопленное число принявших по периодам, начиная с первого.
    /// </param>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Меньше четырёх наблюдений.</exception>
    /// <remarks>
    /// Сначала работает регрессия Басса: приращения объясняются накопленным
    /// числом и его квадратом, из коэффициентов восстанавливаются
    /// <c>m</c>, <c>p</c>, <c>q</c>. Полученная оценка служит начальным
    /// приближением для нелинейного метода наименьших квадратов — сама по
    /// себе она смещена, но без неё безградиентная оптимизация уходит
    /// в локальный минимум.
    /// </remarks>
    public void Fit(Vector cumulativeAdopters)
    {
        ArgumentNullException.ThrowIfNull(cumulativeAdopters);
        if (cumulativeAdopters.Count < 4)
            throw new ArgumentException("Нужно минимум четыре наблюдения.", nameof(cumulativeAdopters));

        int n = cumulativeAdopters.Count;
        var cumulative = new double[n];
        for (int i = 0; i < n; i++) cumulative[i] = cumulativeAdopters[i];

        (double m, double p, double q) = RegressionGuess(cumulative);

        double[] best = NelderMead.MinimizePositive(
            v => SumSquaredError(cumulative, v[0], v[1], v[2]),
            [m, p, q]);

        MarketPotential = best[0];
        Innovation = best[1];
        Imitation = best[2];
        RSquared = ComputeRSquared(cumulative);
    }

    /// <summary>Накопленное число принявших по периодам.</summary>
    /// <param name="periods">Число периодов прогноза.</param>
    /// <returns>Вектор длиной <paramref name="periods"/>.</returns>
    public Vector Cumulative(int periods)
    {
        var v = new Vector(periods);
        for (int i = 0; i < periods; i++) v[i] = CumulativeAt(i + 1, MarketPotential, Innovation, Imitation);
        return v;
    }

    /// <summary>Число новых принявших в каждом периоде.</summary>
    /// <param name="periods">Число периодов прогноза.</param>
    /// <returns>Вектор длиной <paramref name="periods"/>.</returns>
    public Vector Adopters(int periods)
    {
        var v = new Vector(periods);
        double previous = 0;

        for (int i = 0; i < periods; i++)
        {
            double current = CumulativeAt(i + 1, MarketPotential, Innovation, Imitation);
            v[i] = current - previous;
            previous = current;
        }

        return v;
    }

    /// <summary>Значение кривой в непрерывном времени.</summary>
    private static double CumulativeAt(double t, double m, double p, double q)
    {
        if (p <= 0) return 0;

        double e = Math.Exp(-(p + q) * t);
        return m * (1.0 - e) / (1.0 + (q / p * e));
    }

    private static double SumSquaredError(double[] observed, double m, double p, double q)
    {
        double sum = 0;
        for (int i = 0; i < observed.Length; i++)
        {
            double d = observed[i] - CumulativeAt(i + 1, m, p, q);
            sum += d * d;
        }
        return sum;
    }

    private double ComputeRSquared(double[] observed)
    {
        double mean = 0;
        for (int i = 0; i < observed.Length; i++) mean += observed[i];
        mean /= observed.Length;

        double ssTotal = 0, ssResidual = 0;
        for (int i = 0; i < observed.Length; i++)
        {
            double fitted = CumulativeAt(i + 1, MarketPotential, Innovation, Imitation);
            ssResidual += Math.Pow(observed[i] - fitted, 2);
            ssTotal += Math.Pow(observed[i] - mean, 2);
        }

        return ssTotal > 0 ? 1.0 - (ssResidual / ssTotal) : 0;
    }

    /// <summary>
    /// Регрессия Басса: <c>n_t = a + b N_(t-1) + c N_(t-1)^2</c>, откуда
    /// <c>m</c> — положительный корень квадратного уравнения,
    /// <c>p = a / m</c>, <c>q = -c m</c>.
    /// </summary>
    private static (double M, double P, double Q) RegressionGuess(double[] cumulative)
    {
        int n = cumulative.Length - 1;
        var design = new double[3, 3];
        var rhs = new double[3];

        for (int i = 1; i <= n; i++)
        {
            double prev = cumulative[i - 1];
            double increment = cumulative[i] - prev;
            double[] row = [1.0, prev, prev * prev];

            for (int j = 0; j < 3; j++)
            {
                rhs[j] += row[j] * increment;
                for (int k = 0; k < 3; k++) design[j, k] += row[j] * row[k];
            }
        }

        double[]? beta = EconMath.SolveLinear(design, rhs);
        double last = cumulative[^1];

        if (beta is null) return (last * 3, 0.02, 0.4);

        double a = beta[0], b = beta[1], c = beta[2];
        double m = last * 3;

        if (c < 0)
        {
            double discriminant = (b * b) - (4 * a * c);
            if (discriminant >= 0)
            {
                double root = (-b - Math.Sqrt(discriminant)) / (2 * c);
                if (root > last) m = root;
            }
        }

        double p = m > 0 ? Math.Max(a / m, 1e-4) : 0.02;
        double q = Math.Max(-c * m, 1e-3);

        return (m, p, q);
    }
}
