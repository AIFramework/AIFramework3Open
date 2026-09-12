namespace AI.Biology.Phylogeny;

/// <summary>
/// Максимальная экономия: наименьшее число замен, которым дерево объясняет выравнивание.
/// </summary>
/// <remarks>
/// <para>
/// Длина дерева считается алгоритмом Фитча, обобщённым Хартиганом (1973) на узлы с любым числом
/// потомков: множество состояний узла — те, что встречаются в наибольшем числе множеств
/// потомков, а к длине прибавляется число потомков, где этого состояния нет. Для двоичного
/// дерева это ровно Фитч. Результат точен: это минимум по всем расстановкам состояний во
/// внутренних узлах, а не приближение.
/// </para>
/// <para>
/// Все замены стоят одинаково. Пропуск и неизвестный символ по умолчанию означают «любое
/// состояние», как в PAUP; символы N и X считаются обычными состояниями — у белков N это
/// аспарагин, — и если в нуклеотидных данных N означает неизвестность, передайте его в
/// <c>missing</c>.
/// </para>
/// </remarks>
public static class Parsimony
{
    /// <summary>Символы, означающие неизвестное состояние, по умолчанию</summary>
    public const string DefaultMissing = "-?.";

    /// <summary>Длина дерева по экономии — число замен</summary>
    /// <param name="tree">Дерево; имена листьев — ключи выравнивания</param>
    /// <param name="alignment">Выравнивание: имя таксона → последовательность</param>
    /// <param name="missing">Символы неизвестного состояния</param>
    public static int Score(PhylogeneticTree tree, IReadOnlyDictionary<string, string> alignment, string missing = DefaultMissing)
        => SiteScores(tree, alignment, missing).Sum();

    /// <summary>Число замен в каждой позиции выравнивания</summary>
    /// <param name="tree">Дерево</param>
    /// <param name="alignment">Выравнивание</param>
    /// <param name="missing">Символы неизвестного состояния</param>
    public static int[] SiteScores(PhylogeneticTree tree, IReadOnlyDictionary<string, string> alignment, string missing = DefaultMissing)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(alignment);
        ArgumentNullException.ThrowIfNull(missing);

        IReadOnlyList<TreeNode> leaves = tree.Leaves;
        var sequences = new Dictionary<TreeNode, string>();
        int length = -1;

        foreach (TreeNode leaf in leaves)
        {
            if (leaf.Name is null || !alignment.TryGetValue(leaf.Name, out string? sequence))
                throw new ArgumentException($"Для листа «{leaf.Name}» нет последовательности", nameof(alignment));

            if (length >= 0 && sequence.Length != length)
                throw new ArgumentException("Последовательности выравнивания должны быть одной длины", nameof(alignment));

            length = sequence.Length;
            sequences[leaf] = sequence.ToUpperInvariant();
        }

        string unknown = missing.ToUpperInvariant();
        List<TreeNode> postOrder = tree.PostOrder().ToList();
        var sets = new Dictionary<TreeNode, ulong>();
        var scores = new int[Math.Max(length, 0)];
        var states = new Dictionary<char, int>();

        for (int site = 0; site < length; site++)
        {
            states.Clear();

            foreach (string sequence in sequences.Values)
            {
                char c = sequence[site];

                if (unknown.Contains(c) || states.ContainsKey(c))
                    continue;

                if (states.Count == 64)
                    throw new ArgumentException("В одной позиции больше 64 различных символов", nameof(alignment));

                states[c] = states.Count;
            }

            if (states.Count == 0)
                continue;

            ulong all = states.Count == 64 ? ulong.MaxValue : (1UL << states.Count) - 1;
            int cost = 0;

            foreach (TreeNode node in postOrder)
            {
                if (node.IsLeaf)
                {
                    char c = sequences[node][site];
                    sets[node] = unknown.Contains(c) ? all : 1UL << states[c];
                    continue;
                }

                int best = 0;
                ulong chosen = 0;

                for (int s = 0; s < states.Count; s++)
                {
                    ulong bit = 1UL << s;
                    int count = node.Children.Count(child => (sets[child] & bit) != 0);

                    if (count > best)
                    {
                        best = count;
                        chosen = bit;
                    }
                    else if (count == best)
                    {
                        chosen |= bit;
                    }
                }

                sets[node] = chosen;
                cost += node.Children.Count - best;
            }

            scores[site] = cost;
        }

        return scores;
    }
}
