using System;
using System.Threading;

namespace AI.Statistics;

/// <summary>
/// Потокобезопасное ядро генераторов псевдослучайных чисел.
/// Обеспечивает единообразный доступ к Random для всей статистической
/// подсистемы: поток-локальный shared-генератор, явное создание
/// seed-ных RNG, корректный Box-Muller для нормальных величин и
/// заполнение буферов без аллокаций.
/// </summary>
/// <remarks>
/// Любой код, которому нужен Random, должен брать его из этого класса
/// или принимать снаружи. Локальные «new Random()» в горячем коде
/// запрещены (повторы зерна при быстрых подряд вызовах + не
/// потокобезопасны).
/// </remarks>
public static class RandomEngine
{
    // Поток-локальный Random. Seed рассчитывается из Guid, чтобы два
    // потока, стартовавших в одну миллисекунду, не получили
    // одинаковую последовательность.
    private static readonly ThreadLocal<Random> _tls =
        new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

    /// <summary>
    /// Поток-локальный Random. Один экземпляр на поток, безопасно
    /// для параллельного использования.
    /// </summary>
    public static Random Shared => _tls.Value;

    /// <summary>
    /// Создаёт новый <see cref="Random"/>. Если <paramref name="seed"/>
    /// задан — последовательность детерминирована (полезно для
    /// воспроизводимых экспериментов и юнит-тестов).
    /// </summary>
    public static Random Create(int? seed = null)
        => seed.HasValue ? new Random(seed.Value) : new Random();

    #region Равномерное U(0,1)

    /// <summary>Одно равномерное число из [0; 1).</summary>
    public static double NextUniform() => Shared.NextDouble();

    /// <summary>Одно равномерное число из [0; 1) на заданном RNG.</summary>
    public static double NextUniform(Random rng) => rng.NextDouble();

    /// <summary>Заполняет буфер значениями U(0, 1).</summary>
    public static void FillUniform(Span<double> buffer, Random rng)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = rng.NextDouble();
    }

    #endregion

    #region Нормальное N(0,1) через полярный Box-Muller

    // Кэшируем вторую сгенерированную величину Box-Muller'а на поток.
    // Полярный метод выдаёт две независимые N(0,1) за одну итерацию —
    // берём вторую бесплатно.
    [ThreadStatic]
    private static double _sparedGaussian;
    [ThreadStatic]
    private static bool _hasSpared;

    /// <summary>
    /// Стандартная нормальная величина N(0, 1). Полярный Box-Muller
    /// с кэшированием второй компоненты.
    /// </summary>
    public static double NextGaussian(Random rng)
    {
        if (_hasSpared)
        {
            _hasSpared = false;
            return _sparedGaussian;
        }

        double u, v, s;
        do
        {
            u = (2.0 * rng.NextDouble()) - 1.0;
            v = (2.0 * rng.NextDouble()) - 1.0;
            s = (u * u) + (v * v);
        } while (s >= 1.0 || s == 0.0);

        double mul = Math.Sqrt(-2.0 * Math.Log(s) / s);
        _sparedGaussian = v * mul;
        _hasSpared = true;
        return u * mul;
    }

    /// <summary>Стандартная нормальная величина на поток-локальном RNG.</summary>
    public static double NextGaussian() => NextGaussian(Shared);

    /// <summary>Нормальная величина с заданными mean и std.</summary>
    public static double NextGaussian(Random rng, double mean, double std)
        => mean + (std * NextGaussian(rng));

    /// <summary>Заполняет буфер значениями N(0, 1).</summary>
    public static void FillGaussian(Span<double> buffer, Random rng)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = NextGaussian(rng);
    }

    #endregion

    #region Экспоненциальное и прочие базовые

    /// <summary>
    /// Экспоненциальное распределение Exp(rate) через инверсию CDF.
    /// </summary>
    public static double NextExponential(Random rng, double rate = 1.0)
    {
        double u;
        do { u = rng.NextDouble(); } while (u <= 0.0);
        return -Math.Log(u) / rate;
    }

    /// <summary>
    /// Гамма-распределение Gamma(shape, scale) — алгоритм Marsaglia–Tsang.
    /// </summary>
    public static double NextGamma(Random rng, double shape, double scale = 1.0)
    {
        if (shape <= 0) throw new ArgumentOutOfRangeException(nameof(shape));
        if (shape < 1.0)
        {
            double u = rng.NextDouble();
            return NextGamma(rng, shape + 1.0, scale) * Math.Pow(u, 1.0 / shape);
        }

        double d = shape - (1.0 / 3.0);
        double c = 1.0 / Math.Sqrt(9.0 * d);
        while (true)
        {
            double x = NextGaussian(rng);
            double v = 1.0 + c * x;
            if (v <= 0) continue;
            v = v * v * v;
            double u2 = rng.NextDouble();
            if (u2 < 1.0 - 0.0331 * (x * x) * (x * x)) return d * v * scale;
            if (Math.Log(u2) < 0.5 * x * x + d * (1.0 - v + Math.Log(v))) return d * v * scale;
        }
    }

    /// <summary>
    /// Бета-распределение Beta(alpha, beta) через два Gamma-сэмпла.
    /// </summary>
    public static double NextBeta(Random rng, double alpha, double beta)
    {
        double x = NextGamma(rng, alpha);
        double y = NextGamma(rng, beta);
        return x / (x + y);
    }

    /// <summary>Распределение Коши Cauchy(location, scale).</summary>
    public static double NextCauchy(Random rng, double location = 0, double scale = 1)
        => location + scale * Math.Tan(Math.PI * (rng.NextDouble() - 0.5));

    /// <summary>Распределение Лапласа Laplace(mu, b).</summary>
    public static double NextLaplace(Random rng, double mu = 0, double b = 1)
    {
        double u = rng.NextDouble() - 0.5;
        return mu - b * Math.Sign(u) * Math.Log(1.0 - 2.0 * Math.Abs(u));
    }

    /// <summary>Распределение Вейбулла Weibull(shape, scale) через инверсию CDF.</summary>
    public static double NextWeibull(Random rng, double shape, double scale = 1)
    {
        double u;
        do { u = rng.NextDouble(); } while (u <= 0.0);
        return scale * Math.Pow(-Math.Log(u), 1.0 / shape);
    }

    /// <summary>Пуассон Poisson(lambda) — алгоритм Кнута для λ &lt; 30, отбраковка для больших.</summary>
    public static int NextPoisson(Random rng, double lambda)
    {
        if (lambda <= 0) throw new ArgumentOutOfRangeException(nameof(lambda));
        if (lambda < 30)
        {
            double L = Math.Exp(-lambda);
            int k = 0;
            double p = 1.0;
            do { k++; p *= rng.NextDouble(); } while (p > L);
            return k - 1;
        }
        return Math.Max(0, (int)Math.Round(lambda + Math.Sqrt(lambda) * NextGaussian(rng)));
    }

    /// <summary>Распределение Релея Rayleigh(σ) через инверсию CDF.</summary>
    public static double NextRayleigh(Random rng, double sigma = 1.0)
    {
        double u;
        do { u = rng.NextDouble(); } while (u <= 0.0);
        return sigma * Math.Sqrt(-2.0 * Math.Log(u));
    }

    /// <summary>
    /// Распределение Райса Rice(ν, σ): модуль вектора (ν + X, Y),
    /// где X, Y ~ N(0, σ²). При ν = 0 переходит в Rayleigh(σ).
    /// </summary>
    public static double NextRice(Random rng, double nu, double sigma = 1.0)
    {
        double x = nu + NextGaussian(rng) * sigma;
        double y = NextGaussian(rng) * sigma;
        return Math.Sqrt(x * x + y * y);
    }

    #endregion
}
