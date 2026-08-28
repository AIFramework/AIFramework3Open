using AI.DataStructs.Algebraic;
using System;

namespace AI.ClassicMath.MatrixUtils;

/// <summary>
/// Сингулярное разложение (one-sided Jacobi SVD).
/// A = U·Σ·Vᵀ
/// </summary>
[Serializable]
public static class Svd
{
    /// <summary>
    /// SVD с возвратом сингулярных значений как Vector (рекомендуемый API).
    /// </summary>
    /// <param name="A">Матрица произвольного размера (m×n)</param>
    /// <param name="maxSweeps">Максимальное число проходов</param>
    /// <param name="eps">Порог сходимости</param>
    public static (Matrix U, Vector Sigma, Matrix V) DecomposeVector(
        Matrix A, int maxSweeps = 200, double eps = 1e-12)
    {
        var (u, s, v) = Decompose(A, maxSweeps, eps);
        return (u, new Vector(s), v);
    }

    /// <summary>
    /// Выполняет сингулярное разложение матрицы A методом одностороннего вращения Якоби.
    /// Возвращает матрицу U, массив сингулярных значений sigma и матрицу V.
    /// </summary>
    /// <param name="A">Матрица произвольного размера (m×n)</param>
    /// <param name="maxSweeps">Максимальное число проходов</param>
    /// <param name="eps">Порог сходимости</param>
    public static (Matrix U, double[] sigma, Matrix V) Decompose(
        Matrix A, int maxSweeps = 200, double eps = 1e-12)
    {
        int m = A.Height;
        int n = A.Width;

        Matrix B = A.Copy();

        Matrix V = new Matrix(n, n);
        for (int i = 0; i < n; i++) V[i, i] = 1.0;

        for (int sweep = 0; sweep < maxSweeps; sweep++)
        {
            double offNorm = 0;

            for (int p = 0; p < n - 1; p++)
            {
                for (int q = p + 1; q < n; q++)
                {
                    double alpha = 0, beta = 0, gamma = 0;
                    for (int i = 0; i < m; i++)
                    {
                        alpha += B[i, p] * B[i, p];
                        beta += B[i, q] * B[i, q];
                        gamma += B[i, p] * B[i, q];
                    }

                    offNorm += gamma * gamma;

                    if (Math.Abs(gamma) < eps * Math.Sqrt(alpha * beta))
                        continue;

                    double zeta = (beta - alpha) / (2.0 * gamma);

                    // При равных нормах столбцов zeta = 0 и нужен поворот на 45 градусов;
                    // Math.Sign(0) дал бы t = 0, то есть вращение молча пропускалось бы
                    double sign = zeta >= 0 ? 1.0 : -1.0;
                    double t = sign / (Math.Abs(zeta) + Math.Sqrt(1.0 + zeta * zeta));
                    double c = 1.0 / Math.Sqrt(1.0 + t * t);
                    double s = c * t;

                    for (int i = 0; i < m; i++)
                    {
                        double bp = B[i, p];
                        double bq = B[i, q];
                        B[i, p] = c * bp - s * bq;
                        B[i, q] = s * bp + c * bq;
                    }

                    for (int i = 0; i < n; i++)
                    {
                        double vp = V[i, p];
                        double vq = V[i, q];
                        V[i, p] = c * vp - s * vq;
                        V[i, q] = s * vp + c * vq;
                    }
                }
            }

            if (offNorm < eps * eps) break;
        }

        double[] sigma = new double[n];
        Matrix U = new Matrix(m, n);

        for (int j = 0; j < n; j++)
        {
            double norm = 0;
            for (int i = 0; i < m; i++) norm += B[i, j] * B[i, j];
            sigma[j] = Math.Sqrt(norm);

            if (sigma[j] > eps)
            {
                double inv = 1.0 / sigma[j];
                for (int i = 0; i < m; i++) U[i, j] = B[i, j] * inv;
            }
        }

        return (U, sigma, V);
    }
}
