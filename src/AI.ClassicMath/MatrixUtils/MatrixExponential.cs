using AI.DataStructs.Algebraic;
using System;

namespace AI.ClassicMath.MatrixUtils;

/// <summary>
/// Матричная экспонента <c>exp(A) = Σ Aᵏ/k!</c>.
/// </summary>
/// <remarks>
/// Масштабирование и возведение в квадрат: <c>exp(A) = (exp(A/2ˢ))^(2ˢ)</c>. Степень s подбирается по
/// бесконечной норме так, чтобы ‖A/2ˢ‖ ≤ 1/2; тогда ряд Тейлора сходится за два десятка членов без
/// сокращения больших слагаемых. Нужна для точной дискретизации линейных систем и решения
/// <c>x′ = A·x</c>.
/// </remarks>
public static class MatrixExponential
{
    /// <summary>
    /// Вычисляет exp(A).
    /// </summary>
    /// <param name="matrix">Квадратная матрица.</param>
    /// <param name="tolerance">Относительный порог отбрасывания членов ряда.</param>
    /// <param name="maxTerms">Предельное число членов ряда.</param>
    /// <exception cref="ArgumentException">Матрица не квадратная либо содержит не числа.</exception>
    public static Matrix Compute(Matrix matrix, double tolerance = 1e-16, int maxTerms = 60)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        if (!matrix.IsSquared)
            throw new ArgumentException("Ожидается квадратная матрица.", nameof(matrix));

        int n = matrix.Height;
        double norm = InfinityNorm(matrix);

        if (!double.IsFinite(norm))
            throw new ArgumentException("Матрица содержит бесконечные элементы или не числа.", nameof(matrix));

        int squarings = norm > 0.5 ? (int)Math.Ceiling(Math.Log2(norm / 0.5)) : 0;
        Matrix scaled = matrix * Math.Pow(2, -squarings);
        Matrix result = Matrix.Identity(n);
        Matrix term = Matrix.Identity(n);

        for (int k = 1; k <= maxTerms; k++)
        {
            term = term * scaled * (1.0 / k);
            result += term;

            if (MaxAbs(term) <= tolerance * Math.Max(1, MaxAbs(result)))
                break;
        }

        for (int i = 0; i < squarings; i++)
            result *= result;

        return result;
    }

    private static double MaxAbs(Matrix matrix)
    {
        double max = 0;

        for (int i = 0; i < matrix.Data.Length; i++)
            max = Math.Max(max, Math.Abs(matrix.Data[i]));

        return max;
    }

    // Наибольшая сумма модулей по строке
    private static double InfinityNorm(Matrix matrix)
    {
        double best = 0;

        for (int i = 0; i < matrix.Height; i++)
        {
            double row = 0;

            for (int j = 0; j < matrix.Width; j++) row += Math.Abs(matrix[i, j]);

            // Не число в строке должно дойти до проверки, а Math.Max его бы проглотил
            if (double.IsNaN(row)) return double.NaN;

            best = Math.Max(best, row);
        }

        return best;
    }
}
