using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Общая основа решателей: A* в пространстве-времени с вершинными и рёберными ограничениями
/// или с таблицей резервирования, поиск конфликтов и запасной ответ
/// </summary>
/// <remarks>
/// CBS, ICBS и ECBS прежде держали по собственной копии нижнего уровня, и во всех трёх была одна
/// и та же ошибка. Теперь нижний уровень один. Агент, дошедший до цели, стоит на ней всегда,
/// поэтому путь принимается, только если цель свободна от ограничений на всё оставшееся время.
/// </remarks>
internal static class SpaceTimePlanner
{
    /// <summary>Позиция агента в момент t; после конца пути — последняя клетка</summary>
    internal static (int X, int Y) Position(List<(int X, int Y)> path, int t) => t < path.Count ? path[t] : path[^1];

    /// <summary>Горизонт поиска: столько шагов хватит, чтобы переждать любого другого агента</summary>
    internal static int Horizon(GridMap map, int agents) => (map.Width * map.Height) + agents;

    /// <summary>Достижима ли цель без учёта других агентов</summary>
    internal static bool Reachable(GridMap map, int sx, int sy, int gx, int gy)
    {
        if (!map.InBounds(sx, sy) || !map.InBounds(gx, gy) || map.IsBlocked(sx, sy) || map.IsBlocked(gx, gy))
            return false;

        var seen = new bool[map.Width, map.Height];
        var queue = new Queue<(int X, int Y)>();
        seen[sx, sy] = true;
        queue.Enqueue((sx, sy));

        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();

            if (x == gx && y == gy)
                return true;

            foreach ((int nx, int ny) in map.Neighbors(x, y))
            {
                if (!seen[nx, ny])
                {
                    seen[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return false;
    }

    /// <summary>Расстояния по сетке до цели обходом в ширину; недостижимые клетки — int.MaxValue</summary>
    internal static int[,] DistancesTo(GridMap map, int gx, int gy)
    {
        var distance = new int[map.Width, map.Height];

        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                distance[x, y] = int.MaxValue;

        if (!map.InBounds(gx, gy) || map.IsBlocked(gx, gy))
            return distance;

        var queue = new Queue<(int X, int Y)>();
        distance[gx, gy] = 0;
        queue.Enqueue((gx, gy));

        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();

            foreach ((int nx, int ny) in map.Neighbors(x, y))
            {
                if (distance[nx, ny] == int.MaxValue)
                {
                    distance[nx, ny] = distance[x, y] + 1;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return distance;
    }

    /// <summary>
    /// Кратчайший путь агента с учётом ограничений его ветви; пустой список, если пути нет
    /// </summary>
    internal static List<(int X, int Y)> Search(
        GridMap map, MAPFAgent agent, int agentId, IEnumerable<MapfConstraint> constraints, int horizon)
    {
        var own = new HashSet<MapfConstraint>();
        int latestGoalBan = -1;
        int latest = 0;

        foreach (MapfConstraint c in constraints)
        {
            if (c.Agent != agentId)
                continue;

            own.Add(c);
            latest = Math.Max(latest, c.T);

            if (!c.IsEdge && c.X == agent.GoalX && c.Y == agent.GoalY)
                latestGoalBan = Math.Max(latestGoalBan, c.T);
        }

        int limit = horizon + latest;

        bool Allowed(int fromX, int fromY, int x, int y, int t)
            => !own.Contains(MapfConstraint.Vertex(agentId, x, y, t))
                && !own.Contains(MapfConstraint.Edge(agentId, fromX, fromY, x, y, t));

        // Цель принимается, только если на ней нет запретов позже: агент там и останется
        return AStar(map, agent, limit,
            Allowed,
            (x, y, t) => x == agent.GoalX && y == agent.GoalY && t > latestGoalBan,
            (x, y, t) => false);
    }

    /// <summary>
    /// Путь агента в обход таблицы резервирования: либо до цели со стоянкой навсегда, либо, если
    /// задано окно, до его конца; null, если пути нет
    /// </summary>
    internal static List<(int X, int Y)> SearchAgainst(
        GridMap map, MAPFAgent agent, (int X, int Y) start, ReservationTable table, int horizon, int window = -1)
    {
        var mover = new MAPFAgent { Id = agent.Id, StartX = start.X, StartY = start.Y, GoalX = agent.GoalX, GoalY = agent.GoalY };

        // Стоять на цели можно лишь после того, как её покинут все спланированные раньше
        int limit = window >= 0 ? window : table.LastTime + horizon;

        List<(int X, int Y)> path = AStar(map, mover, limit,
            (fx, fy, x, y, t) => table.CanMove(fx, fy, x, y, t),
            (x, y, t) => x == agent.GoalX && y == agent.GoalY && (window >= 0 ? NoLaterUse(table, x, y, t, window) : table.CanPark(x, y, t)),
            (x, y, t) => window >= 0 && t >= window);

        return path.Count > 0 ? path : null;
    }

    /// <summary>Все конфликты путей по возрастанию времени</summary>
    internal static IEnumerable<MapfConflict> Conflicts(IReadOnlyList<List<(int X, int Y)>> paths)
    {
        if (paths.Count == 0)
            yield break;

        int maxT = paths.Max(p => p.Count);

        for (int t = 0; t < maxT; t++)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                (int X, int Y) pi = Position(paths[i], t);

                for (int j = i + 1; j < paths.Count; j++)
                {
                    (int X, int Y) pj = Position(paths[j], t);

                    if (pi == pj)
                    {
                        yield return MapfConflict.Vertex(i, j, pi, t);
                    }
                    else if (t > 0)
                    {
                        (int X, int Y) qi = Position(paths[i], t - 1);
                        (int X, int Y) qj = Position(paths[j], t - 1);

                        if (pi == qj && pj == qi)
                            yield return MapfConflict.Edge(i, j, qi, pi, t);
                    }
                }
            }
        }
    }

    /// <summary>Первый по времени конфликт; null, если путей без конфликтов</summary>
    internal static MapfConflict? FirstConflict(IReadOnlyList<List<(int X, int Y)>> paths)
    {
        foreach (MapfConflict conflict in Conflicts(paths))
            return conflict;

        return null;
    }

    /// <summary>
    /// Запасной ответ, когда решения не нашлось: независимые кратчайшие пути, у недостижимых
    /// целей — стоянка на старте. Такое решение не проходит <see cref="MAPFSolution.IsValid"/>
    /// </summary>
    internal static MAPFSolution Independent(GridMap map, List<MAPFAgent> agents, int horizon)
    {
        var paths = new List<List<(int X, int Y)>>(agents.Count);

        for (int i = 0; i < agents.Count; i++)
        {
            MAPFAgent agent = agents[i];
            List<(int X, int Y)> path = Reachable(map, agent.StartX, agent.StartY, agent.GoalX, agent.GoalY)
                ? Search(map, agent, i, Array.Empty<MapfConstraint>(), horizon)
                : new List<(int X, int Y)>();

            paths.Add(path.Count > 0 ? path : new List<(int X, int Y)> { (agent.StartX, agent.StartY) });
        }

        return new MAPFSolution { Paths = paths };
    }

    /// <summary>Выравнивает пути по длине, дописывая последнюю клетку</summary>
    internal static List<List<(int X, int Y)>> Pad(IEnumerable<List<(int X, int Y)>> paths)
    {
        List<List<(int X, int Y)>> list = paths.ToList();
        int length = list.Count == 0 ? 0 : list.Max(p => p.Count);

        foreach (List<(int X, int Y)> path in list)
        {
            while (path.Count < length)
                path.Add(path[^1]);
        }

        return list;
    }

    private static bool NoLaterUse(ReservationTable table, int x, int y, int t, int window)
    {
        for (int later = t + 1; later <= window; later++)
        {
            if (!table.IsFree(x, y, later))
                return false;
        }

        return true;
    }

    private static List<(int X, int Y)> AStar(
        GridMap map,
        MAPFAgent agent,
        int limit,
        Func<int, int, int, int, int, bool> canMove,
        Func<int, int, int, bool> isGoal,
        Func<int, int, int, bool> isWindowEnd)
    {
        int sx = agent.StartX, sy = agent.StartY, gx = agent.GoalX, gy = agent.GoalY;

        if (!canMove(sx, sy, sx, sy, 0) && !isWindowEnd(sx, sy, 0))
            return new List<(int X, int Y)>();

        int[,] distance = DistancesTo(map, gx, gy);

        int H(int x, int y) => distance[x, y] == int.MaxValue ? int.MaxValue / 4 : distance[x, y];

        var open = new PriorityQueue<(int X, int Y, int T), (int F, int H)>();
        var parent = new Dictionary<(int X, int Y, int T), (int X, int Y, int T)>();
        var closed = new HashSet<(int X, int Y, int T)>();

        open.Enqueue((sx, sy, 0), (H(sx, sy), H(sx, sy)));
        parent[(sx, sy, 0)] = (-1, -1, -1);

        while (open.TryDequeue(out (int X, int Y, int T) state, out _))
        {
            if (!closed.Add(state))
                continue;

            (int x, int y, int t) = state;

            if (isGoal(x, y, t) || isWindowEnd(x, y, t))
                return Reconstruct(state, parent);

            if (t >= limit)
                continue;

            int nt = t + 1;
            IEnumerable<(int X, int Y)> moves = map.Neighbors(x, y).Prepend((x, y));

            foreach ((int nx, int ny) in moves)
            {
                var next = (nx, ny, nt);

                if (closed.Contains(next) || parent.ContainsKey(next))
                    continue;

                if (!canMove(x, y, nx, ny, nt))
                    continue;

                parent[next] = state;
                int h = H(nx, ny);
                open.Enqueue(next, (nt + h, h));
            }
        }

        return new List<(int X, int Y)>();
    }

    private static List<(int X, int Y)> Reconstruct((int X, int Y, int T) goal, Dictionary<(int X, int Y, int T), (int X, int Y, int T)> parent)
    {
        var path = new List<(int X, int Y)>();

        for ((int X, int Y, int T) cur = goal; cur.T >= 0; cur = parent[cur])
            path.Add((cur.X, cur.Y));

        path.Reverse();

        return path;
    }
}
