using System.Buffers;
using AI.Biology.Phylogeny;

namespace AI.Biology.Sequences;

/// <summary>Метод построения дерева-ориентира</summary>
public enum GuideTreeMethod
{
    /// <summary>UPGMA: укоренённое дерево, как в MUSCLE</summary>
    Upgma,

    /// <summary>Присоединение соседей, как в ClustalW; корень — узел степени три</summary>
    NeighborJoining
}

/// <summary>Настройки прогрессивного выравнивания</summary>
public sealed class ProgressiveAlignmentOptions
{
    /// <summary>Метод построения дерева-ориентира</summary>
    public GuideTreeMethod GuideTree { get; init; } = GuideTreeMethod.Upgma;

    /// <summary>Наибольшее число проходов уточнения по разбиениям дерева; нуль — без уточнения</summary>
    public int RefinementPasses { get; init; } = 2;
}

/// <summary>
/// Множественное выравнивание по дереву-ориентиру: прогрессивный метод с уточнением.
/// </summary>
/// <remarks>
/// <para>
/// Точное множественное выравнивание экспоненциально по числу последовательностей, поэтому
/// его строят прогрессивно (Фэн и Дулиттл, 1987; ClustalW, MUSCLE). Все пары выравниваются
/// глобально, расстояние — доля несовпадений среди столбцов без пропусков; по матрице
/// расстояний строится дерево-ориентир — готовыми UPGMA или присоединением соседей из филогении.
/// Затем по дереву снизу вверх сливаются профили: сначала самые близкие последовательности,
/// потом их группы.
/// </para>
/// <para>
/// Профили выравниваются тем же ядром Гото, что и пары. Счёт столбца профиля A против столбца
/// профиля B — средний счёт пар букв, <c>Σ fA(a)·fB(b)·s(a, b)</c>, где частоты считаются от числа
/// строк профиля, так что пропуски вклада не дают. Вставка столбца пропусков стоит так же, как
/// пропуск в паре; пропуски, уже стоящие в профиле, остаются — «однажды пропуск — навсегда пропуск».
/// </para>
/// <para>
/// Ошибки раннего шага частично исправляет уточнение, как в MUSCLE: выравнивание разрезается по
/// каждой ветви дерева на две группы, из групп убираются столбцы сплошных пропусков, профили
/// выравниваются заново, и результат принимается, только если сумма по парам выросла. Поэтому
/// уточнение не может ухудшить выравнивание по этой мере.
/// </para>
/// <para>
/// Весов последовательностей нет: группа из многих близких последовательностей перевешивает
/// одиночную. Штраф пропуска не зависит от позиции, как у ClustalW вблизи гидрофильных участков.
/// Время — n² попарных выравниваний и n слияний профилей, каждое пропорционально произведению
/// длин и размеру алфавита; для сотен последовательностей длиной в тысячи это долго.
/// </para>
/// </remarks>
public static class ProgressiveAlignment
{
    /// <summary>Выравнивает нуклеотидные последовательности</summary>
    /// <param name="labels">Имена</param>
    /// <param name="sequences">Последовательности без пропусков</param>
    /// <param name="scoring">Схема счёта</param>
    /// <param name="options">Настройки</param>
    public static MultipleAlignment Align(
        IReadOnlyList<string> labels, IReadOnlyList<string> sequences,
        ScoringScheme scoring = default, ProgressiveAlignmentOptions? options = null)
    {
        ScoringScheme scheme = Alignment.Resolve(scoring);

        return Run(labels, sequences,
            new PairScoring((a, b) => a == b ? scheme.Match : scheme.Mismatch, scheme.GapOpen, scheme.GapExtend),
            options ?? new ProgressiveAlignmentOptions());
    }

    /// <summary>Выравнивает белки по матрице замен</summary>
    /// <param name="labels">Имена</param>
    /// <param name="sequences">Последовательности без пропусков</param>
    /// <param name="matrix">Матрица замен</param>
    /// <param name="gapOpen">Штраф за открытие пропуска</param>
    /// <param name="gapExtend">Штраф за продление пропуска</param>
    /// <param name="options">Настройки</param>
    public static MultipleAlignment Align(
        IReadOnlyList<string> labels, IReadOnlyList<string> sequences, SubstitutionMatrix matrix,
        double gapOpen = -10, double gapExtend = -0.5, ProgressiveAlignmentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        return Run(labels, sequences, new PairScoring((a, b) => matrix[a, b], gapOpen, gapExtend),
            options ?? new ProgressiveAlignmentOptions());
    }

    private static MultipleAlignment Run(
        IReadOnlyList<string> labels, IReadOnlyList<string> sequences, PairScoring scoring, ProgressiveAlignmentOptions options)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(sequences);
        ArgumentOutOfRangeException.ThrowIfNegative(options.RefinementPasses);

        if (labels.Count == 0 || labels.Count != sequences.Count)
            throw new ArgumentException("Нужна хотя бы одна последовательность, и имён должно быть столько же", nameof(sequences));

        int n = labels.Count;
        var input = new string[n];

        for (int i = 0; i < n; i++)
        {
            string sequence = sequences[i] ?? throw new ArgumentNullException(nameof(sequences));

            if (sequence.Contains('-'))
                throw new ArgumentException($"Последовательность «{labels[i]}» уже содержит пропуски", nameof(sequences));

            input[i] = sequence.ToUpperInvariant();
        }

        var context = new Context(input, scoring);

        if (n == 1)
        {
            return new MultipleAlignment(labels, input)
            {
                GuideTree = new PhylogeneticTree(new TreeNode(labels[0])),
                SumOfPairsScore = 0,
                ProgressiveScore = 0
            };
        }

        DistanceMatrix distances = PairwiseDistances(labels, input, context);
        PhylogeneticTree guide = options.GuideTree == GuideTreeMethod.Upgma
            ? TreeBuilder.Upgma(distances)
            : TreeBuilder.NeighborJoining(distances);

        var byLabel = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < n; i++)
            byLabel[labels[i]] = i;

        var profiles = new Dictionary<TreeNode, Profile>();
        var groups = new List<int[]>();

        foreach (TreeNode node in guide.PostOrder())
        {
            Profile profile;

            if (node.IsLeaf)
            {
                int index = byLabel[node.Name!];
                profile = new Profile([index], [input[index].ToCharArray()]);
            }
            else
            {
                profile = profiles[node.Children[0]];

                for (int k = 1; k < node.Children.Count; k++)
                    profile = Merge(profile, profiles[node.Children[k]], context);

                foreach (TreeNode child in node.Children)
                    _ = profiles.Remove(child);
            }

            profiles[node] = profile;

            if (!ReferenceEquals(node, guide.Root))
                groups.Add(profile.Members.ToArray());
        }

        char[][] rows = Arrange(profiles[guide.Root], n);
        double score = Score(rows, scoring);
        double progressive = score;
        int passes = 0;

        for (int pass = 0; pass < options.RefinementPasses; pass++)
        {
            passes++;
            bool improved = false;

            foreach (int[] group in groups)
            {
                var inGroup = new HashSet<int>(group);
                int[] rest = Enumerable.Range(0, n).Where(i => !inGroup.Contains(i)).ToArray();

                if (rest.Length == 0)
                    continue;

                char[][] candidate = Arrange(Merge(Extract(rows, group), Extract(rows, rest), context), n);
                double candidateScore = Score(candidate, scoring);

                if (candidateScore > score + (1e-9 * Math.Max(1, Math.Abs(score))))
                {
                    rows = candidate;
                    score = candidateScore;
                    improved = true;
                }
            }

            if (!improved)
                break;
        }

        return new MultipleAlignment(labels, rows.Select(r => new string(r)).ToArray())
        {
            GuideTree = guide,
            PairwiseDistances = distances,
            SumOfPairsScore = score,
            ProgressiveScore = progressive,
            RefinementPasses = passes
        };
    }

    private static DistanceMatrix PairwiseDistances(IReadOnlyList<string> labels, string[] input, Context context)
    {
        int n = input.Length;
        var values = new double[n, n];
        int[][] codes = input.Select(s => s.Select(c => context.Code[c]).ToArray()).ToArray();

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                int[] first = codes[i];
                int[] second = codes[j];
                double[] scores = ArrayPool<double>.Shared.Rent(Math.Max(1, first.Length * second.Length));
                AlignmentPath path;

                try
                {
                    for (int x = 0; x < first.Length; x++)
                    {
                        for (int y = 0; y < second.Length; y++)
                            scores[(x * second.Length) + y] = context.Table[first[x], second[y]];
                    }

                    path = GotohAligner.Solve(first.Length, second.Length, scores,
                        context.Scoring.GapOpen, context.Scoring.GapExtend, local: false);
                }
                finally
                {
                    ArrayPool<double>.Shared.Return(scores);
                }

                int compared = 0, identical = 0;

                foreach ((int x, int y) in path.Columns)
                {
                    if (x < 0 || y < 0)
                        continue;

                    compared++;

                    if (first[x] == second[y])
                        identical++;
                }

                double distance = compared == 0 ? 1 : 1 - ((double)identical / compared);
                values[i, j] = distance;
                values[j, i] = distance;
            }
        }

        return new DistanceMatrix(labels, values);
    }

    private static Profile Merge(Profile first, Profile second, Context context)
    {
        double[][] expected = Expected(first, context);
        double[][] frequency = Frequencies(second, context);
        int size = context.Alphabet.Length;
        int n = first.Length, m = second.Length;
        double[] scores = ArrayPool<double>.Shared.Rent(Math.Max(1, n * m));
        AlignmentPath path;

        try
        {
            for (int x = 0; x < n; x++)
            {
                double[] e = expected[x];

                for (int y = 0; y < m; y++)
                {
                    double[] f = frequency[y];
                    double sum = 0;

                    for (int b = 0; b < size; b++)
                        sum += e[b] * f[b];

                    scores[(x * m) + y] = sum;
                }
            }

            path = GotohAligner.Solve(n, m, scores, context.Scoring.GapOpen, context.Scoring.GapExtend, local: false);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(scores);
        }

        int length = path.Columns.Count;
        int total = first.Members.Length + second.Members.Length;
        var rows = new char[total][];

        for (int r = 0; r < total; r++)
            rows[r] = new char[length];

        for (int c = 0; c < length; c++)
        {
            (int x, int y) = path.Columns[c];

            for (int r = 0; r < first.Members.Length; r++)
                rows[r][c] = x >= 0 ? first.Rows[r][x] : '-';

            for (int r = 0; r < second.Members.Length; r++)
                rows[first.Members.Length + r][c] = y >= 0 ? second.Rows[r][y] : '-';
        }

        return new Profile([.. first.Members, .. second.Members], rows);
    }

    // Частоты букв по столбцам: доля строк профиля, у которых в столбце эта буква
    private static double[][] Frequencies(Profile profile, Context context)
    {
        int size = context.Alphabet.Length;
        var result = new double[profile.Length][];
        double weight = 1.0 / profile.Members.Length;

        for (int c = 0; c < profile.Length; c++)
        {
            var column = new double[size];

            foreach (char[] row in profile.Rows)
            {
                if (row[c] != '-')
                    column[context.Code[row[c]]] += weight;
            }

            result[c] = column;
        }

        return result;
    }

    // Ожидаемый счёт буквы b против столбца: Σ f(a)·s(a, b)
    private static double[][] Expected(Profile profile, Context context)
    {
        int size = context.Alphabet.Length;
        double[][] frequency = Frequencies(profile, context);
        var result = new double[profile.Length][];

        for (int c = 0; c < profile.Length; c++)
        {
            var column = new double[size];

            for (int a = 0; a < size; a++)
            {
                double f = frequency[c][a];

                if (f == 0)
                    continue;

                for (int b = 0; b < size; b++)
                    column[b] += f * context.Table[a, b];
            }

            result[c] = column;
        }

        return result;
    }

    // Подвыравнивание группы строк без столбцов, где у группы одни пропуски
    private static Profile Extract(char[][] rows, int[] members)
    {
        int length = rows[members[0]].Length;
        var keep = Enumerable.Range(0, length).Where(c => members.Any(m => rows[m][c] != '-')).ToArray();
        var extracted = members.Select(m => keep.Select(c => rows[m][c]).ToArray()).ToArray();

        return new Profile(members, extracted);
    }

    private static char[][] Arrange(Profile profile, int count)
    {
        var rows = new char[count][];

        for (int k = 0; k < profile.Members.Length; k++)
            rows[profile.Members[k]] = profile.Rows[k];

        return rows;
    }

    private static double Score(char[][] rows, PairScoring scoring)
        => MultipleAlignment.SumOfPairs(rows.Length, rows[0].Length, (r, c) => rows[r][c], scoring);

    private sealed class Profile(int[] members, char[][] rows)
    {
        public int[] Members { get; } = members;

        public char[][] Rows { get; } = rows;

        public int Length => Rows.Length == 0 ? 0 : Rows[0].Length;
    }

    private sealed class Context
    {
        public Context(string[] sequences, PairScoring scoring)
        {
            Scoring = scoring;
            Alphabet = new string(sequences.SelectMany(s => s).Distinct().OrderBy(c => c).ToArray());
            Code = new Dictionary<char, int>();
            Table = new double[Alphabet.Length, Alphabet.Length];

            for (int a = 0; a < Alphabet.Length; a++)
            {
                Code[Alphabet[a]] = a;

                // Буква, которой нет в матрице замен, вызовет ошибку здесь, до выравнивания
                for (int b = 0; b < Alphabet.Length; b++)
                    Table[a, b] = scoring.Similarity(Alphabet[a], Alphabet[b]);
            }
        }

        public PairScoring Scoring { get; }

        public string Alphabet { get; }

        public Dictionary<char, int> Code { get; }

        public double[,] Table { get; }
    }
}
