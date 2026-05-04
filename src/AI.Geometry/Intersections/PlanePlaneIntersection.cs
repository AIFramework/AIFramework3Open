using System;
using AI.Geometry.Primitives;
using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение двух плоскостей — прямая в 3D.
/// </summary>
public static class PlanePlaneIntersection
{
    /// <summary>
    /// Находит прямую пересечения двух плоскостей. Возвращает null, если плоскости параллельны.
    /// </summary>
    public static Line3D Intersect(Plane a, Plane b)
    {
        var dir = Vector.Cross(a.Normal, b.Normal);
        double dirLen2 = Vector.Dot(dir, dir);
        if (Eps.IsZero(dirLen2))
            return null;

        var point = ((-a.D * Vector.Cross(b.Normal, dir)) + (-b.D * Vector.Cross(dir, a.Normal)))
                    * (1.0 / dirLen2);

        // Вычисление точки на прямой пересечения
        // P = ( -d1*(n2 × d) - d2*(d × n1) ) / |d|²
        // Это уже сделано через операторы Vector
        return new Line3D(point, dir);
    }
}
