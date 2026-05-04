using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Curves;

/// <summary>
/// Кривая Безье произвольной степени (алгоритм де Кастельжо).
/// </summary>
public class BezierCurve
{
    private readonly Vector[] _controlPoints;

    /// <summary>
    /// Создаёт кривую Безье по контрольным точкам.
    /// </summary>
    public BezierCurve(Vector[] controlPoints)
    {
        _controlPoints = controlPoints;
    }

    /// <summary>
    /// Вычисляет точку на кривой при параметре t ∈ [0, 1] (алгоритм де Кастельжо).
    /// </summary>
    public Vector Evaluate(double t)
    {
        int n = _controlPoints.Length;
        var points = new Vector[n];
        for (int i = 0; i < n; i++)
            points[i] = _controlPoints[i];

        for (int r = 1; r < n; r++)
            for (int i = 0; i < n - r; i++)
                points[i] = (1 - t) * points[i] + t * points[i + 1];

        return points[0];
    }

    /// <summary>
    /// Равномерная выборка n точек на кривой.
    /// </summary>
    public Vector[] Sample(int n)
    {
        var result = new Vector[n];
        for (int i = 0; i < n; i++)
            result[i] = Evaluate((double)i / (n - 1));
        return result;
    }
}
