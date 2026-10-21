using AI.Biology.Phylogeny;
using AI.Biology.Sequences;
using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Std;

namespace AI.Script.Science;

/// <summary>
/// Пространство <c>bio</c>: последовательности, выравнивание, филогения, популяции и генетика.
/// </summary>
/// <remarks>
/// Последовательность — обычная строка: <c>"ATGGCC"</c>. Регистр и пробелы по краям не важны;
/// буква U означает РНК, иначе последовательность читается как ДНК. Неоднозначные обозначения
/// (N, R, Y) не принимаются — отказ называет символ, а не молча считает его за пропуск.
/// Расчёты — из <c>AI.Biology</c>: генетический код, выравнивание Гото, модели замен, UPGMA и
/// присоединение соседей, модели популяций и эпидемий.
/// </remarks>
[ScriptModule("bio", "Биология: ДНК, белки, выравнивание, деревья, популяции, генетика", Version = "0.1", Group = "отрасли")]
public static partial class BioModule
{
    // --- последовательности ---

    [ScriptFn("gc", "Доля G и C в нуклеотидной последовательности, от 0 до 1",
        Example = "bio.gc(\"ATGGCCATTGTAATGGGCCGC\")")]
    public static double Gc([ScriptParam("последовательность ДНК или РНК")] string sequence)
        => Sequence(sequence).GcContent;

    [ScriptFn("reverse_complement", "Обратно-комплементарная цепь: вторая цепь в обратном порядке",
        Example = "bio.reverse_complement(\"ATGGCC\")")]
    public static string ReverseComplement([ScriptParam("последовательность ДНК или РНК")] string sequence)
        => Sequence(sequence).ReverseComplement().Letters;

    [ScriptFn("transcribe", "Транскрипция ДНК в мРНК: T заменяется на U",
        Example = "bio.transcribe(\"ATGGCC\")")]
    public static string Transcribe([ScriptParam("последовательность ДНК")] string sequence)
        => Sequence(sequence).Transcribe().Letters;

    /// <summary>
    /// Трансляция по стандартному генетическому коду.
    /// </summary>
    /// <remarks>
    /// Белок — в однобуквенном коде аминокислот. По умолчанию трансляция идёт до первого
    /// стоп-кодона; с <c>to_stop: false</c> стоп-кодоны остаются в строке звёздочками. Неполный
    /// последний кодон отбрасывается.
    /// </remarks>
    [ScriptFn("translate", "Трансляция нуклеотидов в белок по стандартному генетическому коду",
        Example = "bio.translate(\"ATGGCCATTGTAATGGGCCGCTGA\")")]
    public static string Translate(
        [ScriptParam("последовательность ДНК или мРНК")] string sequence,
        [ScriptParam("сдвиг рамки считывания: 0, 1 или 2")] int frame = 0,
        [ScriptParam("останавливаться ли на первом стоп-кодоне")] bool to_stop = true)
        => GeneticCode.Translate(Sequence(sequence), frame, to_stop);

    /// <summary>
    /// Открытые рамки считывания в трёх прямых рамках.
    /// </summary>
    /// <remarks>
    /// Рамка — от кодона ATG до ближайшего стоп-кодона; <c>start</c> — позиция ATG с нуля,
    /// <c>length</c> — длина в нуклеотидах со стоп-кодоном. Короткие рамки встречаются в случайной
    /// последовательности сплошь и рядом, поэтому порог длины белка — не украшение.
    /// Обратную цепь проверяют так же: <c>bio.orfs(bio.reverse_complement(s))</c>.
    /// </remarks>
    [ScriptFn("orfs", "Открытые рамки считывания: от ATG до стоп-кодона, с белком",
        Example = "bio.orfs(\"CCATGGCCATTGTAATGGGCTGAAA\", min_length: 3)")]
    public static ScriptTable Orfs(
        [ScriptParam("последовательность ДНК или мРНК")] string sequence,
        [ScriptParam("наименьшая длина белка, аминокислот")] int min_length = 30)
        => ScriptData.Table(GeneticCode.FindOpenReadingFrames(Sequence(sequence), min_length),
            ("start", frame => frame.Start),
            ("length", frame => frame.Length),
            ("protein", frame => frame.Protein));

    /// <summary>
    /// Температура плавления короткого праймера.
    /// </summary>
    /// <remarks>
    /// До 14 нуклеотидов — правило Уоллеса (2 °C на A и T, 4 °C на G и C), длиннее — поправка на
    /// состав. Оценка грубая: соли и концентрация праймера в ней не учтены.
    /// </remarks>
    [ScriptFn("melting", "Температура плавления праймера, °C: правило Уоллеса либо поправка на GC",
        Example = "bio.melting(\"AGCTTAGCGGCATTAC\")")]
    public static double Melting([ScriptParam("последовательность праймера")] string sequence)
        => Sequence(sequence).MeltingTemperatureCelsius();

    [ScriptFn("kmers", "Частоты слов длины k в последовательности: таблица kmer, count",
        Example = "bio.kmers(\"ATGATGCC\", 3)")]
    public static ScriptTable Kmers(
        [ScriptParam("последовательность ДНК или РНК")] string sequence,
        [ScriptParam("длина слова")] int k)
        => ScriptData.Table(
            [.. Sequence(sequence).KmerCounts(k).OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal)],
            ("kmer", pair => pair.Key),
            ("count", pair => pair.Value));

    // --- сравнение ---

    /// <summary>
    /// Попарное выравнивание с аффинным штрафом за пропуск.
    /// </summary>
    /// <remarks>
    /// <c>kind: "global"</c> — Нидлман — Вунш, концы к концам; <c>"local"</c> — Смит — Уотерман,
    /// лучший общий участок. С <c>matrix: "blosum62"</c> выравниваются белки по матрице замен, и
    /// тогда <c>match</c> и <c>mismatch</c> не нужны. Штраф открытия пропуска по умолчанию — −2 для
    /// нуклеотидов и −10 для белков. В ответе выровненные строки <c>first</c> и <c>second</c> с
    /// пропусками «-», <c>identity</c> — доля совпавших позиций, <c>text</c> — выравнивание в три
    /// строки для печати.
    /// </remarks>
    [ScriptFn("align", "Выравнивание двух последовательностей: глобальное или локальное, счёт",
        Example = "bio.align(\"GATTACA\", \"GCATGCA\").identity")]
    public static ScriptRecord Align(
        [ScriptParam("первая последовательность")] string first,
        [ScriptParam("вторая последовательность")] string second,
        [ScriptParam("\"global\" — целиком, \"local\" — лучший общий участок")] string kind = "global",
        [ScriptParam("матрица замен для белков: \"blosum62\"; пусто — нуклеотиды")] string matrix = "",
        [ScriptParam("награда за совпадение")] double match = 1,
        [ScriptParam("штраф за несовпадение")] double mismatch = -1,
        [ScriptParam("штраф открытия пропуска; nan — по умолчанию")] double gap_open = double.NaN,
        [ScriptParam("штраф продления пропуска")] double gap_extend = -0.5)
    {
        const string function = "bio.align";

        bool local = ScriptData.Kind<bool>(kind, "kind", function, ("global", false), ("local", true));
        SubstitutionMatrix? substitution = ScriptData.Kind<SubstitutionMatrix?>(matrix, "matrix", function,
            ("", null), ("blosum62", SubstitutionMatrix.Blosum62));

        string a = first.Trim().ToUpperInvariant();
        string b = second.Trim().ToUpperInvariant();
        AlignmentResult result;

        if (substitution != null)
        {
            double open = double.IsNaN(gap_open) ? -10 : gap_open;

            result = local
                ? Alignment.Local(a, b, substitution, open, gap_extend)
                : Alignment.Global(a, b, substitution, open, gap_extend);
        }
        else
        {
            var scoring = new ScoringScheme(match, mismatch, double.IsNaN(gap_open) ? -2 : gap_open, gap_extend);

            result = local ? Alignment.Local(a, b, scoring) : Alignment.Global(a, b, scoring);
        }

        return ScriptData.Record(result,
            ("score", result.Score),
            ("first", result.First),
            ("second", result.Second),
            ("identity", result.Identity),
            ("matches", result.Matches),
            ("gaps", result.Gaps),
            ("text", result.ToString()));
    }

    [ScriptFn("hamming", "Расстояние Хэмминга: число различающихся позиций строк равной длины",
        Example = "bio.hamming(\"GATTACA\", \"GACTATA\")")]
    public static double Hamming(
        [ScriptParam("первая последовательность")] string first,
        [ScriptParam("вторая последовательность той же длины")] string second)
        => Alignment.HammingDistance(first.Trim().ToUpperInvariant(), second.Trim().ToUpperInvariant());

    /// <summary>
    /// Эволюционное расстояние между выровненными последовательностями.
    /// </summary>
    /// <remarks>
    /// <c>"p"</c> — доля различий без поправки; <c>"jukes_cantor"</c> — поправка на повторные
    /// замены при равновероятных заменах; <c>"kimura"</c> — транзиции и трансверсии с разной
    /// скоростью; <c>"poisson"</c> — для белков. Позиции с пропуском «-» не сравниваются.
    /// Различий 75 % и больше по Джуксу — Кантору — бесконечность: сходство не отличить от
    /// случайного.
    /// </remarks>
    [ScriptFn("distance", "Эволюционное расстояние: p, Джукс — Кантор, Кимура или Пуассон",
        Example = "bio.distance(\"ACGTACGTAC\", \"ACGTTCGTAA\", kind: \"kimura\")")]
    public static double Distance(
        [ScriptParam("первая выровненная последовательность")] string first,
        [ScriptParam("вторая выровненная последовательность той же длины")] string second,
        [ScriptParam("модель: \"p\", \"jukes_cantor\", \"kimura\", \"poisson\"")] string kind = "jukes_cantor")
        => EvolutionaryDistance.Distance(
            first.Trim().ToUpperInvariant(), second.Trim().ToUpperInvariant(), Model(kind, "kind", "bio.distance"));

    /// <summary>
    /// Филогенетическое дерево по выровненным последовательностям.
    /// </summary>
    /// <remarks>
    /// Последовательности — запись «имя: строка», все одной длины (выровненные). Расстояния
    /// считаются по модели <c>distance</c>, дерево строит присоединение соседей (<c>"nj"</c>, без
    /// допущения о часах) либо UPGMA (<c>"upgma"</c>, укоренённое, верно при равной скорости
    /// эволюции). Ответ: запись дерева в формате Newick, суммарная длина ветвей, порядок листьев и
    /// матрица расстояний.
    /// </remarks>
    [ScriptFn("tree", "Филогенетическое дерево: присоединение соседей или UPGMA, запись Newick",
        Example = "bio.tree({ a: \"ACGTACGTAC\", b: \"ACGTACGTTC\", c: \"ACGAACTTTC\", d: \"TCGAACTTTA\" }).newick")]
    public static ScriptRecord Tree(
        [ScriptParam("выровненные последовательности: запись «имя: строка»")] ScriptRecord sequences,
        [ScriptParam("метод: \"nj\" — присоединение соседей, \"upgma\"")] string kind = "nj",
        [ScriptParam("модель расстояния: \"p\", \"jukes_cantor\", \"kimura\", \"poisson\"")] string distance = "jukes_cantor")
    {
        const string function = "bio.tree";

        bool upgma = ScriptData.Kind<bool>(kind, "kind", function, ("nj", false), ("upgma", true));

        ScriptData.Require(sequences.Count >= 3, $"{function}: для дерева нужны хотя бы три последовательности");

        IReadOnlyList<string> labels = sequences.Keys;
        string[] aligned = [.. sequences.Values.Select((value, i) => value.AsString($"{function}: последовательность '{labels[i]}'").Trim().ToUpperInvariant())];
        DistanceMatrix distances = DistanceMatrix.FromSequences(labels, aligned, Model(distance, "distance", function));
        PhylogeneticTree tree = upgma ? TreeBuilder.Upgma(distances) : TreeBuilder.NeighborJoining(distances);
        var matrix = new Matrix(distances.Count, distances.Count);

        for (int i = 0; i < distances.Count; i++)
        {
            for (int j = 0; j < distances.Count; j++) matrix[i, j] = distances[i, j];
        }

        return ScriptData.Plain(
            ("newick", tree.ToNewick(6)),
            ("total_length", tree.TotalLength),
            ("leaves", tree.LeafNames),
            ("distances", matrix));
    }

    // --- внутреннее ---

    /// <summary>Последовательность из строки: с буквой U — РНК, иначе ДНК.</summary>
    private static NucleotideSequence Sequence(string letters)
    {
        string text = letters.Trim().ToUpperInvariant();

        return new NucleotideSequence(text, text.Contains('U', StringComparison.Ordinal) ? NucleicAcid.Rna : NucleicAcid.Dna);
    }

    private static DistanceModel Model(string value, string parameter, string function) => ScriptData.Kind(value, parameter, function,
        ("p", DistanceModel.PDistance),
        ("jukes_cantor", DistanceModel.JukesCantor),
        ("kimura", DistanceModel.Kimura),
        ("poisson", DistanceModel.Poisson));
}
