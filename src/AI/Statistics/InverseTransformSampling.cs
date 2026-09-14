#nullable enable
using System;

namespace AI.Statistics;

/// <summary>
/// Выборка обращением функции распределения по заданному равномерному числу.
/// </summary>
/// <remarks>
/// В отличие от методов <see cref="RandomEngine"/>, здесь нет генератора: результат есть монотонная
/// функция равномерного числа u. Это нужно для детерминированной процедурной генерации (одно и то же u
/// дает одно и то же значение), для квазислучайных точек и для согласованных выборок, где близким u
/// должны соответствовать близкие значения.
/// </remarks>
public static class InverseTransformSampling
{
    /// <summary>Относительная величина слагаемого, после которой сумма вероятностей уже не меняется</summary>
    private const double Negligible = 1e-17;

    /// <summary>
    /// Квантиль распределения Пуассона: наименьшее k, при котором P(X ≤ k) больше u
    /// </summary>
    /// <remarks>
    /// Поиск начинается с моды ⌊λ⌋: вероятность в ней считается через логарифм гамма-функции,
    /// функция распределения в ней суммированием вниз до пренебрежимых слагаемых, а затем
    /// соседние значения перебираются по рекурсии p(k+1) = p(k)·λ/(k+1). Затраты порядка √λ,
    /// переполнения e^(−λ) при большом λ нет.
    /// </remarks>
    /// <param name="u">Равномерное число из [0; 1)</param>
    /// <param name="lambda">Среднее λ ≥ 0, не больше 10⁹</param>
    public static int Poisson(double u, double lambda)
    {
        if (!(u >= 0 && u < 1))
            throw new ArgumentOutOfRangeException(nameof(u), "Равномерное число должно лежать в [0; 1)");

        if (!(lambda >= 0 && lambda <= 1e9))
            throw new ArgumentOutOfRangeException(nameof(lambda), "Среднее должно лежать в [0; 10⁹]");

        if (lambda == 0)
            return 0;

        int mode = (int)Math.Floor(lambda);
        double modeProbability = Math.Exp((mode * Math.Log(lambda)) - lambda - StatInference.LogGamma(mode + 1.0));

        double cdf = 0;
        double p = modeProbability;

        for (int k = mode; k >= 0; k--)
        {
            cdf += p;

            if (p < cdf * Negligible)
                break;

            p *= k / lambda;
        }

        int result = mode;
        double probability = modeProbability;

        if (u < cdf)
        {
            // Вниз: пока P(X ≤ k − 1) > u
            while (result > 0)
            {
                double below = cdf - probability;

                if (below <= u)
                    break;

                cdf = below;
                probability *= result / lambda;
                result--;
            }

            return result;
        }

        // Вверх: пока P(X ≤ k) ≤ u
        while (cdf <= u)
        {
            result++;
            probability *= lambda / result;

            if (probability <= cdf * Negligible)
                break;

            cdf += probability;
        }

        return result;
    }
}
