using System;
using System.Collections.Generic;

namespace AI.Algorithms.NetworkFlow;

/// <summary>
/// Сеть потоков (ориентированный граф с пропускными способностями)
/// </summary>
[Serializable]
public class FlowNetwork
{
    private readonly List<FlowEdge>[] _adj;

    /// <summary>
    /// Число вершин
    /// </summary>
    public int V { get; private set; }

    /// <summary>
    /// Число рёбер
    /// </summary>
    public int E { get; private set; }

    /// <summary>
    /// Создаёт сеть потоков с заданным числом вершин
    /// </summary>
    /// <param name="v">Число вершин</param>
    public FlowNetwork(int v)
    {
        V = v;
        E = 0;
        _adj = new List<FlowEdge>[v];
        for (int i = 0; i < v; i++)
            _adj[i] = new List<FlowEdge>();
    }

    /// <summary>
    /// Добавляет ребро в сеть (добавляется в списки смежности обоих вершин)
    /// </summary>
    /// <param name="e">Ребро потоковой сети</param>
    public void AddEdge(FlowEdge e)
    {
        _adj[e.From].Add(e);
        _adj[e.To].Add(e);
        E++;
    }

    /// <summary>
    /// Возвращает список рёбер, инцидентных вершине v
    /// </summary>
    /// <param name="v">Вершина</param>
    public List<FlowEdge> Adj(int v)
    {
        return _adj[v];
    }

    /// <summary>
    /// Возвращает все рёбра сети
    /// </summary>
    public List<FlowEdge> AllEdges()
    {
        List<FlowEdge> edges = new List<FlowEdge>();
        for (int v = 0; v < V; v++)
        {
            foreach (FlowEdge e in _adj[v])
            {
                if (e.From == v)
                    edges.Add(e);
            }
        }
        return edges;
    }
}
