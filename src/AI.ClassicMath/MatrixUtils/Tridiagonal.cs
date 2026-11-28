using AI.DataStructs.Algebraic;
using System;

namespace AI.ClassicMath.MatrixUtils;

/// <summary>
/// Трёхдиагональные системы: прогонка (алгоритм Томаса).
/// </summary>
/// <remarks>
/// Одномерные разностные схемы и кубические сплайны дают именно такие системы. Решаются они за один
/// проход вперёд и один назад, за линейное время; общий решатель здесь тратил бы куб размера.
/// Прогонка без выбора главного элемента устойчива для матриц с диагональным преобладанием — такими
/// и бывают матрицы схем и сплайнов; на прочих нулевой знаменатель даёт отказ, а не мусор.
/// </remarks>
public static class Tridiagonal
{
    /// <summary>
    /// Решает систему с трёхдиагональной матрицей.
    /// </summary>
    /// <param name="lower">Поддиагональ той же длины, что диагональ; первый элемент не используется.</param>
    /// <param name="diagonal">Главная диагональ.</param>
    /// <param name="upper">Наддиагональ той же длины, что диагональ; последний элемент не используется.</param>
    /// <param name="rightHandSide">Правая часть.</param>
    /// <exception cref="ArgumentException">Длины не согласованы либо встретился нулевой знаменатель.</exception>
    public static Vector Solve(Vector lower, Vector diagonal, Vector upper, Vector rightHandSide)
    {
        ArgumentNullException.ThrowIfNull(lower);
        ArgumentNullException.ThrowIfNull(diagonal);
        ArgumentNullException.ThrowIfNull(upper);
        ArgumentNullException.ThrowIfNull(rightHandSide);

        return new Vector(Solve((double[])lower, (double[])diagonal, (double[])upper, (double[])rightHandSide));
    }

    // Перегрузки с массивами наружу нет намеренно: запись [1, 2, 3] стала бы неоднозначной между массивом и вектором
    private static double[] Solve(double[] lower, double[] diagonal, double[] upper, double[] rightHandSide)
    {
        int size = diagonal.Length;

        if (size == 0)
            throw new ArgumentException("Система пуста.", nameof(diagonal));

        if (lower.Length != size || upper.Length != size || rightHandSide.Length != size)
            throw new ArgumentException("Диагонали и правая часть должны быть одной длины.", nameof(diagonal));

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

        var solution = new double[size];
        solution[size - 1] = modifiedRight[size - 1];

        for (int i = size - 2; i >= 0; i--)
            solution[i] = modifiedRight[i] - (modifiedUpper[i] * solution[i + 1]);

        return solution;
    }
}
