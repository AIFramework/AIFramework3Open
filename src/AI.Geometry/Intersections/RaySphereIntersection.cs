using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение луча и сферы.
/// </summary>
public static class RaySphereIntersection
{
    /// <summary>
    /// Вычисляет параметры t пересечения луча со сферой. Возвращает null, если пересечения нет.
    /// </summary>
    public static (double t1, double t2)? Intersect(Ray ray, Sphere sphere)
    {
        var oc = ray.Origin - sphere.Center;
        double a = Vector.Dot(ray.Direction, ray.Direction);
        double b = 2.0 * Vector.Dot(oc, ray.Direction);
        double c = Vector.Dot(oc, oc) - sphere.Radius * sphere.Radius;
        double disc = b * b - 4 * a * c;

        if (disc < 0)
            return null;

        double sqrtD = Math.Sqrt(disc);
        double t1 = (-b - sqrtD) / (2 * a);
        double t2 = (-b + sqrtD) / (2 * a);
        return (t1, t2);
    }
}
