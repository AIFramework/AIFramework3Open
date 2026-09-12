using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Enhanced CBS (ECBS) — ограниченно-субоптимальный CBS.
/// </summary>
/// <remarks>
/// <para>
/// Верхний уровень — фокальный поиск: из узлов дерева, чья стоимость не больше w·(наименьшая
/// стоимость в открытом списке), берётся узел с наименьшим числом конфликтов. Наименьшая
/// стоимость открытого списка — нижняя оценка оптимума, поэтому найденное решение не дороже
/// w·оптимум по сумме длин путей.
/// </para>
/// <para>
/// Нижний уровень — обычный A* с общими для семейства CBS ограничениями, без фокального поиска
/// по конфликтам: полный ECBS ищет и там, но заявлять его здесь было бы неправдой. Недостижимая
/// цель прежде переполняла сумму нижних оценок и вызывала исключение.
/// </para>
/// </remarks>
[Serializable]
public class ECBS
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly double _w;
    private readonly int _timeLimit;

    /// <summary>
    /// Создаёт решатель ECBS.
    /// </summary>
    /// <param name="map">Карта.</param>
    /// <param name="agents">Список агентов.</param>
    /// <param name="suboptimalityBound">Коэффициент субоптимальности (w ≥ 1.0).</param>
    /// <param name="timeLimit">Лимит по времени в миллисекундах.</param>
    public ECBS(GridMap map, List<MAPFAgent> agents, double suboptimalityBound = 1.5,
        int timeLimit = 1000)
    {
        _map = map;
        _agents = agents;
        _w = Math.Max(1.0, suboptimalityBound);
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
        var root = new CTNode { Constraints = new HashSet<MapfConstraint>(), Paths = new List<List<(int X, int Y)>>() };

        for (int i = 0; i < n; i++)
        {
            MAPFAgent agent = _agents[i];
            List<(int X, int Y)> path = SpaceTimePlanner.Reachable(_map, agent.StartX, agent.StartY, agent.GoalX, agent.GoalY)
                ? SpaceTimePlanner.Search(_map, agent, i, root.Constraints, horizon)
                : new List<(int X, int Y)>();

            if (path.Count == 0)
                return SpaceTimePlanner.Independent(_map, _agents, horizon);

            root.Paths.Add(path);
        }

        Evaluate(root);

        var open = new List<CTNode> { root };

        while (open.Count > 0 && sw.ElapsedMilliseconds < _timeLimit)
        {
            long lowerBound = open.Min(nd => nd.Cost);
            double focalBound = lowerBound * _w;

            CTNode node = open.Where(nd => nd.Cost <= focalBound + 1e-9)
                .OrderBy(nd => nd.Conflicts)
                .ThenBy(nd => nd.Cost)
                .First();

            open.Remove(node);

            MapfConflict? conflict = SpaceTimePlanner.FirstConflict(node.Paths);

            if (conflict is null)
                return new MAPFSolution { Paths = node.Paths };

            foreach (MapfConstraint constraint in new[] { conflict.Value.ForFirst, conflict.Value.ForSecond })
            {
                var constraints = new HashSet<MapfConstraint>(node.Constraints) { constraint };
                List<(int X, int Y)> path = SpaceTimePlanner.Search(_map, _agents[constraint.Agent], constraint.Agent, constraints, horizon);

                if (path.Count == 0)
                    continue;

                var paths = new List<List<(int X, int Y)>>(node.Paths);
                paths[constraint.Agent] = path;

                var child = new CTNode { Constraints = constraints, Paths = paths };
                Evaluate(child);
                open.Add(child);
            }
        }

        return SpaceTimePlanner.Independent(_map, _agents, horizon);
    }

    private static void Evaluate(CTNode node)
    {
        node.Cost = node.Paths.Sum(p => (long)p.Count);
        node.Conflicts = SpaceTimePlanner.Conflicts(node.Paths).Count();
    }

    [Serializable]
    private class CTNode
    {
        public HashSet<MapfConstraint> Constraints;
        public List<List<(int X, int Y)>> Paths;
        public long Cost;
        public int Conflicts;
    }
}
