using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Windowed Hierarchical Cooperative A* (WHCA*).
/// Планирует пути агентов последовательно в скользящем окне длиной w шагов,
/// используя таблицу резервирования для избежания конфликтов.
/// </summary>
/// <remarks>
/// <para>
/// В каждом окне агенты по очереди резервируют клетки и переходы до конца окна; агент, чей путь
/// короче окна, стоит на последней клетке до конца окна — и эта стоянка тоже резервируется.
/// Прежде резервировались только клетки самого пути, поэтому агенты обменивались местами,
/// а на агента, раньше закончившего путь, наезжали.
/// </para>
/// <para>
/// Метод неполон: если в окне пути нет, агент стоит на месте, а за отведённое число окон
/// не все могут дойти — такое решение не проходит <see cref="MAPFSolution.IsValid"/>.
/// </para>
/// </remarks>
[Serializable]
public class WHCA
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;
    private readonly int _windowSize;

    /// <summary>
    /// Создаёт решатель WHCA*.
    /// </summary>
    /// <param name="map">Карта.</param>
    /// <param name="agents">Список агентов.</param>
    /// <param name="windowSize">Размер временного окна планирования.</param>
    public WHCA(GridMap map, List<MAPFAgent> agents, int windowSize = 16)
    {
        _map = map;
        _agents = agents;
        _windowSize = Math.Max(1, windowSize);
    }

    /// <summary>
    /// Запускает поиск решения.
    /// </summary>
    public MAPFSolution Solve()
    {
        int n = _agents.Count;

        if (n == 0)
            return new MAPFSolution();

        var pos = new (int X, int Y)[n];
        for (int i = 0; i < n; i++)
            pos[i] = (_agents[i].StartX, _agents[i].StartY);

        var fullPaths = new List<List<(int X, int Y)>>(n);
        for (int i = 0; i < n; i++)
            fullPaths.Add(new List<(int X, int Y)> { pos[i] });

        int horizon = SpaceTimePlanner.Horizon(_map, n);
        int maxRounds = (_map.Width + _map.Height) * 4;

        for (int round = 0; round < maxRounds; round++)
        {
            if (Enumerable.Range(0, n).All(i => pos[i] == (_agents[i].GoalX, _agents[i].GoalY)))
                break;

            var table = new ReservationTable();
            var windowPaths = new List<(int X, int Y)>[n];

            for (int i = 0; i < n; i++)
            {
                windowPaths[i] = SpaceTimePlanner.SearchAgainst(_map, _agents[i], pos[i], table, horizon, _windowSize)
                    ?? new List<(int X, int Y)> { pos[i] };

                table.Reserve(windowPaths[i], 0, _windowSize);
            }

            for (int t = 1; t <= _windowSize; t++)
            {
                for (int i = 0; i < n; i++)
                {
                    pos[i] = SpaceTimePlanner.Position(windowPaths[i], t);
                    fullPaths[i].Add(pos[i]);
                }
            }
        }

        return new MAPFSolution { Paths = fullPaths };
    }
}
