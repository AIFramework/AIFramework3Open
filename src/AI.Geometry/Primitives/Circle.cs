using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Primitives;

/// <summary>
/// Окружность на плоскости.
/// </summary>
/// <param name="Center">Центр окружности (2D).</param>
/// <param name="Radius">Радиус.</param>
public record Circle(Vector Center, double Radius)
{
    /// <summary>
    /// Площадь круга.
    /// </summary>
    public double Area => Math.PI * Radius * Radius;

    /// <summary>
    /// Длина окружности.
    /// </summary>
    public double Circumference => 2.0 * Math.PI * Radius;

    /// <summary>
    /// Проверяет, содержит ли круг данную точку.
    /// </summary>
    public bool Contains(Vector point)
    {
        var d = point - Center;
        return Vector.Dot(d, d) <= Radius * Radius;
    }
}
