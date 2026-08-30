using System.Text;

namespace AI.Biology.Sequences;

/// <summary>Открытая рамка считывания</summary>
/// <param name="Start">Позиция стартового кодона</param>
/// <param name="Length">Длина в нуклеотидах, включая стоп-кодон</param>
/// <param name="Protein">Трансляция рамки в однобуквенном коде</param>
public readonly record struct OpenReadingFrame(int Start, int Length, string Protein);

/// <summary>
/// Стандартный генетический код: трансляция кодонов в аминокислоты.
/// </summary>
/// <remarks>
/// <para>
/// Код вырожден: 64 кодона кодируют 20 аминокислот и три сигнала остановки, поэтому обратное
/// однозначное преобразование невозможно — по белку нельзя восстановить нуклеотидную
/// последовательность.
/// </para>
/// <para>
/// Реализован стандартный код. Митохондриальные и прочие варианты отличаются несколькими
/// кодонами и здесь не поддерживаются: подставить стандартную таблицу к митохондриальной
/// последовательности значило бы получить неверный белок молча.
/// </para>
/// </remarks>
public static class GeneticCode
{
    private static readonly Dictionary<string, char> Table = Build();

    /// <summary>Стартовый кодон</summary>
    public const string StartCodon = "AUG";

    /// <summary>Аминокислота по кодону; звёздочка означает сигнал остановки</summary>
    /// <param name="codon">Кодон из трёх букв РНК либо ДНК</param>
    public static char Translate(string codon)
    {
        ArgumentNullException.ThrowIfNull(codon);

        string key = codon.Trim().ToUpperInvariant().Replace('T', 'U');

        return key.Length != 3
            ? throw new ArgumentException("Кодон состоит из трёх нуклеотидов", nameof(codon))
            : Table.TryGetValue(key, out char amino)
                ? amino
                : throw new ArgumentException($"Неизвестный кодон «{codon}»", nameof(codon));
    }

    /// <summary>
    /// Трансляция последовательности с заданной рамки до стоп-кодона либо до конца
    /// </summary>
    /// <param name="sequence">Последовательность</param>
    /// <param name="frame">Сдвиг рамки: 0, 1 или 2</param>
    /// <param name="stopAtTerminator">Останавливаться ли на стоп-кодоне</param>
    public static string Translate(NucleotideSequence sequence, int frame = 0, bool stopAtTerminator = true)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frame, 2);

        var protein = new StringBuilder();

        for (int i = frame; i + 3 <= sequence.Length; i += 3)
        {
            char amino = Translate(sequence.Letters.Substring(i, 3));

            if (amino == '*')
            {
                if (stopAtTerminator)
                    break;

                _ = protein.Append('*');
                continue;
            }

            _ = protein.Append(amino);
        }

        return protein.ToString();
    }

    /// <summary>
    /// Открытые рамки считывания во всех трёх прямых рамках
    /// </summary>
    /// <param name="sequence">Последовательность</param>
    /// <param name="minimumLength">Наименьшая длина белка в аминокислотах</param>
    /// <remarks>
    /// Ищутся участки от стартового кодона до ближайшего стоп-кодона. Короткие рамки
    /// возникают в случайной последовательности сплошь и рядом, поэтому порог длины —
    /// не украшение, а способ отделить сигнал от шума.
    /// </remarks>
    public static IReadOnlyList<OpenReadingFrame> FindOpenReadingFrames(
        NucleotideSequence sequence, int minimumLength = 30)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        string letters = sequence.Letters.Replace('T', 'U');
        var frames = new List<OpenReadingFrame>();

        for (int start = 0; start + 3 <= letters.Length; start++)
        {
            if (!letters.AsSpan(start, 3).SequenceEqual(StartCodon))
                continue;

            var protein = new StringBuilder();
            int position = start;

            for (; position + 3 <= letters.Length; position += 3)
            {
                char amino = Translate(letters.Substring(position, 3));

                if (amino == '*')
                {
                    if (protein.Length >= minimumLength)
                        frames.Add(new OpenReadingFrame(start, position + 3 - start, protein.ToString()));

                    break;
                }

                _ = protein.Append(amino);
            }
        }

        return frames;
    }

    /// <summary>Число кодонов, кодирующих аминокислоту — мера вырожденности кода</summary>
    /// <param name="aminoAcid">Аминокислота в однобуквенном коде</param>
    public static int Degeneracy(char aminoAcid)
        => Table.Values.Count(a => a == char.ToUpperInvariant(aminoAcid));

    private static Dictionary<string, char> Build()
    {
        // Таблица записана блоками по первому основанию: так её проще сверить с учебником
        const string Bases = "UCAG";
        const string Amino =
            "FFLLSSSSYY**CC*W" +
            "LLLLPPPPHHQQRRRR" +
            "IIIMTTTTNNKKSSRR" +
            "VVVVAAAADDEEGGGG";

        var table = new Dictionary<string, char>(64, StringComparer.Ordinal);
        int index = 0;

        foreach (char first in Bases)
            foreach (char second in Bases)
                foreach (char third in Bases)
                    table[$"{first}{second}{third}"] = Amino[index++];

        return table;
    }
}
