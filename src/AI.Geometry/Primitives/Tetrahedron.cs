using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Primitives;

/// <summary>
/// Тетраэдр, заданный четырьмя вершинами.
/// </summary>
/// <param name="A">Первая вершина.</param>
/// <param name="B">Вторая вершина.</param>
/// <param name="C">Третья вершина.</param>
/// <param name="D">Четвёртая вершина.</param>
public record Tetrahedron(Vector A, Vector B, Vector C, Vector D)
{
    /// <summary>
    /// Объём тетраэдра (1/6 модуля смешанного произведения).
    /// </summary>
    public double Volume()
    {
        var ab = B - A;
        var ac = C - A;
        var ad = D - A;
        var cross = Vector.Cross(ab, ac);
        return Math.Abs(Vector.Dot(cross, ad)) / 6.0;
    }

    /// <summary>
    /// Центроид (центр масс) тетраэдра.
    /// </summary>
    public Vector Centroid => 0.25 * (A + B + C + D);
}
