namespace AI.Biology.Phylogeny;

/// <summary>Модель пересчёта различий между последовательностями в эволюционное расстояние</summary>
public enum DistanceModel
{
    /// <summary>Доля различающихся позиций, без поправки на повторные замены</summary>
    PDistance,

    /// <summary>Джукс — Кантор: все замены нуклеотидов равновероятны</summary>
    JukesCantor,

    /// <summary>Кимура: транзиции и трансверсии идут с разной скоростью</summary>
    Kimura,

    /// <summary>Пуассоновская поправка для белков: −ln(1 − p)</summary>
    Poisson
}

/// <summary>Итог попозиционного сравнения двух нуклеотидных последовательностей</summary>
/// <param name="Sites">Число сравнимых позиций: в обеих последовательностях однозначный нуклеотид</param>
/// <param name="Differences">Число различий</param>
/// <param name="Transitions">Из них транзиций: A↔G, C↔T</param>
/// <param name="Transversions">Из них трансверсий: пурин ↔ пиримидин</param>
public readonly record struct SiteComparison(int Sites, int Differences, int Transitions, int Transversions)
{
    /// <summary>Доля различий</summary>
    public double P => Sites == 0 ? double.NaN : (double)Differences / Sites;

    /// <summary>Доля позиций с транзицией</summary>
    public double TransitionFraction => Sites == 0 ? double.NaN : (double)Transitions / Sites;

    /// <summary>Доля позиций с трансверсией</summary>
    public double TransversionFraction => Sites == 0 ? double.NaN : (double)Transversions / Sites;
}

/// <summary>
/// Эволюционные расстояния между выровненными последовательностями.
/// </summary>
/// <remarks>
/// <para>
/// Наблюдаемая доля различий p занижает число произошедших замен: в одной позиции могла случиться
/// вторая замена, а то и возврат к исходному нуклеотиду. Модели замен восстанавливают ожидаемое
/// число замен на позицию. По Джуксу — Кантору <c>d = −¾·ln(1 − 4p/3)</c>; при p ≥ 3/4
/// последовательности насыщены — различий не больше, чем у случайных, — и расстояние бесконечно.
/// Кимура различает транзиции P и трансверсии Q: <c>d = −½·ln(1 − 2P − Q) − ¼·ln(1 − 2Q)</c>.
/// </para>
/// <para>
/// Позиции с пропуском или неоднозначным символом отбрасываются для каждой пары отдельно.
/// Для нуклеотидных моделей неоднозначные символы — N и прочие коды IUPAC; для белков — X,
/// потому что N там аспарагин.
/// </para>
/// </remarks>
public static class EvolutionaryDistance
{
    private const string Gaps = "-.?";
    private const string NucleotideUnknown = "-.?NRYKMSWBDHV";
    private const string ProteinUnknown = "-.?X*";

    /// <summary>Сравнивает две выровненные нуклеотидные последовательности</summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность той же длины</param>
    public static SiteComparison CompareNucleotides(string first, string second)
    {
        RequireAligned(first, second);

        int sites = 0, differences = 0, transitions = 0;

        for (int k = 0; k < first.Length; k++)
        {
            int a = Nucleotide(first[k]);
            int b = Nucleotide(second[k]);

            if (a < 0 || b < 0)
                continue;

            sites++;

            if (a == b)
                continue;

            differences++;

            // Коды A = 0, C = 1, G = 2, T = 3: транзиции A↔G и C↔T отличаются вторым битом
            if ((a ^ b) == 2)
                transitions++;
        }

        return new SiteComparison(sites, differences, transitions, differences - transitions);
    }

    /// <summary>Доля различающихся позиций без учёта пропусков</summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность той же длины</param>
    public static double PDistance(string first, string second)
    {
        RequireAligned(first, second);

        int sites = 0, differences = 0;

        for (int k = 0; k < first.Length; k++)
        {
            char a = char.ToUpperInvariant(first[k]);
            char b = char.ToUpperInvariant(second[k]);

            if (Gaps.Contains(a) || Gaps.Contains(b))
                continue;

            sites++;

            if (a != b)
                differences++;
        }

        return sites == 0 ? double.NaN : (double)differences / sites;
    }

    /// <summary>Расстояние Джукса — Кантора по доле различий</summary>
    /// <param name="p">Доля различающихся позиций</param>
    public static double JukesCantor(double p)
    {
        if (double.IsNaN(p))
            return double.NaN;

        ArgumentOutOfRangeException.ThrowIfNegative(p);

        return p >= 0.75 ? double.PositiveInfinity : -0.75 * Math.Log(1 - (4 * p / 3));
    }

    /// <summary>Расстояние Джукса — Кантора между нуклеотидными последовательностями</summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность той же длины</param>
    public static double JukesCantor(string first, string second) => JukesCantor(CompareNucleotides(first, second).P);

    /// <summary>
    /// Дисперсия оценки Джукса — Кантора: <c>p(1 − p) / (n·(1 − 4p/3)²)</c>
    /// </summary>
    /// <param name="p">Доля различий</param>
    /// <param name="sites">Число сравнимых позиций</param>
    /// <remarks>Растёт без предела при приближении к насыщению: далёкие расстояния оцениваются плохо.</remarks>
    public static double JukesCantorVariance(double p, int sites)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sites);

        double factor = 1 - (4 * p / 3);

        return p >= 0.75 ? double.PositiveInfinity : p * (1 - p) / (sites * factor * factor);
    }

    /// <summary>Расстояние Кимуры по долям транзиций и трансверсий</summary>
    /// <param name="transitions">Доля позиций с транзицией P</param>
    /// <param name="transversions">Доля позиций с трансверсией Q</param>
    public static double Kimura(double transitions, double transversions)
    {
        if (double.IsNaN(transitions) || double.IsNaN(transversions))
            return double.NaN;

        double first = 1 - (2 * transitions) - transversions;
        double second = 1 - (2 * transversions);

        return first <= 0 || second <= 0
            ? double.PositiveInfinity
            : (-0.5 * Math.Log(first)) - (0.25 * Math.Log(second));
    }

    /// <summary>Расстояние Кимуры между нуклеотидными последовательностями</summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность той же длины</param>
    public static double Kimura(string first, string second)
    {
        SiteComparison comparison = CompareNucleotides(first, second);

        return Kimura(comparison.TransitionFraction, comparison.TransversionFraction);
    }

    /// <summary>Пуассоновское расстояние между белками</summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность той же длины</param>
    public static double Poisson(string first, string second)
    {
        RequireAligned(first, second);

        int sites = 0, differences = 0;

        for (int k = 0; k < first.Length; k++)
        {
            char a = char.ToUpperInvariant(first[k]);
            char b = char.ToUpperInvariant(second[k]);

            if (ProteinUnknown.Contains(a) || ProteinUnknown.Contains(b))
                continue;

            sites++;

            if (a != b)
                differences++;
        }

        if (sites == 0)
            return double.NaN;

        double p = (double)differences / sites;

        return p >= 1 ? double.PositiveInfinity : -Math.Log(1 - p);
    }

    /// <summary>Расстояние по выбранной модели</summary>
    /// <param name="first">Первая последовательность</param>
    /// <param name="second">Вторая последовательность той же длины</param>
    /// <param name="model">Модель</param>
    public static double Distance(string first, string second, DistanceModel model) => model switch
    {
        DistanceModel.PDistance => PDistance(first, second),
        DistanceModel.JukesCantor => JukesCantor(first, second),
        DistanceModel.Kimura => Kimura(first, second),
        DistanceModel.Poisson => Poisson(first, second),
        _ => throw new ArgumentOutOfRangeException(nameof(model))
    };

    /// <summary>Код нуклеотида: A = 0, C = 1, G = 2, T и U = 3; −1 для пропуска и неоднозначности</summary>
    internal static int Nucleotide(char letter)
    {
        char upper = char.ToUpperInvariant(letter);

        return upper switch
        {
            'A' => 0,
            'C' => 1,
            'G' => 2,
            'T' or 'U' => 3,
            _ => NucleotideUnknown.Contains(upper)
                ? -1
                : throw new ArgumentException($"Символ «{letter}» не является нуклеотидом", nameof(letter))
        };
    }

    private static void RequireAligned(string first, string second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Length != second.Length)
            throw new ArgumentException("Последовательности должны быть выровнены: длины различаются", nameof(second));
    }
}
