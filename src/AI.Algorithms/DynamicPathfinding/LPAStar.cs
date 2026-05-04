using System;
using System.Collections.Generic;

namespace AI.Algorithms.DynamicPathfinding;

/// <summary>
/// Алгоритм LPA* (Lifelong Planning A*) — инкрементальный алгоритм
/// поиска кратчайшего пути на сетке, эффективно обновляющий решение
/// при изменениях стоимости рёбер.
/// </summary>
[Serializable]
public class LPAStar
{
    private readonly int _width;
    private readonly int _height;
    private readonly (int X, int Y) _start;
    private readonly (int X, int Y) _goal;
    private readonly bool[,] _blocked;
    private readonly double[,] _g;
    private readonly double[,] _rhs;

    private readonly SortedSet<((double K1, double K2) Key, int X, int Y)> _openSet;
    private readonly Dictionary<(int, int), (double K1, double K2)> _openKeys;

    private static readonly (int DX, int DY)[] Dirs =
    {
        (-1, 0), (1, 0), (0, -1), (0, 1),
        (-1, -1), (-1, 1), (1, -1), (1, 1)
    };

    /// <summary>
    /// Создаёт экземпляр LPA* для сетки заданного размера
    /// </summary>
    /// <param name="width">Ширина сетки</param>
    /// <param name="height">Высота сетки</param>
    /// <param name="start">Начальная ячейка</param>
    /// <param name="goal">Целевая ячейка</param>
    public LPAStar(int width, int height, (int X, int Y) start, (int X, int Y) goal)
    {
        _width = width;
        _height = height;
        _start = start;
        _goal = goal;
        _blocked = new bool[width, height];
        _g = new double[width, height];
        _rhs = new double[width, height];

        _openSet = new SortedSet<((double K1, double K2) Key, int X, int Y)>(
            Comparer<((double K1, double K2) Key, int X, int Y)>.Create((a, b) =>
            {
                int c1 = a.Key.K1.CompareTo(b.Key.K1);
                if (c1 != 0) return c1;
                int c2 = a.Key.K2.CompareTo(b.Key.K2);
                if (c2 != 0) return c2;
                int c3 = a.X.CompareTo(b.X);
                if (c3 != 0) return c3;
                return a.Y.CompareTo(b.Y);
            }));
        _openKeys = new Dictionary<(int, int), (double K1, double K2)>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                _g[x, y] = double.MaxValue;
                _rhs[x, y] = double.MaxValue;
            }
        }

        _rhs[_start.X, _start.Y] = 0;
        var key = CalculateKey(_start.X, _start.Y);
        _openSet.Add((key, _start.X, _start.Y));
        _openKeys[(_start.X, _start.Y)] = key;
    }

    private (double K1, double K2) CalculateKey(int x, int y)
    {
        double minGRhs = Math.Min(_g[x, y], _rhs[x, y]);
        double h = Heuristic(x, y, _goal.X, _goal.Y);
        return (minGRhs + h, minGRhs);
    }

    private static double Heuristic(int x1, int y1, int x2, int y2)
    {
        return Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
    }

    private double Cost(int x1, int y1, int x2, int y2)
    {
        if (_blocked[x1, y1] || _blocked[x2, y2])
            return double.MaxValue;
        int dx = Math.Abs(x1 - x2);
        int dy = Math.Abs(y1 - y2);
        return (dx + dy == 2) ? Math.Sqrt(2.0) : 1.0;
    }

    private bool InBounds(int x, int y)
    {
        return x >= 0 && x < _width && y >= 0 && y < _height;
    }

    private void UpdateVertex(int x, int y)
    {
        if (x != _start.X || y != _start.Y)
        {
            double minRhs = double.MaxValue;
            foreach (var (dx, dy) in Dirs)
            {
                int nx = x + dx, ny = y + dy;
                if (InBounds(nx, ny))
                {
                    double c = Cost(x, y, nx, ny);
                    if (c < double.MaxValue && _g[nx, ny] < double.MaxValue)
                    {
                        double val = _g[nx, ny] + c;
                        if (val < minRhs) minRhs = val;
                    }
                }
            }
            _rhs[x, y] = minRhs;
        }

        if (_openKeys.TryGetValue((x, y), out var oldKey))
        {
            _openSet.Remove((oldKey, x, y));
            _openKeys.Remove((x, y));
        }

        if (Math.Abs(_g[x, y] - _rhs[x, y]) > 1e-9)
        {
            var key = CalculateKey(x, y);
            _openSet.Add((key, x, y));
            _openKeys[(x, y)] = key;
        }
    }

    /// <summary>
    /// Обновляет стоимость ячейки (блокировка/разблокировка)
    /// и помечает затронутые вершины для пересчёта
    /// </summary>
    /// <param name="x">Координата X</param>
    /// <param name="y">Координата Y</param>
    /// <param name="blocked">true — заблокировать, false — разблокировать</param>
    public void UpdateEdgeCost(int x, int y, bool blocked)
    {
        if (_blocked[x, y] == blocked) return;
        _blocked[x, y] = blocked;

        UpdateVertex(x, y);
        foreach (var (dx, dy) in Dirs)
        {
            int nx = x + dx, ny = y + dy;
            if (InBounds(nx, ny))
                UpdateVertex(nx, ny);
        }
    }

    /// <summary>
    /// Вычисляет (или обновляет) кратчайший путь
    /// </summary>
    public void ComputeShortestPath()
    {
        while (_openSet.Count > 0)
        {
            var top = _openSet.Min;
            var topKey = top.Key;

            var goalKey = CalculateKey(_goal.X, _goal.Y);
            bool goalConsistent = Math.Abs(_rhs[_goal.X, _goal.Y] - _g[_goal.X, _goal.Y]) < 1e-9;

            if (CompareKeys(topKey, goalKey) >= 0 && goalConsistent)
                break;

            _openSet.Remove(top);
            _openKeys.Remove((top.X, top.Y));

            int ux = top.X, uy = top.Y;

            if (_g[ux, uy] > _rhs[ux, uy])
            {
                _g[ux, uy] = _rhs[ux, uy];
                foreach (var (dx, dy) in Dirs)
                {
                    int nx = ux + dx, ny = uy + dy;
                    if (InBounds(nx, ny))
                        UpdateVertex(nx, ny);
                }
            }
            else
            {
                _g[ux, uy] = double.MaxValue;
                UpdateVertex(ux, uy);
                foreach (var (dx, dy) in Dirs)
                {
                    int nx = ux + dx, ny = uy + dy;
                    if (InBounds(nx, ny))
                        UpdateVertex(nx, ny);
                }
            }
        }
    }

    private static int CompareKeys((double K1, double K2) a, (double K1, double K2) b)
    {
        int c1 = a.K1.CompareTo(b.K1);
        if (c1 != 0) return c1;
        return a.K2.CompareTo(b.K2);
    }

    /// <summary>
    /// Возвращает кратчайший путь от старта до цели
    /// </summary>
    /// <returns>Список ячеек пути или пустой список, если путь не найден</returns>
    public List<(int X, int Y)> GetPath()
    {
        List<(int X, int Y)> path = new List<(int X, int Y)>();

        if (_g[_goal.X, _goal.Y] >= double.MaxValue)
            return path;

        int cx = _goal.X, cy = _goal.Y;
        path.Add((cx, cy));

        int maxSteps = _width * _height;
        int steps = 0;

        while ((cx != _start.X || cy != _start.Y) && steps++ < maxSteps)
        {
            double bestVal = double.MaxValue;
            int bestX = cx, bestY = cy;

            foreach (var (dx, dy) in Dirs)
            {
                int nx = cx + dx, ny = cy + dy;
                if (InBounds(nx, ny))
                {
                    double c = Cost(cx, cy, nx, ny);
                    if (c < double.MaxValue)
                    {
                        double val = c + _g[nx, ny];
                        if (val < bestVal)
                        {
                            bestVal = val;
                            bestX = nx;
                            bestY = ny;
                        }
                    }
                }
            }

            if (bestX == cx && bestY == cy)
                break;

            cx = bestX;
            cy = bestY;
            path.Add((cx, cy));
        }

        path.Reverse();
        return path;
    }
}
