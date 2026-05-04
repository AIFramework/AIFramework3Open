using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Primitives;

/// <summary>
/// Отрезок между двумя точками.
/// </summary>
/// <param name="A">Первый конец.</param>
/// <param name="B">Второй конец.</param>
public record Segment(Vector A, Vector B)
{
    /// <summary>
    /// Длина отрезка.
    /// </summary>
    public double Length
    {
        get
        {
            var d = B - A;
            return Math.Sqrt(Vector.Dot(d, d));
        }
    }

    /// <summary>
    /// Направляющий вектор отрезка (ненормализованный).
    /// </summary>
    public Vector Direction => B - A;

    /// <summary>
    /// Середина отрезка.
    /// </summary>
    public Vector Midpoint => 0.5 * (A + B);

    /// <summary>
    /// Точка на отрезке при параметре t ∈ [0, 1].
    /// </summary>
    public Vector PointAt(double t)
    {
        return A + t * (B - A);
    }
}
