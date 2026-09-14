#nullable enable

using AI.DataStructs.Algebraic;
using System;

namespace AI.MathUtils.Nonlinear;

/// <summary>
/// Общие приемы нелинейных решателей: вычисление функции на массиве и численный якобиан.
/// </summary>
internal static class NumericalJacobian
{
    /// <summary>
    /// Относительный шаг центральной разности по умолчанию: кубический корень машинного эпсилон.
    /// Ошибка складывается из округления (порядка eps/h) и отбрасывания (порядка h²), минимум дает ∛eps.
    /// </summary>
    internal const double DefaultStep = 6.055454e-6;

    /// <summary>
    /// Вычисляет функцию векторного аргумента на массиве и проверяет размер результата.
    /// </summary>
    internal static double[] Evaluate(Func<Vector, Vector> function, double[] point, int expectedLength = -1)
    {
        Vector value = function(new Vector(point)) ?? throw new InvalidOperationException("Функция вернула null.");

        if (expectedLength >= 0 && value.Count != expectedLength)
            throw new InvalidOperationException($"Функция вернула {value.Count} значений вместо {expectedLength}.");

        return value.ToArray();
    }

    /// <summary>
    /// Якобиан центральными разностями: J[i, j] = ∂fᵢ/∂xⱼ.
    /// </summary>
    internal static double[,] Central(Func<double[], double[]> function, double[] point, int outputs, double relativeStep)
    {
        double step = relativeStep > 0 ? relativeStep : DefaultStep;
        int n = point.Length;
        var jacobian = new double[outputs, n];
        var probe = (double[])point.Clone();

        for (int j = 0; j < n; j++)
        {
            double delta = step * Math.Max(Math.Abs(point[j]), 1.0);
            double forward = point[j] + delta;
            double backward = point[j] - delta;

            probe[j] = forward;
            double[] plus = function(probe);
            probe[j] = backward;
            double[] minus = function(probe);
            probe[j] = point[j];

            // Делим на фактическую разность аргументов, а не на 2·delta: так учитывается округление
            double width = forward - backward;

            for (int i = 0; i < outputs; i++)
                jacobian[i, j] = (plus[i] - minus[i]) / width;
        }

        return jacobian;
    }

    /// <summary>
    /// Переводит пользовательский якобиан в массив с проверкой размеров.
    /// </summary>
    internal static double[,] FromMatrix(Matrix jacobian, int rows, int columns)
    {
        if (jacobian == null)
            throw new InvalidOperationException("Якобиан равен null.");

        if (jacobian.Height != rows || jacobian.Width != columns)
            throw new InvalidOperationException(
                $"Якобиан имеет размер {jacobian.Height}x{jacobian.Width}, ожидался {rows}x{columns}.");

        var result = new double[rows, columns];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
                result[i, j] = jacobian[i, j];
        }

        return result;
    }

    /// <summary>
    /// Максимум модуля компонент.
    /// </summary>
    internal static double InfinityNorm(double[] values)
    {
        double max = 0;

        foreach (double value in values)
            max = Math.Max(max, Math.Abs(value));

        return max;
    }

    /// <summary>
    /// Сумма квадратов компонент.
    /// </summary>
    internal static double SumOfSquares(double[] values)
    {
        double sum = 0;

        foreach (double value in values)
            sum += value * value;

        return sum;
    }
}
