using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Primitives;

/// <summary>
/// Эллипс на плоскости.
/// </summary>
/// <param name="Center">Центр эллипса (2D).</param>
/// <param name="A">Большая полуось.</param>
/// <param name="B">Малая полуось.</param>
/// <param name="Angle">Угол наклона главной оси (рад).</param>
public record Ellipse(Vector Center, double A, double B, double Angle)
{
    /// <summary>
    /// Площадь эллипса.
    /// </summary>
    public double Area => Math.PI * A * B;

    /// <summary>
    /// Точка на эллипсе при параметре theta (рад).
    /// </summary>
    public Vector PointAt(double theta)
    {
        double cos = Math.Cos(Angle);
        double sin = Math.Sin(Angle);
        double x = A * Math.Cos(theta);
        double y = B * Math.Sin(theta);
        return new Vector(new[]
        {
            Center[0] + x * cos - y * sin,
            Center[1] + x * sin + y * cos
        });
    }
}
