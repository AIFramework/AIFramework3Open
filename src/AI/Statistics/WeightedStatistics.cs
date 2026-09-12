#nullable enable
using AI.DataStructs.Algebraic;
using System;

namespace AI.Statistics;

/// <summary>
/// Статистики взвешенной выборки: облака частиц, выборки по значимости, наблюдения разной надёжности.
/// Веса неотрицательны и не обязаны быть нормированы — всё делится на их сумму.
/// </summary>
public static class WeightedStatistics
{
    /// <summary>Взвешенное среднее Σw·x / Σw</summary>
    /// <param name="values">Значения</param>
    /// <param name="weights">Неотрицательные веса</param>
    public static double Mean(Vector values, Vector weights)
    {
        double total = TotalWeight(values, weights);
        double sum = 0;

        for (int i = 0; i < values.Count; i++)
            sum += weights[i] * values[i];

        return sum / total;
    }

    /// <summary>
    /// Взвешенная дисперсия Σw·(x − m)² / Σw — дисперсия самого взвешенного распределения (облака),
    /// а не несмещённая оценка по выборке
    /// </summary>
    /// <param name="values">Значения</param>
    /// <param name="weights">Неотрицательные веса</param>
    public static double Variance(Vector values, Vector weights)
    {
        double total = TotalWeight(values, weights);
        double mean = Mean(values, weights);
        double sum = 0;

        for (int i = 0; i < values.Count; i++)
        {
            double deviation = values[i] - mean;
            sum += weights[i] * deviation * deviation;
        }

        return sum / total;
    }

    /// <summary>
    /// Взвешенный квантиль — обращение взвешенной функции распределения: наименьшее значение, до которого
    /// включительно накоплено не меньше доли <paramref name="q"/> всего веса. Значение с нулевым весом
    /// не возвращается никогда
    /// </summary>
    /// <param name="values">Значения</param>
    /// <param name="weights">Неотрицательные веса</param>
    /// <param name="q">Уровень на [0; 1]; 0.5 — медиана</param>
    public static double Quantile(Vector values, Vector weights, double q)
    {
        if (double.IsNaN(q) || q < 0 || q > 1)
            throw new ArgumentOutOfRangeException(nameof(q), "Уровень квантиля лежит на [0; 1]");

        double total = TotalWeight(values, weights);
        double[] sorted = values.ToArray();
        int[] order = new int[sorted.Length];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        Array.Sort(sorted, order);

        double threshold = q * total;
        double accumulated = 0;
        double last = double.NaN;

        for (int k = 0; k < order.Length; k++)
        {
            double weight = weights[order[k]];
            if (weight <= 0)
                continue;

            accumulated += weight;
            last = sorted[k];
            if (accumulated >= threshold)
                return last;
        }

        // Накопленная сумма может недобрать до q·Σw на ошибках округления — тогда это верхний край
        return last;
    }

    /// <summary>
    /// Эффективный размер выборки Киша (Σw)² / Σw²: сколько равновесных наблюдений стоит взвешенная выборка.
    /// Равные веса — число наблюдений, весь вес на одном — единица
    /// </summary>
    /// <param name="weights">Неотрицательные веса</param>
    public static double EffectiveSampleSize(Vector weights)
    {
        double total = TotalWeight(weights);
        double squares = 0;

        foreach (double weight in weights)
            squares += weight * weight;

        return total * total / squares;
    }

    /// <summary>Сумма весов после проверки: весов столько же, сколько значений, и они годятся в веса</summary>
    internal static double TotalWeight(Vector values, Vector weights)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(weights);
        if (values.Count != weights.Count)
            throw new ArgumentException($"Значений {values.Count}, а весов {weights.Count}", nameof(weights));

        return TotalWeight(weights);
    }

    /// <summary>Сумма весов: веса неотрицательны, конечны и не все нулевые</summary>
    private static double TotalWeight(Vector weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        if (weights.Count == 0)
            throw new ArgumentException("Выборка пуста", nameof(weights));

        double total = 0;
        foreach (double weight in weights)
        {
            if (!(weight >= 0) || double.IsInfinity(weight))
                throw new ArgumentException("Веса должны быть неотрицательными конечными числами", nameof(weights));
            total += weight;
        }

        if (!(total > 0))
            throw new ArgumentException("Сумма весов равна нулю", nameof(weights));

        return total;
    }
}
