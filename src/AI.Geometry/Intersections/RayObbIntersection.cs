using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Intersections;

/// <summary>
/// Пересечение луча и OBB (преобразование в локальное пространство + метод slab).
/// </summary>
public static class RayObbIntersection
{
    /// <summary>
    /// Вычисляет параметры t входа и выхода луча из OBB. Возвращает null, если пересечения нет.
    /// </summary>
    public static (double tMin, double tMax)? Intersect(Ray ray, Obb box)
    {
        var diff = ray.Origin - box.Center;

        double tMin = double.NegativeInfinity;
        double tMax = double.PositiveInfinity;

        for (int i = 0; i < 3; i++)
        {
            var axis = new Vector(new[] { box.Rotation[0, i], box.Rotation[1, i], box.Rotation[2, i] });
            double e = Vector.Dot(axis, diff);
            double f = Vector.Dot(axis, ray.Direction);

            if (Math.Abs(f) > 1e-15)
            {
                double t0 = (-e - box.HalfExtents[i]) / f;
                double t1 = (-e + box.HalfExtents[i]) / f;
                if (t0 > t1) (t0, t1) = (t1, t0);
                tMin = Math.Max(tMin, t0);
                tMax = Math.Min(tMax, t1);
                if (tMin > tMax) return null;
            }
            else
            {
                if (-e - box.HalfExtents[i] > 0 || -e + box.HalfExtents[i] < 0)
                    return null;
            }
        }

        return (tMin, tMax);
    }
}
