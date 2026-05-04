using AI.DataStructs.Algebraic;
using System;

namespace AI.MathUtils.Algebra;

/// <summary>
/// Метод Гаусса с выч. сложностью O(n^3)
/// </summary>
[Serializable]
public static class Gauss
{
    /// <summary>
    /// Решение СЛАУ методом Гаусса
    /// </summary>
    /// <param name="A">Матрица коэффициентов</param>
    /// <param name="B">Вектор свободных членов</param>
    public static Vector SolvingEquations(Matrix A, Vector B)
    {
        // После выбора главного элемента по столбцу: если макс. модуль в столбце ниже порога — система вырождена
        const double EpsilonSingular = 1e-12;
        int Count = B.Count;
        Vector x = new Vector(Count);
        double coef;

        // Прямой ход с частичным выбором главного элемента по столбцу (partial pivoting на каждом шаге)
        for (int index = 0; index < Count; index++)
        {
            int pivotRow = index;
            double maxAbs = Math.Abs(A[index, index]);
            for (int i = index + 1; i < Count; i++)
            {
                double a = Math.Abs(A[i, index]);
                if (a > maxAbs)
                {
                    maxAbs = a;
                    pivotRow = i;
                }
            }

            if (maxAbs < EpsilonSingular)
            {
                throw new InvalidOperationException(
                    $"Матрица вырожденная или плохо обусловленная. " +
                    $"Максимальный модуль в столбце {index} (строки {index}…{Count - 1}) = {maxAbs} близок к нулю.");
            }

            if (pivotRow != index)
            {
                for (int j = 0; j < Count; j++)
                {
                    double temp = A[index, j];
                    A[index, j] = A[pivotRow, j];
                    A[pivotRow, j] = temp;
                }

                double tempB = B[index];
                B[index] = B[pivotRow];
                B[pivotRow] = tempB;
            }

            coef = 1.0 / A[index, index];
            A[index, index] = 1.0;

            for (int j = index + 1; j < Count; j++)
            {
                A[index, j] *= coef;
            }

            B[index] *= coef;

            for (int k = index + 1; k < Count; k++)
            {
                coef = A[k, index];
                A[k, index] = 0;
                for (int j = index + 1; j < Count; j++)
                {
                    A[k, j] = A[k, j] - (A[index, j] * coef);
                }

                B[k] = B[k] - (B[index] * coef);
            }
        }

        // Обратный ход
        for (int index = Count - 1; index >= 0; index--)
        {
            coef = B[index];
            for (int j = index + 1; j < Count; j++)
            {
                coef -= A[index, j] * x[j];
            }

            x[index] = coef;
        }

        return x;
    }

}
