using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Polygons;

/// <summary>
/// Ориентация тройки точек на плоскости.
/// </summary>
public static class Orientation2D
{
    /// <summary>
    /// Определяет ориентацию тройки точек: +1 (CCW), -1 (CW), 0 (коллинеарны).
    /// </summary>
    public static int Orient(Vector a, Vector b, Vector c)
    {
        double cross = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]);
        return Eps.Sign(cross);
    }
}
