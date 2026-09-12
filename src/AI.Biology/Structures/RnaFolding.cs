using System.Text;

namespace AI.Biology.Structures;

/// <summary>Вторичная структура РНК: последовательность и пары оснований</summary>
public sealed class RnaSecondaryStructure
{
    /// <summary>Создаёт структуру</summary>
    /// <param name="sequence">Последовательность РНК</param>
    /// <param name="pairs">Пары оснований: номер открывающего и закрывающего</param>
    public RnaSecondaryStructure(string sequence, IEnumerable<(int Open, int Close)> pairs)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(pairs);

        Sequence = sequence;
        Pairs = pairs.OrderBy(p => p.Open).ToList();
        DotBracket = RnaFolding.ToDotBracket(sequence.Length, Pairs);
    }

    /// <summary>Последовательность</summary>
    public string Sequence { get; }

    /// <summary>Пары оснований по возрастанию открывающего</summary>
    public IReadOnlyList<(int Open, int Close)> Pairs { get; }

    /// <summary>Число пар</summary>
    public int PairCount => Pairs.Count;

    /// <summary>Запись скобками: пара — «(» и «)», непарное основание — «.»</summary>
    public string DotBracket { get; }

    /// <summary>Последовательность и скобочная запись под ней</summary>
    public override string ToString() => $"{Sequence}{Environment.NewLine}{DotBracket}";
}

/// <summary>
/// Вторичная структура РНК: укладка по Нуссинову и скобочная запись.
/// </summary>
/// <remarks>
/// <para>
/// Алгоритм Нуссинова (1978) находит структуру с наибольшим числом пар оснований без
/// псевдоузлов: пары вложены друг в друга или идут подряд, но не перекрещиваются. Пары —
/// уотсон-криковские A–U и G–C и, по желанию, неканоническая G–U. Между основаниями пары
/// не меньше трёх непарных: петля короче стерически невозможна.
/// </para>
/// <para>
/// Это модель подсчёта пар, а не свободной энергии. Реальную укладку определяют стэкинг соседних
/// пар и штрафы за петли (параметры Тёрнера, алгоритм Цукера); число пар — лишь грубое к ним
/// приближение, и на длинных РНК укладка по Нуссинову с настоящей обычно расходится. Энергетической
/// модели здесь нет.
/// </para>
/// </remarks>
public static class RnaFolding
{
    /// <summary>
    /// Укладка с наибольшим числом пар оснований
    /// </summary>
    /// <param name="sequence">Последовательность РНК; тимин считается урацилом</param>
    /// <param name="minimumLoop">Наименьшее число непарных оснований внутри пары</param>
    /// <param name="allowWobble">Разрешать ли пары G–U</param>
    public static RnaSecondaryStructure Nussinov(string sequence, int minimumLoop = 3, bool allowWobble = true)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLoop);

        string rna = Normalize(sequence);
        int n = rna.Length;
        var best = new int[Math.Max(n, 1), Math.Max(n, 1)];

        for (int span = minimumLoop + 1; span < n; span++)
        {
            for (int i = 0; i + span < n; i++)
            {
                int j = i + span;
                int value = Math.Max(best[i + 1, j], best[i, j - 1]);

                if (CanPair(rna[i], rna[j], allowWobble))
                    value = Math.Max(value, Inner(best, i, j) + 1);

                for (int k = i + 1; k < j - 1; k++)
                    value = Math.Max(value, best[i, k] + best[k + 1, j]);

                best[i, j] = value;
            }
        }

        var pairs = new List<(int, int)>();
        var pending = new Stack<(int I, int J)>();

        if (n > 0)
            pending.Push((0, n - 1));

        while (pending.Count > 0)
        {
            (int i, int j) = pending.Pop();

            if (j - i <= minimumLoop || best[i, j] == 0)
                continue;

            if (best[i, j] == best[i + 1, j])
            {
                pending.Push((i + 1, j));
            }
            else if (best[i, j] == best[i, j - 1])
            {
                pending.Push((i, j - 1));
            }
            else if (CanPair(rna[i], rna[j], allowWobble) && best[i, j] == Inner(best, i, j) + 1)
            {
                pairs.Add((i, j));
                pending.Push((i + 1, j - 1));
            }
            else
            {
                for (int k = i + 1; k < j - 1; k++)
                {
                    if (best[i, j] != best[i, k] + best[k + 1, j])
                        continue;

                    pending.Push((i, k));
                    pending.Push((k + 1, j));
                    break;
                }
            }
        }

        return new RnaSecondaryStructure(rna, pairs);
    }

    /// <summary>Могут ли основания образовать пару</summary>
    /// <param name="first">Первое основание</param>
    /// <param name="second">Второе основание</param>
    /// <param name="allowWobble">Разрешать ли пары G–U</param>
    public static bool CanPair(char first, char second, bool allowWobble = true)
    {
        char a = ToRna(first);
        char b = ToRna(second);

        return (a, b) switch
        {
            ('A', 'U') or ('U', 'A') or ('G', 'C') or ('C', 'G') => true,
            ('G', 'U') or ('U', 'G') => allowWobble,
            _ => false
        };
    }

    /// <summary>Пары оснований из скобочной записи</summary>
    /// <param name="dotBracket">Запись из «(», «)» и «.»</param>
    /// <exception cref="FormatException">Скобки не сбалансированы или встречен другой символ</exception>
    public static IReadOnlyList<(int Open, int Close)> ParseDotBracket(string dotBracket)
    {
        ArgumentNullException.ThrowIfNull(dotBracket);

        var open = new Stack<int>();
        var pairs = new List<(int, int)>();

        for (int i = 0; i < dotBracket.Length; i++)
        {
            switch (dotBracket[i])
            {
                case '.':
                    break;
                case '(':
                    open.Push(i);
                    break;
                case ')':
                    if (open.Count == 0)
                        throw new FormatException($"Лишняя закрывающая скобка в позиции {i + 1}");

                    pairs.Add((open.Pop(), i));
                    break;
                default:
                    throw new FormatException(
                        $"Символ «{dotBracket[i]}» в позиции {i + 1}: псевдоузлы и другие скобки не поддерживаются");
            }
        }

        return open.Count == 0
            ? pairs.OrderBy(p => p.Item1).ToList()
            : throw new FormatException($"Не закрыто скобок: {open.Count}");
    }

    /// <summary>Скобочная запись пар оснований</summary>
    /// <param name="length">Длина последовательности</param>
    /// <param name="pairs">Пары</param>
    /// <exception cref="ArgumentException">Пары перекрещиваются или основание занято дважды</exception>
    public static string ToDotBracket(int length, IEnumerable<(int Open, int Close)> pairs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentNullException.ThrowIfNull(pairs);

        var text = Enumerable.Repeat('.', length).ToArray();
        var list = pairs.ToList();

        foreach ((int a, int b) in list)
        {
            if (a < 0 || b >= length || a >= b)
                throw new ArgumentException($"Пара ({a}, {b}) вне последовательности", nameof(pairs));

            if (text[a] != '.' || text[b] != '.')
                throw new ArgumentException($"Основание в паре ({a}, {b}) уже занято", nameof(pairs));

            text[a] = '(';
            text[b] = ')';
        }

        foreach ((int a, int b) in list)
        {
            foreach ((int c, int d) in list)
            {
                if (a < c && c < b && b < d)
                    throw new ArgumentException($"Пары ({a}, {b}) и ({c}, {d}) образуют псевдоузел", nameof(pairs));
            }
        }

        return new string(text);
    }

    /// <summary>
    /// Расстояние по парам оснований: сколько пар есть только в одной из двух структур
    /// </summary>
    /// <param name="first">Первая структура в скобочной записи</param>
    /// <param name="second">Вторая структура той же длины</param>
    public static int BasePairDistance(string first, string second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Length != second.Length)
            throw new ArgumentException("Структуры должны быть одной длины", nameof(second));

        var a = new HashSet<(int, int)>(ParseDotBracket(first));
        var b = new HashSet<(int, int)>(ParseDotBracket(second));

        return a.Count(p => !b.Contains(p)) + b.Count(p => !a.Contains(p));
    }

    /// <summary>
    /// Допустима ли структура: пары комплементарны, не перекрещиваются и замыкают петлю не короче заданной
    /// </summary>
    /// <param name="sequence">Последовательность</param>
    /// <param name="pairs">Пары</param>
    /// <param name="minimumLoop">Наименьшая петля</param>
    /// <param name="allowWobble">Разрешены ли пары G–U</param>
    public static bool IsValid(string sequence, IEnumerable<(int Open, int Close)> pairs, int minimumLoop = 3, bool allowWobble = true)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(pairs);

        var list = pairs.ToList();
        var used = new HashSet<int>();

        foreach ((int a, int b) in list)
        {
            if (a < 0 || b >= sequence.Length || b - a <= minimumLoop || !used.Add(a) || !used.Add(b)
                || !CanPair(sequence[a], sequence[b], allowWobble))
            {
                return false;
            }
        }

        return !list.Any(p => list.Any(q => p.Open < q.Open && q.Open < p.Close && p.Close < q.Close));
    }

    private static int Inner(int[,] best, int i, int j) => i + 1 <= j - 1 ? best[i + 1, j - 1] : 0;

    private static char ToRna(char letter)
    {
        char upper = char.ToUpperInvariant(letter);

        return upper == 'T' ? 'U' : upper;
    }

    private static string Normalize(string sequence)
    {
        var builder = new StringBuilder(sequence.Length);

        foreach (char letter in sequence.Trim())
        {
            char rna = ToRna(letter);

            if (rna is not ('A' or 'C' or 'G' or 'U'))
                throw new ArgumentException($"Символ «{letter}» не является основанием РНК", nameof(sequence));

            _ = builder.Append(rna);
        }

        return builder.ToString();
    }
}
