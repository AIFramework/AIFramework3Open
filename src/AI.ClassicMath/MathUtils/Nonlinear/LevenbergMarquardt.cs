#nullable enable

using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using System;

namespace AI.MathUtils.Nonlinear;

/// <summary>
/// Нелинейный метод наименьших квадратов Левенберга-Марквардта: минимизирует ‖r(x)‖²
/// для функции невязок r: Rⁿ → Rᵐ.
/// </summary>
/// <remarks>
/// Шаг находится из системы (JᵀJ + λ·D)·δ = -Jᵀr, где D - диагональное масштабирование Марквардта:
/// накопленный максимум диагонали JᵀJ. Масштаб делает метод нечувствительным к единицам измерения параметров,
/// а накопление максимума не дает затуханию исчезнуть, когда столбец якобиана временно мал.
/// λ меняется по правилу Нильсена в зависимости от отношения фактического уменьшения к предсказанному.
/// </remarks>
public static class LevenbergMarquardt
{
    /// <summary>
    /// Находит минимум суммы квадратов невязок.
    /// </summary>
    /// <param name="residuals">Функция невязок r(x).</param>
    /// <param name="start">Начальное приближение.</param>
    /// <param name="jacobian">Аналитический якобиан r(x) размером m×n; если null, считается центральными разностями.</param>
    /// <param name="options">Настройки; если null, используются значения по умолчанию.</param>
    /// <returns>Решение, сумма квадратов, число итераций, признак сходимости и якобиан в решении.</returns>
    public static LeastSquaresResult Solve(
        Func<Vector, Vector> residuals,
        Vector start,
        Func<Vector, Matrix>? jacobian = null,
        LevenbergMarquardtOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(residuals);
        ArgumentNullException.ThrowIfNull(start);

        options ??= new LevenbergMarquardtOptions();

        int n = start.Count;
        int evaluations = 0;
        double[] x = start.ToArray();
        double[] r = NumericalJacobian.Evaluate(residuals, x);
        int m = r.Length;
        evaluations++;

        if (m == 0)
            throw new ArgumentException("Функция невязок вернула пустой вектор.", nameof(residuals));

        double[] Residual(double[] point)
        {
            evaluations++;
            return NumericalJacobian.Evaluate(residuals, point, m);
        }

        double[,] Jacobian(double[] point) => jacobian != null
            ? NumericalJacobian.FromMatrix(jacobian(new Vector(point)), m, n)
            : NumericalJacobian.Central(Residual, point, m, options.DerivativeStep);

        double cost = NumericalJacobian.SumOfSquares(r);
        double[,] j = Jacobian(x);
        var scale = new double[n];
        double lambda = options.InitialDamping;
        double growth = 2;
        bool converged = false;
        int iteration = 0;

        while (iteration < options.MaxIterations)
        {
            var (normal, gradient) = NormalEquations(j, r);

            for (int k = 0; k < n; k++)
                scale[k] = Math.Max(scale[k], normal[k, k]);

            if (cost == 0 || NumericalJacobian.InfinityNorm(gradient) <= options.GradientTolerance)
            {
                converged = true;
                break;
            }

            iteration++;
            double[]? step = DampedStep(normal, gradient, scale, lambda);

            if (step == null)
            {
                lambda *= growth;
                growth *= 2;
                continue;
            }

            double stepNorm = Norm(step);

            if (stepNorm <= options.StepTolerance * (Norm(x) + options.StepTolerance))
            {
                converged = true;
                break;
            }

            var candidate = new double[n];

            for (int k = 0; k < n; k++)
                candidate[k] = x[k] + step[k];

            double[] candidateResiduals = Residual(candidate);
            double candidateCost = NumericalJacobian.SumOfSquares(candidateResiduals);

            // Предсказанное линейной моделью уменьшение: -(2·gᵀδ + δᵀ·JᵀJ·δ), g = Jᵀr
            double predicted = -(2 * Dot(gradient, step) + Quadratic(normal, step));
            double actual = cost - candidateCost;
            double ratio = predicted > 0 && !double.IsNaN(candidateCost) ? actual / predicted : -1;

            if (ratio > 0)
            {
                double previousCost = cost;
                x = candidate;
                r = candidateResiduals;
                cost = candidateCost;
                j = Jacobian(x);
                lambda *= Math.Max(1.0 / 3.0, 1 - Math.Pow((2 * ratio) - 1, 3));
                growth = 2;

                if (actual <= options.CostTolerance * previousCost)
                {
                    converged = true;
                    break;
                }
            }
            else
            {
                lambda *= growth;
                growth *= 2;
            }
        }

        return new LeastSquaresResult
        {
            Solution = new Vector(x),
            Residuals = new Vector(r),
            Cost = cost,
            Iterations = iteration,
            FunctionEvaluations = evaluations,
            Converged = converged,
            Jacobian = new Matrix(j)
        };
    }

    // Нормальные уравнения: JᵀJ и g = Jᵀr
    private static (double[,] Normal, double[] Gradient) NormalEquations(double[,] jacobian, double[] residuals)
    {
        int m = jacobian.GetLength(0);
        int n = jacobian.GetLength(1);
        var normal = new double[n, n];
        var gradient = new double[n];

        for (int a = 0; a < n; a++)
        {
            for (int b = a; b < n; b++)
            {
                double sum = 0;

                for (int row = 0; row < m; row++)
                    sum += jacobian[row, a] * jacobian[row, b];

                normal[a, b] = sum;
                normal[b, a] = sum;
            }

            double g = 0;

            for (int row = 0; row < m; row++)
                g += jacobian[row, a] * residuals[row];

            gradient[a] = g;
        }

        return (normal, gradient);
    }

    // Решение (JᵀJ + λ·D)·δ = -g разложением Холецкого; null, если матрица не положительно определена
    private static double[]? DampedStep(double[,] normal, double[] gradient, double[] scale, double lambda)
    {
        int n = gradient.Length;
        var system = new Matrix(n, n);
        var right = new Vector(n);

        for (int a = 0; a < n; a++)
        {
            for (int b = 0; b < n; b++)
                system[a, b] = normal[a, b];

            // Нулевой столбец якобиана: берем единичный масштаб, чтобы система осталась невырожденной
            system[a, a] += lambda * (scale[a] > 0 ? scale[a] : 1.0);
            right[a] = -gradient[a];
        }

        Vector solution;

        try
        {
            solution = Cholesky.Solve(system, right);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        double[] step = solution.ToArray();

        foreach (double value in step)
        {
            if (!double.IsFinite(value))
                return null;
        }

        return step;
    }

    private static double Dot(double[] left, double[] right)
    {
        double sum = 0;

        for (int i = 0; i < left.Length; i++)
            sum += left[i] * right[i];

        return sum;
    }

    private static double Quadratic(double[,] matrix, double[] vector)
    {
        double sum = 0;

        for (int a = 0; a < vector.Length; a++)
        {
            for (int b = 0; b < vector.Length; b++)
                sum += vector[a] * matrix[a, b] * vector[b];
        }

        return sum;
    }

    private static double Norm(double[] values) => Math.Sqrt(NumericalJacobian.SumOfSquares(values));
}
