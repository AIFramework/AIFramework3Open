#nullable enable

using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using System;

namespace AI.MathUtils.Nonlinear;

/// <summary>
/// Метод Ньютона для квадратных систем нелинейных уравнений F(x) = 0 с дроблением шага.
/// </summary>
/// <remarks>
/// Направление находится из J·δ = -F. Длина шага подбирается дроблением пополам до выполнения условия Армихо
/// для функции ½‖F‖², что расширяет область сходимости по сравнению с чистым методом Ньютона.
/// При вырожденном якобиане шаг берется по псевдообратной матрице (решение наименьших квадратов).
/// </remarks>
public static class NewtonSystemSolver
{
    private const double ArmijoConstant = 1e-4;
    private const double MinStepFraction = 1e-10;

    /// <summary>
    /// Решает систему F(x) = 0.
    /// </summary>
    /// <param name="function">Функция F: Rⁿ → Rⁿ.</param>
    /// <param name="start">Начальное приближение.</param>
    /// <param name="jacobian">Аналитический якобиан F размером n×n; если null, считается центральными разностями.</param>
    /// <param name="options">Настройки; если null, используются значения по умолчанию.</param>
    /// <returns>Решение, невязка, число итераций и признак сходимости.</returns>
    public static NonlinearSystemResult Solve(
        Func<Vector, Vector> function,
        Vector start,
        Func<Vector, Matrix>? jacobian = null,
        NewtonSystemOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(start);

        options ??= new NewtonSystemOptions();

        int n = start.Count;
        int evaluations = 0;

        double[] Evaluate(double[] point)
        {
            evaluations++;
            return NumericalJacobian.Evaluate(function, point, n);
        }

        double[,] Jacobian(double[] point) => jacobian != null
            ? NumericalJacobian.FromMatrix(jacobian(new Vector(point)), n, n)
            : NumericalJacobian.Central(Evaluate, point, n, options.DerivativeStep);

        double[] x = start.ToArray();
        double[] f = Evaluate(x);
        double merit = 0.5 * NumericalJacobian.SumOfSquares(f);
        double[,]? j = null;
        bool fresh = false;
        int iteration = 0;

        while (iteration < options.MaxIterations && NumericalJacobian.InfinityNorm(f) > options.FunctionTolerance)
        {
            if (j == null)
            {
                j = Jacobian(x);
                fresh = true;
            }

            iteration++;
            double[] direction = NewtonDirection(j, f);

            // Для точного направления Ньютона производная ½‖F‖² вдоль δ равна -‖F‖²
            double slope = -2 * merit;
            double fraction = 1;
            double[]? candidate = null;
            double[]? candidateValue = null;
            double candidateMerit = 0;

            while (fraction >= MinStepFraction)
            {
                var trial = new double[n];

                for (int k = 0; k < n; k++)
                    trial[k] = x[k] + (fraction * direction[k]);

                double[] value = Evaluate(trial);
                double trialMerit = 0.5 * NumericalJacobian.SumOfSquares(value);

                if (double.IsFinite(trialMerit) && trialMerit <= merit + (ArmijoConstant * fraction * slope))
                {
                    candidate = trial;
                    candidateValue = value;
                    candidateMerit = trialMerit;
                    break;
                }

                fraction *= 0.5;
            }

            if (candidate == null || candidateValue == null)
            {
                // Приближенный якобиан Бройдена мог дать плохое направление: пересчитываем его и повторяем
                if (!fresh)
                {
                    j = null;
                    continue;
                }

                break;
            }

            var step = new double[n];
            var change = new double[n];

            for (int k = 0; k < n; k++)
            {
                step[k] = candidate[k] - x[k];
                change[k] = candidateValue[k] - f[k];
            }

            x = candidate;
            f = candidateValue;
            merit = candidateMerit;

            if (NumericalJacobian.InfinityNorm(step) <= options.StepTolerance * (1 + NumericalJacobian.InfinityNorm(x)))
                break;

            if (options.UseBroyden)
            {
                BroydenUpdate(j, step, change);
                fresh = false;
            }
            else
            {
                j = null;
            }
        }

        double residualNorm = NumericalJacobian.InfinityNorm(f);

        return new NonlinearSystemResult
        {
            Solution = new Vector(x),
            Residual = new Vector(f),
            ResidualNorm = residualNorm,
            Iterations = iteration,
            FunctionEvaluations = evaluations,
            Converged = residualNorm <= options.FunctionTolerance
        };
    }

    // Решение J·δ = -F: LU-разложение, а при вырожденности псевдообратная матрица
    private static double[] NewtonDirection(double[,] jacobian, double[] value)
    {
        int n = value.Length;
        var matrix = new Matrix(jacobian);
        var right = new Vector(n);

        for (int i = 0; i < n; i++)
            right[i] = -value[i];

        try
        {
            double[] direction = LU.Solve(matrix, right).ToArray();

            if (Array.TrueForAll(direction, double.IsFinite))
                return direction;
        }
        catch (InvalidOperationException)
        {
            // Вырожденный якобиан: ниже берется решение наименьших квадратов
        }

        Matrix inverse = Pseudoinverse.Compute(matrix);
        var result = new double[n];

        for (int i = 0; i < n; i++)
        {
            double sum = 0;

            for (int k = 0; k < n; k++)
                sum += inverse[i, k] * right[k];

            result[i] = sum;
        }

        return result;
    }

    // Обновление Бройдена: J ← J + (Δf - J·s)·sᵀ / (sᵀs)
    private static void BroydenUpdate(double[,] jacobian, double[] step, double[] change)
    {
        int n = step.Length;
        double denominator = NumericalJacobian.SumOfSquares(step);

        if (denominator == 0)
            return;

        for (int i = 0; i < n; i++)
        {
            double predicted = 0;

            for (int k = 0; k < n; k++)
                predicted += jacobian[i, k] * step[k];

            double factor = (change[i] - predicted) / denominator;

            for (int k = 0; k < n; k++)
                jacobian[i, k] += factor * step[k];
        }
    }
}
