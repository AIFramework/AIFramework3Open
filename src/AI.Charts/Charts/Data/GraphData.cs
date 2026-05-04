using System;
using System.Collections.Generic;

namespace AI.Charts.Data;

/// <summary>
/// Данные для визуализации направленного графа (DAG, дерево задач, конечный автомат).
/// Универсальная модель: узлы с координатами и группами, рёбра между ними.
/// </summary>
[Serializable]
public sealed class GraphData
{
    /// <summary>Узлы графа.</summary>
    public IReadOnlyList<GraphNode> Nodes { get; }

    /// <summary>Рёбра (направленные: от Source к Target).</summary>
    public IReadOnlyList<GraphEdge> Edges { get; }

    public GraphData(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
    {
        Nodes = nodes ?? [];
        Edges = edges ?? [];
    }

    /// <summary>
    /// Автоматическая раскладка: ярусное дерево (tier -> Y, порядок в ярусе -> X).
    /// </summary>
    public static GraphData CreateTieredLayout(
        IReadOnlyList<(string id, string label, string subtitle, int tier)> steps,
        IReadOnlyList<(string from, string to)> dependencies,
        double nodeWidth = 1.1, double nodeGap = 0.2, double tierGap = 0.5)
    {
        var tiers = new SortedDictionary<int, List<int>>();
        for (int i = 0; i < steps.Count; i++)
        {
            int t = steps[i].tier;
            if (!tiers.ContainsKey(t))
                tiers[t] = new List<int>();
            tiers[t].Add(i);
        }

        var nodes = new GraphNode[steps.Count];
        var idToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < steps.Count; i++)
            idToIndex[steps[i].id] = i;

        foreach (var kv in tiers)
        {
            int tierLevel = kv.Key;
            var indices = kv.Value;
            double totalWidth = indices.Count * nodeWidth + (indices.Count - 1) * nodeGap;
            double startX = -totalWidth / 2.0 + nodeWidth / 2.0;

            for (int j = 0; j < indices.Count; j++)
            {
                int idx = indices[j];
                double x = startX + j * (nodeWidth + nodeGap);
                double y = -tierLevel * tierGap;
                var s = steps[idx];
                nodes[idx] = new GraphNode(x, y, s.label, s.subtitle, s.tier);
            }
        }

        var edges = new List<GraphEdge>();
        foreach (var (from, to) in dependencies)
        {
            if (idToIndex.TryGetValue(from, out int fi) && idToIndex.TryGetValue(to, out int ti))
                edges.Add(new GraphEdge(fi, ti));
        }

        return new GraphData(nodes, edges);
    }
}

/// <summary>Узел графа.</summary>
[Serializable]
public sealed class GraphNode
{
    public double X { get; set; }
    public double Y { get; set; }
    public string Label { get; set; }
    public string Subtitle { get; set; }
    public int Group { get; set; }

    public GraphNode(double x, double y, string label, string subtitle = null, int group = 0)
    {
        X = x;
        Y = y;
        Label = label ?? "";
        Subtitle = subtitle;
        Group = group;
    }
}

/// <summary>Направленное ребро графа.</summary>
[Serializable]
public sealed class GraphEdge
{
    public int SourceIndex { get; set; }
    public int TargetIndex { get; set; }

    public GraphEdge(int sourceIndex, int targetIndex)
    {
        SourceIndex = sourceIndex;
        TargetIndex = targetIndex;
    }
}
