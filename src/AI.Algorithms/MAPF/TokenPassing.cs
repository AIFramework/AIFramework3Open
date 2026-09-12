using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Token Passing — децентрализованный алгоритм, в котором токен
/// передаётся между агентами по кругу. Агент, владеющий токеном,
/// планирует свой путь, остальные ожидают на месте.
/// </summary>
/// <remarks>
/// <para>
/// Разовая версия для задачи с заданными целями. Владелец токена планирует путь в обход уже
/// зарезервированных путей, стоянок на целях и агентов, ещё не получивших токен, — они стоят
/// на своих стартах. Токен получает первый из ожидающих, у кого путь уже есть: так агент,
/// стоящий на чужой цели или в чужом коридоре, уходит первым. Прежде путь строился в обход
/// текущих позиций всех агентов, и агент ждал, пока чужая цель освободится: в коридоре из трёх
/// клеток двое тратили на двоих пять тактов вместо двух. Выбор владельца стоит до n² поисков пути.
/// </para>
/// <para>
/// Решение без столкновений по построению. Если агенту пути нет, он остаётся на старте, и тогда
/// решение не проходит <see cref="MAPFSolution.IsValid"/>: цель не достигнута.
/// </para>
/// </remarks>
[Serializable]
public class TokenPassing
{
    private readonly GridMap _map;
    private readonly List<MAPFAgent> _agents;

    /// <summary>
    /// Создаёт решатель Token Passing.
    /// </summary>
    public TokenPassing(GridMap map, List<MAPFAgent> agents)
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

        foreach (MAPFAgent agent in _agents)
            table.Block(agent.StartX, agent.StartY);

        List<int> waiting = Enumerable.Range(0, n).ToList();

        while (waiting.Count > 0)
        {
            int holder = -1;
            List<(int X, int Y)> path = null;

            foreach (int candidate in waiting)
            {
                MAPFAgent agent = _agents[candidate];
                table.Unblock(agent.StartX, agent.StartY);
                path = SpaceTimePlanner.SearchAgainst(_map, agent, (agent.StartX, agent.StartY), table, horizon);

                if (path != null)
                {
                    holder = candidate;
                    break;
                }

                table.Block(agent.StartX, agent.StartY);
            }

            // Пути нет ни у кого: первый ожидающий остаётся на старте
            if (holder < 0)
            {
                holder = waiting[0];
                table.Unblock(_agents[holder].StartX, _agents[holder].StartY);
                path = new List<(int X, int Y)> { (_agents[holder].StartX, _agents[holder].StartY) };
            }

            waiting.Remove(holder);
            paths[holder] = path;
            table.Reserve(path);
        }

        return new MAPFSolution { Paths = SpaceTimePlanner.Pad(paths) };
    }
}
