using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Primitives;

/// <summary>
/// Прямая в трёхмерном пространстве, заданная точкой и направлением.
/// </summary>
/// <param name="Point">Точка на прямой (3D).</param>
/// <param name="Direction">Направляющий вектор (3D).</param>
public record Line3D(Vector Point, Vector Direction)
{
    /// <summary>
    /// Создаёт прямую по двум точкам.
    /// </summary>
    public static Line3D FromTwoPoints(Vector a, Vector b)
    {
        return new Line3D(a, b - a);
    }

    /// <summary>
    /// Точка на прямой при параметре t: P + t * D.
    /// </summary>
    public Vector PointAt(double t)
    {
        return Point + t * Direction;
    }
}
