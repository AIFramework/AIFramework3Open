using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.Fuzzy.Inference;

/// <summary>
/// Вывод по Ларсену: как у Мамдани, но импликация задаётся произведением: μ = w · μ_терм(u),
/// агрегирование правил обычно по максимуму: μ_agg(u) = max_i (w_i · μ_i(u)).
/// </summary>
public static class FuzzyLarsenInference
{
    /// <summary>
    /// Матрица импликации Ларсена: элемент [i,j] = if[i] · then[j].
    /// </summary>
    public static Matrix GetImplicationMatrixLarsen(Vector ifVector, Vector thenVector)
    {
        Matrix m = new Matrix(ifVector.Count, thenVector.Count);
        for (int i = 0; i < ifVector.Count; i++)
        for (int j = 0; j < thenVector.Count; j++)
            m[i, j] = FLV.LarsenImplication(ifVector[i], thenVector[j]);
        return m;
    }

    /// <summary>
    /// Агрегирование на сетке: μ_agg[k] = max_i (w_i · μ_rule[i][k]).
    /// </summary>
    public static Vector AggregateMaxProduct(IReadOnlyList<double> ruleWeights, IReadOnlyList<Vector> outputTermSamples)
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
                double v = ruleWeights[r] * outputTermSamples[r][k];
                if (v > mx)
                    mx = v;
            }

            agg[k] = mx;
        }

        return agg;
    }

    /// <summary>
    /// Полный шаг Ларсена: агрегирование max-product и центр тяжести (как у Мамдани после агрегирования).
    /// </summary>
    public static double InferCentroid(IReadOnlyList<double> ruleWeights, IReadOnlyList<Vector> outputTermSamples, Vector universe)
    {
        Vector agg = AggregateMaxProduct(ruleWeights, outputTermSamples);
        return FuzzyMamdaniInference.DefuzzifyCentroid(universe, agg);
    }
}
