using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Algorithms.MAPF;

/// <summary>
/// Safe Interval Path Planning (SIPP).
/// Планирует путь одного агента через безопасные временные интервалы,
/// избегая динамических препятствий.
/// </summary>
[Serializable]
public class SIPP
{
    private readonly GridMap _map;
    private readonly List<(int X, int Y, int TimeStart, int TimeEnd)> _dynamicObstacles;
    private readonly Dictionary<long, List<(int Start, int End)>> _safeIntervals;
    private readonly int _maxTime;

    /// <summary>
    /// Создаёт планировщик SIPP.
    /// </summary>
    /// <param name="map">Карта.</param>
    /// <param name="dynamicObstacles">Динамические препятствия (ячейка заблокирована с TimeStart по TimeEnd).</param>
    public SIPP(GridMap map, List<(int X, int Y, int TimeStart, int TimeEnd)> dynamicObstacles)
    {
        _map = map;
        _dynamicObstacles = dynamicObstacles;
        _maxTime = map.Width * map.Height + dynamicObstacles.Count + 100;
        _safeIntervals = new Dictionary<long, List<(int Start, int End)>>();
        ComputeSafeIntervals();
    }

    private long CellKey(int x, int y) => (long)x * _map.Height + y;

    private void ComputeSafeIntervals()
    {
        var obstByCell = new Dictionary<long, List<(int S, int E)>>();
        foreach (var obs in _dynamicObstacles)
        {
            long k = CellKey(obs.X, obs.Y);
            if (!obstByCell.TryGetValue(k, out var list))
            {
                list = new List<(int, int)>();
                obstByCell[k] = list;
            }
            list.Add((obs.TimeStart, obs.TimeEnd));
        }

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                long k = CellKey(x, y);
                if (_map.IsBlocked(x, y))
                {
                    _safeIntervals[k] = new List<(int, int)>();
                    continue;
                }

                if (!obstByCell.TryGetValue(k, out var obsList))
                {
                    _safeIntervals[k] = new List<(int, int)> { (0, _maxTime) };
                    continue;
                }

                obsList.Sort((a, b) => a.S.CompareTo(b.S));
                var merged = new List<(int S, int E)>();
                foreach (var o in obsList)
                {
                    if (merged.Count > 0 && o.S <= merged[^1].E + 1)
                        merged[^1] = (merged[^1].S, Math.Max(merged[^1].E, o.E));
                    else
                        merged.Add(o);
                }

                var safe = new List<(int Start, int End)>();
                int prev = 0;
                foreach (var o in merged)
                {
                    if (prev < o.S) safe.Add((prev, o.S - 1));
                    prev = o.E + 1;
                }
                if (prev <= _maxTime) safe.Add((prev, _maxTime));
                _safeIntervals[k] = safe;
            }
        }
    }

    private List<(int Start, int End)> GetIntervals(int x, int y)
    {
        long k = CellKey(x, y);
        return _safeIntervals.TryGetValue(k, out var list) ? list : new List<(int, int)>();
    }

    /// <summary>
    /// Ищет путь от (sx, sy) до (gx, gy), начиная с момента startTime,
    /// избегая динамических препятствий.
    /// </summary>
    /// <returns>Список позиций (включая ожидания) или пустой список при неудаче.</returns>
    public List<(int X, int Y)> FindPath(int sx, int sy, int gx, int gy, int startTime = 0)
    {
        var startIntervals = GetIntervals(sx, sy);
        int startIdx = -1;
        for (int i = 0; i < startIntervals.Count; i++)
        {
            if (startTime >= startIntervals[i].Start && startTime <= startIntervals[i].End)
            { startIdx = i; break; }
        }
        if (startIdx < 0) return new List<(int X, int Y)>();

        var open = new PriorityQueue<(int X, int Y, int Idx), int>();
        var gVal = new Dictionary<(int X, int Y, int Idx), int>();
        var parent = new Dictionary<(int X, int Y, int Idx), (int X, int Y, int Idx)?>();

        var s0 = (sx, sy, startIdx);
        gVal[s0] = startTime;
        parent[s0] = null;
        open.Enqueue(s0, startTime + Math.Abs(sx - gx) + Math.Abs(sy - gy));

        while (open.Count > 0)
        {
            var cur = open.Dequeue();
            int g = gVal.TryGetValue(cur, out int gv) ? gv : int.MaxValue;

            if (cur.X == gx && cur.Y == gy)
                return ReconstructPath(cur, parent, gVal, startTime);

            var curIntervals = GetIntervals(cur.X, cur.Y);
            if (cur.Idx >= curIntervals.Count) continue;
            var curInt = curIntervals[cur.Idx];

            foreach (var (nx, ny) in _map.Neighbors(cur.X, cur.Y))
            {
                var nIntervals = GetIntervals(nx, ny);
                for (int ni = 0; ni < nIntervals.Count; ni++)
                {
                    var nInt = nIntervals[ni];
                    int earliest = Math.Max(g + 1, nInt.Start);
                    if (earliest > nInt.End) continue;
                    int depart = earliest - 1;
                    if (depart < g || depart > curInt.End) continue;

                    var nState = (nx, ny, ni);
                    if (!gVal.TryGetValue(nState, out int existing) || earliest < existing)
                    {
                        gVal[nState] = earliest;
                        parent[nState] = cur;
                        int h = Math.Abs(nx - gx) + Math.Abs(ny - gy);
                        open.Enqueue(nState, earliest + h);
                    }
                }
            }
        }

        return new List<(int X, int Y)>();
    }

    private List<(int X, int Y)> ReconstructPath(
        (int X, int Y, int Idx) goal,
        Dictionary<(int X, int Y, int Idx), (int X, int Y, int Idx)?> parent,
        Dictionary<(int X, int Y, int Idx), int> gVal,
        int startTime)
    {
        var seq = new List<(int X, int Y, int G)>();
        (int X, int Y, int Idx)? cur = goal;
        while (cur != null)
        {
            var c = cur.Value;
            seq.Add((c.X, c.Y, gVal[c]));
            cur = parent[c];
        }
        seq.Reverse();

        var path = new List<(int X, int Y)>();
        for (int i = 0; i < seq.Count; i++)
        {
            if (i == 0)
            {
                path.Add((seq[i].X, seq[i].Y));
                continue;
            }
            int waits = seq[i].G - seq[i - 1].G - 1;
            for (int w = 0; w < waits; w++)
                path.Add((seq[i - 1].X, seq[i - 1].Y));
            path.Add((seq[i].X, seq[i].Y));
        }
        return path;
    }
}
