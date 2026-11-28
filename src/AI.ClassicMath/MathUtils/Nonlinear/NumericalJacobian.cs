#nullable enable

using AI.DataStructs.Algebraic;
using System;

namespace AI.MathUtils.Nonlinear;

/// <summary>
/// Общие приемы нелинейных решателей: вычисление функции на массиве и проверка якобиана.
/// Сам численный якобиан считает <see cref="AI.HighLevelFunctions.NumericalDifferentiation.Jacobian"/>.
/// </summary>
internal static class NumericalJacobian
{
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
