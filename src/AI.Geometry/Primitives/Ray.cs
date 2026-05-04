using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Primitives;

/// <summary>
/// Луч, заданный началом и направлением.
/// </summary>
/// <param name="Origin">Начало луча.</param>
/// <param name="Direction">Направление луча.</param>
public record Ray(Vector Origin, Vector Direction)
{
    /// <summary>
    /// Точка на луче при параметре t: O + t * D.
    /// </summary>
    public Vector PointAt(double t)
    {
        return Origin + t * Direction;
    }
}
