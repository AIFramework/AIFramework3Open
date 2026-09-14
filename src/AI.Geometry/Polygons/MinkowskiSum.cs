#nullable enable
using System;
using System.Collections.Generic;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polygons;

/// <summary>
/// Сумма Минковского выпуклых многоугольников: множество всех сумм a + b, где a из первого, b из второго.
/// </summary>
/// <remarks>
/// Стороны обоих многоугольников сливаются в порядке полярного угла, сложность O(n + m) после построения оболочек.
/// Применяется для расширения препятствий на размер тела и в проверках столкновений.
/// </remarks>
public static class MinkowskiSum
{
    /// <summary>
    /// Сумма Минковского двух выпуклых многоугольников. Порядок вершин входа любой; от невыпуклого входа берется
    /// выпуклая оболочка. Допустимы вырожденные входы: точка и отрезок.
    /// </summary>
    /// <param name="a">Первый выпуклый многоугольник (2D).</param>
    /// <param name="b">Второй выпуклый многоугольник (2D).</param>
    /// <returns>Вершины суммы против часовой стрелки, как у <see cref="ConvexHull.Compute"/>.</returns>
    public static Vector[] Convex(Vector[] a, Vector[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Length == 0 || b.Length == 0)
            return Array.Empty<Vector>();

        var p = StartAtBottom(ConvexHull.Compute(a));
        var q = StartAtBottom(ConvexHull.Compute(b));
        int n = p.Length, m = q.Length;
        var sums = new List<Vector>(n + m);

        if (n < 3 || m < 3)
        {
            // Вырожденный вход: сумм не больше 2·max(n, m), оболочка по ним дешева
            foreach (var u in p)
            {
                foreach (var v in q)
                    sums.Add(u + v);
            }

            return ConvexHull.Compute(sums.ToArray());
        }

        int i = 0, j = 0;
        while (i < n || j < m)
        {
            sums.Add(p[i % n] + q[j % m]);
            var ep = p[(i + 1) % n] - p[i % n];
            var eq = q[(j + 1) % m] - q[j % m];
            double cross = ep[0] * eq[1] - ep[1] * eq[0];
            if (cross >= 0 && i < n)
                i++;
            if (cross <= 0 && j < m)
                j++;
        }

        // Оболочка убирает вершины на одной прямой, появившиеся при параллельных сторонах
        return ConvexHull.Compute(sums.ToArray());
    }

    /// <summary>Поворачивает цикл вершин так, чтобы первой была самая нижняя (при равенстве самая левая).</summary>
    private static Vector[] StartAtBottom(Vector[] hull)
    {
        int start = 0;
        for (int k = 1; k < hull.Length; k++)
        {
            if (hull[k][1] < hull[start][1] || (hull[k][1] == hull[start][1] && hull[k][0] < hull[start][0]))
                start = k;
        }

        var rotated = new Vector[hull.Length];
        for (int k = 0; k < hull.Length; k++)
            rotated[k] = hull[(start + k) % hull.Length];
        return rotated;
    }
}
