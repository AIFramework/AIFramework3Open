using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.Fuzzy.Inference;

/// <summary>
/// Вывод по Сугено (Такаги–Сугено): следствие — крисп-значение или линейная форма от входов; агрегирование взвешенным средним.
/// </summary>
public static class FuzzySugenoInference
{
    /// <summary>
    /// Нулевой порядок (синглтоны): z = Σ w_i c_i / Σ w_i.
    /// </summary>
    public static double WeightedAverageSingletons(IReadOnlyList<double> ruleWeights, IReadOnlyList<double> consequentSingletons)
    {
        if (ruleWeights == null || consequentSingletons == null || ruleWeights.Count != consequentSingletons.Count)
            throw new ArgumentException("Списки весов и следствий должны быть одинаковой ненулевой длины.");

        double num = 0, den = 0;
        for (int i = 0; i < ruleWeights.Count; i++)
        {
            double w = Math.Max(0, ruleWeights[i]);
            num += w * consequentSingletons[i];
            den += w;
        }

        return den < AI.AISettings.GlobalEps ? 0 : num / den;
    }

    /// <summary>
    /// Первый порядок: z_i = constant + linearCoeffs · inputs; результат z = Σ w_i z_i / Σ w_i.
    /// </summary>
    public static double TakagiSugenoOrder1(Vector inputs, IReadOnlyList<(double weight, Vector linearCoeffs, double constant)> rules)
    {
        if (inputs == null || rules == null || rules.Count == 0)
            throw new ArgumentException("Пустые входы или правила.");

        double num = 0, den = 0;
        foreach (var rule in rules)
        {
            double w = Math.Max(0, rule.weight);
            if (rule.linearCoeffs != null && rule.linearCoeffs.Count != inputs.Count)
                throw new ArgumentException("Размерность linearCoeffs должна совпадать с числом входов.");

            double zi = rule.constant;
            if (rule.linearCoeffs != null)
                zi += inputs.Dot(rule.linearCoeffs);

            num += w * zi;
            den += w;
        }

        return den < AI.AISettings.GlobalEps ? 0 : num / den;
    }

    /// <summary>
    /// Матрица импликации в смысле произведения для формирования весов правил (как скалярное «и» условий).
    /// Здесь [i,j] = if[i] * then[j] — вспомогательно для схем с раздельными векторами условий/следствий.
    /// </summary>
    public static Matrix GetProductImplicationMatrix(Vector ifVector, Vector thenVector)
    {
        Matrix m = new Matrix(ifVector.Count, thenVector.Count);
        for (int i = 0; i < ifVector.Count; i++)
        for (int j = 0; j < thenVector.Count; j++)
            m[i, j] = ifVector[i] * thenVector[j];
        return m;
    }
}
