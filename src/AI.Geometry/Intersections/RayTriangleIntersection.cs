using System;
using AI.Geometry.Primitives;
using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение луча и треугольника (алгоритм Мёллера-Трумбора).
/// </summary>
public static class RayTriangleIntersection
{
    /// <summary>
    /// Вычисляет параметр t пересечения луча с треугольником. Возвращает null, если пересечения нет.
    /// </summary>
    public static double? Intersect(Ray ray, Triangle tri)
    {
        var edge1 = tri.B - tri.A;
        var edge2 = tri.C - tri.A;
        var h = Vector.Cross(ray.Direction, edge2);
        double a = Vector.Dot(edge1, h);

        if (Eps.IsZero(a))
            return null;

        double f = 1.0 / a;
        var s = ray.Origin - tri.A;
        double u = f * Vector.Dot(s, h);
        if (u < 0 || u > 1)
            return null;

        var q = Vector.Cross(s, edge1);
        double v = f * Vector.Dot(ray.Direction, q);
        if (v < 0 || u + v > 1)
            return null;

        double t = f * Vector.Dot(edge2, q);
        return t;
    }
}
