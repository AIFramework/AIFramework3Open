using System;

namespace AI.DataStructs.Algebraic;

public partial class Matrix
{
    /// <summary>Машинный эпсилон двойной точности (double.Epsilon — наименьшее субнормальное, а не он)</summary>
    private const double MachineEpsilon = 2.220446049250313e-16;

    /// <summary>
    /// LU-разложение PA = LU с частичным выбором главного элемента на копии матрицы
    /// </summary>
    /// <remarks>
    /// Единое ядро исключения для определителя, обратной матрицы и треугольной формы. Прежде
    /// исключение шло без перестановки строк: нулевой ведущий элемент давал деление на ноль, NaN
    /// подменялся нулём, и определитель хорошо обусловленной матрицы получался равным 0.
    /// </remarks>
    /// <param name="permutation">permutation[i] — номер строки исходной матрицы, стоящей на i-м месте</param>
    /// <param name="sign">Знак перестановки строк: ±1</param>
    /// <returns>Построчно: под диагональю — множители L (единичная диагональ не хранится), на диагонали и выше — U</returns>
    private double[] FactorLu(out int[] permutation, out int sign)
    {
        int n = Height;
        var lu = new double[n * n];

        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                lu[(i * n) + j] = this[i, j];

        permutation = new int[n];
        for (int i = 0; i < n; i++)
            permutation[i] = i;

        sign = 1;

        for (int k = 0; k < n; k++)
        {
            int pivotRow = k;
            double largest = Math.Abs(lu[(k * n) + k]);

            for (int i = k + 1; i < n; i++)
            {
                double candidate = Math.Abs(lu[(i * n) + k]);
                if (candidate > largest)
                {
                    largest = candidate;
                    pivotRow = i;
                }
            }

            if (pivotRow != k)
            {
                for (int j = 0; j < n; j++)
                    (lu[(k * n) + j], lu[(pivotRow * n) + j]) = (lu[(pivotRow * n) + j], lu[(k * n) + j]);

                (permutation[k], permutation[pivotRow]) = (permutation[pivotRow], permutation[k]);
                sign = -sign;
            }

            double pivot = lu[(k * n) + k];

            // Столбец на диагонали и ниже нулевой: исключать нечего, U вырождена
            if (pivot == 0)
                continue;

            for (int i = k + 1; i < n; i++)
            {
                double factor = lu[(i * n) + k] / pivot;
                lu[(i * n) + k] = factor;

                if (factor == 0)
                    continue;

                for (int j = k + 1; j < n; j++)
                    lu[(i * n) + j] -= factor * lu[(k * n) + j];
            }
        }

        return lu;
    }

    /// <summary>Определитель через LU с выбором главного элемента</summary>
    private double PivotedDeterminant()
    {
        int n = Height;

        // Точная проверка, а не приближённая: у почти диагональной матрицы определитель не равен произведению диагонали
        if (IsExactlyTriangular())
        {
            double product = 1;
            for (int i = 0; i < n; i++)
                product *= this[i, i];
            return product;
        }

        double[] lu = FactorLu(out _, out int sign);
        double determinant = sign;

        for (int k = 0; k < n; k++)
            determinant *= lu[(k * n) + k];

        return determinant;
    }

    /// <summary>
    /// Обратная матрица через LU с выбором главного элемента и n решений треугольных систем
    /// </summary>
    /// <remarks>
    /// Вырожденность определяется относительным порогом: ведущий элемент не больше n·ε·‖A‖∞.
    /// Абсолютный порог по определителю, как прежде, зависит от масштаба: 10⁻⁶·I размера 2×2 имеет
    /// определитель 10⁻¹² и при этом обусловлена идеально.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Матрица вырождена, численно вырождена или содержит NaN и бесконечности</exception>
    private Matrix PivotedInverse()
    {
        int n = Height;
        double norm = 0;

        for (int i = 0; i < n; i++)
        {
            double row = 0;
            for (int j = 0; j < n; j++)
                row += Math.Abs(this[i, j]);
            norm = Math.Max(norm, row);
        }

        if (!double.IsFinite(norm))
            throw new InvalidOperationException("Матрица содержит NaN или бесконечность — обратной у неё нет.");

        double[] lu = FactorLu(out int[] permutation, out _);
        double tolerance = n * MachineEpsilon * norm;

        for (int k = 0; k < n; k++)
        {
            double pivot = Math.Abs(lu[(k * n) + k]);
            if (pivot <= tolerance)
                throw new InvalidOperationException(
                    $"Матрица вырождена или численно вырождена: ведущий элемент {pivot:G3} не больше порога n·ε·‖A‖∞ = {tolerance:G3}.");
        }

        var inverse = new Matrix(n, n);
        var column = new double[n];

        for (int c = 0; c < n; c++)
        {
            // Прямой ход: L·y = P·e_c
            for (int i = 0; i < n; i++)
            {
                double sum = permutation[i] == c ? 1 : 0;
                for (int j = 0; j < i; j++)
                    sum -= lu[(i * n) + j] * column[j];
                column[i] = sum;
            }

            // Обратный ход: U·x = y
            for (int i = n - 1; i >= 0; i--)
            {
                double sum = column[i];
                for (int j = i + 1; j < n; j++)
                    sum -= lu[(i * n) + j] * column[j];
                column[i] = sum / lu[(i * n) + i];
            }

            for (int i = 0; i < n; i++)
                inverse[i, c] = column[i];
        }

        return inverse;
    }

    /// <summary>Все элементы строго ниже или строго выше диагонали равны нулю</summary>
    private bool IsExactlyTriangular()
    {
        bool upper = true, lower = true;

        for (int i = 0; i < Height && (upper || lower); i++)
        {
            for (int j = 0; j < Width; j++)
            {
                if (j < i && this[i, j] != 0)
                    upper = false;
                else if (j > i && this[i, j] != 0)
                    lower = false;
            }
        }

        return upper || lower;
    }
}
