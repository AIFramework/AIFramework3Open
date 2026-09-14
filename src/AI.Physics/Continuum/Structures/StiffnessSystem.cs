#nullable enable
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;

namespace AI.Physics.Continuum.Structures;

/// <summary>
/// Система K·u = F метода жесткостей для ферм и рам.
/// </summary>
/// <remarks>
/// Закрепленные степени свободы исключаются, строки и столбцы масштабируются по диагонали (S·K·S, s = 1/√Kᵢᵢ),
/// чтобы порог вырожденности LU работал относительно жесткости, а не в абсолютных числах. Степень свободы без
/// жесткости (плоская ферма поперек своей плоскости) закрепляется сама, если на нее нет нагрузки. Изменяемая
/// конструкция распознается трижды: нагрузка на степень свободы без жесткости и без опоры, вырожденная матрица
/// и перемещение больше сотой доли размера конструкции.
/// </remarks>
internal static class StiffnessSystem
{
    /// <summary>Диагональ меньше этой доли наибольшей: у степени свободы нет жесткости</summary>
    private const double Stiffless = 1e-12;

    /// <summary>Перемещение больше этой доли размера конструкции: линейный расчет неприменим</summary>
    private const double MaxDrift = 1e-2;

    /// <summary>Решает систему метода жесткостей</summary>
    /// <param name="stiffness">Матрица жесткости</param>
    /// <param name="loads">Узловые нагрузки</param>
    /// <param name="supported">Степень свободы закреплена опорой</param>
    /// <param name="describe">Словами: где эта степень свободы</param>
    /// <param name="size">Размер конструкции, м</param>
    /// <param name="translation">Степень свободы есть сдвиг, а не поворот</param>
    /// <param name="what">Что считается: ферма, рама</param>
    /// <returns>Перемещения и все закрепленные степени свободы: опоры и оси без жесткости</returns>
    public static (Vector Displacement, HashSet<int> Fixed) Solve(Matrix stiffness, double[] loads, Func<int, bool> supported,
        Func<int, string> describe, double size, Func<int, bool> translation, string what)
    {
        int count = loads.Length;
        double scale = Enumerable.Range(0, count).Select(dof => Math.Abs(stiffness[dof, dof])).DefaultIfEmpty(0).Max();
        double heaviest = loads.Select(Math.Abs).DefaultIfEmpty(0).Max();
        HashSet<int> fixedDofs = Enumerable.Range(0, count)
            .Where(dof => supported(dof) || Math.Abs(stiffness[dof, dof]) <= Stiffless * scale)
            .ToHashSet();

        int loose = fixedDofs.FirstOrDefault(dof => !supported(dof) && heaviest > 0 && Math.Abs(loads[dof]) > 1e-9 * heaviest, -1);
        if (loose >= 0)
            throw new InvalidOperationException($"Конструкция ({what}) изменяема: нагрузку {describe(loose)} ничто не держит");

        double[] factor = Enumerable.Range(0, count)
            .Select(dof => fixedDofs.Contains(dof) ? 1 : 1 / Math.Sqrt(Math.Abs(stiffness[dof, dof])))
            .ToArray();
        var reduced = new Matrix(count, count);
        var rhs = new Vector(count);

        for (int row = 0; row < count; row++)
        {
            rhs[row] = fixedDofs.Contains(row) ? 0 : loads[row] * factor[row];

            for (int column = 0; column < count; column++)
            {
                reduced[row, column] = fixedDofs.Contains(row) || fixedDofs.Contains(column)
                    ? (row == column ? 1 : 0)
                    : stiffness[row, column] * factor[row] * factor[column];
            }
        }

        Vector scaled;
        try
        {
            scaled = LU.Solve(reduced, rhs);
        }
        catch (Exception error) when (error is ArithmeticException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidOperationException($"Конструкция ({what}) изменяема: узлы могут двигаться без деформации элементов", error);
        }

        var displacement = new Vector(count);
        for (int dof = 0; dof < count; dof++)
            displacement[dof] = scaled[dof] * factor[dof];

        double drift = Enumerable.Range(0, count).Where(translation).Select(dof => Math.Abs(displacement[dof])).DefaultIfEmpty(0).Max();
        if (!displacement.All(double.IsFinite) || (size > 0 && drift > MaxDrift * size))
        {
            throw new InvalidOperationException(
                $"Конструкция ({what}) изменяема или перегружена: перемещения больше сотой доли ее размера, линейный расчет неприменим");
        }

        return (displacement, fixedDofs);
    }
}
