using AI.DataStructs.Algebraic;
using System;
using System.Threading.Tasks;

namespace AI.Statistics.MonteCarlo;

/// <summary>
/// Расчёт интеграла методом Монте-Карло. Многопоточная версия:
/// iter реплик считаются параллельно с независимыми RNG, а
/// результаты усредняются — даёт линейное ускорение по ядрам и
/// оценку дисперсии оценки через реплики.
/// </summary>
[Serializable]
public class Integration
{
    /// <summary>
    /// Взятие одномерного интеграла на [a; b].
    /// </summary>
    /// <param name="func">Подынтегральная функция</param>
    /// <param name="a">Нижний предел</param>
    /// <param name="b">Верхний предел</param>
    /// <param name="n">Число точек на реплику</param>
    /// <param name="iter">Число реплик</param>
    /// <param name="seed">Зерно (детерминируется полностью при задании)</param>
    public static double CalcIntegral1D(
        Func<double, double> func, double a, double b,
        int n = 50000, int iter = 20, int? seed = null)
    {
        if (iter <= 0) throw new ArgumentException("iter > 0", nameof(iter));

        double[] partial = new double[iter];

        Parallel.For(0, iter, i =>
        {
            // Каждая реплика — свой Random, с seed'ом сдвинутым на индекс.
            Random rng = seed.HasValue
                ? RandomEngine.Create(seed.Value + i)
                : RandomEngine.Create();
            partial[i] = Cl1D(func, a, b, n, rng);
        });

        double total = 0;
        for (int i = 0; i < iter; i++) total += partial[i];
        return total / iter;
    }

    /// <summary>
    /// Многомерный интеграл ∫...∫ f(x) dx на гиперпрямоугольнике [a_i; b_i].
    /// </summary>
    /// <param name="func">Подынтегральная функция (принимает вектор размерности dim)</param>
    /// <param name="lower">Нижние пределы по каждому измерению</param>
    /// <param name="upper">Верхние пределы по каждому измерению</param>
    /// <param name="n">Число точек на реплику</param>
    /// <param name="iter">Число реплик</param>
    /// <param name="seed">Зерно</param>
    public static double CalcIntegralND(
        Func<Vector, double> func, Vector lower, Vector upper,
        int n = 50000, int iter = 10, int? seed = null)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));
        if (lower.Count != upper.Count)
            throw new ArgumentException("lower и upper должны быть одной длины");
        if (iter <= 0) throw new ArgumentException("iter > 0", nameof(iter));

        int dim = lower.Count;
        double volume = 1.0;
        for (int d = 0; d < dim; d++) volume *= (upper[d] - lower[d]);

        double[] partial = new double[iter];

        Parallel.For(0, iter, i =>
        {
            Random rng = seed.HasValue
                ? RandomEngine.Create(seed.Value + i)
                : RandomEngine.Create();

            double sum = 0.0;
            Vector pt = new Vector(dim);
            for (int j = 0; j < n; j++)
            {
                for (int d = 0; d < dim; d++)
                    pt[d] = lower[d] + rng.NextDouble() * (upper[d] - lower[d]);
                sum += func(pt);
            }
            partial[i] = volume * sum / n;
        });

        double total = 0;
        for (int i = 0; i < iter; i++) total += partial[i];
        return total / iter;
    }

    // Одна репликация Монте-Карло.
    private static double Cl1D(Func<double, double> func, double a, double b, int n, Random random)
    {
        Vector samples = ((b - a) * Statistic.UniformDistribution(n, random)) + a;
        samples = samples.Transform(func);
        return (b - a) * samples.Mean();
    }
}
