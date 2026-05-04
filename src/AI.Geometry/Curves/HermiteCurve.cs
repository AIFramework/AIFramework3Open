using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Curves;

/// <summary>
/// Кусочно-кубическая кривая Эрмита.
/// </summary>
public class HermiteCurve
{
    private readonly (Vector point, Vector tangent)[] _segments;

    /// <summary>
    /// Создаёт кривую Эрмита по массиву (точка, касательная).
    /// </summary>
    public HermiteCurve((Vector point, Vector tangent)[] segments)
    {
        _segments = segments;
    }

    /// <summary>
    /// Вычисляет точку на кривой при глобальном параметре t ∈ [0, segments-1].
    /// </summary>
    public Vector Evaluate(double t)
    {
        int n = _segments.Length - 1;
        if (n < 1) return _segments[0].point;

        t = Math.Max(0, Math.Min(n, t));
        int seg = (int)Math.Floor(t);
        if (seg >= n) seg = n - 1;
        double s = t - seg;

        var p0 = _segments[seg].point;
        var m0 = _segments[seg].tangent;
        var p1 = _segments[seg + 1].point;
        var m1 = _segments[seg + 1].tangent;

        double s2 = s * s, s3 = s2 * s;
        double h00 = 2 * s3 - 3 * s2 + 1;
        double h10 = s3 - 2 * s2 + s;
        double h01 = -2 * s3 + 3 * s2;
        double h11 = s3 - s2;

        return h00 * p0 + h10 * m0 + h01 * p1 + h11 * m1;
    }

    /// <summary>
    /// Равномерная выборка n точек на кривой.
    /// </summary>
    public Vector[] Sample(int n)
    {
        int segments = _segments.Length - 1;
        var result = new Vector[n];
        for (int i = 0; i < n; i++)
            result[i] = Evaluate((double)i / (n - 1) * segments);
        return result;
    }
}
