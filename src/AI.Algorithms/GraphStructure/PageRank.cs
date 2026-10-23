using System;
using AI.Algorithms.EWG;

namespace AI.Algorithms.GraphStructure;

/// <summary>
/// PageRank — важность вершин ориентированного графа: вершина важна, если на неё ведут дуги из
/// важных вершин. Считается степенным методом до сходимости.
/// </summary>
/// <remarks>
/// <para>
/// У взвешенного графа (<see cref="Weighted{T}"/>) вершина отдаёт важность по дугам пропорционально
/// их весам: граф ссылок, где вес — сколько раз одно упоминает другое, подаётся как есть.
/// </para>
/// <para>
/// Вершина без исходящих дуг («тупик») отдаёт свою важность так же, как телепорт, — по вектору
/// персонализации. Иначе важность утекала бы из графа, и сумма оценок падала бы с каждой итерацией.
/// </para>
/// <para>
/// Персонализация смещает оценку к выбранным вершинам (персонализированный PageRank): например,
/// к коду открытого файла, чтобы карта решения показывала прежде всего то, что с ним связано.
/// </para>
/// </remarks>
[Serializable]
public class PageRank
{
    /// <summary>Оценка каждой вершины; сумма — единица.</summary>
    public double[] Scores { get; }

    /// <summary>Сколько итераций понадобилось.</summary>
    public int Iterations { get; }

    /// <summary>Сошёлся ли метод до предела итераций.</summary>
    public bool Converged { get; }

    /// <summary>Считает PageRank невзвешенного графа: у каждой дуги вес 1.</summary>
    /// <param name="graph">Ориентированный граф (дуги — <see cref="Graph.AddArc"/>).</param>
    /// <param name="damping">Вероятность пойти по дуге, а не телепортироваться (обычно 0,85).</param>
    /// <param name="tolerance">Сходимость: сумма изменений оценок за итерацию меньше этого.</param>
    /// <param name="maxIterations">Предел итераций.</param>
    /// <param name="personalization">
    /// Куда ведёт телепорт и тупики: неотрицательные веса вершин, нормируются сами; <c>null</c> —
    /// равномерно.
    /// </param>
    public PageRank(Graph graph, double damping = 0.85, double tolerance = 1e-9, int maxIterations = 100, double[] personalization = null)
        : this(Arcs(graph), damping, tolerance, maxIterations, personalization)
    {
    }

    private PageRank((int[][] Targets, double[][] Weights) arcs, double damping, double tolerance, int maxIterations, double[] personalization)
    {
        if (damping is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(damping), "Коэффициент затухания должен быть в [0, 1).");

        int n = arcs.Targets.Length;
        Scores = new double[n];
        if (n == 0)
        {
            Converged = true;
            return;
        }

        double[] teleport = Normalize(personalization, n);
        double[] outWeight = new double[n];
        for (int i = 0; i < n; i++)
        {
            foreach (double w in arcs.Weights[i])
            {
                if (w < 0)
                    throw new ArgumentException("Веса дуг не могут быть отрицательными.");
                outWeight[i] += w;
            }
        }

        double[] current = (double[])teleport.Clone();
        double[] next = new double[n];

        for (int iteration = 1; iteration <= maxIterations; iteration++)
        {
            double dangling = 0;
            for (int i = 0; i < n; i++)
            {
                if (outWeight[i] <= 0)
                    dangling += current[i];
            }

            for (int i = 0; i < n; i++)
                next[i] = (1 - damping + damping * dangling) * teleport[i];

            for (int i = 0; i < n; i++)
            {
                if (outWeight[i] <= 0)
                    continue;

                double share = damping * current[i] / outWeight[i];
                int[] targets = arcs.Targets[i];
                double[] weights = arcs.Weights[i];
                for (int k = 0; k < targets.Length; k++)
                    next[targets[k]] += share * weights[k];
            }

            double change = 0;
            for (int i = 0; i < n; i++)
                change += Math.Abs(next[i] - current[i]);

            (current, next) = (next, current);
            Iterations = iteration;
            if (change < tolerance)
            {
                Converged = true;
                break;
            }
        }

        Array.Copy(current, Scores, n);
    }

    /// <summary>
    /// Считает PageRank взвешенного графа: вершина отдаёт важность по дугам пропорционально весам
    /// (дуги — <see cref="GraphW{T}.AddArce"/>).
    /// </summary>
    /// <param name="graph">Взвешенный ориентированный граф; веса неотрицательные.</param>
    /// <param name="damping">Вероятность пойти по дуге, а не телепортироваться (обычно 0,85).</param>
    /// <param name="tolerance">Сходимость: сумма изменений оценок за итерацию меньше этого.</param>
    /// <param name="maxIterations">Предел итераций.</param>
    /// <param name="personalization">Куда ведёт телепорт и тупики; <c>null</c> — равномерно.</param>
    public static PageRank Weighted<T>(GraphW<T> graph, double damping = 0.85, double tolerance = 1e-9, int maxIterations = 100, double[] personalization = null)
        where T : BaseEdge, new()
    {
        ArgumentNullException.ThrowIfNull(graph);
        var targets = new int[graph.V][];
        var weights = new double[graph.V][];
        for (int i = 0; i < graph.V; i++)
        {
            T[] edges = graph.AdjEW(i);
            targets[i] = new int[edges.Length];
            weights[i] = new double[edges.Length];
            for (int k = 0; k < edges.Length; k++)
            {
                targets[i][k] = edges[k].Other(i);
                weights[i][k] = edges[k].W;
            }
        }

        return new PageRank((targets, weights), damping, tolerance, maxIterations, personalization);
    }

    /// <summary>Дуги невзвешенного графа с весом 1.</summary>
    private static (int[][] Targets, double[][] Weights) Arcs(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var targets = new int[graph.V][];
        var weights = new double[graph.V][];
        for (int i = 0; i < graph.V; i++)
        {
            targets[i] = graph.Adj(i);
            weights[i] = new double[targets[i].Length];
            Array.Fill(weights[i], 1.0);
        }

        return (targets, weights);
    }

    /// <summary>Вектор телепорта: веса, нормированные к единице; без весов или с нулевой суммой — равномерный.</summary>
    private static double[] Normalize(double[] personalization, int n)
    {
        if (personalization != null && personalization.Length != n)
            throw new ArgumentException("Длина вектора персонализации должна совпадать с числом вершин.", nameof(personalization));

        var result = new double[n];
        double sum = 0;
        for (int i = 0; i < n && personalization != null; i++)
        {
            if (personalization[i] < 0)
                throw new ArgumentException("Веса персонализации не могут быть отрицательными.", nameof(personalization));
            sum += personalization[i];
        }

        for (int i = 0; i < n; i++)
            result[i] = sum > 0 ? personalization[i] / sum : 1.0 / n;

        return result;
    }
}
