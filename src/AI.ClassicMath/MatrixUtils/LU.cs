using AI.DataStructs.Algebraic;
using System;

namespace AI.ClassicMath.MatrixUtils;

/// <summary>
/// LU-разложение с частичным выбором главного элемента.
/// A = P·L·U
/// </summary>
[Serializable]
public static class LU
{
    /// <summary>
    /// Выполняет LU-разложение матрицы A с частичным выбором главного элемента.
    /// Возвращает нижнетреугольную матрицу L (с единицами на диагонали),
    /// верхнетреугольную матрицу U и массив перестановок строк.
    /// </summary>
    /// <param name="A">Квадратная матрица</param>
    public static (Matrix L, Matrix U, int[] perm) Decompose(Matrix A)
    {
        int n = A.Height;
        if (n != A.Width)
            throw new ArgumentException("Матрица должна быть квадратной");

        Matrix work = A.Copy();
        int[] perm = new int[n];
        for (int i = 0; i < n; i++) perm[i] = i;

        Matrix L = new Matrix(n, n);

        for (int k = 0; k < n; k++)
        {
            double maxVal = Math.Abs(work[k, k]);
            int pivotRow = k;
            for (int i = k + 1; i < n; i++)
            {
                double val = Math.Abs(work[i, k]);
                if (val > maxVal)
                {
                    maxVal = val;
                    pivotRow = i;
                }
            }

            if (pivotRow != k)
            {
                work.Swap(k, pivotRow, 1);
                L.Swap(k, pivotRow, 1);
                (perm[k], perm[pivotRow]) = (perm[pivotRow], perm[k]);
            }

            double pivot = work[k, k];
            if (Math.Abs(pivot) < 1e-15)
                throw new InvalidOperationException("Матрица вырождена, LU-разложение невозможно");

            for (int i = k + 1; i < n; i++)
            {
                double mult = work[i, k] / pivot;
                L[i, k] = mult;
                for (int j = k; j < n; j++)
                    work[i, j] -= mult * work[k, j];
            }
        }

        for (int i = 0; i < n; i++) L[i, i] = 1.0;

        return (L, work, perm);
    }

    /// <summary>
    /// Решает систему линейных уравнений Ax = b методом LU-разложения.
    /// </summary>
    public static Vector Solve(Matrix A, Vector b)
    {
        int n = A.Height;
        var (L, U, perm) = Decompose(A);

        Vector pb = new Vector(n);
        for (int i = 0; i < n; i++) pb[i] = b[perm[i]];

        Vector y = new Vector(n);
        for (int i = 0; i < n; i++)
        {
            double sum = pb[i];
            for (int j = 0; j < i; j++) sum -= L[i, j] * y[j];
            y[i] = sum;
        }

        Vector x = new Vector(n);
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = y[i];
            for (int j = i + 1; j < n; j++) sum -= U[i, j] * x[j];
            x[i] = sum / U[i, i];
        }

        return x;
    }

    /// <summary>
    /// Вычисляет определитель матрицы через LU-разложение.
    /// </summary>
    public static double Determinant(Matrix A)
    {
        int n = A.Height;
        var (_, U, perm) = Decompose(A);

        int swaps = 0;
        for (int i = 0; i < n; i++)
            if (perm[i] != i)
            {
                int target = Array.IndexOf(perm, i, i);
                if (target != i)
                {
                    (perm[i], perm[target]) = (perm[target], perm[i]);
                    swaps++;
                }
            }

        double det = (swaps % 2 == 0) ? 1.0 : -1.0;
        for (int i = 0; i < n; i++) det *= U[i, i];
        return det;
    }
}
