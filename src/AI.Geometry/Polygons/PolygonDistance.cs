#nullable enable
using System;
using AI.Geometry.Polylines;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polygons;

/// <summary>
/// Расстояние от точки до многоугольника как до области на плоскости.
/// </summary>
public static class PolygonDistance
{
    /// <summary>
    /// Расстояние от точки до многоугольника: 0 внутри (по числу обмоток, <see cref="PointInPolygon.Contains"/>),
    /// снаружи расстояние до ближайшей стороны.
    /// </summary>
    /// <param name="point">Точка (2D).</param>
    /// <param name="polygon">Вершины многоугольника (2D), обход любой.</param>
    /// <returns>Неотрицательное расстояние.</returns>
    public static double Distance(Vector point, Vector[] polygon)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(polygon);
        if (polygon.Length == 0)
            throw new ArgumentException("У многоугольника нет вершин", nameof(polygon));

        if (PointInPolygon.Contains(point, polygon))
            return 0;

        // Граница многоугольника есть замкнутая ломаная
        var boundary = new Vector[polygon.Length + 1];
        Array.Copy(polygon, boundary, polygon.Length);
        boundary[^1] = polygon[0];
        return Polyline.Distance(point, boundary);
    }
}
