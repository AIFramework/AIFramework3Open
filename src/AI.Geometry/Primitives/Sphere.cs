using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Primitives;

/// <summary>
/// Сфера в трёхмерном пространстве.
/// </summary>
/// <param name="Center">Центр сферы (3D).</param>
/// <param name="Radius">Радиус.</param>
public record Sphere(Vector Center, double Radius)
{
    /// <summary>
    /// Объём шара.
    /// </summary>
    public double Volume => (4.0 / 3.0) * Math.PI * Radius * Radius * Radius;

    /// <summary>
    /// Площадь поверхности сферы.
    /// </summary>
    public double SurfaceArea => 4.0 * Math.PI * Radius * Radius;

    /// <summary>
    /// Проверяет, содержит ли шар данную точку.
    /// </summary>
    public bool Contains(Vector point)
    {
        var d = point - Center;
        return Vector.Dot(d, d) <= Radius * Radius;
    }
}
