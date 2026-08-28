using AI.Algorithms.EWG;
using AI.Algorithms.GraphStructure;
using AI.Algorithms.MST;
using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Взвешенный граф вместе с признаком направленности.
/// </summary>
/// <remarks>
/// <c>GraphW</c> из фреймворка не помнит, строили его рёбрами или дугами, а алгоритмам
/// (топологическая сортировка, остовное дерево) это различие важно. Признак хранится рядом,
/// чтобы отказ «остовного дерева у ориентированного графа не бывает» приходил из проверки,
/// а не из бессмысленного результата.
/// </remarks>
public sealed class ScriptGraph
{
    /// <summary>Взвешенный граф.</summary>
    public GraphW<Edge> Weighted { get; }

    /// <summary>Невзвешенное представление для обходов.</summary>
    public Graph Plain { get; }

    /// <summary>Ориентирован ли граф.</summary>
    public bool IsDirected { get; }

    /// <summary>Число вершин.</summary>
    public int Vertices { get; }

    /// <summary>Число рёбер либо дуг.</summary>
    public int Edges { get; }

    /// <summary>Создаёт граф.</summary>
    public ScriptGraph(GraphW<Edge> weighted, Graph plain, bool directed, int vertices, int edges)
    {
        Weighted = weighted;
        Plain = plain;
        IsDirected = directed;
        Vertices = vertices;
        Edges = edges;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{(IsDirected ? "орграф" : "граф")}: вершин {Vertices}, {(IsDirected ? "дуг" : "рёбер")} {Edges}";
}

/// <summary>
/// Пространство <c>graph</c>: графы и алгоритмы на них.
/// </summary>
/// <remarks>
/// Граф задаётся таблицей рёбер — это тот вид, в котором связи приходят из данных
/// (<c>io.read_csv</c>), а не результатом ручной сборки объекта по вершине за раз.
/// </remarks>
[ScriptModule("graph", "Графы: кратчайшие пути, остовные деревья, компоненты, сортировка", Version = "0.1")]
public static class GraphModule
{
    /// <summary>Тип-тег дескриптора графа.</summary>
    public const string GraphHandle = "graph.graph";

    /// <summary>
    /// Строит граф по списку рёбер.
    /// </summary>
    /// <remarks>
    /// Вершины нумеруются подряд от нуля; число вершин выводится из наибольшего номера, если
    /// не задано явно. Явное число нужно, когда в графе есть изолированные вершины: по одним
    /// рёбрам о них узнать неоткуда.
    /// </remarks>
    [ScriptFn("of", "Граф по таблице рёбер с колонками from, to и необязательной weight", Returns = GraphHandle,
        Example = "let g = graph.of(edges)")]
    public static ScriptHandle Of(
        IScriptContext context,
        [ScriptParam("таблица рёбер: from, to, weight")] ScriptTable edges,
        [ScriptParam("ориентированный граф")] bool directed = false,
        [ScriptParam("число вершин; 0 — вывести из рёбер")] int vertices = 0)
    {
        ScriptColumn from = edges.Column("from");
        ScriptColumn to = edges.Column("to");
        bool weighted = edges.TryGet("weight", out ScriptColumn weight);

        int count = vertices;

        for (int i = 0; i < edges.RowCount; i++)
        {
            count = Math.Max(count, Index(from[i], i, "from") + 1);
            count = Math.Max(count, Index(to[i], i, "to") + 1);
        }

        if (count == 0) throw new ScriptError(DiagnosticCodes.SizeMismatch, "graph.of: граф пуст");

        context.CountAllocation(count + edges.RowCount);

        var graph = new GraphW<Edge>(count);
        var plain = new Graph(count);

        for (int i = 0; i < edges.RowCount; i++)
        {
            int a = Index(from[i], i, "from");
            int b = Index(to[i], i, "to");
            double w = weighted ? weight[i].AsNumber($"graph.of: вес ребра {i}") : 1;

            if (directed)
            {
                graph.AddArce(a, b, w);
                plain.AddArc(a, b);
                continue;
            }

            // Неориентированное ребро укладывается ДВУМЯ дугами. Взвешенный граф хранит
            // ребро один раз, но Дейкстра ослабляет его только в направлении StartV → EndV:
            // с одной дугой обратный ход оказался бы недоступен, и кратчайший путь молча
            // получился бы длиннее настоящего.
            graph.AddArce(a, b, w);
            graph.AddArce(b, a, w);
            plain.AddEdge(a, b);
        }

        var result = new ScriptGraph(graph, plain, directed, count, edges.RowCount);

        return new ScriptHandle(GraphHandle, result, result.ToString());
    }

    [ScriptFn("size", "Число вершин и рёбер", Example = "g.size().vertices")]
    [ScriptMethod(GraphHandle)]
    public static ScriptRecord Size([ScriptParam("граф")] ScriptHandle graph)
    {
        var model = (ScriptGraph)graph.Target;

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("vertices", ScriptValue.Num(model.Vertices)),
            new KeyValuePair<string, ScriptValue>("edges", ScriptValue.Num(model.Edges)),
            new KeyValuePair<string, ScriptValue>("directed", ScriptValue.Bool(model.IsDirected)),
        ]);
    }

    [ScriptFn("neighbors", "Соседи вершины", Example = "g.neighbors(0)")]
    [ScriptMethod(GraphHandle)]
    public static Vector Neighbors(
        [ScriptParam("граф")] ScriptHandle graph,
        [ScriptParam("вершина")] int vertex)
    {
        var model = (ScriptGraph)graph.Target;
        RequireVertex(model, vertex, "graph.neighbors");

        int[] adjacent = model.Plain.Adj(vertex);
        var result = new Vector(adjacent.Length);

        for (int i = 0; i < adjacent.Length; i++) result[i] = adjacent[i];

        return result;
    }

    /// <summary>
    /// Кратчайшие пути от вершины по алгоритму Дейкстры.
    /// </summary>
    /// <remarks>
    /// Недостижимая вершина получает <c>inf</c>, а не большое число: «бесконечно далеко» и
    /// «очень далеко» — разные факты, и путать их в таблице расстояний опасно.
    /// </remarks>
    [ScriptFn("dijkstra", "Кратчайшие расстояния от вершины с учётом весов",
        Example = "g.dijkstra(0)")]
    [ScriptMethod(GraphHandle)]
    public static Vector Dijkstra(
        [ScriptParam("граф")] ScriptHandle graph,
        [ScriptParam("начальная вершина")] int from)
    {
        var model = (ScriptGraph)graph.Target;
        RequireVertex(model, from, "graph.dijkstra");

        var search = new DijkstraSPath<Edge>(model.Weighted, from);
        double[] distances = search.Distances;
        var result = new Vector(distances.Length);

        for (int i = 0; i < distances.Length; i++)
            result[i] = double.IsPositiveInfinity(distances[i]) || distances[i] >= double.MaxValue / 2
                ? double.PositiveInfinity
                : distances[i];

        return result;
    }

    [ScriptFn("bfs", "Расстояния в рёбрах от вершины обходом в ширину", Example = "g.bfs(0)")]
    [ScriptMethod(GraphHandle)]
    public static Vector Bfs(
        [ScriptParam("граф")] ScriptHandle graph,
        [ScriptParam("начальная вершина")] int from)
    {
        var model = (ScriptGraph)graph.Target;
        RequireVertex(model, from, "graph.bfs");

        var search = new BFS(model.Plain, from);
        var result = new Vector(model.Vertices);

        for (int i = 0; i < model.Vertices; i++)
            result[i] = search.Visited[i] ? search.DistanceTo[i] : double.PositiveInfinity;

        return result;
    }

    [ScriptFn("path", "Кратчайший путь между вершинами в рёбрах", Example = "g.path(0, to: 5)")]
    [ScriptMethod(GraphHandle)]
    public static Vector Path(
        [ScriptParam("граф")] ScriptHandle graph,
        [ScriptParam("начальная вершина")] int from,
        [ScriptParam("конечная вершина")] int to)
    {
        var model = (ScriptGraph)graph.Target;
        RequireVertex(model, from, "graph.path");
        RequireVertex(model, to, "graph.path");

        var search = new BFS(model.Plain, from);

        if (!search.Visited[to]) return new Vector(0);

        var path = new List<int>(search.PathTo(to));
        var result = new Vector(path.Count);

        for (int i = 0; i < path.Count; i++) result[i] = path[i];

        return result;
    }

    [ScriptFn("mst", "Минимальное остовное дерево: рёбра и суммарный вес",
        Example = "g.mst().weight")]
    [ScriptMethod(GraphHandle)]
    public static ScriptRecord Mst([ScriptParam("граф")] ScriptHandle graph)
    {
        var model = (ScriptGraph)graph.Target;

        if (model.IsDirected)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                "graph.mst: остовное дерево строится для неориентированного графа",
                "постройте граф с directed: false");
        }

        var kruskal = new Kruskal<Edge>(model.Weighted);

        var from = new Vector(kruskal.MSTEdges.Count);
        var to = new Vector(kruskal.MSTEdges.Count);
        var weights = new Vector(kruskal.MSTEdges.Count);

        for (int i = 0; i < kruskal.MSTEdges.Count; i++)
        {
            Edge edge = kruskal.MSTEdges[i];

            from[i] = edge.StartV;
            to[i] = edge.EndV;
            weights[i] = edge.W;
        }

        ScriptTable table = ScriptTable.Create(
        [
            ScriptColumn.FromVector("from", from),
            ScriptColumn.FromVector("to", to),
            ScriptColumn.FromVector("weight", weights),
        ]);

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("edges", ScriptValue.Table(table)),
            new KeyValuePair<string, ScriptValue>("weight", ScriptValue.Num(kruskal.TotalWeight)),
        ]);
    }

    [ScriptFn("topological", "Топологический порядок вершин ориентированного графа",
        Example = "g.topological()")]
    [ScriptMethod(GraphHandle)]
    public static ScriptRecord Topological([ScriptParam("граф")] ScriptHandle graph)
    {
        var model = (ScriptGraph)graph.Target;

        if (!model.IsDirected)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                "graph.topological: порядок определён только для ориентированного графа",
                "постройте граф с directed: true");
        }

        var sort = new TopologicalSort(model.Plain);
        var order = new Vector(sort.HasCycle ? 0 : sort.Order.Length);

        if (!sort.HasCycle)
        {
            for (int i = 0; i < sort.Order.Length; i++) order[i] = sort.Order[i];
        }

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("order", ScriptValue.Vec(order)),
            new KeyValuePair<string, ScriptValue>("has_cycle", ScriptValue.Bool(sort.HasCycle)),
        ]);
    }

    [ScriptFn("components", "Метка компоненты связности для каждой вершины",
        Example = "g.components()")]
    [ScriptMethod(GraphHandle)]
    public static Vector Components([ScriptParam("граф")] ScriptHandle graph)
    {
        var model = (ScriptGraph)graph.Target;
        var labels = new Vector(model.Vertices);

        for (int i = 0; i < model.Vertices; i++) labels[i] = -1;

        int component = 0;

        for (int start = 0; start < model.Vertices; start++)
        {
            if (labels[start] >= 0) continue;

            var search = new BFS(model.Plain, start);

            for (int i = 0; i < model.Vertices; i++)
            {
                if (search.Visited[i] && labels[i] < 0) labels[i] = component;
            }

            component++;
        }

        return labels;
    }

    private static int Index(ScriptValue value, int row, string column)
    {
        double number = value.AsNumber($"graph.of: колонка '{column}', строка {row}");
        double rounded = Math.Round(number);

        if (Math.Abs(number - rounded) > 1e-9 || rounded < 0)
        {
            throw new ScriptError(
                DiagnosticCodes.TypeMismatch,
                $"graph.of: номер вершины в колонке '{column}' строки {row} — {ScriptFormatter.Number(number)}",
                "вершины нумеруются целыми числами от нуля; для имён используйте table.encode");
        }

        return (int)rounded;
    }

    private static void RequireVertex(ScriptGraph graph, int vertex, string what)
    {
        if (vertex >= 0 && vertex < graph.Vertices) return;

        throw new ScriptError(
            DiagnosticCodes.IndexOutOfRange,
            $"{what}: вершина {vertex} вне графа из {graph.Vertices} вершин");
    }
}
