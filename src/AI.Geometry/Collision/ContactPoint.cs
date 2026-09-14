#nullable enable
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Точка касания двух тел
/// </summary>
/// <param name="Position">Точка посередине между поверхностями тел</param>
/// <param name="Depth">Глубина проникновения в этой точке вдоль нормали касания</param>
/// <param name="FeatureId">
/// Номер пары элементов (вершина, ребро, грань), давших точку: совпадает между шагами, пока касаются те же элементы,
/// и служит для теплого старта решателя; <c>null</c>, если номер неизвестен
/// </param>
public readonly record struct ContactPoint(Vector3 Position, double Depth, int? FeatureId = null);
