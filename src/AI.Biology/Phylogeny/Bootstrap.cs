using AI.Insights;

namespace AI.Biology.Phylogeny;

/// <summary>Дерево с бутстреп-поддержкой его ветвей</summary>
public sealed class BootstrapResult : IInterpretable
{
    private readonly Dictionary<TreeNode, string?> _keys;

    internal BootstrapResult(PhylogeneticTree tree, IReadOnlyDictionary<string, double> support, int replicates)
    {
        Tree = tree;
        SplitSupport = support;
        Replicates = replicates;
        _keys = tree.SplitKeys().ToDictionary(p => p.Node, p => p.Key);
    }

    /// <summary>Дерево по исходным данным</summary>
    public PhylogeneticTree Tree { get; }

    /// <summary>Число псевдовыборок</summary>
    public int Replicates { get; }

    /// <summary>Доля псевдовыборок, в дереве которых есть каждое разбиение исходного дерева</summary>
    public IReadOnlyDictionary<string, double> SplitSupport { get; }

    /// <summary>Поддержка ветви над узлом; NaN для листьев и корня</summary>
    /// <param name="node">Узел дерева <see cref="Tree"/></param>
    public double SupportOf(TreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return !ReferenceEquals(node, Tree.Root) && _keys.TryGetValue(node, out string? key) && key is not null
            ? SplitSupport[key]
            : double.NaN;
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int total = SplitSupport.Count;
        int strong = SplitSupport.Values.Count(v => v >= 0.95);
        int weak = SplitSupport.Values.Count(v => v < 0.7);
        double weakest = total == 0 ? double.NaN : SplitSupport.Values.Min();

        return new InterpretationBuilder("Бутстреп-поддержка дерева")
            .Summary(total == 0
                ? "Внутренних ветвей нет: у дерева меньше четырёх листьев, и поддерживать нечего."
                : $"Внутренних ветвей {total}: поддержка не ниже 95 % у {strong}, ниже 70 % у {weak}; "
                  + $"самая слабая — {Fmt.Pct(weakest)} по {Replicates} псевдовыборкам.")
            .Metric("Псевдовыборок", Replicates, null, "выравниваний из случайно повторённых столбцов", MetricQuality.Unknown, 0)
            .Metric("Внутренних ветвей", total, null, null, MetricQuality.Unknown, 0)
            .Metric("Сильно поддержанных", strong, null, "поддержка не ниже 95 %", MetricQuality.Good, 0)
            .Metric("Слабо поддержанных", weak, null, "поддержка ниже 70 %",
                weak > 0 ? MetricQuality.Warning : MetricQuality.Good, 0)
            .FindingIf(weak > 0,
                "Слабо поддержанные ветви данные не разрешают: при другой случайной выборке столбцов "
                + "эти группы распадаются. Их честнее изображать многоразветвлённым узлом.")
            .Warning("Бутстреп-поддержка — не вероятность того, что группа существует. Она показывает "
                + "устойчивость вывода к случайности выборки позиций и не защищает от систематической "
                + "ошибки: неверная модель замен даёт неверную группу со стопроцентной поддержкой.")
            .WarningIf(Replicates < 100,
                "Меньше ста псевдовыборок: сама оценка поддержки шумит на несколько процентов.")
            .Build();
    }
}

/// <summary>
/// Непараметрический бутстреп Фельзенштейна (1985) для деревьев по расстояниям.
/// </summary>
/// <remarks>
/// Столбцы выравнивания выбираются случайно с возвращением, по псевдовыборке строится дерево
/// тем же методом, и для каждой ветви исходного дерева считается доля псевдовыборок, где она
/// есть. Если хотя бы в одной псевдовыборке расстояние насыщено, построение прерывается ошибкой:
/// данные слишком далеки для выбранной модели.
/// </remarks>
public static class Bootstrap
{
    /// <summary>Бутстреп-поддержка дерева, построенного по расстояниям</summary>
    /// <param name="labels">Имена таксонов</param>
    /// <param name="alignedSequences">Выровненные последовательности</param>
    /// <param name="builder">Метод построения, например <see cref="TreeBuilder.NeighborJoining"/></param>
    /// <param name="model">Модель расстояний</param>
    /// <param name="replicates">Число псевдовыборок</param>
    /// <param name="random">Генератор случайных чисел</param>
    public static BootstrapResult DistanceTree(
        IReadOnlyList<string> labels,
        IReadOnlyList<string> alignedSequences,
        Func<DistanceMatrix, PhylogeneticTree> builder,
        DistanceModel model,
        int replicates,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(alignedSequences);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(replicates);

        PhylogeneticTree reference = builder(DistanceMatrix.FromSequences(labels, alignedSequences, model));
        IReadOnlySet<string> splits = reference.Splits();
        var counts = splits.ToDictionary(s => s, _ => 0, StringComparer.Ordinal);

        int length = alignedSequences.Count == 0 ? 0 : alignedSequences[0].Length;
        var columns = new int[length];
        var resampled = new string[alignedSequences.Count];

        for (int replicate = 0; replicate < replicates; replicate++)
        {
            for (int k = 0; k < length; k++)
                columns[k] = random.Next(length);

            for (int s = 0; s < alignedSequences.Count; s++)
            {
                string source = alignedSequences[s];
                resampled[s] = string.Create(length, (source, columns), static (span, state) =>
                {
                    for (int k = 0; k < span.Length; k++)
                        span[k] = state.source[state.columns[k]];
                });
            }

            PhylogeneticTree tree = builder(DistanceMatrix.FromSequences(labels, resampled, model));

            foreach (string split in tree.Splits())
            {
                if (counts.ContainsKey(split))
                    counts[split]++;
            }
        }

        var support = counts.ToDictionary(p => p.Key, p => (double)p.Value / replicates, StringComparer.Ordinal);

        return new BootstrapResult(reference, support, replicates);
    }
}
