using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Priority Inheritance with Backtracking (PIBT).
/// На каждом временном шаге агенты по приоритету выбирают ход;
/// при блокировке нижестоящий агент наследует приоритет и отходит.
/// </summary>
/// <remarks>
/// <para>
/// Агент перебирает клетки по расстоянию до цели (обход в ширину, а не манхэттенская оценка —
/// она обманывает у стен). Если клетка занята агентом, ещё не сделавшим ход, тот наследует
/// приоритет и должен освободить её; вернуться на клетку того, кто его толкает, он не может —
/// так исключается обмен местами. Если отойти некуда, толкающий пробует следующую клетку.
/// Прежде проверка обмена стояла не там, и агенты зацикливались или менялись местами.
/// </para>
/// <para>
/// Приоритет растёт с каждым шагом, проведённым не на цели, поэтому застрявший агент рано
/// или поздно получает право хода. Гарантия PIBT — каждый агент рано или поздно побывает
/// на цели — доказана лишь для графов, где любые две соседние клетки лежат на общем цикле;
/// одновременного прибытия всех она не обещает. Если за отведённое число шагов не все дошли,
/// решение не проходит <see cref="MAPFSolution.IsValid"/>.
/// </para>
/// </remarks>
[Serializable]
public class PIBT
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly int _maxTimesteps;

    /// <summary>
    /// Создаёт решатель PIBT.
    /// </summary>
    /// <param name="map">Карта.</param>
    /// <param name="agents">Список агентов.</param>
    /// <param name="maxTimesteps">Максимальное число шагов симуляции.</param>
    public PIBT(GridMap map, List<MAPFAgent> agents, int maxTimesteps = 200)
    {
        _map = map;
        _agents = agents;
        _maxTimesteps = maxTimesteps;
    }

    /// <summary>
    /// Запускает поиск решения.
    /// </summary>
    public MAPFSolution Solve()
    {
        int n = _agents.Count;
        var pos = new (int X, int Y)[n];
        var distance = new int[n][,];

        for (int i = 0; i < n; i++)
        {
            pos[i] = (_agents[i].StartX, _agents[i].StartY);
            distance[i] = SpaceTimePlanner.DistancesTo(_map, _agents[i].GoalX, _agents[i].GoalY);
        }

        var paths = new List<List<(int X, int Y)>>(n);
        for (int i = 0; i < n; i++)
            paths.Add(new List<(int X, int Y)> { pos[i] });

        // Дробная часть — постоянная добавка для однозначного порядка при равных приоритетах
        var priority = new double[n];
        for (int i = 0; i < n; i++)
            priority[i] = (double)(n - i) / (n + 1);

        for (int t = 0; t < _maxTimesteps; t++)
        {
            if (Enumerable.Range(0, n).All(i => AtGoal(i, pos[i])))
                break;

            var next = new (int X, int Y)?[n];

            foreach (int i in Enumerable.Range(0, n).OrderByDescending(i => priority[i]))
            {
                if (next[i] == null)
                    Step(i, -1, pos, next, distance);
            }

            for (int i = 0; i < n; i++)
            {
                pos[i] = next[i]!.Value;
                paths[i].Add(pos[i]);

                double tie = priority[i] - Math.Floor(priority[i]);
                priority[i] = AtGoal(i, pos[i]) ? tie : priority[i] + 1;
            }
        }

        return new MAPFSolution { Paths = paths };
    }

    private bool Step(int agent, int pusher, (int X, int Y)[] pos, (int X, int Y)?[] next, int[][,] distance)
    {
        (int X, int Y) here = pos[agent];

        // При равном расстоянии — сначала свободные клетки, а вытесняемый уходит подальше
        // от цели толкающего, а не в тупик на его пути
        List<(int X, int Y)> candidates = _map.Neighbors(here.X, here.Y);
        candidates.Add(here);
        candidates = candidates
            .OrderBy(c => distance[agent][c.X, c.Y])
            .ThenBy(c => Occupant(c, agent, pos, next) >= 0 ? 1 : 0)
            .ThenByDescending(c => pusher >= 0 ? distance[pusher][c.X, c.Y] : 0)
            .ToList();

        foreach ((int X, int Y) cell in candidates)
        {
            if (Claimed(cell, agent, next))
                continue;

            // Вернуться на клетку толкающего нельзя: это был бы обмен местами
            if (pusher >= 0 && cell == pos[pusher])
                continue;

            next[agent] = cell;
            int occupant = Occupant(cell, agent, pos, next);

            if (occupant >= 0 && !Step(occupant, agent, pos, next, distance))
            {
                next[agent] = null;
                continue;
            }

            return true;
        }

        next[agent] = here;
        return false;
    }

    private static bool Claimed((int X, int Y) cell, int agent, (int X, int Y)?[] next)
    {
        for (int k = 0; k < next.Length; k++)
        {
            if (k != agent && next[k] == cell)
                return true;
        }

        return false;
    }

    // Агент, стоящий в клетке и ещё не выбравший ход
    private static int Occupant((int X, int Y) cell, int agent, (int X, int Y)[] pos, (int X, int Y)?[] next)
    {
        for (int k = 0; k < pos.Length; k++)
        {
            if (k != agent && next[k] == null && pos[k] == cell)
                return k;
        }

        return -1;
    }

    private bool AtGoal(int agent, (int X, int Y) cell) => cell == (_agents[agent].GoalX, _agents[agent].GoalY);
}
