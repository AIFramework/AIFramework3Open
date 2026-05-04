using System;
using AI.Geometry.Primitives;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Distances;

/// <summary>
/// Расстояние между двумя прямыми в 3D (скрещивающиеся прямые).
/// </summary>
public static class LineLine
{
    /// <summary>
    /// Минимальное расстояние между двумя прямыми в 3D.
    /// </summary>
    public static double Distance(Line3D a, Line3D b)
    {
        var cross = Vector.Cross(a.Direction, b.Direction);
        double crossLen2 = Vector.Dot(cross, cross);

        if (crossLen2 < 1e-20)
        {
            var ap = b.Point - a.Point;
            var c2 = Vector.Cross(ap, a.Direction);
            return Math.Sqrt(Vector.Dot(c2, c2)) / Math.Sqrt(Vector.Dot(a.Direction, a.Direction));
        }

        var w0 = a.Point - b.Point;
        return Math.Abs(Vector.Dot(cross, w0)) / Math.Sqrt(crossLen2);
    }

    /// <summary>
    /// Ближайшие точки на двух прямых.
    /// </summary>
    public static (Vector onA, Vector onB) ClosestPoints(Line3D a, Line3D b)
    {
        var w0 = a.Point - b.Point;
        double da = Vector.Dot(a.Direction, a.Direction);
        double db = Vector.Dot(b.Direction, b.Direction);
        double dab = Vector.Dot(a.Direction, b.Direction);
        double dw_a = Vector.Dot(a.Direction, w0);
        double dw_b = Vector.Dot(b.Direction, w0);

        double denom = da * db - dab * dab;
        double ta, tb;

        if (Math.Abs(denom) < 1e-20)
        {
            ta = 0;
            tb = dw_b / db;
        }
        else
        {
            ta = (dab * dw_b - db * dw_a) / denom;
            tb = (da * dw_b - dab * dw_a) / denom;
        }

        return (a.PointAt(ta), b.PointAt(tb));
    }
}
