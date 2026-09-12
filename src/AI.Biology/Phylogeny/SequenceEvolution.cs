using AI.Biology.Sequences;

namespace AI.Biology.Phylogeny;

/// <summary>
/// Моделирование эволюции последовательностей вдоль дерева.
/// </summary>
/// <remarks>
/// Корень получает случайную последовательность с равными частотами нуклеотидов, дальше каждая
/// позиция независимо меняется вдоль каждой ветви с вероятностями перехода выбранной модели.
/// Это те же допущения, на которых стоят расстояния и правдоподобие, поэтому смоделированные
/// данные — честная проверка методов реконструкции: истинное дерево известно.
/// </remarks>
public static class SequenceEvolution
{
    private const string Letters = "ACGT";

    /// <summary>Моделирует последовательности листьев</summary>
    /// <param name="tree">Дерево с неотрицательными длинами ветвей и различными именами листьев</param>
    /// <param name="length">Длина последовательностей</param>
    /// <param name="random">Генератор случайных чисел</param>
    /// <param name="model">Модель замен; по умолчанию Джукс — Кантор</param>
    public static IReadOnlyDictionary<string, string> Simulate(
        PhylogeneticTree tree, int length, Random random, NucleotideModel? model = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        model ??= NucleotideModel.JukesCantor;

        var states = new Dictionary<TreeNode, byte[]>();
        var matrix = new double[16];
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (TreeNode node in tree.PreOrder())
        {
            var sequence = new byte[length];

            if (node.Parent is null)
            {
                for (int site = 0; site < length; site++)
                    sequence[site] = (byte)random.Next(4);
            }
            else
            {
                model.Transition(node.BranchLength, matrix);
                byte[] parent = states[node.Parent];

                for (int site = 0; site < length; site++)
                    sequence[site] = Substitute(parent[site], matrix, random);
            }

            states[node] = sequence;

            if (!node.IsLeaf)
                continue;

            if (string.IsNullOrWhiteSpace(node.Name) || result.ContainsKey(node.Name))
                throw new ArgumentException($"Имя листа «{node.Name}» пусто или повторяется", nameof(tree));

            result[node.Name] = new string(sequence.Select(s => Letters[s]).ToArray());
        }

        return result;
    }

    /// <summary>
    /// Моделирует эволюцию с заменами, вставками и делециями и возвращает истинное выравнивание листьев
    /// </summary>
    /// <param name="tree">Дерево с неотрицательными длинами ветвей и различными именами листьев</param>
    /// <param name="rootLength">Длина последовательности в корне</param>
    /// <param name="random">Генератор случайных чисел</param>
    /// <param name="indelRate">Ожидаемое число вставок и делеций на позицию на единицу длины ветви</param>
    /// <param name="meanIndelLength">Средняя длина вставки или делеции; длина распределена геометрически</param>
    /// <param name="model">Модель замен; по умолчанию Джукс — Кантор</param>
    /// <remarks>
    /// <para>
    /// Каждая позиция помнит, от какой позиции корня или какой вставки она происходит, и в
    /// выравнивании гомологичные позиции встают в один столбец. Такое выравнивание истинно по
    /// построению, и с ним сравнивают методы выравнивания — так же, как с истинным деревом сравнивают
    /// методы филогении.
    /// </para>
    /// <para>
    /// Число событий на ветви — пуассоновское с ожиданием <c>indelRate·t·длина</c>, половина — вставки,
    /// половина — делеции; события разыгрываются по очереди, без учёта того, что длина меняется
    /// вдоль ветви. Вставки из разных ветвей в одно место встают друг за другом в произвольном
    /// порядке: по отношению друг к другу они — пропуски, и порядок на выравнивание не влияет.
    /// </para>
    /// </remarks>
    public static MultipleAlignment SimulateAlignment(
        PhylogeneticTree tree, int rootLength, Random random,
        double indelRate = 0.05, double meanIndelLength = 2, NucleotideModel? model = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rootLength);

        if (indelRate < 0 || double.IsNaN(indelRate))
            throw new ArgumentOutOfRangeException(nameof(indelRate), "Частота вставок и делеций не может быть отрицательной");

        if (!(meanIndelLength >= 1))
            throw new ArgumentOutOfRangeException(nameof(meanIndelLength), "Средняя длина вставки не меньше единицы");

        model ??= NucleotideModel.JukesCantor;

        // Общий порядок столбцов: каждая последовательность — подпоследовательность этого порядка
        var order = new List<int>(Enumerable.Range(0, rootLength));
        int nextColumn = rootLength;
        var sequences = new Dictionary<TreeNode, List<(int Column, byte State)>>();
        var matrix = new double[16];

        foreach (TreeNode node in tree.PreOrder())
        {
            if (node.Parent is null)
            {
                sequences[node] = Enumerable.Range(0, rootLength).Select(c => (c, (byte)random.Next(4))).ToList();
                continue;
            }

            model.Transition(node.BranchLength, matrix);
            var sequence = sequences[node.Parent].Select(site => (site.Column, Substitute(site.State, matrix, random))).ToList();

            int events = Poisson(indelRate * node.BranchLength * sequence.Count, random);

            for (int e = 0; e < events; e++)
            {
                int length = GeometricLength(meanIndelLength, random);

                if (random.Next(2) == 0 && sequence.Count > 0)
                {
                    int start = random.Next(sequence.Count);
                    sequence.RemoveRange(start, Math.Min(length, sequence.Count - start));
                    continue;
                }

                int position = random.Next(sequence.Count + 1);
                int globalIndex = sequence.Count == 0 ? order.Count
                    : position == 0 ? order.IndexOf(sequence[0].Column)
                    : order.IndexOf(sequence[position - 1].Column) + 1;

                var inserted = new List<(int, byte)>(length);

                for (int k = 0; k < length; k++)
                {
                    order.Insert(globalIndex + k, nextColumn);
                    inserted.Add((nextColumn++, (byte)random.Next(4)));
                }

                sequence.InsertRange(position, inserted);
            }

            sequences[node] = sequence;
        }

        IReadOnlyList<TreeNode> leaves = tree.Leaves;
        var rank = new Dictionary<int, int>();

        for (int i = 0; i < order.Count; i++)
            rank[order[i]] = i;

        var used = new SortedSet<int>(leaves.SelectMany(l => sequences[l]).Select(s => rank[s.Column]));
        var columnOf = used.Select((r, i) => (r, i)).ToDictionary(p => p.r, p => p.i);
        var rows = new List<string>();
        var labels = new List<string>();

        foreach (TreeNode leaf in leaves)
        {
            var row = Enumerable.Repeat('-', used.Count).ToArray();

            foreach ((int column, byte state) in sequences[leaf])
                row[columnOf[rank[column]]] = Letters[state];

            labels.Add(leaf.Name ?? throw new ArgumentException("У листа нет имени", nameof(tree)));
            rows.Add(new string(row));
        }

        return new MultipleAlignment(labels, rows);
    }

    private static byte Substitute(byte from, double[] matrix, Random random)
    {
        double draw = random.NextDouble();

        for (int to = 0; to < 4; to++)
        {
            draw -= matrix[(from * 4) + to];

            if (draw < 0)
                return (byte)to;
        }

        return 3;
    }

    private static int Poisson(double mean, Random random)
    {
        double limit = Math.Exp(-mean);
        double product = random.NextDouble();
        int count = 0;

        while (product > limit)
        {
            count++;
            product *= random.NextDouble();
        }

        return count;
    }

    private static int GeometricLength(double mean, Random random)
    {
        double stop = 1 / mean;
        int length = 1;

        while (random.NextDouble() > stop)
            length++;

        return length;
    }
}
