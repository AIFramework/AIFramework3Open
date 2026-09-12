namespace AI.Biology.Phylogeny;

/// <summary>
/// Модель замен нуклеотидов для правдоподобия: Джукс — Кантор или Кимура.
/// </summary>
/// <remarks>
/// Равновесные частоты нуклеотидов равны четверти. Длина ветви t — ожидаемое число замен
/// на позицию: скорости транзиций α и трансверсий β нормированы так, что α + 2β = 1, а
/// κ = α/β. Вероятности перехода за время t: без замены <c>¼ + ¼e^(−4βt) + ½e^(−2(α+β)t)</c>,
/// в транзицию <c>¼ + ¼e^(−4βt) − ½e^(−2(α+β)t)</c>, в каждую из двух трансверсий <c>¼ − ¼e^(−4βt)</c>.
/// При κ = 1 это Джукс — Кантор.
/// </remarks>
public sealed class NucleotideModel
{
    private NucleotideModel(double kappa) => Kappa = kappa;

    /// <summary>Отношение скоростей транзиций и трансверсий κ</summary>
    public double Kappa { get; }

    /// <summary>Модель Джукса — Кантора</summary>
    public static NucleotideModel JukesCantor { get; } = new(1);

    /// <summary>Модель Кимуры с заданным κ</summary>
    /// <param name="kappa">Отношение скоростей транзиций и трансверсий</param>
    public static NucleotideModel Kimura(double kappa)
        => kappa > 0 && !double.IsInfinity(kappa)
            ? new NucleotideModel(kappa)
            : throw new ArgumentOutOfRangeException(nameof(kappa), "κ должно быть положительным числом");

    /// <summary>
    /// Вероятности перехода за время t: элемент [from·4 + to], нуклеотиды в порядке A, C, G, T
    /// </summary>
    /// <param name="time">Длина ветви</param>
    /// <param name="probabilities">Массив из 16 элементов для результата</param>
    public void Transition(double time, double[] probabilities)
    {
        ArgumentNullException.ThrowIfNull(probabilities);

        if (time < 0 || double.IsNaN(time))
            throw new ArgumentOutOfRangeException(nameof(time), "Длина ветви не может быть отрицательной");

        double beta = 1 / (Kappa + 2);
        double alpha = Kappa / (Kappa + 2);
        double slow = Math.Exp(-4 * beta * time);
        double fast = Math.Exp(-2 * (alpha + beta) * time);

        double same = 0.25 + (0.25 * slow) + (0.5 * fast);
        double transition = 0.25 + (0.25 * slow) - (0.5 * fast);
        double transversion = 0.25 - (0.25 * slow);

        for (int from = 0; from < 4; from++)
        {
            for (int to = 0; to < 4; to++)
            {
                probabilities[(from * 4) + to] = from == to ? same
                    : (from ^ to) == 2 ? transition
                    : transversion;
            }
        }
    }

    /// <summary>Название модели</summary>
    public override string ToString() => Kappa == 1 ? "JC69" : $"K80 (κ = {Kappa})";
}

/// <summary>Итог подбора длин ветвей</summary>
/// <param name="Tree">Дерево с подобранными длинами</param>
/// <param name="LogLikelihood">Логарифм правдоподобия</param>
/// <param name="Passes">Сколько проходов по ветвям понадобилось</param>
public sealed record BranchLengthFit(PhylogeneticTree Tree, double LogLikelihood, int Passes);

/// <summary>
/// Правдоподобие дерева по выравниванию нуклеотидов — алгоритм «обрезки» Фельзенштейна (1981).
/// </summary>
/// <remarks>
/// <para>
/// Правдоподобие позиции — сумма по всем расстановкам нуклеотидов во внутренних узлах, число
/// которых растёт как 4ⁿ. Обрезка считает её за линейное время: для каждого узла снизу вверх
/// хранится вероятность наблюдаемых листьев под ним при каждом состоянии узла. Одинаковые
/// столбцы выравнивания считаются один раз. Чтобы произведения вероятностей не уходили в
/// машинный ноль на больших деревьях, частичные правдоподобия в каждом узле нормируются,
/// а логарифмы множителей накапливаются.
/// </para>
/// <para>
/// Пропуски и неоднозначные символы означают «любой нуклеотид». Длины ветвей подбираются
/// покоординатным подъёмом: по одной ветви за раз золотым сечением по логарифму длины на
/// [10⁻⁸; 10]; одномерного оптимизатора в репозитории нет, поэтому он здесь. Поиска топологии
/// нет — сравнивать деревья по правдоподобию можно, искать лучшее среди всех — нет.
/// </para>
/// </remarks>
public static class TreeLikelihood
{
    private const double ShortestBranch = 1e-8;
    private const double LongestBranch = 10;
    private const int GoldenSteps = 40;

    /// <summary>Логарифм правдоподобия дерева</summary>
    /// <param name="tree">Дерево с неотрицательными длинами ветвей</param>
    /// <param name="alignment">Выравнивание: имя листа → последовательность нуклеотидов</param>
    /// <param name="model">Модель замен; по умолчанию Джукс — Кантор</param>
    public static double LogLikelihood(
        PhylogeneticTree tree, IReadOnlyDictionary<string, string> alignment, NucleotideModel? model = null)
        => new Pruner(tree, alignment, model ?? NucleotideModel.JukesCantor).LogLikelihood();

    /// <summary>Логарифм правдоподобия каждой позиции выравнивания</summary>
    /// <param name="tree">Дерево</param>
    /// <param name="alignment">Выравнивание</param>
    /// <param name="model">Модель замен</param>
    public static double[] SiteLogLikelihoods(
        PhylogeneticTree tree, IReadOnlyDictionary<string, string> alignment, NucleotideModel? model = null)
        => new Pruner(tree, alignment, model ?? NucleotideModel.JukesCantor).SiteLogLikelihoods();

    /// <summary>
    /// Подбирает длины ветвей, максимизируя правдоподобие при фиксированной топологии
    /// </summary>
    /// <param name="tree">Дерево; не изменяется, результат — копия</param>
    /// <param name="alignment">Выравнивание</param>
    /// <param name="model">Модель замен</param>
    /// <param name="maxPasses">Предел проходов по всем ветвям</param>
    /// <param name="tolerance">Проход, улучшивший логарифм правдоподобия меньше этого, последний</param>
    /// <remarks>
    /// Отрицательные длины, которые даёт присоединение соседей, перед подбором заменяются на 10⁻⁸.
    /// Две ветви у двоичного корня по отдельности не определены — важна только их сумма, — и
    /// подобранное разделение между ними произвольно.
    /// </remarks>
    public static BranchLengthFit OptimizeBranchLengths(
        PhylogeneticTree tree, IReadOnlyDictionary<string, string> alignment,
        NucleotideModel? model = null, int maxPasses = 30, double tolerance = 1e-7)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPasses);

        PhylogeneticTree copy = tree.Clone();
        List<TreeNode> branches = copy.PreOrder().Where(n => !ReferenceEquals(n, copy.Root)).ToList();

        foreach (TreeNode branch in branches)
            branch.BranchLength = Math.Clamp(double.IsNaN(branch.BranchLength) ? 0.1 : branch.BranchLength, ShortestBranch, LongestBranch);

        var pruner = new Pruner(copy, alignment, model ?? NucleotideModel.JukesCantor);
        double current = pruner.LogLikelihood();
        int passes = 0;

        while (passes < maxPasses)
        {
            passes++;
            double before = current;

            foreach (TreeNode branch in branches)
                current = MaximizeAlong(branch, pruner, current);

            if (current - before < tolerance)
                break;
        }

        return new BranchLengthFit(copy, current, passes);
    }

    private static double MaximizeAlong(TreeNode branch, Pruner pruner, double current)
    {
        double original = branch.BranchLength;

        double Evaluate(double logLength)
        {
            branch.BranchLength = Math.Exp(logLength);
            return pruner.LogLikelihood();
        }

        double ratio = (Math.Sqrt(5) - 1) / 2;
        double low = Math.Log(ShortestBranch);
        double high = Math.Log(LongestBranch);
        double left = high - (ratio * (high - low));
        double right = low + (ratio * (high - low));
        double leftValue = Evaluate(left);
        double rightValue = Evaluate(right);

        for (int step = 0; step < GoldenSteps; step++)
        {
            if (leftValue >= rightValue)
            {
                high = right;
                right = left;
                rightValue = leftValue;
                left = high - (ratio * (high - low));
                leftValue = Evaluate(left);
            }
            else
            {
                low = left;
                left = right;
                leftValue = rightValue;
                right = low + (ratio * (high - low));
                rightValue = Evaluate(right);
            }
        }

        double bestLog = leftValue >= rightValue ? left : right;
        double bestValue = Math.Max(leftValue, rightValue);

        // Подъём не должен ухудшать: если сечение нашло не лучший максимум, остаётся прежняя длина
        if (bestValue < current)
        {
            branch.BranchLength = original;
            return current;
        }

        branch.BranchLength = Math.Exp(bestLog);

        return bestValue;
    }

    private sealed class Pruner
    {
        private readonly PhylogeneticTree _tree;
        private readonly NucleotideModel _model;
        private readonly List<TreeNode> _postOrder;
        private readonly Dictionary<TreeNode, int> _leafColumn = new();
        private readonly int[][] _patterns;
        private readonly double[] _weights;
        private readonly int[] _siteToPattern;

        public Pruner(PhylogeneticTree tree, IReadOnlyDictionary<string, string> alignment, NucleotideModel model)
        {
            ArgumentNullException.ThrowIfNull(tree);
            ArgumentNullException.ThrowIfNull(alignment);

            _tree = tree;
            _model = model;
            _postOrder = tree.PostOrder().ToList();

            IReadOnlyList<TreeNode> leaves = tree.Leaves;
            var sequences = new string[leaves.Count];
            int length = -1;

            for (int i = 0; i < leaves.Count; i++)
            {
                string? name = leaves[i].Name;

                if (name is null || !alignment.TryGetValue(name, out string? sequence))
                    throw new ArgumentException($"Для листа «{name}» нет последовательности", nameof(alignment));

                if (length >= 0 && sequence.Length != length)
                    throw new ArgumentException("Последовательности выравнивания должны быть одной длины", nameof(alignment));

                length = sequence.Length;
                sequences[i] = sequence;
                _leafColumn[leaves[i]] = i;
            }

            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            var patterns = new List<int[]>();
            var weights = new List<double>();
            _siteToPattern = new int[Math.Max(length, 0)];
            var codes = new int[leaves.Count];
            var key = new char[leaves.Count];

            for (int site = 0; site < length; site++)
            {
                for (int i = 0; i < leaves.Count; i++)
                {
                    codes[i] = EvolutionaryDistance.Nucleotide(sequences[i][site]);
                    key[i] = (char)('0' + codes[i] + 1);
                }

                string text = new(key);

                if (!index.TryGetValue(text, out int pattern))
                {
                    pattern = patterns.Count;
                    index[text] = pattern;
                    patterns.Add((int[])codes.Clone());
                    weights.Add(0);
                }

                weights[pattern]++;
                _siteToPattern[site] = pattern;
            }

            _patterns = patterns.ToArray();
            _weights = weights.ToArray();
        }

        public double LogLikelihood()
        {
            double[] perPattern = PatternLogLikelihoods();
            double total = 0;

            for (int p = 0; p < perPattern.Length; p++)
                total += _weights[p] * perPattern[p];

            return total;
        }

        public double[] SiteLogLikelihoods()
        {
            double[] perPattern = PatternLogLikelihoods();

            return _siteToPattern.Select(p => perPattern[p]).ToArray();
        }

        private double[] PatternLogLikelihoods()
        {
            int count = _patterns.Length;
            var partials = new Dictionary<TreeNode, double[]>();
            var scale = new double[count];
            var matrix = new double[16];

            foreach (TreeNode node in _postOrder)
            {
                var values = new double[count * 4];

                if (node.IsLeaf)
                {
                    int column = _leafColumn[node];

                    for (int p = 0; p < count; p++)
                    {
                        int code = _patterns[p][column];

                        for (int x = 0; x < 4; x++)
                            values[(p * 4) + x] = code < 0 || code == x ? 1 : 0;
                    }
                }
                else
                {
                    Array.Fill(values, 1.0);

                    foreach (TreeNode child in node.Children)
                    {
                        _model.Transition(child.BranchLength, matrix);
                        double[] below = partials[child];

                        for (int p = 0; p < count; p++)
                        {
                            for (int x = 0; x < 4; x++)
                            {
                                double sum = 0;

                                for (int y = 0; y < 4; y++)
                                    sum += matrix[(x * 4) + y] * below[(p * 4) + y];

                                values[(p * 4) + x] *= sum;
                            }
                        }

                        _ = partials.Remove(child);
                    }

                    for (int p = 0; p < count; p++)
                    {
                        double largest = Math.Max(Math.Max(values[p * 4], values[(p * 4) + 1]), Math.Max(values[(p * 4) + 2], values[(p * 4) + 3]));

                        if (largest <= 0)
                            continue;

                        for (int x = 0; x < 4; x++)
                            values[(p * 4) + x] /= largest;

                        scale[p] += Math.Log(largest);
                    }
                }

                partials[node] = values;
            }

            double[] root = partials[_tree.Root];
            var result = new double[count];

            for (int p = 0; p < count; p++)
            {
                double sum = 0.25 * (root[p * 4] + root[(p * 4) + 1] + root[(p * 4) + 2] + root[(p * 4) + 3]);
                result[p] = Math.Log(sum) + scale[p];
            }

            return result;
        }
    }
}
