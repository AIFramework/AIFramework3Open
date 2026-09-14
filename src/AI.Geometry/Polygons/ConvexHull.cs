#nullable enable
using System;
using System.Collections.Generic;
using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polygons;

/// <summary>
/// Выпуклая оболочка множества точек на плоскости (монотонная цепь Эндрю).
/// </summary>
/// <remarks>
/// Повороты проверяются точным предикатом <see cref="RobustPredicates.Orient2D(double, double, double, double, double, double)"/>,
/// поэтому почти коллинеарные точки не ломают оболочку. Сложность O(n log n).
/// </remarks>
public static class ConvexHull
{
    /// <summary>
    /// Вершины выпуклой оболочки против часовой стрелки, начиная с самой левой (при равенстве самой нижней) точки.
    /// Точки на сторонах оболочки и повторы не входят. Для одной различной точки возвращается она сама, для точек
    /// на одной прямой два крайних конца.
    /// </summary>
    /// <param name="points">Точки (2D).</param>
    /// <returns>Новые векторы вершин оболочки.</returns>
    public static Vector[] Compute(Vector[] points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var sorted = new (double X, double Y)[points.Length];
        for (int i = 0; i < points.Length; i++)
            sorted[i] = (points[i][0], points[i][1]);
        Array.Sort(sorted);

        if (sorted.Length == 0)
            return Array.Empty<Vector>();

        var hull = new List<(double X, double Y)>(2 * sorted.Length);

        // Нижняя цепь слева направо, затем верхняя справа налево; поворот направо или по прямой выбрасывает точку
        for (int pass = 0; pass < 2; pass++)
        {
            int start = hull.Count;
            for (int k = 0; k < sorted.Length; k++)
            {
                var p = sorted[pass == 0 ? k : sorted.Length - 1 - k];
                while (hull.Count >= start + 2 && Turn(hull[^2], hull[^1], p) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }

            // Последняя точка цепи совпадает с первой точкой следующей
            hull.RemoveAt(hull.Count - 1);
        }

        // Все точки совпадают: цепи схлопнулись, а для двух различных точек остаются оба конца
        if (hull.Count == 2 && hull[0] == hull[1])
            hull.RemoveAt(1);
        if (hull.Count == 0)
            hull.Add(sorted[0]);

        var result = new Vector[hull.Count];
        for (int i = 0; i < hull.Count; i++)
            result[i] = new Vector(hull[i].X, hull[i].Y);
        return result;
    }

    private static int Turn((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
    {
        return RobustPredicates.Orient2D(a.X, a.Y, b.X, b.Y, c.X, c.Y);
    }
}
