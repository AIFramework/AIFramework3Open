using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Improved CBS (ICBS) — улучшенный CBS с классификацией конфликтов
/// на кардинальные, полукардинальные и некардинальные.
/// Кардинальные конфликты приоритизируются при ветвлении.
/// </summary>
/// <remarks>
/// Кардинальный конфликт — тот, разрешение которого удорожает путь обоим агентам: ветвление
/// по нему поднимает нижнюю оценку дерева быстрее всего. Оптимальность та же, что у CBS;
/// нижний уровень и ограничения — общие с ним, включая рёберные ограничения для обмена местами.
/// </remarks>
[Serializable]
public class ICBS
{
    private const int ClassifiedConflicts = 16;

    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly int _timeLimit;

    /// <summary>
    /// Создаёт решатель ICBS.
    /// </summary>
    public ICBS(GridMap map, List<MAPFAgent> agents, int timeLimit = 1000)
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

        root.Cost = root.Paths.Sum(p => p.Count);

        var open = new PriorityQueue<CTNode, (int Cost, long Order)>();
        long order = 0;
        open.Enqueue(root, (root.Cost, order++));

        while (sw.ElapsedMilliseconds < _timeLimit && open.TryDequeue(out CTNode node, out _))
        {
            List<MapfConflict> conflicts = SpaceTimePlanner.Conflicts(node.Paths).Take(ClassifiedConflicts).ToList();

            if (conflicts.Count == 0)
                return new MAPFSolution { Paths = node.Paths };

            foreach (CTNode child in SelectBranches(node, conflicts, horizon))
                open.Enqueue(child, (child.Cost, order++));
        }

        return SpaceTimePlanner.Independent(_map, _agents, horizon);
    }

    // Дети каждого конфликта строятся сразу: они же и показывают, кардинален ли конфликт.
    // Берётся кардинальный, иначе полукардинальный, иначе первый
    private IEnumerable<CTNode> SelectBranches(CTNode node, List<MapfConflict> conflicts, int horizon)
    {
        CTNode[] semi = null;
        CTNode[] first = null;

        foreach (MapfConflict conflict in conflicts)
        {
            CTNode a = Branch(node, conflict.ForFirst, horizon);
            CTNode b = Branch(node, conflict.ForSecond, horizon);

            bool aIncreases = a == null || a.Paths[conflict.First].Count > node.Paths[conflict.First].Count;
            bool bIncreases = b == null || b.Paths[conflict.Second].Count > node.Paths[conflict.Second].Count;

            first ??= new[] { a, b };

            if (aIncreases && bIncreases)
                return new[] { a, b }.Where(c => c != null);

            if ((aIncreases || bIncreases) && semi == null)
                semi = new[] { a, b };
        }

        return (semi ?? first).Where(c => c != null);
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
