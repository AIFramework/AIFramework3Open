#nullable enable

using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Distances;

/// <summary>
/// Ближайшие точки и расстояние между двумя отрезками в 3D.
/// </summary>
/// <remarks>
/// Минимизируется |a0 + s·(a1 − a0) − b0 − t·(b1 − b0)|² при s, t ∈ [0, 1] (Эриксон, «Real-Time Collision Detection», 5.1.9).
/// Параллельные и вырожденные в точку отрезки обрабатываются отдельно.
/// </remarks>
public static class SegmentSegment
{
    /// <summary>
    /// Ближайшие точки двух отрезков.
    /// </summary>
    /// <param name="a0">Начало первого отрезка.</param>
    /// <param name="a1">Конец первого отрезка.</param>
    /// <param name="b0">Начало второго отрезка.</param>
    /// <param name="b1">Конец второго отрезка.</param>
    /// <returns>Параметры s и t ближайших точек и сами точки на первом и втором отрезке.</returns>
    public static (double S, double T, Vector3 OnA, Vector3 OnB) ClosestPoints(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
    {
        Vector3 d1 = a1 - a0;
        Vector3 d2 = b1 - b0;
        Vector3 r = a0 - b0;
        double a = d1.Dot(d1);
        double e = d2.Dot(d2);
        double f = d2.Dot(r);
        double s, t;

        if (a == 0 && e == 0)
        {
            s = 0;
            t = 0;
        }
        else if (a == 0)
        {
            s = 0;
            t = Math.Clamp(f / e, 0, 1);
        }
        else
        {
            double c = d1.Dot(r);

            if (e == 0)
            {
                t = 0;
                s = Math.Clamp(-c / a, 0, 1);
            }
            else
            {
                double b = d1.Dot(d2);
                double denominator = (a * e) - (b * b);

                // У параллельных отрезков годится любая точка первого: берется начало
                s = denominator > 1e-14 * a * e ? Math.Clamp(((b * f) - (c * e)) / denominator, 0, 1) : 0;
                t = ((b * s) + f) / e;

                if (t < 0)
                {
                    t = 0;
                    s = Math.Clamp(-c / a, 0, 1);
                }
                else if (t > 1)
                {
                    t = 1;
                    s = Math.Clamp((b - c) / a, 0, 1);
                }
            }
        }

        return (s, t, a0 + (d1 * s), b0 + (d2 * t));
    }

    /// <summary>
    /// Расстояние между двумя отрезками.
    /// </summary>
    /// <param name="a0">Начало первого отрезка.</param>
    /// <param name="a1">Конец первого отрезка.</param>
    /// <param name="b0">Начало второго отрезка.</param>
    /// <param name="b1">Конец второго отрезка.</param>
    public static double Distance(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
    {
        var (_, _, onA, onB) = ClosestPoints(a0, a1, b0, b1);
        return onA.DistanceTo(onB);
    }

    /// <summary>
    /// Ближайшие точки двух трехмерных отрезков.
    /// </summary>
    /// <param name="a">Первый отрезок.</param>
    /// <param name="b">Второй отрезок.</param>
    /// <returns>Точки на первом и втором отрезке.</returns>
    public static (Vector OnA, Vector OnB) ClosestPoints(Segment a, Segment b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var (_, _, onA, onB) = ClosestPoints(
            Vector3.FromVector(a.A), Vector3.FromVector(a.B), Vector3.FromVector(b.A), Vector3.FromVector(b.B));
        return (onA.ToVector(), onB.ToVector());
    }

    /// <summary>
    /// Расстояние между двумя трехмерными отрезками.
    /// </summary>
    /// <param name="a">Первый отрезок.</param>
    /// <param name="b">Второй отрезок.</param>
    public static double Distance(Segment a, Segment b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return Distance(Vector3.FromVector(a.A), Vector3.FromVector(a.B), Vector3.FromVector(b.A), Vector3.FromVector(b.B));
    }
}
