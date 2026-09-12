namespace AI.Biology.Sequences;

/// <summary>
/// Матрица замен аминокислот: счёт за выравнивание одной аминокислоты против другой.
/// </summary>
/// <remarks>
/// <para>
/// Для белков схема «совпадение — несовпадение» бессмысленна: замена лейцина изолейцином почти
/// не меняет свойств белка и в родственных белках встречается постоянно, а замена на пролин
/// ломает спираль. Матрица BLOSUM62 выведена Хеникоффами (1992) из блоков консервативных участков,
/// сгруппированных при 62 % идентичности, и остаётся стандартом по умолчанию для выравнивания белков.
/// </para>
/// <para>
/// Здесь только двадцать стандартных аминокислот. Неоднозначные B, Z, X и знак остановки
/// не поддерживаются: встретив их, выравнивание сообщит об ошибке, а не подставит ноль молча.
/// </para>
/// </remarks>
public sealed class SubstitutionMatrix
{
    private readonly double[,] _scores = new double[128, 128];
    private readonly bool[] _known = new bool[128];

    /// <summary>Создаёт матрицу замен</summary>
    /// <param name="name">Название</param>
    /// <param name="alphabet">Буквы в порядке строк и столбцов</param>
    /// <param name="scores">Симметричная матрица счетов</param>
    public SubstitutionMatrix(string name, string alphabet, double[,] scores)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrEmpty(alphabet);
        ArgumentNullException.ThrowIfNull(scores);

        string letters = alphabet.ToUpperInvariant();
        int size = letters.Length;

        if (scores.GetLength(0) != size || scores.GetLength(1) != size)
            throw new ArgumentException($"Матрица должна быть {size}×{size} по числу букв алфавита", nameof(scores));

        if (letters.Distinct().Count() != size || letters.Any(c => c >= 128))
            throw new ArgumentException("Буквы алфавита должны быть различными латинскими символами", nameof(alphabet));

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                if (Math.Abs(scores[i, j] - scores[j, i]) > 1e-12)
                    throw new ArgumentException(
                        $"Матрица замен несимметрична: {letters[i]}{letters[j]} ≠ {letters[j]}{letters[i]}", nameof(scores));

                _scores[letters[i], letters[j]] = scores[i, j];
            }

            _known[letters[i]] = true;
        }

        Name = name;
        Alphabet = letters;
    }

    /// <summary>Название матрицы</summary>
    public string Name { get; }

    /// <summary>Допустимые буквы</summary>
    public string Alphabet { get; }

    /// <summary>Счёт за выравнивание двух букв</summary>
    /// <param name="first">Первая буква</param>
    /// <param name="second">Вторая буква</param>
    public double this[char first, char second]
    {
        get
        {
            char a = char.ToUpperInvariant(first);
            char b = char.ToUpperInvariant(second);

            return a < 128 && b < 128 && _known[a] && _known[b]
                ? _scores[a, b]
                : throw new ArgumentException($"Пары «{first}{second}» нет в матрице {Name}: допустимы буквы {Alphabet}");
        }
    }

    /// <summary>Матрица BLOSUM62 для двадцати стандартных аминокислот</summary>
    public static SubstitutionMatrix Blosum62 { get; } = new("BLOSUM62", "ARNDCQEGHILKMFPSTWYV", new double[,]
    {
        //  A   R   N   D   C   Q   E   G   H   I   L   K   M   F   P   S   T   W   Y   V
        {   4, -1, -2, -2,  0, -1, -1,  0, -2, -1, -1, -1, -1, -2, -1,  1,  0, -3, -2,  0 }, // A
        {  -1,  5,  0, -2, -3,  1,  0, -2,  0, -3, -2,  2, -1, -3, -2, -1, -1, -3, -2, -3 }, // R
        {  -2,  0,  6,  1, -3,  0,  0,  0,  1, -3, -3,  0, -2, -3, -2,  1,  0, -4, -2, -3 }, // N
        {  -2, -2,  1,  6, -3,  0,  2, -1, -1, -3, -4, -1, -3, -3, -1,  0, -1, -4, -3, -3 }, // D
        {   0, -3, -3, -3,  9, -3, -4, -3, -3, -1, -1, -3, -1, -2, -3, -1, -1, -2, -2, -1 }, // C
        {  -1,  1,  0,  0, -3,  5,  2, -2,  0, -3, -2,  1,  0, -3, -1,  0, -1, -2, -1, -2 }, // Q
        {  -1,  0,  0,  2, -4,  2,  5, -2,  0, -3, -3,  1, -2, -3, -1,  0, -1, -3, -2, -2 }, // E
        {   0, -2,  0, -1, -3, -2, -2,  6, -2, -4, -4, -2, -3, -3, -2,  0, -2, -2, -3, -3 }, // G
        {  -2,  0,  1, -1, -3,  0,  0, -2,  8, -3, -3, -1, -2, -1, -2, -1, -2, -2,  2, -3 }, // H
        {  -1, -3, -3, -3, -1, -3, -3, -4, -3,  4,  2, -3,  1,  0, -3, -2, -1, -3, -1,  3 }, // I
        {  -1, -2, -3, -4, -1, -2, -3, -4, -3,  2,  4, -2,  2,  0, -3, -2, -1, -2, -1,  1 }, // L
        {  -1,  2,  0, -1, -3,  1,  1, -2, -1, -3, -2,  5, -1, -3, -1,  0, -1, -3, -2, -2 }, // K
        {  -1, -1, -2, -3, -1,  0, -2, -3, -2,  1,  2, -1,  5,  0, -2, -1, -1, -1, -1,  1 }, // M
        {  -2, -3, -3, -3, -2, -3, -3, -3, -1,  0,  0, -3,  0,  6, -4, -2, -2,  1,  3, -1 }, // F
        {  -1, -2, -2, -1, -3, -1, -1, -2, -2, -3, -3, -1, -2, -4,  7, -1, -1, -4, -3, -2 }, // P
        {   1, -1,  1,  0, -1,  0,  0,  0, -1, -2, -2,  0, -1, -2, -1,  4,  1, -3, -2, -2 }, // S
        {   0, -1,  0, -1, -1, -1, -1, -2, -2, -1, -1, -1, -1, -2, -1,  1,  5, -2, -2,  0 }, // T
        {  -3, -3, -4, -4, -2, -2, -3, -2, -2, -3, -2, -3, -1,  1, -4, -3, -2, 11,  2, -3 }, // W
        {  -2, -2, -2, -3, -2, -1, -2, -3,  2, -1, -1, -2, -1,  3, -3, -2, -2,  2,  7, -1 }, // Y
        {   0, -3, -3, -3, -1, -2, -2, -3, -3,  3,  1, -2,  1, -1, -2, -2,  0, -3, -1,  4 }  // V
    });

    /// <summary>Название матрицы</summary>
    public override string ToString() => Name;
}
