#nullable enable

using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using System;

namespace AI.MathUtils.Statistics;

/// <summary>
/// Сигма-точки распределения: детерминированный набор точек с весами, через который гауссиана переносится
/// нелинейной функцией (unscented transform). Точки лежат в среднем и на ±√(n + λ) вдоль столбцов корня
/// ковариации, поэтому среднее и ковариация образа точны для линейной функции и с поправкой второго порядка
/// для гладкой.
/// </summary>
/// <param name="Points">Точки: первая в среднем, дальше пары вдоль каждого столбца корня ковариации.</param>
/// <param name="MeanWeights">Веса точек для среднего; сумма равна единице.</param>
/// <param name="CovarianceWeights">Веса точек для ковариации.</param>
public sealed record SigmaPoints(Vector[] Points, double[] MeanWeights, double[] CovarianceWeights)
{
    /// <summary>
    /// Число точек: 2n + 1 для n входов.
    /// </summary>
    public int Count => Points.Length;
}

/// <summary>
/// Образ гауссианы после переноса функцией через сигма-точки.
/// </summary>
/// <param name="Mean">Среднее образа.</param>
/// <param name="Covariance">Ковариация образа (m × m).</param>
/// <param name="CrossCovariance">Взаимная ковариация входа и образа (n × m).</param>
/// <param name="Linearization">
/// Статистическая линеаризация функции (m × n): матрица A, для которой образ ближе всего к A·(x − x̄) + ȳ
/// в среднем по распределению входа. Для линейной функции совпадает с ее матрицей, для гладкой играет
/// роль якобиана, усредненного по разбросу входа.
/// </param>
public sealed record UnscentedResult(Vector Mean, Matrix Covariance, Matrix CrossCovariance, Matrix Linearization);

/// <summary>
/// Перенос гауссианы через нелинейную функцию сигма-точками (unscented transform).
/// Для n входов функция вычисляется 2n + 1 раз, что для гладких законов заменяет розыгрыш сотен выборок.
/// </summary>
public static class UnscentedTransform
{
    /// <summary>
    /// Сигма-точки для среднего и ковариации. Параметры по умолчанию (α = 1, β = 2, κ = 0) дают λ = 0:
    /// точки лежат на ±√n·σ, вес среднего у центральной точки равен нулю, а ковариация получает поправку β
    /// для гауссова входа. Такие веса неотрицательны при любом n, и ковариация образа остается
    /// неотрицательно определенной.
    /// </summary>
    /// <param name="mean">Среднее входа, n чисел.</param>
    /// <param name="covariance">Ковариация входа, симметричная положительно определенная матрица n × n.</param>
    /// <param name="alpha">Размах точек вокруг среднего, обычно от 1e-3 до 1.</param>
    /// <param name="beta">Поправка к весу центральной точки в ковариации; 2 для гауссова входа.</param>
    /// <param name="kappa">Вторичный масштаб; 0 или 3 − n.</param>
    /// <exception cref="ArgumentException">Размеры не согласованы или n + λ не положительно.</exception>
    /// <exception cref="InvalidOperationException">Ковариация не положительно определена.</exception>
    public static SigmaPoints Points(Vector mean, Matrix covariance, double alpha = 1, double beta = 2, double kappa = 0)
    {
        ArgumentNullException.ThrowIfNull(mean);
        ArgumentNullException.ThrowIfNull(covariance);

        int n = mean.Count;

        if (covariance.Height != n || covariance.Width != n)
            throw new ArgumentException("Ковариация должна быть квадратной матрицей размера входа.", nameof(covariance));

        Matrix root = Cholesky.Decompose(covariance);
        return Points(mean, column => Column(root, column, n), alpha, beta, kappa);
    }

    /// <summary>
    /// Сигма-точки для независимых входов, заданных средним и СКО. Нулевое СКО допустимо: такой вход
    /// не разбрасывается, и его пара точек совпадает с центральной.
    /// </summary>
    /// <param name="mean">Среднее входа, n чисел.</param>
    /// <param name="stds">СКО каждого входа, n неотрицательных чисел.</param>
    /// <param name="alpha">Размах точек вокруг среднего.</param>
    /// <param name="beta">Поправка к весу центральной точки в ковариации.</param>
    /// <param name="kappa">Вторичный масштаб.</param>
    public static SigmaPoints Points(Vector mean, Vector stds, double alpha = 1, double beta = 2, double kappa = 0)
    {
        ArgumentNullException.ThrowIfNull(mean);
        ArgumentNullException.ThrowIfNull(stds);

        if (stds.Count != mean.Count)
            throw new ArgumentException("Число СКО должно совпадать с размером среднего.", nameof(stds));

        for (int i = 0; i < stds.Count; i++)
        {
            if (stds[i] < 0 || double.IsNaN(stds[i]))
                throw new ArgumentException($"СКО входа {i} должно быть неотрицательным числом.", nameof(stds));
        }

        return Points(mean, column => Axis(column, stds[column], mean.Count), alpha, beta, kappa);
    }

    /// <summary>
    /// Переносит гауссиану с полной ковариацией через векторную функцию.
    /// </summary>
    /// <param name="mean">Среднее входа.</param>
    /// <param name="covariance">Ковариация входа.</param>
    /// <param name="function">Функция из n входов в m выходов.</param>
    /// <param name="alpha">Размах точек вокруг среднего.</param>
    /// <param name="beta">Поправка к весу центральной точки в ковариации.</param>
    /// <param name="kappa">Вторичный масштаб.</param>
    public static UnscentedResult Transform(Vector mean, Matrix covariance, Func<Vector, Vector> function,
        double alpha = 1, double beta = 2, double kappa = 0)
    {
        ArgumentNullException.ThrowIfNull(function);
        SigmaPoints points = Points(mean, covariance, alpha, beta, kappa);
        return Transform(points, mean, function, crossCovariance => Solve(covariance, crossCovariance));
    }

    /// <summary>
    /// Переносит гауссиану с независимыми входами через векторную функцию.
    /// </summary>
    /// <param name="mean">Среднее входа.</param>
    /// <param name="stds">СКО каждого входа; нуль допустим.</param>
    /// <param name="function">Функция из n входов в m выходов.</param>
    /// <param name="alpha">Размах точек вокруг среднего.</param>
    /// <param name="beta">Поправка к весу центральной точки в ковариации.</param>
    /// <param name="kappa">Вторичный масштаб.</param>
    public static UnscentedResult Transform(Vector mean, Vector stds, Func<Vector, Vector> function,
        double alpha = 1, double beta = 2, double kappa = 0)
    {
        ArgumentNullException.ThrowIfNull(function);
        SigmaPoints points = Points(mean, stds, alpha, beta, kappa);
        return Transform(points, mean, function, crossCovariance => Divide(crossCovariance, stds));
    }

    /// <summary>
    /// Переносит гауссиану с независимыми входами через скалярную функцию: среднее и дисперсия образа
    /// и градиент статистической линеаризации по каждому входу.
    /// </summary>
    /// <param name="mean">Среднее входа.</param>
    /// <param name="stds">СКО каждого входа; нуль допустим.</param>
    /// <param name="function">Скалярная функция входов.</param>
    /// <param name="alpha">Размах точек вокруг среднего.</param>
    /// <param name="beta">Поправка к весу центральной точки в ковариации.</param>
    /// <param name="kappa">Вторичный масштаб.</param>
    /// <returns>Среднее образа, его дисперсия и градиент: вход с нулевым СКО получает нулевую производную.</returns>
    public static (double Mean, double Variance, Vector Gradient) Transform(Vector mean, Vector stds, Func<Vector, double> function,
        double alpha = 1, double beta = 2, double kappa = 0)
    {
        ArgumentNullException.ThrowIfNull(function);
        UnscentedResult result = Transform(mean, stds, x => new Vector(function(x)), alpha, beta, kappa);
        var gradient = new Vector(mean.Count);

        for (int j = 0; j < mean.Count; j++)
            gradient[j] = result.Linearization[0, j];

        return (result.Mean[0], result.Covariance[0, 0], gradient);
    }

    private static SigmaPoints Points(Vector mean, Func<int, Vector> direction, double alpha, double beta, double kappa)
    {
        int n = mean.Count;
        double lambda = (alpha * alpha * (n + kappa)) - n;

        if (n + lambda <= 0)
            throw new ArgumentException("Параметры должны давать n + λ > 0: увеличьте α или κ.");

        double spread = Math.Sqrt(n + lambda);
        var points = new Vector[(2 * n) + 1];
        var meanWeights = new double[points.Length];
        var covarianceWeights = new double[points.Length];
        points[0] = mean.Clone();
        meanWeights[0] = lambda / (n + lambda);
        covarianceWeights[0] = meanWeights[0] + 1 - (alpha * alpha) + beta;

        for (int i = 0; i < n; i++)
        {
            Vector step = direction(i) * spread;
            points[1 + i] = mean + step;
            points[1 + n + i] = mean - step;
            meanWeights[1 + i] = meanWeights[1 + n + i] = 1 / (2 * (n + lambda));
            covarianceWeights[1 + i] = covarianceWeights[1 + n + i] = meanWeights[1 + i];
        }

        return new SigmaPoints(points, meanWeights, covarianceWeights);
    }

    // Образ по точкам: среднее, ковариация, взаимная ковариация и линеаризация A = (P⁻¹·P_xy)ᵀ,
    // где деление на ковариацию входа делает переданная функция
    private static UnscentedResult Transform(SigmaPoints points, Vector mean, Func<Vector, Vector> function, Func<Matrix, Matrix> divide)
    {
        int n = mean.Count;
        var images = new Vector[points.Count];

        for (int k = 0; k < points.Count; k++)
            images[k] = function(points.Points[k]) ?? throw new InvalidOperationException("Функция вернула null.");

        int m = images[0].Count;
        var imageMean = new Vector(m);

        for (int k = 0; k < points.Count; k++)
            imageMean += images[k] * points.MeanWeights[k];

        var covariance = new Matrix(m, m);
        var cross = new Matrix(n, m);

        for (int k = 0; k < points.Count; k++)
        {
            Vector dy = images[k] - imageMean;
            Vector dx = points.Points[k] - mean;
            double weight = points.CovarianceWeights[k];

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < m; j++)
                    covariance[i, j] += weight * dy[i] * dy[j];
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                    cross[i, j] += weight * dx[i] * dy[j];
            }
        }

        return new UnscentedResult(imageMean, covariance, cross, divide(cross).Transpose());
    }

    // P⁻¹·B для положительно определенной P через разложение Холецкого
    private static Matrix Solve(Matrix covariance, Matrix right)
    {
        Matrix lower = Cholesky.Decompose(covariance);
        int n = covariance.Height;
        var result = new Matrix(n, right.Width);

        for (int column = 0; column < right.Width; column++)
        {
            var y = new double[n];

            for (int i = 0; i < n; i++)
            {
                double sum = right[i, column];

                for (int k = 0; k < i; k++)
                    sum -= lower[i, k] * y[k];

                y[i] = sum / lower[i, i];
            }

            for (int i = n - 1; i >= 0; i--)
            {
                double sum = y[i];

                for (int k = i + 1; k < n; k++)
                    sum -= lower[k, i] * result[k, column];

                result[i, column] = sum / lower[i, i];
            }
        }

        return result;
    }

    // Деление на диагональную ковариацию: строка входа с нулевым СКО дает нулевую производную
    private static Matrix Divide(Matrix cross, Vector stds)
    {
        var result = new Matrix(cross.Height, cross.Width);

        for (int i = 0; i < cross.Height; i++)
        {
            double variance = stds[i] * stds[i];

            for (int j = 0; j < cross.Width; j++)
                result[i, j] = variance > 0 ? cross[i, j] / variance : 0;
        }

        return result;
    }

    private static Vector Column(Matrix matrix, int column, int n)
    {
        var result = new Vector(n);

        for (int i = 0; i < n; i++)
            result[i] = matrix[i, column];

        return result;
    }

    private static Vector Axis(int index, double length, int n)
    {
        var result = new Vector(n);
        result[index] = length;
        return result;
    }
}
