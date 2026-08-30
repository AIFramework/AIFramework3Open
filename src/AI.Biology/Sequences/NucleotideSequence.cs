using System.Text;

namespace AI.Biology.Sequences;

/// <summary>Тип нуклеиновой кислоты</summary>
public enum NucleicAcid
{
    /// <summary>ДНК: азотистые основания A, C, G, T</summary>
    Dna,

    /// <summary>РНК: вместо тимина урацил</summary>
    Rna
}

/// <summary>
/// Последовательность нуклеотидов.
/// </summary>
/// <remarks>
/// Хранится как строка прописных букв. Неоднозначные символы кода IUPAC (N, R, Y и прочие)
/// не допускаются: молча пропустить их значило бы посчитать состав и массу неверно,
/// а угадывать за исследователя нельзя.
/// </remarks>
public sealed class NucleotideSequence
{
    private readonly string _letters;

    /// <summary>Создаёт последовательность</summary>
    /// <param name="letters">Буквенная запись</param>
    /// <param name="kind">Тип нуклеиновой кислоты</param>
    /// <exception cref="ArgumentException">Встречен недопустимый символ</exception>
    public NucleotideSequence(string letters, NucleicAcid kind = NucleicAcid.Dna)
    {
        ArgumentNullException.ThrowIfNull(letters);

        string upper = letters.Trim().ToUpperInvariant();
        string alphabet = kind == NucleicAcid.Dna ? "ACGT" : "ACGU";

        foreach (char letter in upper)
        {
            if (!alphabet.Contains(letter, StringComparison.Ordinal))
                throw new ArgumentException(
                    $"Символ «{letter}» не входит в алфавит {alphabet}: неоднозначные обозначения не поддерживаются",
                    nameof(letters));
        }

        _letters = upper;
        Kind = kind;
    }

    /// <summary>Тип нуклеиновой кислоты</summary>
    public NucleicAcid Kind { get; }

    /// <summary>Длина последовательности</summary>
    public int Length => _letters.Length;

    /// <summary>Буквенная запись</summary>
    public string Letters => _letters;

    /// <summary>Нуклеотид по номеру</summary>
    /// <param name="index">Номер, начиная с нуля</param>
    public char this[int index] => _letters[index];

    /// <summary>Число вхождений нуклеотида</summary>
    /// <param name="nucleotide">Буква</param>
    public int Count(char nucleotide) => _letters.Count(c => c == char.ToUpperInvariant(nucleotide));

    /// <summary>
    /// Доля гуанина и цитозина — величина, определяющая температуру плавления двойной спирали
    /// </summary>
    public double GcContent
    {
        get
        {
            if (Length == 0)
                return 0;

            int gc = _letters.Count(c => c is 'G' or 'C');

            return (double)gc / Length;
        }
    }

    /// <summary>
    /// Обратно-комплементарная последовательность — вторая цепь, прочитанная в обратную сторону
    /// </summary>
    public NucleotideSequence ReverseComplement()
    {
        var builder = new StringBuilder(Length);

        for (int i = Length - 1; i >= 0; i--)
            _ = builder.Append(Complement(_letters[i], Kind));

        return new NucleotideSequence(builder.ToString(), Kind);
    }

    /// <summary>Транскрипция ДНК в РНК: тимин заменяется урацилом</summary>
    /// <exception cref="InvalidOperationException">Последовательность уже является РНК</exception>
    public NucleotideSequence Transcribe()
        => Kind == NucleicAcid.Rna
            ? throw new InvalidOperationException("Последовательность уже является РНК")
            : new NucleotideSequence(_letters.Replace('T', 'U'), NucleicAcid.Rna);

    /// <summary>Обратная транскрипция РНК в ДНК</summary>
    /// <exception cref="InvalidOperationException">Последовательность уже является ДНК</exception>
    public NucleotideSequence ReverseTranscribe()
        => Kind == NucleicAcid.Dna
            ? throw new InvalidOperationException("Последовательность уже является ДНК")
            : new NucleotideSequence(_letters.Replace('U', 'T'), NucleicAcid.Dna);

    /// <summary>Подпоследовательность</summary>
    /// <param name="start">Начало</param>
    /// <param name="length">Длина</param>
    public NucleotideSequence Slice(int start, int length)
        => new(_letters.Substring(start, length), Kind);

    /// <summary>
    /// Частоты слов длины <paramref name="size"/> — основа выравнивания без выравнивания
    /// и сборки геномов
    /// </summary>
    /// <param name="size">Длина слова</param>
    public IReadOnlyDictionary<string, int> KmerCounts(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i + size <= Length; i++)
        {
            string word = _letters.Substring(i, size);
            counts[word] = counts.GetValueOrDefault(word) + 1;
        }

        return counts;
    }

    /// <summary>
    /// Приблизительная температура плавления короткого олигонуклеотида
    /// </summary>
    /// <remarks>
    /// Для последовательностей короче четырнадцати нуклеотидов применяется правило Уоллеса
    /// (2 °C на A и T, 4 °C на G и C), для более длинных — формула с поправкой на состав.
    /// Обе оценки грубы: точный расчёт требует термодинамики ближайших соседей и учёта
    /// концентрации солей.
    /// </remarks>
    public double MeltingTemperatureCelsius()
    {
        int at = _letters.Count(c => c is 'A' or 'T' or 'U');
        int gc = _letters.Count(c => c is 'G' or 'C');

        return Length < 14
            ? (2 * at) + (4 * gc)
            : 64.9 + (41.0 * (gc - 16.4) / Length);
    }

    /// <summary>Комплементарный нуклеотид</summary>
    /// <param name="nucleotide">Буква</param>
    /// <param name="kind">Тип нуклеиновой кислоты</param>
    public static char Complement(char nucleotide, NucleicAcid kind = NucleicAcid.Dna) => nucleotide switch
    {
        'A' => kind == NucleicAcid.Dna ? 'T' : 'U',
        'T' or 'U' => 'A',
        'G' => 'C',
        'C' => 'G',
        _ => throw new ArgumentException($"Неизвестный нуклеотид «{nucleotide}»", nameof(nucleotide))
    };

    /// <summary>Буквенная запись</summary>
    public override string ToString() => _letters;
}
