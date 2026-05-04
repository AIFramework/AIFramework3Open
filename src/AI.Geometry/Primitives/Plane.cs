using System;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Primitives;

/// <summary>
/// Плоскость в трёхмерном пространстве: N · X + D = 0.
/// </summary>
/// <param name="Normal">Нормаль к плоскости.</param>
/// <param name="D">Свободный член уравнения.</param>
public record Plane(Vector Normal, double D)
{
    /// <summary>
    /// Создаёт плоскость по точке и нормали.
    /// </summary>
    public static Plane FromPointNormal(Vector point, Vector normal)
    {
        double d = -Vector.Dot(normal, point);
        return new Plane(normal, d);
    }

    /// <summary>
    /// Создаёт плоскость по трём точкам (правая тройка).
    /// </summary>
    public static Plane FromThreePoints(Vector a, Vector b, Vector c)
    {
        var ab = b - a;
        var ac = c - a;
        var n = Vector.Cross(ab, ac);
        double d = -Vector.Dot(n, a);
        return new Plane(n, d);
    }

    /// <summary>
    /// Создаёт плоскость из общего уравнения ax + by + cz + d = 0.
    /// </summary>
    public static Plane FromGeneral(double a, double b, double c, double d)
    {
        return new Plane(new Vector(new[] { a, b, c }), d);
    }

    /// <summary>
    /// Знаковое расстояние от точки до плоскости.
    /// </summary>
    public double SignedDistance(Vector point)
    {
        double dot = Vector.Dot(Normal, point) + D;
        double norm = Math.Sqrt(Vector.Dot(Normal, Normal));
        return dot / norm;
    }

    /// <summary>
    /// Произвольная точка на плоскости.
    /// </summary>
    public Vector PointOnPlane
    {
        get
        {
            double nn = Vector.Dot(Normal, Normal);
            return (-D / nn) * Normal;
        }
    }
}
