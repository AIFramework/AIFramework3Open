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
    // Кэш привязан к конкретному экземпляру Random (_spareOwner):
    // иначе seed-ный генератор мог бы получить значение из потока
    // ЧУЖОГО генератора, ломая документированную детерминированность.
    [ThreadStatic]
    private static double _sparedGaussian;
    [ThreadStatic]
    private static bool _hasSpared;
    [ThreadStatic]
    private static Random _spareOwner;

    /// <summary>
    /// Стандартная нормальная величина N(0, 1). Полярный Box-Muller
    /// с кэшированием второй компоненты.
    /// </summary>
    public static double NextGaussian(Random rng)
    {
        // Используем кэш только если он был создан именно этим RNG —
        // это сохраняет детерминированность seed-ных генераторов.
        if (_hasSpared && ReferenceEquals(_spareOwner, rng))
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
        _spareOwner = rng;
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
        // Отбрасываем 0, чтобы u лежало строго внутри (-0.5; 0.5):
        // на границе Log(0) дал бы -Infinity.
        double u;
        do { u = rng.NextDouble(); } while (u <= 0.0);
        u -= 0.5;
        return mu - b * Math.Sign(u) * Math.Log(1.0 - 2.0 * Math.Abs(u));
    }

    /// <summary>Распределение Вейбулла Weibull(shape, scale) через инверсию CDF.</summary>
    public static double NextWeibull(Random rng, double shape, double scale = 1)
    {
        double u;
        do { u = rng.NextDouble(); } while (u <= 0.0);
        return scale * Math.Pow(-Math.Log(u), 1.0 / shape);
    }

    /// <summary>
    /// Пуассон Poisson(lambda): алгоритм Кнута (перемножение равномерных
    /// до порога e^{-λ}) для λ &lt; 30, метод отбраковки Аткинсона
    /// (PA, 1979, логистическая огибающая) для λ ≥ 30 — корректные
    /// хвостовые вероятности, в отличие от нормальной аппроксимации.
    /// </summary>
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

        // Метод Аткинсона (PA): отбраковка с логистической огибающей.
        double c = 0.767 - 3.36 / lambda;
        double beta = Math.PI / Math.Sqrt(3.0 * lambda);
        double alpha = beta * lambda;
        double kConst = Math.Log(c) - lambda - Math.Log(beta);
        while (true)
        {
            double u = rng.NextDouble();
            if (u <= 0.0 || u >= 1.0) continue;
            double x = (alpha - Math.Log((1.0 - u) / u)) / beta;
            int n = (int)Math.Floor(x + 0.5);
            if (n < 0) continue;
            double v = rng.NextDouble();
            if (v <= 0.0) continue;
            double y = alpha - beta * x;
            double t = 1.0 + Math.Exp(y);
            double lhs = y + Math.Log(v / (t * t));
            double rhs = kConst + n * Math.Log(lambda) - StatInference.LogGamma(n + 1.0);
            if (lhs <= rhs) return n;
        }
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

    #region Круговое и усечённое

    /// <summary>
    /// Распределение фон Мизеса VM(μ, κ) — «нормальное на окружности» для направлений —
    /// алгоритм Беста — Фишера (1979). Результат в радианах на [0; 2π).
    /// </summary>
    /// <remarks>
    /// κ = 0 — равномерное по окружности. При κ больше 1e5 распределение неотличимо от N(μ, 1/κ),
    /// а формулы алгоритма теряют точность, поэтому берётся нормальное.
    /// </remarks>
    /// <param name="rng">Генератор</param>
    /// <param name="mu">Среднее направление, радианы</param>
    /// <param name="kappa">Концентрация κ ≥ 0: чем больше, тем уже разброс</param>
    public static double NextVonMises(Random rng, double mu, double kappa)
    {
        if (!(kappa >= 0) || double.IsInfinity(kappa))
            throw new ArgumentOutOfRangeException(nameof(kappa), "Концентрация — конечное неотрицательное число");

        if (kappa < 1e-8)
            return CircularStatistics.WrapRadians(2.0 * Math.PI * rng.NextDouble());

        if (kappa > 1e5)
            return CircularStatistics.WrapRadians(mu + (NextGaussian(rng) / Math.Sqrt(kappa)));

        double tau = 1.0 + Math.Sqrt(1.0 + (4.0 * kappa * kappa));
        double rho = (tau - Math.Sqrt(2.0 * tau)) / (2.0 * kappa);
        double r = (1.0 + (rho * rho)) / (2.0 * rho);

        while (true)
        {
            double z = Math.Cos(Math.PI * rng.NextDouble());
            double f = (1.0 + (r * z)) / (r + z);
            double c = kappa * (r - f);
            double u = rng.NextDouble();

            if ((c * (2.0 - c)) - u > 0 || (u > 0 && Math.Log(c / u) + 1.0 - c >= 0))
            {
                double angle = Math.Acos(Math.Clamp(f, -1.0, 1.0));
                return CircularStatistics.WrapRadians(rng.NextDouble() < 0.5 ? mu - angle : mu + angle);
            }
        }
    }

    /// <summary>
    /// Нормальное N(mean, std²), усечённое до [lower; upper], — точная выборка отбраковкой по Роберту (1995).
    /// </summary>
    /// <remarks>
    /// Отбраковка из обычного нормального для интервала далеко в хвосте почти никогда не попадала бы в него;
    /// здесь огибающая выбирается по положению интервала — экспоненциальная, равномерная или нормальная, —
    /// и доля принятых остаётся высокой при любом положении.
    /// </remarks>
    /// <param name="rng">Генератор</param>
    /// <param name="mean">Среднее исходного нормального</param>
    /// <param name="std">Стандартное отклонение исходного нормального</param>
    /// <param name="lower">Нижняя граница; допускается −∞</param>
    /// <param name="upper">Верхняя граница; допускается +∞</param>
    public static double NextTruncatedGaussian(Random rng, double mean, double std, double lower, double upper)
    {
        if (!(std > 0) || double.IsInfinity(std))
            throw new ArgumentOutOfRangeException(nameof(std), "Отклонение — конечное положительное число");

        if (!(lower < upper))
            throw new ArgumentException("Нижняя граница должна быть меньше верхней", nameof(lower));

        return mean + (std * StandardTruncated(rng, (lower - mean) / std, (upper - mean) / std));
    }

    /// <summary>Стандартное нормальное, усечённое до [a; b]</summary>
    private static double StandardTruncated(Random rng, double a, double b)
    {
        if (a >= 0)
            return OneSidedTruncated(rng, a, b);

        if (b <= 0)
            return -OneSidedTruncated(rng, -b, -a);

        // Интервал накрывает ноль: узкий — равномерная огибающая, широкий — обычное нормальное
        if (b - a < Math.Sqrt(2.0 * Math.PI))
        {
            while (true)
            {
                double z = a + ((b - a) * rng.NextDouble());
                if (rng.NextDouble() <= Math.Exp(-0.5 * z * z))
                    return z;
            }
        }

        while (true)
        {
            double z = NextGaussian(rng);
            if (z >= a && z <= b)
                return z;
        }
    }

    /// <summary>
    /// Правый хвост [a; b], a ≥ 0: экспоненциальная огибающая с оптимальным показателем λ,
    /// а для интервала уже масштаба этой экспоненты — равномерная
    /// </summary>
    private static double OneSidedTruncated(Random rng, double a, double b)
    {
        double lambda = 0.5 * (a + Math.Sqrt((a * a) + 4.0));

        if (b - a < 1.0 / lambda)
        {
            while (true)
            {
                double z = a + ((b - a) * rng.NextDouble());
                if (rng.NextDouble() <= Math.Exp(0.5 * ((a * a) - (z * z))))
                    return z;
            }
        }

        while (true)
        {
            double z = a + NextExponential(rng, lambda);
            if (z <= b && rng.NextDouble() <= Math.Exp(-0.5 * (z - lambda) * (z - lambda)))
                return z;
        }
    }

    #endregion
}
