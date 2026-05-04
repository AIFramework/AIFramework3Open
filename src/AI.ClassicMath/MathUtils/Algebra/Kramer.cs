using AI.DataStructs.Algebraic;
using System;
using System.Threading.Tasks;

namespace AI.MathUtils.Algebra;

/// <summary>
/// Метод Крамера
/// </summary>
[Serializable]
public class Kramer
{
    /// <summary>
    /// Решение СЛАУ методом Крамера
    /// </summary>
    /// <param name="A">Матрица коэффициентов</param>
    /// <param name="B">Вектор свободных членов</param>
    public Vector SolvingEquations(Matrix A, Vector B)
    {
        if (!A.IsSquared)
            throw new InvalidOperationException("Матрица коэффициентов должна быть квадратной");

        if (A.Height != B.Count)
            throw new InvalidOperationException("Число строк матрицы A должно совпадать с длиной вектора B");

        double detA = A.Determinant;

        const double EpsilonSingular = 1e-12;
        if (Math.Abs(detA) < EpsilonSingular)
            throw new InvalidOperationException(
                $"Матрица вырожденная или плохо обусловленная (det = {detA}). Метод Крамера неприменим.");

        Vector x = new Vector(B.Count);

        _ = Parallel.For(0, B.Count, i =>
        {
            Matrix newA = A.Copy();
            for (int r = 0; r < B.Count; r++)
                newA[r, i] = B[r];
            x[i] = newA.Determinant / detA;
        });

        return x;
    }
}
