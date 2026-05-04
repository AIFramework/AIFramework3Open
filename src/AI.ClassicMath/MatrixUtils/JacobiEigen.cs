using AI.DataStructs.Algebraic;
using System;

namespace AI.ClassicMath.MatrixUtils;

/// <summary>
/// Метод Якоби для собственных значений и векторов симметричной матрицы.
/// Классический циклический алгоритм вращений.
/// </summary>
[Serializable]
public static class JacobiEigen
{
    /// <summary>
    /// Собственные значения и векторы с возвратом значений как Vector (рекомендуемый API).
    /// </summary>
    /// <param name="A">Симметричная квадратная матрица</param>
    /// <param name="maxIter">Максимальное число проходов</param>
    /// <param name="eps">Порог сходимости</param>
    public static (Vector eigenvalues, Matrix eigenvectors) ComputeVector(
        Matrix A, int maxIter = 200, double eps = 1e-12)
    {
        var (vals, vecs) = Compute(A, maxIter, eps);
        return (new Vector(vals), vecs);
    }

    /// <summary>
    /// Вычисляет собственные значения и собственные векторы симметричной матрицы.
    /// </summary>
    /// <param name="A">Симметричная квадратная матрица</param>
    /// <param name="maxIter">Максимальное число проходов</param>
    /// <param name="eps">Порог сходимости</param>
    public static (double[] eigenvalues, Matrix eigenvectors) Compute(
        Matrix A, int maxIter = 200, double eps = 1e-12)
    {
        int n = A.Height;
        if (n != A.Width)
            throw new ArgumentException("Матрица должна быть квадратной");

        Matrix D = A.Copy();

        Matrix V = new Matrix(n, n);
        for (int i = 0; i < n; i++) V[i, i] = 1.0;

        for (int iter = 0; iter < maxIter; iter++)
        {
            int p = 0, q = 1;
            double maxOff = Math.Abs(D[0, 1]);

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double val = Math.Abs(D[i, j]);
                    if (val > maxOff)
                    {
                        maxOff = val;
                        p = i;
                        q = j;
                    }
                }
            }

            if (maxOff < eps) break;

            double app = D[p, p];
            double aqq = D[q, q];
            double apq = D[p, q];

            double theta;
            if (Math.Abs(app - aqq) < 1e-15)
                theta = Math.PI / 4.0;
            else
                theta = 0.5 * Math.Atan2(2.0 * apq, app - aqq);

            double c = Math.Cos(theta);
            double s = Math.Sin(theta);

            for (int i = 0; i < n; i++)
            {
                double dip = D[i, p];
                double diq = D[i, q];
                D[i, p] = c * dip + s * diq;
                D[i, q] = -s * dip + c * diq;
            }

            for (int j = 0; j < n; j++)
            {
                double dpj = D[p, j];
                double dqj = D[q, j];
                D[p, j] = c * dpj + s * dqj;
                D[q, j] = -s * dpj + c * dqj;
            }

            for (int i = 0; i < n; i++)
            {
                double vip = V[i, p];
                double viq = V[i, q];
                V[i, p] = c * vip + s * viq;
                V[i, q] = -s * vip + c * viq;
            }
        }

        double[] eigenvalues = new double[n];
        for (int i = 0; i < n; i++) eigenvalues[i] = D[i, i];

        return (eigenvalues, V);
    }
}
