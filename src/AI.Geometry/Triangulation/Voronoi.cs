#nullable enable
using System;
using System.Collections.Generic;
using AI.Geometry.Polygons;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Triangulation;

/// <summary>
/// Ячейки Вороного, построенные как двойственный граф триангуляции Делоне и обрезанные прямоугольником.
/// </summary>
/// <remarks>
/// Соседи точки по Вороному совпадают с ее соседями по Делоне, поэтому ячейка точки есть прямоугольник, отсеченный
/// полуплоскостями серединных перпендикуляров к ребрам Делоне из этой точки (<see cref="PolygonClipping.HalfPlane"/>).
/// Так бесконечные ячейки на краю оболочки обрезаются без отдельной обработки лучей.
/// </remarks>
public static class Voronoi
{
    /// <summary>
    /// Ячейки Вороного всех точек внутри прямоугольника [min, max]: множество точек прямоугольника, для которых
    /// данная точка ближайшая. Совпадающие точки получают одинаковые ячейки.
    /// </summary>
    /// <param name="points">Точки (2D).</param>
    /// <param name="min">Нижний левый угол прямоугольника (2D).</param>
    /// <param name="max">Верхний правый угол прямоугольника (2D).</param>
    /// <returns>Ячейка каждой точки против часовой стрелки; пустой массив, если ячейка не задевает прямоугольник.</returns>
    public static Vector[][] Cells(Vector[] points, Vector min, Vector max)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(min);
        ArgumentNullException.ThrowIfNull(max);
        if (!(min[0] < max[0] && min[1] < max[1]))
            throw new ArgumentException("Прямоугольник пуст: min должен быть меньше max по обеим осям", nameof(max));

        int n = points.Length;
        var representative = new int[n];
        var first = new Dictionary<(double, double), int>();
        for (int i = 0; i < n; i++)
        {
            var key = (points[i][0], points[i][1]);
            if (!first.TryGetValue(key, out representative[i]))
            {
                first[key] = i;
                representative[i] = i;
            }
        }

        var neighbors = new HashSet<int>[n];
        for (int i = 0; i < n; i++)
            neighbors[i] = new HashSet<int>();

        var triangles = Delaunay.Triangulate(points);
        foreach (var (a, b, c) in triangles)
        {
            Link(a, b);
            Link(b, c);
            Link(c, a);
        }

        if (triangles.Count == 0)
        {
            // Все точки на одной прямой: соседи идут подряд в порядке (x, y)
            var distinct = new List<int>(first.Values);
            distinct.Sort((i, j) => (points[i][0], points[i][1]).CompareTo((points[j][0], points[j][1])));
            for (int k = 1; k < distinct.Count; k++)
                Link(distinct[k - 1], distinct[k]);
        }

        Vector[] box =
        [
            new Vector(min[0], min[1]),
            new Vector(max[0], min[1]),
            new Vector(max[0], max[1]),
            new Vector(min[0], max[1]),
        ];

        var cells = new Vector[n][];
        for (int i = 0; i < n; i++)
        {
            int site = representative[i];
            if (site != i)
            {
                cells[i] = Copy(cells[site]);
                continue;
            }

            var cell = box;
            foreach (int other in neighbors[site])
            {
                if (cell.Length == 0)
                    break;

                // Направление повернуто от (other − site) на 90° против часовой: слева остается сторона site
                double ux = points[other][0] - points[site][0], uy = points[other][1] - points[site][1];
                var middle = new Vector((points[site][0] + points[other][0]) / 2, (points[site][1] + points[other][1]) / 2);
                cell = PolygonClipping.HalfPlane(cell, new Line2D(middle, new Vector(-uy, ux)));
            }

            cells[i] = cell == box ? Copy(box) : cell;
        }

        return cells;

        void Link(int a, int b)
        {
            neighbors[a].Add(b);
            neighbors[b].Add(a);
        }
    }

    private static Vector[] Copy(Vector[] polygon)
    {
        var copy = new Vector[polygon.Length];
        for (int k = 0; k < polygon.Length; k++)
            copy[k] = new Vector(polygon[k]);
        return copy;
    }
}
