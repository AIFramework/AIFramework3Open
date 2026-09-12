using System;
using System.Collections.Generic;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Таблица резервирования приоритетного планирования: занятые клетки, пройденные переходы,
/// стоянки на целях и неподвижные агенты
/// </summary>
/// <remarks>
/// Прежние HCA, WHCA и Token Passing резервировали только клетки. Этого мало: два агента
/// обменивались местами на соседних клетках, а агент, раньше дошедший до цели, оказывался
/// на пути агента, спланированного позже. Здесь запрещены и встречный переход по тому же
/// ребру, и остановка на цели, через которую кто-то пройдёт позже.
/// </remarks>
[Serializable]
internal sealed class ReservationTable
{
    private readonly HashSet<(int X, int Y, int T)> _cells = new();
    private readonly HashSet<(int FromX, int FromY, int X, int Y, int T)> _moves = new();
    private readonly Dictionary<(int X, int Y), int> _parkedFrom = new();
    private readonly Dictionary<(int X, int Y), int> _lastUse = new();
    private readonly HashSet<(int X, int Y)> _static = new();

    /// <summary>Последний момент, в который что-либо зарезервировано</summary>
    public int LastTime { get; private set; }

    /// <summary>Свободна ли клетка в момент t</summary>
    public bool IsFree(int x, int y, int t)
        => !_static.Contains((x, y))
            && !_cells.Contains((x, y, t))
            && !(_parkedFrom.TryGetValue((x, y), out int from) && t >= from);

    /// <summary>Можно ли перейти из клетки в соседнюю (или остаться) к моменту t</summary>
    public bool CanMove(int fromX, int fromY, int x, int y, int t)
        => IsFree(x, y, t) && !_moves.Contains((x, y, fromX, fromY, t));

    /// <summary>Можно ли остаться в клетке навсегда начиная с момента t</summary>
    public bool CanPark(int x, int y, int t)
        => !_static.Contains((x, y))
            && !_parkedFrom.ContainsKey((x, y))
            && (!_lastUse.TryGetValue((x, y), out int last) || last < t);

    /// <summary>Клетка занята неподвижным агентом во все моменты</summary>
    public void Block(int x, int y) => _static.Add((x, y));

    /// <summary>Снимает неподвижного агента с клетки</summary>
    public void Unblock(int x, int y) => _static.Remove((x, y));

    /// <summary>
    /// Резервирует путь, начавшийся в момент <paramref name="start"/>; после конца пути агент либо
    /// стоит на последней клетке всегда (<paramref name="until"/> отрицательно), либо до момента until
    /// </summary>
    public void Reserve(List<(int X, int Y)> path, int start = 0, int until = -1)
    {
        for (int k = 0; k < path.Count; k++)
        {
            (int x, int y) = path[k];
            int t = start + k;
            _cells.Add((x, y, t));
            Touch(x, y, t);

            if (k > 0)
                _moves.Add((path[k - 1].X, path[k - 1].Y, x, y, t));
        }

        (int lx, int ly) = path[^1];
        int end = start + path.Count - 1;

        if (until < 0)
        {
            _parkedFrom[(lx, ly)] = end;
            return;
        }

        for (int t = end + 1; t <= until; t++)
        {
            _cells.Add((lx, ly, t));
            Touch(lx, ly, t);
        }
    }

    private void Touch(int x, int y, int t)
    {
        if (!_lastUse.TryGetValue((x, y), out int last) || last < t)
            _lastUse[(x, y)] = t;

        LastTime = Math.Max(LastTime, t);
    }
}
