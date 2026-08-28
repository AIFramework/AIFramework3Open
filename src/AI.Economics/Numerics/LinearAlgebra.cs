using System;

namespace AI.Economics.Numerics;

/// <summary>
/// Матричные операции, нужные эконометрике, риск-менеджменту и портфельной
/// оптимизации: произведения, разложения и собственные числа.
/// </summary>
internal static class LinearAlgebra
{
    /// <summary>Произведение матриц.</summary>
    public static double[,] Multiply(double[,] left, double[,] right)
    {
        int n = left.GetLength(0), k = left.GetLength(1), m = right.GetLength(1);
        var product = new double[n, m];

        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
            {
                double sum = 0;
                for (int t = 0; t < k; t++) sum += left[i, t] * right[t, j];
                product[i, j] = sum;
            }

        return product;
    }

    /// <summary>Произведение матрицы на вектор.</summary>
    public static double[] Multiply(double[,] matrix, double[] vector)
    {
        int n = matrix.GetLength(0), k = matrix.GetLength(1);
        var product = new double[n];

        for (int i = 0; i < n; i++)
        {
            double sum = 0;
            for (int j = 0; j < k; j++) sum += matrix[i, j] * vector[j];
            product[i] = sum;
        }

        return product;
    }

    /// <summary>Транспонирование.</summary>
    public static double[,] Transpose(double[,] matrix)
    {
        int n = matrix.GetLength(0), m = matrix.GetLength(1);
        var result = new double[m, n];

        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++) result[j, i] = matrix[i, j];

        return result;
    }

    /// <summary>Произведение <c>X' W X</c>, где <c>W</c> — диагональная матрица весов.</summary>
    public static double[,] WeightedGram(double[,] x, double[] weights)
    {
        int n = x.GetLength(0), k = x.GetLength(1);
        var gram = new double[k, k];

        for (int a = 0; a < k; a++)
            for (int b = a; b < k; b++)
            {
                double sum = 0;
                for (int i = 0; i < n; i++) sum += weights[i] * x[i, a] * x[i, b];
                gram[a, b] = sum;
                gram[b, a] = sum;
            }

        return gram;
    }

    /// <summary>Произведение <c>X' W y</c> с диагональными весами.</summary>
    public static double[] WeightedCross(double[,] x, double[] weights, double[] y)
    {
        int n = x.GetLength(0), k = x.GetLength(1);
        var cross = new double[k];

        for (int a = 0; a < k; a++)
        {
            double sum = 0;
            for (int i = 0; i < n; i++) sum += weights[i] * x[i, a] * y[i];
            cross[a] = sum;
        }

        return cross;
    }

    /// <summary>Единичная матрица.</summary>
    public static double[,] Identity(int size)
    {
        var identity = new double[size, size];
        for (int i = 0; i < size; i++) identity[i, i] = 1;
        return identity;
    }

    /// <summary>Копия матрицы.</summary>
    public static double[,] Copy(double[,] matrix) => (double[,])matrix.Clone();

    /// <summary>
    /// Разложение Холецкого положительно определённой матрицы.
    /// </summary>
    /// <remarks>
    /// К диагонали добавляется малая величина, если матрица оказалась вырожденной:
    /// выборочные ковариационные матрицы почти всегда слегка не положительно
    /// определены из-за ошибок округления, и падать на этом нельзя.
    /// </remarks>
    public static double[,] Cholesky(double[,] matrix, double ridge = 1e-10)
    {
        int n = matrix.GetLength(0);
        var lower = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = matrix[i, j];
                for (int k = 0; k < j; k++) sum -= lower[i, k] * lower[j, k];

                if (i == j)
                {
                    double diagonal = Math.Max(sum, ridge);
                    lower[i, j] = Math.Sqrt(diagonal);
                }
                else
                {
                    lower[i, j] = lower[j, j] > 0 ? sum / lower[j, j] : 0;
                }
            }
        }

        return lower;
    }

    /// <summary>
    /// Собственные числа и векторы симметричной матрицы методом Якоби.
    /// </summary>
    /// <param name="matrix">Симметричная матрица.</param>
    /// <param name="sweeps">Число проходов.</param>
    /// <returns>Собственные числа по убыванию и матрица векторов по столбцам.</returns>
    public static (double[] Values, double[,] Vectors) SymmetricEigen(double[,] matrix, int sweeps = 100)
    {
        int n = matrix.GetLength(0);
        var a = Copy(matrix);
        var v = Identity(n);

        for (int sweep = 0; sweep < sweeps; sweep++)
        {
            double off = 0;
            for (int p = 0; p < n; p++)
                for (int q = p + 1; q < n; q++) off += a[p, q] * a[p, q];

            if (off < 1e-20) break;

            for (int p = 0; p < n - 1; p++)
            {
                for (int q = p + 1; q < n; q++)
                {
                    if (Math.Abs(a[p, q]) < 1e-18) continue;

                    double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt((theta * theta) + 1));
                    if (theta == 0) t = 1;

                    double c = 1 / Math.Sqrt((t * t) + 1);
                    double s = t * c;

                    for (int k = 0; k < n; k++)
                    {
                        double akp = a[k, p], akq = a[k, q];
                        a[k, p] = (c * akp) - (s * akq);
                        a[k, q] = (s * akp) + (c * akq);
                    }

                    for (int k = 0; k < n; k++)
                    {
                        double apk = a[p, k], aqk = a[q, k];
                        a[p, k] = (c * apk) - (s * aqk);
                        a[q, k] = (s * apk) + (c * aqk);
                    }

                    for (int k = 0; k < n; k++)
                    {
                        double vkp = v[k, p], vkq = v[k, q];
                        v[k, p] = (c * vkp) - (s * vkq);
                        v[k, q] = (s * vkp) + (c * vkq);
                    }
                }
            }
        }

        var values = new double[n];
        for (int i = 0; i < n; i++) values[i] = a[i, i];

        // Сортировка по убыванию собственных чисел вместе со столбцами векторов
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        Array.Sort(order, (x, y) => values[y].CompareTo(values[x]));

        var sortedValues = new double[n];
        var sortedVectors = new double[n, n];

        for (int j = 0; j < n; j++)
        {
            sortedValues[j] = values[order[j]];
            for (int i = 0; i < n; i++) sortedVectors[i, j] = v[i, order[j]];
        }

        return (sortedValues, sortedVectors);
    }

    /// <summary>Ковариационная матрица столбцов.</summary>
    /// <param name="data">Матрица «наблюдения x переменные».</param>
    /// <param name="sample">Делить на <c>n-1</c> вместо <c>n</c>.</param>
    public static double[,] Covariance(double[,] data, bool sample = true)
    {
        int n = data.GetLength(0), k = data.GetLength(1);
        var means = new double[k];

        for (int j = 0; j < k; j++)
        {
            double sum = 0;
            for (int i = 0; i < n; i++) sum += data[i, j];
            means[j] = sum / n;
        }

        double divisor = sample && n > 1 ? n - 1 : n;
        var covariance = new double[k, k];

        for (int a = 0; a < k; a++)
            for (int b = a; b < k; b++)
            {
                double sum = 0;
                for (int i = 0; i < n; i++) sum += (data[i, a] - means[a]) * (data[i, b] - means[b]);

                double value = sum / divisor;
                covariance[a, b] = value;
                covariance[b, a] = value;
            }

        return covariance;
    }

    /// <summary>Корреляционная матрица из ковариационной.</summary>
    public static double[,] ToCorrelation(double[,] covariance)
    {
        int k = covariance.GetLength(0);
        var correlation = new double[k, k];

        for (int a = 0; a < k; a++)
            for (int b = 0; b < k; b++)
            {
                double denominator = Math.Sqrt(covariance[a, a] * covariance[b, b]);
                correlation[a, b] = denominator > 1e-18 ? covariance[a, b] / denominator : a == b ? 1 : 0;
            }

        return correlation;
    }

    /// <summary>Квадратичная форма <c>w' A w</c>.</summary>
    public static double QuadraticForm(double[] weights, double[,] matrix)
    {
        int n = weights.Length;
        double total = 0;

        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++) total += weights[i] * matrix[i, j] * weights[j];

        return total;
    }

    /// <summary>Скалярное произведение.</summary>
    public static double Dot(double[] left, double[] right)
    {
        double sum = 0;
        for (int i = 0; i < left.Length && i < right.Length; i++) sum += left[i] * right[i];
        return sum;
    }
}
