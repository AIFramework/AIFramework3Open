using AI.Geometry.Primitives;
using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение луча и плоскости.
/// </summary>
public static class RayPlaneIntersection
{
    /// <summary>
    /// Вычисляет параметр t пересечения луча с плоскостью. Возвращает null, если луч параллелен плоскости.
    /// </summary>
    public static double? Intersect(Ray ray, Plane plane)
    {
        double denom = Vector.Dot(plane.Normal, ray.Direction);
        if (Eps.IsZero(denom))
            return null;

        double t = -(Vector.Dot(plane.Normal, ray.Origin) + plane.D) / denom;
        return t;
    }
}
