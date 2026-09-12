using System;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Конфликт двух агентов вместе с ограничениями, которыми его разрешает каждая из двух ветвей
/// </summary>
[Serializable]
internal readonly record struct MapfConflict(int First, int Second, int T, MapfConstraint ForFirst, MapfConstraint ForSecond)
{
    /// <summary>Оба агента в одной клетке в момент t</summary>
    internal static MapfConflict Vertex(int first, int second, (int X, int Y) cell, int t)
        => new(first, second, t, MapfConstraint.Vertex(first, cell.X, cell.Y, t), MapfConstraint.Vertex(second, cell.X, cell.Y, t));

    /// <summary>Агенты обменялись клетками на шаге к моменту t</summary>
    internal static MapfConflict Edge(int first, int second, (int X, int Y) fromFirst, (int X, int Y) toFirst, int t)
        => new(first, second, t,
            MapfConstraint.Edge(first, fromFirst.X, fromFirst.Y, toFirst.X, toFirst.Y, t),
            MapfConstraint.Edge(second, toFirst.X, toFirst.Y, fromFirst.X, fromFirst.Y, t));
}
