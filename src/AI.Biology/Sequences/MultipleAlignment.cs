using System.Text;
using AI.Biology.Phylogeny;
using AI.Insights;

namespace AI.Biology.Sequences;

/// <summary>Точность выравнивания относительно эталонного</summary>
/// <param name="PairScore">Доля пар остатков эталона, выровненных так же (SP-мера BAliBASE)</param>
/// <param name="ColumnScore">Доля столбцов эталона, воспроизведённых целиком (TC-мера)</param>
/// <param name="ReferencePairs">Число пар остатков в эталоне</param>
/// <param name="ReferenceColumns">Число столбцов эталона, где не меньше двух остатков</param>
public readonly record struct AlignmentAccuracy(double PairScore, double ColumnScore, int ReferencePairs, int ReferenceColumns);

/// <summary>
/// Множественное выравнивание: последовательности, дополненные пропусками до общей длины,
/// так что в каждом столбце стоят предположительно гомологичные позиции.
/// </summary>
/// <remarks>
/// <para>
/// Качество выравнивания без эталона оценивают суммой по парам (SP): каждая пара строк
/// проецируется в попарное выравнивание — столбцы из двух пропусков выбрасываются — и получает
/// обычный счёт с аффинным штрафом. С эталоном — долей верно выровненных пар остатков и долей
/// целиком воспроизведённых столбцов, как в наборе BAliBASE.
/// </para>
/// <para>
/// Строки выравнивания — выровненные последовательности для филогении: <see cref="Distances"/>
/// строит по ним матрицу расстояний, где позиции с пропуском отбрасываются попарно.
/// </para>
/// </remarks>
public sealed class MultipleAlignment : IInterpretable
{
    private readonly string[] _labels;
    private readonly string[] _rows;
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

    /// <summary>Создаёт выравнивание из готовых строк</summary>
    /// <param name="labels">Имена последовательностей</param>
    /// <param name="rows">Строки одной длины; пропуск — «-»</param>
    public MultipleAlignment(IReadOnlyList<string> labels, IReadOnlyList<string> rows)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(rows);

        if (labels.Count == 0 || labels.Count != rows.Count)
            throw new ArgumentException("Нужна хотя бы одна строка, и имён должно быть столько же, сколько строк", nameof(rows));

        for (int i = 0; i < labels.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(labels[i]) || !_index.TryAdd(labels[i], i))
                throw new ArgumentException($"Имя «{labels[i]}» пусто или повторяется", nameof(labels));

            if (rows[i] is null || rows[i].Length != rows[0].Length)
                throw new ArgumentException("Строки выравнивания должны быть одной длины", nameof(rows));
        }

        _labels = labels.ToArray();
        _rows = rows.ToArray();
    }

    /// <summary>Имена последовательностей</summary>
    public IReadOnlyList<string> Labels => _labels;

    /// <summary>Строки с пропусками</summary>
    public IReadOnlyList<string> Rows => _rows;

    /// <summary>Число последовательностей</summary>
    public int Count => _rows.Length;

    /// <summary>Число столбцов</summary>
    public int Length => _rows[0].Length;

    /// <summary>Строка по имени</summary>
    /// <param name="label">Имя последовательности</param>
    public string this[string label]
        => _index.TryGetValue(label, out int i) ? _rows[i] : throw new KeyNotFoundException($"Последовательности «{label}» нет");

    /// <summary>Дерево-ориентир, по которому шло выравнивание; null для выравнивания из готовых строк</summary>
    public PhylogeneticTree? GuideTree { get; internal init; }

    /// <summary>Попарные расстояния, по которым построено дерево-ориентир</summary>
    public DistanceMatrix? PairwiseDistances { get; internal init; }

    /// <summary>Сумма по парам в схеме счёта, которой выравнивали</summary>
    public double? SumOfPairsScore { get; internal init; }

    /// <summary>Сумма по парам после прогрессивного шага, до уточнения</summary>
    public double? ProgressiveScore { get; internal init; }

    /// <summary>Сколько проходов уточнения выполнено</summary>
    public int RefinementPasses { get; internal init; }

    /// <summary>Последовательность без пропусков</summary>
    /// <param name="index">Номер строки</param>
    public string Sequence(int index) => _rows[index].Replace("-", string.Empty, StringComparison.Ordinal);

    /// <summary>Число столбцов, где во всех строках одна и та же буква</summary>
    public int ConservedColumns
    {
        get
        {
            int conserved = 0;

            for (int c = 0; c < Length; c++)
            {
                char first = char.ToUpperInvariant(_rows[0][c]);

                if (first != '-' && _rows.All(r => char.ToUpperInvariant(r[c]) == first))
                    conserved++;
            }

            return conserved;
        }
    }

    /// <summary>Доля пропусков среди всех позиций</summary>
    public double GapFraction => Length == 0 ? 0 : (double)_rows.Sum(r => r.Count(c => c == '-')) / (Count * Length);

    /// <summary>Средняя по парам доля совпадений среди столбцов без пропусков</summary>
    public double MeanPairwiseIdentity
    {
        get
        {
            double sum = 0;
            int pairs = 0;

            for (int s = 0; s < Count; s++)
            {
                for (int t = s + 1; t < Count; t++)
                {
                    int compared = 0, identical = 0;

                    for (int c = 0; c < Length; c++)
                    {
                        char a = char.ToUpperInvariant(_rows[s][c]);
                        char b = char.ToUpperInvariant(_rows[t][c]);

                        if (a == '-' || b == '-')
                            continue;

                        compared++;

                        if (a == b)
                            identical++;
                    }

                    if (compared == 0)
                        continue;

                    sum += (double)identical / compared;
                    pairs++;
                }
            }

            return pairs == 0 ? 0 : sum / pairs;
        }
    }

    /// <summary>Сумма по парам при схеме счёта для нуклеотидов</summary>
    /// <param name="scoring">Схема счёта</param>
    public double SumOfPairs(ScoringScheme scoring = default)
    {
        ScoringScheme scheme = Alignment.Resolve(scoring);

        return SumOfPairs(_rows.Length, Length, (r, c) => _rows[r][c],
            new PairScoring((a, b) => char.ToUpperInvariant(a) == char.ToUpperInvariant(b) ? scheme.Match : scheme.Mismatch,
                scheme.GapOpen, scheme.GapExtend));
    }

    /// <summary>Сумма по парам при матрице замен</summary>
    /// <param name="matrix">Матрица замен</param>
    /// <param name="gapOpen">Штраф за открытие пропуска</param>
    /// <param name="gapExtend">Штраф за продление пропуска</param>
    public double SumOfPairs(SubstitutionMatrix matrix, double gapOpen = -10, double gapExtend = -0.5)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        return SumOfPairs(_rows.Length, Length, (r, c) => _rows[r][c], new PairScoring((a, b) => matrix[a, b], gapOpen, gapExtend));
    }

    /// <summary>
    /// Точность относительно эталонного выравнивания тех же последовательностей
    /// </summary>
    /// <param name="reference">Эталон: те же имена и те же последовательности без пропусков</param>
    public AlignmentAccuracy CompareWith(MultipleAlignment reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (reference.Count != Count)
            throw new ArgumentException("В эталоне другое число последовательностей", nameof(reference));

        // Номер строки этого выравнивания для каждой строки эталона
        var own = new int[Count];

        for (int r = 0; r < Count; r++)
        {
            if (!_index.TryGetValue(reference._labels[r], out int i))
                throw new ArgumentException($"В выравнивании нет последовательности «{reference._labels[r]}»", nameof(reference));

            if (!string.Equals(reference.Sequence(r), Sequence(i), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Последовательность «{reference._labels[r]}» в эталоне другая", nameof(reference));

            own[i] = r;
        }

        List<List<(int Row, int Residue)>> mine = ColumnResidues(this, own);
        List<List<(int Row, int Residue)>> theirs = ColumnResidues(reference, Enumerable.Range(0, Count).ToArray());

        var minePairs = new HashSet<(int, int, int, int)>();

        foreach (List<(int Row, int Residue)> column in mine)
        {
            foreach ((int a, int b) in Pairs(column.Count))
                minePairs.Add((column[a].Row, column[a].Residue, column[b].Row, column[b].Residue));
        }

        var mineColumns = new HashSet<string>(mine.Where(c => c.Count >= 2).Select(Key), StringComparer.Ordinal);
        int referencePairs = 0, matchedPairs = 0, referenceColumns = 0, matchedColumns = 0;

        foreach (List<(int Row, int Residue)> column in theirs)
        {
            foreach ((int a, int b) in Pairs(column.Count))
            {
                referencePairs++;

                if (minePairs.Contains((column[a].Row, column[a].Residue, column[b].Row, column[b].Residue)))
                    matchedPairs++;
            }

            if (column.Count < 2)
                continue;

            referenceColumns++;

            if (mineColumns.Contains(Key(column)))
                matchedColumns++;
        }

        return new AlignmentAccuracy(
            referencePairs == 0 ? 1 : (double)matchedPairs / referencePairs,
            referenceColumns == 0 ? 1 : (double)matchedColumns / referenceColumns,
            referencePairs,
            referenceColumns);
    }

    /// <summary>Матрица эволюционных расстояний по выровненным строкам</summary>
    /// <param name="model">Модель замен</param>
    public DistanceMatrix Distances(DistanceModel model = DistanceModel.JukesCantor)
        => DistanceMatrix.FromSequences(_labels, _rows, model);

    /// <summary>Запись в формате FASTA</summary>
    /// <param name="lineWidth">Символов в строке</param>
    public string ToFasta(int lineWidth = 60)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineWidth);

        var text = new StringBuilder();

        for (int r = 0; r < Count; r++)
        {
            text.Append('>').AppendLine(_labels[r]);

            for (int start = 0; start < Length; start += lineWidth)
                text.AppendLine(_rows[r].Substring(start, Math.Min(lineWidth, Length - start)));
        }

        return text.ToString();
    }

    /// <summary>Читает выравнивание из FASTA</summary>
    /// <param name="text">Записи «&gt;имя» и строки с пропусками</param>
    /// <exception cref="FormatException">Нет записей, строка до заголовка или разная длина строк</exception>
    public static MultipleAlignment ParseFasta(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var labels = new List<string>();
        var rows = new List<StringBuilder>();

        foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();

            if (line.Length == 0)
                continue;

            if (line[0] == '>')
            {
                string label = line[1..].Trim();

                if (label.Length == 0)
                    throw new FormatException("Пустое имя в заголовке FASTA");

                labels.Add(label);
                rows.Add(new StringBuilder());
                continue;
            }

            if (rows.Count == 0)
                throw new FormatException("Строка последовательности до первого заголовка «>»");

            foreach (char c in line)
            {
                if (!char.IsWhiteSpace(c))
                    rows[^1].Append(c);
            }
        }

        if (labels.Count == 0)
            throw new FormatException("В тексте нет ни одной записи FASTA");

        string[] result = rows.Select(r => r.ToString()).ToArray();

        return result.Any(r => r.Length != result[0].Length)
            ? throw new FormatException("Строки выравнивания разной длины: это не выравнивание, а набор последовательностей")
            : new MultipleAlignment(labels, result);
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int conserved = ConservedColumns;
        double identity = MeanPairwiseIdentity;

        var builder = new InterpretationBuilder("Множественное выравнивание")
            .Summary($"Последовательностей {Count}, столбцов {Length}: полностью консервативных {conserved} "
                + $"({Fmt.Pct(Length == 0 ? 0 : (double)conserved / Length)}), средняя попарная идентичность "
                + $"{Fmt.Pct(identity)}, пропусков {Fmt.Pct(GapFraction)}.")
            .Metric("Последовательностей", Count, null, null, MetricQuality.Unknown, 0)
            .Metric("Столбцов", Length, null, null, MetricQuality.Unknown, 0)
            .Metric("Консервативных столбцов", conserved, null, "одна и та же буква во всех строках", MetricQuality.Unknown, 0)
            .Metric("Средняя идентичность", Fmt.Pct(identity), null, "по парам, среди столбцов без пропусков",
                identity < 0.25 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Доля пропусков", Fmt.Pct(GapFraction), null, null);

        if (SumOfPairsScore is double score)
            builder = builder.Metric("Сумма по парам", score, null, "счёт всех попарных проекций", MetricQuality.Unknown, 1);

        double gain = SumOfPairsScore is double after && ProgressiveScore is double before ? after - before : 0;

        return builder
            .FindingIf(gain > 0,
                $"Уточнение по разбиениям дерева подняло сумму по парам на {Fmt.Num(gain, 1)} за {RefinementPasses} проход(а): "
                + "часть решений раннего прогрессивного шага оказалась неудачной и пересмотрена.")
            .WarningIf(identity < 0.25,
                "Идентичность ниже четверти — «сумеречная зона»: при таком сходстве выравнивание по одной "
                + "последовательности ненадёжно, и отдельные столбцы могут не отражать гомологию.")
            .WarningIf(GuideTree is not null,
                "Выравнивание прогрессивное: пропуск, вставленный на раннем шаге, сохраняется до конца. "
                + "Дерево-ориентир построено по попарной идентичности и служит порядком выравнивания, "
                + "а не оценкой филогении — для неё стройте дерево заново по готовому выравниванию.")
            .Build();
    }

    /// <summary>Выравнивание блоками по 60 столбцов, звёздочка под консервативным столбцом</summary>
    public override string ToString()
    {
        int width = Math.Min(20, _labels.Max(l => l.Length));
        var text = new StringBuilder();

        for (int start = 0; start < Length || start == 0; start += 60)
        {
            int size = Math.Min(60, Length - start);

            for (int r = 0; r < Count; r++)
            {
                string label = _labels[r].Length > width ? _labels[r][..width] : _labels[r].PadRight(width);
                text.Append(label).Append(' ').AppendLine(_rows[r].Substring(start, size));
            }

            text.Append(new string(' ', width + 1));

            for (int c = start; c < start + size; c++)
            {
                char first = char.ToUpperInvariant(_rows[0][c]);
                text.Append(first != '-' && _rows.All(row => char.ToUpperInvariant(row[c]) == first) ? '*' : ' ');
            }

            text.AppendLine().AppendLine();

            if (Length == 0)
                break;
        }

        return text.ToString();
    }

    /// <summary>Сумма по парам: каждая пара строк проецируется и получает счёт с аффинным штрафом</summary>
    internal static double SumOfPairs(int count, int length, Func<int, int, char> at, PairScoring scoring)
    {
        double total = 0;

        for (int s = 0; s < count; s++)
        {
            for (int t = s + 1; t < count; t++)
            {
                bool gapInS = false, gapInT = false;

                for (int c = 0; c < length; c++)
                {
                    char a = at(s, c);
                    char b = at(t, c);

                    if (a == '-' && b == '-')
                        continue;

                    if (a == '-')
                    {
                        total += gapInS ? scoring.GapExtend : scoring.GapOpen;
                        gapInS = true;
                        gapInT = false;
                    }
                    else if (b == '-')
                    {
                        total += gapInT ? scoring.GapExtend : scoring.GapOpen;
                        gapInT = true;
                        gapInS = false;
                    }
                    else
                    {
                        total += scoring.Similarity(a, b);
                        gapInS = false;
                        gapInT = false;
                    }
                }
            }
        }

        return total;
    }

    private static List<List<(int Row, int Residue)>> ColumnResidues(MultipleAlignment alignment, int[] rowNumber)
    {
        var position = new int[alignment.Count];
        var columns = new List<List<(int, int)>>(alignment.Length);

        for (int c = 0; c < alignment.Length; c++)
        {
            var column = new List<(int, int)>();

            for (int r = 0; r < alignment.Count; r++)
            {
                if (alignment._rows[r][c] == '-')
                    continue;

                column.Add((rowNumber[r], position[r]++));
            }

            column.Sort();
            columns.Add(column);
        }

        return columns;
    }

    private static IEnumerable<(int, int)> Pairs(int count)
    {
        for (int a = 0; a < count; a++)
            for (int b = a + 1; b < count; b++)
                yield return (a, b);
    }

    private static string Key(List<(int Row, int Residue)> column)
        => string.Join(";", column.Select(p => $"{p.Row}:{p.Residue}"));
}
