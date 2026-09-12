using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Conflict-Based Search (CBS) — оптимальный алгоритм многоагентного поиска пути.
/// Верхний уровень строит дерево ограничений (constraint tree),
/// нижний уровень использует A* в пространстве-времени.
/// </summary>
/// <remarks>
/// <para>
/// Оптимален по сумме длин путей: конфликт в клетке разрешается вершинными ограничениями,
/// обмен местами — рёберными. Прежде обмен разрешался двумя вершинными ограничениями: они
/// запрещают больше, чем сам конфликт, и могут отсечь оптимальное решение.
/// </para>
/// <para>
/// Если цель недостижима или время вышло, возвращаются независимые кратчайшие пути —
/// такое решение не проходит <see cref="MAPFSolution.IsValid"/>. Прежде недостижимая цель
/// вызывала исключение.
/// </para>
/// </remarks>
[Serializable]
public class CBS
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly int _timeLimit;

    /// <summary>
    /// Создаёт решатель CBS.
    /// </summary>
    /// <param name="map">Карта.</param>
    /// <param name="agents">Список агентов.</param>
    /// <param name="timeLimit">Лимит по времени в миллисекундах.</param>
    public CBS(GridMap map, List<MAPFAgent> agents, int timeLimit = 1000)
    {
        _map = map;
        _agents = agents;
        _timeLimit = timeLimit;
    }

    /// <summary>
    /// Запускает поиск решения.
    /// </summary>
    public MAPFSolution Solve()
    {
        var sw = Stopwatch.StartNew();
        int n = _agents.Count;

        if (n == 0)
            return new MAPFSolution();

        int horizon = SpaceTimePlanner.Horizon(_map, n);
        CTNode root = Root(horizon);

        if (root == null)
            return SpaceTimePlanner.Independent(_map, _agents, horizon);

        var open = new PriorityQueue<CTNode, (int Cost, long Order)>();
        long order = 0;
        open.Enqueue(root, (root.Cost, order++));

        while (sw.ElapsedMilliseconds < _timeLimit && open.TryDequeue(out CTNode node, out _))
        {
            MapfConflict? conflict = SpaceTimePlanner.FirstConflict(node.Paths);

            if (conflict is null)
                return new MAPFSolution { Paths = node.Paths };

            foreach (MapfConstraint constraint in new[] { conflict.Value.ForFirst, conflict.Value.ForSecond })
            {
                CTNode child = Branch(node, constraint, horizon);

                if (child != null)
                    open.Enqueue(child, (child.Cost, order++));
            }
        }

        return SpaceTimePlanner.Independent(_map, _agents, horizon);
    }

    private CTNode Root(int horizon)
    {
        var root = new CTNode { Constraints = new HashSet<MapfConstraint>(), Paths = new List<List<(int X, int Y)>>() };

        for (int i = 0; i < _agents.Count; i++)
        {
            MAPFAgent agent = _agents[i];

            if (!SpaceTimePlanner.Reachable(_map, agent.StartX, agent.StartY, agent.GoalX, agent.GoalY))
                return null;

            List<(int X, int Y)> path = SpaceTimePlanner.Search(_map, agent, i, root.Constraints, horizon);

            if (path.Count == 0)
                return null;

            root.Paths.Add(path);
        }

        root.Cost = root.Paths.Sum(p => p.Count);

        return root;
    }

    private CTNode Branch(CTNode parent, MapfConstraint constraint, int horizon)
    {
        var constraints = new HashSet<MapfConstraint>(parent.Constraints) { constraint };
        List<(int X, int Y)> path = SpaceTimePlanner.Search(_map, _agents[constraint.Agent], constraint.Agent, constraints, horizon);

        if (path.Count == 0)
            return null;

        var paths = new List<List<(int X, int Y)>>(parent.Paths);
        paths[constraint.Agent] = path;

        return new CTNode { Constraints = constraints, Paths = paths, Cost = paths.Sum(p => p.Count) };
    }

    [Serializable]
    private class CTNode
    {
        public HashSet<MapfConstraint> Constraints;
        public List<List<(int X, int Y)>> Paths;
        public int Cost;
    }
}
