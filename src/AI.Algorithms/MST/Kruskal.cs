using System;
using System.Collections.Generic;
using AI.Algorithms.EWG;

namespace AI.Algorithms.MST;

/// <summary>
/// Алгоритм Крускала для нахождения минимального остовного дерева (MST).
/// Сортирует рёбра по весу и объединяет компоненты с помощью Union-Find.
/// </summary>
[Serializable]
public class Kruskal<T> where T : BaseEdge, new()
{
    /// <summary>
    /// Рёбра минимального остовного дерева
    /// </summary>
    public List<T> MSTEdges { get; }

    /// <summary>
    /// Суммарный вес минимального остовного дерева
    /// </summary>
    public double TotalWeight { get; private set; }

    /// <summary>
    /// Строит минимальное остовное дерево алгоритмом Крускала
    /// </summary>
    /// <param name="graph">Взвешенный неориентированный граф</param>
    public Kruskal(GraphW<T> graph)
    {
        MSTEdges = new List<T>();
        TotalWeight = 0;

        List<T> allEdges = CollectEdges(graph);
        allEdges.Sort((a, b) => a.CompareTo(b));

        UnionFind uf = new UnionFind(graph.V);

        foreach (T edge in allEdges)
        {
            int u = edge.Either();
            int v = edge.Other(u);

            if (uf.Find(u) != uf.Find(v))
            {
                uf.Union(u, v);
                MSTEdges.Add(edge);
                TotalWeight += edge.W;

                if (MSTEdges.Count == graph.V - 1)
                    break;
            }
        }
    }

    // Каждое ребро — один раз: неориентированное лежит в списках обоих концов. Параллельные рёбра
    // не выбрасываются: прежде из пары оставалось первое попавшееся, а не лёгкое, и остов тяжелел.
    // Крускал сам возьмёт лёгкое, а тяжёлое отбросит как замыкающее цикл
    private static List<T> CollectEdges(GraphW<T> graph)
    {
        HashSet<T> seen = new HashSet<T>(ReferenceEqualityComparer.Instance);
        List<T> edges = new List<T>();

        for (int i = 0; i < graph.V; i++)
        {
            foreach (T e in graph.AdjEW(i))
            {
                if (seen.Add(e))
                    edges.Add(e);
            }
        }

        return edges;
    }

    /// <summary>
    /// Структура данных «система непересекающихся множеств» (Union-Find)
    /// с ранговой эвристикой и сжатием путей
    /// </summary>
    [Serializable]
    internal class UnionFind
    {
        private readonly int[] _parent;
        private readonly int[] _rank;

        /// <summary>
        /// Создаёт Union-Find для n элементов
        /// </summary>
        /// <param name="n">Количество элементов</param>
        public UnionFind(int n)
        {
            _parent = new int[n];
            _rank = new int[n];
            for (int i = 0; i < n; i++)
                _parent[i] = i;
        }

        /// <summary>
        /// Находит представителя множества, содержащего элемент x
        /// </summary>
        public int Find(int x)
        {
            while (_parent[x] != x)
            {
                _parent[x] = _parent[_parent[x]];
                x = _parent[x];
            }
            return x;
        }

        /// <summary>
        /// Объединяет множества, содержащие элементы x и y
        /// </summary>
        public void Union(int x, int y)
        {
            int rx = Find(x), ry = Find(y);
            if (rx == ry) return;

            if (_rank[rx] < _rank[ry])
                _parent[rx] = ry;
            else if (_rank[rx] > _rank[ry])
                _parent[ry] = rx;
            else
            {
                _parent[ry] = rx;
                _rank[rx]++;
            }
        }
    }
}
