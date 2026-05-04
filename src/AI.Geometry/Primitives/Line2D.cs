using System;
using AI.Geometry.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Primitives;

/// <summary>
/// Прямая на плоскости, заданная точкой и направлением.
/// </summary>
/// <param name="Point">Точка на прямой (2D).</param>
/// <param name="Direction">Направляющий вектор (2D).</param>
public record Line2D(Vector Point, Vector Direction)
{
    /// <summary>
    /// Создаёт прямую по двум точкам.
    /// </summary>
    public static Line2D FromTwoPoints(Vector a, Vector b)
    {
        return new Line2D(a, b - a);
    }

    /// <summary>
    /// Создаёт прямую из общего уравнения ax + by + c = 0.
    /// </summary>
    public static Line2D FromGeneral(double a, double b, double c)
    {
        Vector dir = new Vector(new[] { -b, a });
        Vector point;
        if (Math.Abs(a) > Math.Abs(b))
            point = new Vector(new[] { -c / a, 0.0 });
        else
            point = new Vector(new[] { 0.0, -c / b });
        return new Line2D(point, dir);
    }

    /// <summary>
    /// Возвращает коэффициенты общего уравнения (a, b, c): ax + by + c = 0.
    /// </summary>
    public (double a, double b, double c) ToGeneral()
    {
        double a = -Direction[1];
        double b = Direction[0];
        double c = -(a * Point[0] + b * Point[1]);
        return (a, b, c);
    }

    /// <summary>
    /// Точка на прямой при параметре t: P + t * D.
    /// </summary>
    public Vector PointAt(double t)
    {
        return Point + t * Direction;
    }
}
