using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Hierarchical Cooperative A* (HCA*).
/// Планирует пути агентов последовательно на всём горизонте:
/// каждый следующий агент учитывает пути всех ранее спланированных
/// через таблицу резервирования.
/// </summary>
/// <remarks>
/// <para>
/// Резервируются клетки, переходы (встречный переход по тому же ребру запрещён — обмена местами
/// нет) и стоянки на целях: агент останавливается на цели, только если через неё никто не пройдёт
/// позже. Прежде резервировались только клетки, и агенты обменивались местами.
/// </para>
/// <para>
/// Приоритетное планирование неполно: агент, для которого пути в обход уже спланированных нет,
/// остаётся на старте, и решение тогда не проходит <see cref="MAPFSolution.IsValid"/>.
/// </para>
/// </remarks>
[Serializable]
public class HCA
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;

    /// <summary>
    /// Создаёт решатель HCA*.
    /// </summary>
    public HCA(GridMap map, List<MAPFAgent> agents)
    {
        _map = map;
        _agents = agents;
    }

    /// <summary>
    /// Запускает поиск решения.
    /// </summary>
    public MAPFSolution Solve()
    {
        int n = _agents.Count;

        if (n == 0)
            return new MAPFSolution();

        int horizon = SpaceTimePlanner.Horizon(_map, n);
        var table = new ReservationTable();
        var paths = new List<(int X, int Y)>[n];

        // Сначала — агенты с дальними целями: им труднее всего обходить других
        IEnumerable<int> order = Enumerable.Range(0, n)
            .OrderByDescending(i => H(_agents[i].StartX, _agents[i].StartY, _agents[i].GoalX, _agents[i].GoalY));

        foreach (int i in order)
        {
            MAPFAgent agent = _agents[i];
            (int X, int Y) start = (agent.StartX, agent.StartY);

            paths[i] = SpaceTimePlanner.SearchAgainst(_map, agent, start, table, horizon)
                ?? new List<(int X, int Y)> { start };

            table.Reserve(paths[i]);
        }

        return new MAPFSolution { Paths = SpaceTimePlanner.Pad(paths) };
    }

    private static int H(int x, int y, int gx, int gy) => Math.Abs(x - gx) + Math.Abs(y - gy);
}
