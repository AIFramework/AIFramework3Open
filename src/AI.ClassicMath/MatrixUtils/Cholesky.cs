using AI.DataStructs.Algebraic;
using System;

namespace AI.ClassicMath.MatrixUtils;

/// <summary>
/// Разложение Холецкого для симметричных положительно определённых матриц.
/// A = L·Lᵀ
/// </summary>
[Serializable]
public static class Cholesky
{
    /// <summary>
    /// Выполняет разложение Холецкого. Возвращает нижнетреугольную матрицу L,
    /// такую что A = L·Lᵀ.
    /// </summary>
    /// <param name="A">Симметричная положительно определённая матрица</param>
    /// <exception cref="InvalidOperationException">Если матрица не является положительно определённой</exception>
    public static Matrix Decompose(Matrix A)
    {
        int n = A.Height;
        if (n != A.Width)
            throw new ArgumentException("Матрица должна быть квадратной");

        Matrix L = new Matrix(n, n);

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = 0;

                if (j == i)
                {
                    for (int k = 0; k < j; k++)
                        sum += L[j, k] * L[j, k];

                    double diag = A[i, i] - sum;
                    if (diag <= 0)
                        throw new InvalidOperationException(
                            "Матрица не является положительно определённой");

                    L[i, j] = Math.Sqrt(diag);
                }
                else
                {
                    for (int k = 0; k < j; k++)
                        sum += L[i, k] * L[j, k];

                    L[i, j] = (A[i, j] - sum) / L[j, j];
                }
            }
        }

        return L;
    }

    /// <summary>
    /// Решает систему Ax = b через разложение Холецкого (прямая и обратная подстановки).
    /// </summary>
    public static Vector Solve(Matrix A, Vector b)
    {
        int n = A.Height;
        Matrix L = Decompose(A);

        Vector y = new Vector(n);
        for (int i = 0; i < n; i++)
        {
            double sum = b[i];
            for (int j = 0; j < i; j++) sum -= L[i, j] * y[j];
            y[i] = sum / L[i, i];
        }

        Vector x = new Vector(n);
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = y[i];
            for (int j = i + 1; j < n; j++) sum -= L[j, i] * x[j];
            x[i] = sum / L[i, i];
        }

        return x;
    }
}
