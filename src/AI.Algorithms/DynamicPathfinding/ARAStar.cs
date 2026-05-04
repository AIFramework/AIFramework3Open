using System;
using System.Collections.Generic;
using AI.Algorithms.EWG;

namespace AI.Algorithms.DynamicPathfinding;

/// <summary>
/// Алгоритм ARA* (Anytime Repairing A*) — «в-любое-время» алгоритм поиска
/// кратчайшего пути с регулируемой субоптимальностью. Начинает с большого
/// значения epsilon (быстрый, но неоптимальный поиск), затем постепенно
/// уменьшает его для улучшения решения.
/// </summary>
[Serializable]
public class ARAStar<T> where T : BaseEdge, new()
{
    private readonly GraphW<T> _graph;
    private readonly int _start;
    private readonly int _goal;
    private readonly Func<int, double> _heuristic;
    private readonly double[] _g;
    private readonly int[] _parent;
    private readonly SortedSet<(double Key, int Vertex)> _open;
    private readonly HashSet<int> _openSet;
    private readonly HashSet<int> _closed;
    private readonly HashSet<int> _incons;

    /// <summary>
    /// Текущее значение epsilon (коэффициент субоптимальности)
    /// </summary>
    public double Epsilon { get; private set; }

    /// <summary>
    /// Стоимость текущего найденного пути
    /// </summary>
    public double PathCost
    {
        get { return _g[_goal]; }
    }

    /// <summary>
    /// Создаёт экземпляр ARA* для взвешенного графа
    /// </summary>
    /// <param name="graph">Взвешенный граф</param>
    /// <param name="start">Начальная вершина</param>
    /// <param name="goal">Целевая вершина</param>
    /// <param name="heuristic">Функция эвристики h(v) — оценка расстояния от v до цели</param>
    /// <param name="initialEpsilon">Начальное значение epsilon (>= 1.0)</param>
    public ARAStar(GraphW<T> graph, int start, int goal,
        Func<int, double> heuristic, double initialEpsilon)
    {
        _graph = graph;
        _start = start;
        _goal = goal;
        _heuristic = heuristic;
        Epsilon = initialEpsilon;

        _g = new double[graph.V];
        _parent = new int[graph.V];

        for (int i = 0; i < graph.V; i++)
        {
            _g[i] = double.MaxValue;
            _parent[i] = -1;
        }

        _g[_start] = 0;

        _open = new SortedSet<(double Key, int Vertex)>(
            Comparer<(double Key, int Vertex)>.Create((a, b) =>
            {
                int c = a.Key.CompareTo(b.Key);
                if (c != 0) return c;
                return a.Vertex.CompareTo(b.Vertex);
            }));
        _openSet = new HashSet<int>();
        _closed = new HashSet<int>();
        _incons = new HashSet<int>();

        InsertOpen(_start);
    }

    private double FValue(int v)
    {
        return _g[v] + Epsilon * _heuristic(v);
    }

    private void InsertOpen(int v)
    {
        if (_openSet.Contains(v))
        {
            _open.RemoveWhere(x => x.Vertex == v);
        }
        _open.Add((FValue(v), v));
        _openSet.Add(v);
    }

    private void RemoveOpen(int v)
    {
        _open.RemoveWhere(x => x.Vertex == v);
        _openSet.Remove(v);
    }

    /// <summary>
    /// Улучшает текущее решение при текущем значении epsilon.
    /// Расширяет вершины из OPEN, пока целевая вершина не будет оптимально
    /// достижима с точностью до epsilon.
    /// </summary>
    public void ImprovePath()
    {
        while (_open.Count > 0)
        {
            var top = _open.Min;

            if (top.Key >= FValue(_goal) && _g[_goal] < double.MaxValue)
                break;

            _open.Remove(top);
            _openSet.Remove(top.Vertex);
            _closed.Add(top.Vertex);

            int u = top.Vertex;

            foreach (T edge in _graph.AdjEW(u))
            {
                int w = edge.Other(u);
                double newG = _g[u] + edge.W;

                if (newG < _g[w])
                {
                    _g[w] = newG;
                    _parent[w] = u;

                    if (!_closed.Contains(w))
                    {
                        InsertOpen(w);
                    }
                    else
                    {
                        _incons.Add(w);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Уменьшает значение epsilon на заданную величину (но не менее 1.0)
    /// и подготавливает OPEN-список для нового улучшения.
    /// После вызова следует вызвать ImprovePath() для улучшения решения.
    /// </summary>
    /// <param name="delta">Величина уменьшения epsilon</param>
    public void DecreaseEpsilon(double delta)
    {
        Epsilon = Math.Max(1.0, Epsilon - delta);

        foreach (int v in _incons)
        {
            InsertOpen(v);
        }
        _incons.Clear();
        _closed.Clear();

        var oldEntries = new List<(double Key, int Vertex)>(_open);
        _open.Clear();
        _openSet.Clear();
        foreach (var entry in oldEntries)
        {
            _open.Add((FValue(entry.Vertex), entry.Vertex));
            _openSet.Add(entry.Vertex);
        }
    }

    /// <summary>
    /// Возвращает текущий найденный путь от старта до цели
    /// </summary>
    /// <returns>Список вершин пути или пустой список, если путь не найден</returns>
    public List<int> GetPath()
    {
        List<int> path = new List<int>();
        if (_g[_goal] >= double.MaxValue)
            return path;

        int cur = _goal;
        while (cur != -1)
        {
            path.Add(cur);
            if (cur == _start) break;
            cur = _parent[cur];
        }
        path.Reverse();
        return path;
    }
}
