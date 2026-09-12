using System;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Ограничение ветви дерева CBS: агенту нельзя оказаться в клетке в момент T, а для рёберного
/// ограничения — прийти в неё в момент T именно из клетки (FromX, FromY)
/// </summary>
/// <remarks>
/// Конфликт обмена местами разрешается рёберными ограничениями. Прежде он разрешался двумя
/// вершинными, и они запрещали больше, чем нужно: вместе с обменом — и все решения, где агент
/// приходит в ту же клетку в тот же момент из другой соседней.
/// </remarks>
[Serializable]
internal readonly record struct MapfConstraint(int Agent, int FromX, int FromY, int X, int Y, int T)
{
    /// <summary>Рёберное ли ограничение</summary>
    internal bool IsEdge => FromX >= 0;

    /// <summary>Нельзя быть в клетке в момент t</summary>
    internal static MapfConstraint Vertex(int agent, int x, int y, int t) => new(agent, -1, -1, x, y, t);

    /// <summary>Нельзя прийти в клетку в момент t из заданной соседней</summary>
    internal static MapfConstraint Edge(int agent, int fromX, int fromY, int x, int y, int t) => new(agent, fromX, fromY, x, y, t);
}
