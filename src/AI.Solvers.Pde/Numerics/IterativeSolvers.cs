using AI.DataStructs.Algebraic;

namespace AI.Solvers.Pde.Numerics;

/// <summary>Итог работы итерационного решателя</summary>
/// <param name="Solution">Найденное решение</param>
/// <param name="Iterations">Число итераций</param>
/// <param name="Residual">Норма невязки на выходе</param>
/// <param name="Converged">Достигнут ли заданный порог</param>
public readonly record struct IterativeResult(Vector Solution, int Iterations, double Residual, bool Converged);

/// <summary>
/// Итерационные решатели разреженных систем.
/// </summary>
/// <remarks>
/// Матрицы дискретизованных краевых задач симметричны и положительно определены, поэтому
/// метод сопряжённых градиентов сходится за число шагов порядка корня из числа
/// обусловленности — на порядки быстрее прямого разложения, которое к тому же заполнило бы
/// нулевые места и съело бы память.
/// </remarks>
public static class IterativeSolvers
{
    /// <summary>
    /// Метод сопряжённых градиентов с диагональным предобусловливателем
    /// </summary>
    /// <param name="matrix">Симметричная положительно определённая матрица</param>
    /// <param name="rightHandSide">Правая часть</param>
    /// <param name="tolerance">Относительный порог по норме невязки</param>
    /// <param name="maxIterations">Предел итераций; по умолчанию размер системы, умноженный на десять</param>
    /// <param name="initialGuess">Начальное приближение; по умолчанию нулевое</param>
    public static IterativeResult ConjugateGradient(
        SparseMatrix matrix,
        Vector rightHandSide,
        double tolerance = 1e-10,
        int maxIterations = 0,
        Vector? initialGuess = null)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(rightHandSide);

        int size = rightHandSide.Count;

        if (matrix.Rows != size || matrix.Columns != size)
            throw new ArgumentException("Матрица должна быть квадратной и согласованной с правой частью", nameof(matrix));

        if (maxIterations <= 0)
            maxIterations = Math.Max(100, size * 10);

        Vector solution = initialGuess is null ? new Vector(size) : new Vector(initialGuess.ToArray());
        Vector diagonal = matrix.Diagonal();

        Vector residual = rightHandSide - matrix.Multiply(solution);
        Vector preconditioned = ApplyPreconditioner(diagonal, residual);
        Vector direction = new(preconditioned.ToArray());

        double rightNorm = Norm(rightHandSide);
        double threshold = tolerance * Math.Max(rightNorm, 1e-300);
        double rho = Dot(residual, preconditioned);

        int iteration = 0;

        for (; iteration < maxIterations; iteration++)
        {
            double residualNorm = Norm(residual);

            if (residualNorm <= threshold)
                return new IterativeResult(solution, iteration, residualNorm, true);

            Vector applied = matrix.Multiply(direction);
            double denominator = Dot(direction, applied);

            if (Math.Abs(denominator) < 1e-300)
                return new IterativeResult(solution, iteration, residualNorm, false);

            double alpha = rho / denominator;

            for (int i = 0; i < size; i++)
            {
                solution[i] += alpha * direction[i];
                residual[i] -= alpha * applied[i];
            }

            preconditioned = ApplyPreconditioner(diagonal, residual);

            double updated = Dot(residual, preconditioned);
            double beta = updated / rho;
            rho = updated;

            for (int i = 0; i < size; i++)
                direction[i] = preconditioned[i] + (beta * direction[i]);
        }

        double finalResidual = Norm(residual);

        return new IterativeResult(solution, iteration, finalResidual, finalResidual <= threshold);
    }

    /// <summary>
    /// Прогонка для трёхдиагональной системы (алгоритм Томаса)
    /// </summary>
    /// <remarks>
    /// Одномерные схемы дают именно такие системы, и решаются они за один проход вперёд
    /// и один назад — привлекать общий решатель здесь незачем.
    /// </remarks>
    /// <param name="lower">Поддиагональ, первый элемент не используется</param>
    /// <param name="diagonal">Главная диагональ</param>
    /// <param name="upper">Наддиагональ, последний элемент не используется</param>
    /// <param name="rightHandSide">Правая часть</param>
    public static Vector SolveTridiagonal(Vector lower, Vector diagonal, Vector upper, Vector rightHandSide)
    {
        ArgumentNullException.ThrowIfNull(lower);
        ArgumentNullException.ThrowIfNull(diagonal);
        ArgumentNullException.ThrowIfNull(upper);
        ArgumentNullException.ThrowIfNull(rightHandSide);

        int size = diagonal.Count;

        var modifiedUpper = new double[size];
        var modifiedRight = new double[size];

        if (Math.Abs(diagonal[0]) < 1e-300)
            throw new ArgumentException("Нулевой ведущий элемент: система вырождена", nameof(diagonal));

        modifiedUpper[0] = upper[0] / diagonal[0];
        modifiedRight[0] = rightHandSide[0] / diagonal[0];

        for (int i = 1; i < size; i++)
        {
            double denominator = diagonal[i] - (lower[i] * modifiedUpper[i - 1]);

            if (Math.Abs(denominator) < 1e-300)
                throw new ArgumentException("Прогонка неустойчива: нулевой знаменатель", nameof(diagonal));

            modifiedUpper[i] = i < size - 1 ? upper[i] / denominator : 0.0;
            modifiedRight[i] = (rightHandSide[i] - (lower[i] * modifiedRight[i - 1])) / denominator;
        }

        var solution = new Vector(size);
        solution[size - 1] = modifiedRight[size - 1];

        for (int i = size - 2; i >= 0; i--)
            solution[i] = modifiedRight[i] - (modifiedUpper[i] * solution[i + 1]);

        return solution;
    }

    private static Vector ApplyPreconditioner(Vector diagonal, Vector residual)
    {
        var result = new Vector(residual.Count);

        for (int i = 0; i < residual.Count; i++)
            result[i] = Math.Abs(diagonal[i]) > 1e-300 ? residual[i] / diagonal[i] : residual[i];

        return result;
    }

    private static double Dot(Vector left, Vector right)
    {
        double sum = 0;

        for (int i = 0; i < left.Count; i++)
            sum += left[i] * right[i];

        return sum;
    }

    private static double Norm(Vector vector) => Math.Sqrt(Dot(vector, vector));
}
