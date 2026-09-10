using AI.Algorithms.EWG;

namespace AI.Simulation.Space;

/// <summary>
/// Сеть контактов для агентной модели: кто с кем взаимодействует.
/// </summary>
/// <remarks>
/// <para>
/// Хранится в <see cref="AI.Algorithms.EWG.Graph"/> из <c>AI.Algorithms</c> — неориентированном графе
/// с проверкой повторных рёбер; своя смежность здесь не заводится. Узлы сети — номера агентов.
/// </para>
/// <para>
/// Генераторы дают четыре классические сети с разными свойствами. Полный граф — перемешивание
/// всех со всеми, то есть ровно то допущение, на котором стоят модели на ОДУ. Случайный граф
/// Эрдёша — Реньи — степени узлов по Пуассону. «Тесный мир» Уоттса — Строгаца — соседские
/// связи плюс немного дальних, резко сокращающих расстояния. Безмасштабная сеть Барабаши —
/// Альберт — у немногих узлов очень много связей, и именно они разносят эпидемию.
/// </para>
/// </remarks>
public sealed class ContactNetwork
{
    private readonly Graph _graph;

    /// <summary>Создаёт сеть без связей</summary>
    /// <param name="nodes">Число узлов</param>
    public ContactNetwork(int nodes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nodes);

        _graph = new Graph(nodes);
    }

    /// <summary>Число узлов</summary>
    public int NodeCount => _graph.V;

    /// <summary>Число связей</summary>
    public int EdgeCount => _graph.E;

    /// <summary>Средняя степень узла</summary>
    public double MeanDegree => 2.0 * EdgeCount / NodeCount;

    /// <summary>Граф сети — для алгоритмов из <c>AI.Algorithms</c></summary>
    public Graph Graph => _graph;

    /// <summary>Связывает два узла; повторная связь не добавляется</summary>
    /// <param name="a">Первый узел</param>
    /// <param name="b">Второй узел</param>
    public void Connect(int a, int b)
    {
        RequireNode(a);
        RequireNode(b);

        if (a == b)
            throw new ArgumentException("Узел не может быть связан сам с собой", nameof(b));

        _graph.AddEdge(a, b);
    }

    /// <summary>Связаны ли узлы</summary>
    /// <param name="a">Первый узел</param>
    /// <param name="b">Второй узел</param>
    public bool AreConnected(int a, int b)
    {
        RequireNode(a);
        RequireNode(b);

        return _graph.HasEdge(a, b);
    }

    /// <summary>Контакты узла</summary>
    /// <param name="node">Узел</param>
    public int[] Contacts(int node)
    {
        RequireNode(node);

        return _graph.Adj(node);
    }

    /// <summary>Степень узла — число его контактов</summary>
    /// <param name="node">Узел</param>
    public int Degree(int node) => Contacts(node).Length;

    /// <summary>Полный граф: каждый связан с каждым</summary>
    /// <param name="nodes">Число узлов</param>
    public static ContactNetwork Complete(int nodes)
    {
        var network = new ContactNetwork(nodes);

        for (int a = 0; a < nodes; a++)
        {
            for (int b = a + 1; b < nodes; b++)
                network.Connect(a, b);
        }

        return network;
    }

    /// <summary>
    /// Кольцо: каждый узел связан с ближайшими соседями по кругу, по половине с каждой стороны
    /// </summary>
    /// <param name="nodes">Число узлов</param>
    /// <param name="neighbours">Число соседей каждого узла; чётное</param>
    public static ContactNetwork Ring(int nodes, int neighbours)
    {
        var network = new ContactNetwork(nodes);

        foreach ((int a, int b) in RingEdges(nodes, neighbours))
            network.Connect(a, b);

        return network;
    }

    /// <summary>
    /// Случайный граф Эрдёша — Реньи: каждая пара связана независимо с заданной вероятностью
    /// </summary>
    /// <param name="nodes">Число узлов</param>
    /// <param name="probability">Вероятность связи пары</param>
    /// <param name="random">Генератор случайных чисел</param>
    public static ContactNetwork ErdosRenyi(int nodes, double probability, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (probability is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(probability), "Вероятность лежит на отрезке [0, 1]");

        var network = new ContactNetwork(nodes);

        for (int a = 0; a < nodes; a++)
        {
            for (int b = a + 1; b < nodes; b++)
            {
                if (random.NextDouble() < probability)
                    network.Connect(a, b);
            }
        }

        return network;
    }

    /// <summary>
    /// «Тесный мир» Уоттса — Строгаца: кольцо, в котором часть связей перекинута в случайные узлы
    /// </summary>
    /// <remarks>
    /// При нулевой доле перекидывания получается кольцо, при единичной — случайный граф.
    /// Уже нескольких процентов перекинутых связей хватает, чтобы среднее расстояние в сети
    /// упало почти до случайного, а соседская кластеризация осталась почти кольцевой.
    /// </remarks>
    /// <param name="nodes">Число узлов</param>
    /// <param name="neighbours">Число соседей в исходном кольце; чётное</param>
    /// <param name="rewiring">Доля перекидываемых связей</param>
    /// <param name="random">Генератор случайных чисел</param>
    public static ContactNetwork WattsStrogatz(int nodes, int neighbours, double rewiring, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (rewiring is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(rewiring), "Доля перекидывания лежит на отрезке [0, 1]");

        // В графе нет удаления рёбер, поэтому перекидывание делается на списке, а граф строится в конце
        List<(int A, int B)> edges = RingEdges(nodes, neighbours);
        var present = new HashSet<(int, int)>(edges.Select(e => Key(e.A, e.B)));

        for (int i = 0; i < edges.Count; i++)
        {
            if (random.NextDouble() >= rewiring)
                continue;

            (int a, int b) = edges[i];

            for (int attempt = 0; attempt < 100; attempt++)
            {
                int target = random.Next(nodes);

                if (target == a || present.Contains(Key(a, target)))
                    continue;

                _ = present.Remove(Key(a, b));
                _ = present.Add(Key(a, target));
                edges[i] = (a, target);
                break;
            }
        }

        var network = new ContactNetwork(nodes);

        foreach ((int a, int b) in edges)
            network.Connect(a, b);

        return network;
    }

    /// <summary>
    /// Безмасштабная сеть Барабаши — Альберт: новый узел присоединяется к уже существующим
    /// с вероятностью, пропорциональной их степени
    /// </summary>
    /// <remarks>
    /// Начинается с полного графа на <c>linksPerNode + 1</c> узлах, затем каждый новый узел
    /// приносит ровно <paramref name="linksPerNode"/> связей. «Богатые богатеют»: хвост
    /// распределения степеней степенной, и у немногих узлов связей на порядок больше среднего.
    /// </remarks>
    /// <param name="nodes">Число узлов</param>
    /// <param name="linksPerNode">Число связей, которые приносит новый узел</param>
    /// <param name="random">Генератор случайных чисел</param>
    public static ContactNetwork BarabasiAlbert(int nodes, int linksPerNode, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(linksPerNode);

        if (nodes <= linksPerNode)
            throw new ArgumentOutOfRangeException(nameof(nodes), "Узлов должно быть больше, чем связей у нового узла");

        var network = new ContactNetwork(nodes);

        // Каждый конец каждой связи записан сюда один раз: выбор случайного элемента
        // и есть выбор узла с вероятностью, пропорциональной степени
        var endpoints = new List<int>();

        for (int a = 0; a <= linksPerNode; a++)
        {
            for (int b = a + 1; b <= linksPerNode; b++)
            {
                network.Connect(a, b);
                endpoints.Add(a);
                endpoints.Add(b);
            }
        }

        var chosen = new HashSet<int>();

        for (int node = linksPerNode + 1; node < nodes; node++)
        {
            chosen.Clear();

            while (chosen.Count < linksPerNode)
                _ = chosen.Add(endpoints[random.Next(endpoints.Count)]);

            foreach (int target in chosen)
            {
                network.Connect(node, target);
                endpoints.Add(node);
                endpoints.Add(target);
            }
        }

        return network;
    }

    private static List<(int A, int B)> RingEdges(int nodes, int neighbours)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nodes);

        if (neighbours <= 0 || neighbours % 2 != 0 || neighbours >= nodes)
            throw new ArgumentOutOfRangeException(nameof(neighbours),
                "Число соседей в кольце — положительное чётное число меньше числа узлов");

        var edges = new List<(int, int)>(nodes * neighbours / 2);

        for (int a = 0; a < nodes; a++)
        {
            for (int step = 1; step <= neighbours / 2; step++)
                edges.Add((a, (a + step) % nodes));
        }

        return edges;
    }

    private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

    private void RequireNode(int node)
    {
        if (node < 0 || node >= NodeCount)
            throw new ArgumentOutOfRangeException(nameof(node), $"Узла {node} в сети из {NodeCount} нет");
    }
}
