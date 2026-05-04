using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Curves;

/// <summary>
/// Кривая Кэтмулла-Рома (центрипетальная параметризация).
/// </summary>
public class CatmullRomCurve
{
    private readonly Vector[] _points;
    private readonly double _alpha;

    /// <summary>
    /// Создаёт кривую Кэтмулла-Рома.
    /// </summary>
    /// <param name="points">Контрольные точки (минимум 4).</param>
    /// <param name="alpha">Параметр (0 — uniform, 0.5 — centripetal, 1 — chordal).</param>
    public CatmullRomCurve(Vector[] points, double alpha = 0.5)
    {
        _points = points;
        _alpha = alpha;
    }

    /// <summary>
    /// Вычисляет точку на кривой при параметре t ∈ [0, N-3], где N = число точек.
    /// </summary>
    public Vector Evaluate(double t)
    {
        int n = _points.Length;
        int segments = n - 3;
        if (segments < 1) throw new InvalidOperationException("Необходимо минимум 4 точки.");

        t = Math.Max(0, Math.Min(segments, t));
        int seg = (int)Math.Floor(t);
        if (seg >= segments) seg = segments - 1;
        double s = t - seg;

        var p0 = _points[seg];
        var p1 = _points[seg + 1];
        var p2 = _points[seg + 2];
        var p3 = _points[seg + 3];

        return CatmullRomSegment(p0, p1, p2, p3, s);
    }

    /// <summary>
    /// Равномерная выборка n точек на кривой.
    /// </summary>
    public Vector[] Sample(int n)
    {
        int segments = _points.Length - 3;
        var result = new Vector[n];
        for (int i = 0; i < n; i++)
            result[i] = Evaluate((double)i / (n - 1) * segments);
        return result;
    }

    private Vector CatmullRomSegment(Vector p0, Vector p1, Vector p2, Vector p3, double t)
    {
        double t0 = 0;
        double t1 = t0 + Knot(p0, p1);
        double t2 = t1 + Knot(p1, p2);
        double t3 = t2 + Knot(p2, p3);

        double u = t1 + t * (t2 - t1);

        var a1 = Lerp(p0, p1, t0, t1, u);
        var a2 = Lerp(p1, p2, t1, t2, u);
        var a3 = Lerp(p2, p3, t2, t3, u);

        var b1 = Lerp(a1, a2, t0, t2, u);
        var b2 = Lerp(a2, a3, t1, t3, u);

        return Lerp(b1, b2, t1, t2, u);
    }

    private double Knot(Vector a, Vector b)
    {
        var d = b - a;
        double dist2 = Vector.Dot(d, d);
        return Math.Pow(dist2, _alpha * 0.5);
    }

    private static Vector Lerp(Vector a, Vector b, double ta, double tb, double t)
    {
        double f = (t - ta) / (tb - ta);
        return (1 - f) * a + f * b;
    }
}
