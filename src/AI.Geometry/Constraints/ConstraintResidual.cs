#nullable enable

namespace AI.Geometry.Constraints;

/// <summary>
/// Невязка одного ограничения в найденном решении.
/// </summary>
/// <param name="Name">Имя ограничения.</param>
/// <param name="Kind">Вид ограничения (например, distance, tangent).</param>
/// <param name="Value">Евклидова норма невязок блока; ноль означает, что ограничение выполнено.</param>
public sealed record ConstraintResidual(string Name, string Kind, double Value);
