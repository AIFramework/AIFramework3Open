using System;
using System.Collections.Generic;

namespace AI.Fuzzy.Inference;

/// <summary>
/// Монотонность функции принадлежности следствия для обратного отображения μ(z) -> z.
/// </summary>
public enum TsukamotoOutputMonotonicity
{
    /// <summary>μ(z) не убывает по z на [zMin, zMax].</summary>
    Increasing,
    /// <summary>μ(z) не возрастает по z на [zMin, zMax].</summary>
    Decreasing
}

/// <summary>
/// Вывод по Цукамото: для каждого правила находится чёткое z_i = μ_Ci⁻¹(α_i), где α_i — степень срабатывания;
/// требуется, чтобы μ_Ci была монотонной на выбранном интервале (обратная однозначна).
/// Итог: z = Σ α_i z_i / Σ α_i (взвешенное среднее по степеням срабатывания).
/// </summary>
public static class FuzzyTsukamotoInference
{
    /// <summary>
    /// Находит z ∈ [zMin, zMax], для которого μ(z) ≈ alpha (бисекция).
    /// Для немонотонных μ результат не определён — используйте только монотонные следствия.
    /// </summary>
    /// <param name="membership">Функция принадлежности μ(z) на [zMin, zMax].</param>
    /// <param name="alpha">Целевой уровень принадлежности [0,1].</param>
    /// <param name="zMin">Левая граница поиска.</param>
    /// <param name="zMax">Правая граница поиска.</param>
    /// <param name="monotonicity">Монотонность μ на отрезке.</param>
    /// <param name="bisectionIterations">Число итераций бисекции.</param>
    public static double InverseMonotoneMembership(
        Func<double, double> membership,
        double alpha,
        double zMin,
        double zMax,
        TsukamotoOutputMonotonicity monotonicity,
        int bisectionIterations = 48)
    {
        if (zMin > zMax)
            throw new ArgumentException("zMin не может быть больше zMax.");
        if (membership == null)
            throw new ArgumentNullException(nameof(membership));

        double a = Clamp01(alpha);

        double muLo = membership(zMin);
        double muHi = membership(zMax);

        if (monotonicity == TsukamotoOutputMonotonicity.Increasing)
        {
            if (a <= muLo)
                return zMin;
            if (a >= muHi)
                return zMax;
        }
        else
        {
            if (a >= muLo)
                return zMin;
            if (a <= muHi)
                return zMax;
        }

        double lo = zMin;
        double hi = zMax;
        for (int it = 0; it < bisectionIterations; it++)
        {
            double mid = 0.5 * (lo + hi);
            double muMid = membership(mid);

            if (monotonicity == TsukamotoOutputMonotonicity.Increasing)
            {
                if (muMid < a)
                    lo = mid;
                else
                    hi = mid;
            }
            else
            {
                if (muMid > a)
                    lo = mid;
                else
                    hi = mid;
            }
        }

        return 0.5 * (lo + hi);
    }

    /// <summary>
    /// Полный вывод Цукамото: для правила i берётся α_i, z_i = μ_Ci⁻¹(α_i), затем взвешенное среднее.
    /// </summary>
    /// <param name="ruleWeights">Степени срабатывания правил α_i (обычно [0,1]).</param>
    /// <param name="consequentMemberships">Монотонные функции принадлежности следствий на [zMin, zMax].</param>
    /// <param name="zMin">Левая граница универсума выхода.</param>
    /// <param name="zMax">Правая граница универсума выхода.</param>
    /// <param name="monotonicity">Рост или спад μ(z) на отрезке [zMin, zMax].</param>
    public static double Infer(
        IReadOnlyList<double> ruleWeights,
        IReadOnlyList<Func<double, double>> consequentMemberships,
        double zMin,
        double zMax,
        TsukamotoOutputMonotonicity monotonicity)
    {
        if (ruleWeights == null || consequentMemberships == null || ruleWeights.Count == 0)
            throw new ArgumentException("Пустые правила.");
        if (ruleWeights.Count != consequentMemberships.Count)
            throw new ArgumentException("Число весов и следствий должно совпадать.");

        var crisp = new List<double>(ruleWeights.Count);
        for (int i = 0; i < ruleWeights.Count; i++)
        {
            double alpha = Math.Max(0, ruleWeights[i]);
            crisp.Add(InverseMonotoneMembership(consequentMemberships[i], alpha, zMin, zMax, monotonicity));
        }

        return FuzzySugenoInference.WeightedAverageSingletons(ruleWeights, crisp);
    }

    private static double Clamp01(double x)
    {
        if (x < 0)
            return 0;
        if (x > 1)
            return 1;
        return x;
    }
}
