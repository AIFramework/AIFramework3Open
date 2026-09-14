#nullable enable

using AI.Geometry.Primitives;

namespace AI.Geometry.Hull;

/// <summary>
/// Треугольная грань выпуклой оболочки.
/// </summary>
/// <param name="A">Индекс первой вершины в <see cref="ConvexHull3D.Vertices"/>.</param>
/// <param name="B">Индекс второй вершины.</param>
/// <param name="C">Индекс третьей вершины; обход A, B, C против часовой стрелки при взгляде снаружи.</param>
/// <param name="Normal">Единичная внешняя нормаль.</param>
/// <param name="Offset">Свободный член плоскости грани: Normal · x = Offset.</param>
public readonly record struct HullFace(int A, int B, int C, Vector3 Normal, double Offset)
{
    /// <summary>
    /// Знаковое расстояние от точки до плоскости грани: положительно снаружи оболочки.
    /// </summary>
    /// <param name="point">Точка.</param>
    public double SignedDistance(Vector3 point) => Normal.Dot(point) - Offset;
}
