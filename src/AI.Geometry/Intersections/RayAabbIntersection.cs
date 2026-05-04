using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение луча и AABB (метод slab).
/// </summary>
public static class RayAabbIntersection
{
    /// <summary>
    /// Вычисляет параметры t входа и выхода луча из AABB. Возвращает null, если пересечения нет.
    /// </summary>
    public static (double tMin, double tMax)? Intersect(Ray ray, Aabb box)
    {
        int dim = box.Min.Count;
        double tMin = double.NegativeInfinity;
        double tMax = double.PositiveInfinity;

        for (int i = 0; i < dim; i++)
        {
            double invD = 1.0 / ray.Direction[i];
            double t0 = (box.Min[i] - ray.Origin[i]) * invD;
            double t1 = (box.Max[i] - ray.Origin[i]) * invD;

            if (invD < 0)
                (t0, t1) = (t1, t0);

            tMin = Math.Max(tMin, t0);
            tMax = Math.Min(tMax, t1);

            if (tMax < tMin)
                return null;
        }

        return (tMin, tMax);
    }
}
