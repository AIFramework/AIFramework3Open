using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.Fuzzy.Inference;

/// <summary>
/// Вывод по Мамдани: агрегирование заключений как нечётких множеств с последующей дефаззификацией.
/// Агрегирование по правилам: μ(u) = max_i min(w_i, μ_i(u)).
/// </summary>
public static class FuzzyMamdaniInference
{
    /// <summary>
    /// Строит матрицу импликации Мамдани (минимум) между двумя дискретными нечёткими векторами условия и следствия.
    /// Элемент [i,j] = min(if[i], then[j]).
    /// </summary>
    public static Matrix GetImplicationMatrixMamdani(Vector ifVector, Vector thenVector)
    {
        Matrix m = new Matrix(ifVector.Count, thenVector.Count);
        for (int i = 0; i < ifVector.Count; i++)
        for (int j = 0; j < thenVector.Count; j++)
            m[i, j] = FLV.MamdaniImplication(ifVector[i], thenVector[j]);
        return m;
    }

    /// <summary>
    /// Агрегирование выходных термов на общей дискретной сетке: μ_agg[k] = max_i min(w_i, μ_rule[i][k]).
    /// </summary>
    /// <param name="ruleWeights">Степень срабатывания каждого правила [0,1].</param>
    /// <param name="outputTermSamples">Для каждого правила — дискретные отсчёты функции принадлежности следствия (одинаковая длина).</param>
    public static Vector AggregateMaxMin(IReadOnlyList<double> ruleWeights, IReadOnlyList<Vector> outputTermSamples)
    {
        if (ruleWeights == null || outputTermSamples == null || ruleWeights.Count == 0)
            throw new ArgumentException("Пустой список правил.");
        if (ruleWeights.Count != outputTermSamples.Count)
            throw new ArgumentException("Число весов и термов должно совпадать.");

        int n = outputTermSamples[0].Count;
        Vector agg = new Vector(n);
        for (int k = 0; k < n; k++)
        {
            double mx = 0;
            for (int r = 0; r < ruleWeights.Count; r++)
            {
                double v = Math.Min(ruleWeights[r], outputTermSamples[r][k]);
                if (v > mx) mx = v;
            }
            agg[k] = mx;
        }

        return agg;
    }

    /// <summary>
    /// Дефаззификация методом центра тяжести на дискретной сетке универсума.
    /// </summary>
    /// <param name="universe">Значения выходной переменной u на сетке.</param>
    /// <param name="membership">Значения μ(u) на той же сетке.</param>
    public static double DefuzzifyCentroid(Vector universe, Vector membership)
    {
        if (universe.Count != membership.Count || universe.Count == 0)
            throw new ArgumentException("Размеры universe и membership должны совпадать и быть ненулевыми.");

        double sMu = membership.Sum();
        if (sMu < AI.AISettings.GlobalEps)
            return 0;

        return (universe * membership).Sum() / sMu;
    }

    /// <summary>
    /// Полный шаг Мамдани: агрегирование max-min и центр тяжести.
    /// </summary>
    public static double InferCentroid(IReadOnlyList<double> ruleWeights, IReadOnlyList<Vector> outputTermSamples, Vector universe)
    {
        Vector agg = AggregateMaxMin(ruleWeights, outputTermSamples);
        return DefuzzifyCentroid(universe, agg);
    }
}
