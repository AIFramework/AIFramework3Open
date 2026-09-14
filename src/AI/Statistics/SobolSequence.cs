#nullable enable
using System;
using AI.DataStructs.Algebraic;

namespace AI.Statistics;

/// <summary>
/// Последовательность Соболя: квазислучайные точки единичного куба размерности до 21 с направляющими
/// числами Джо и Куо (таблица new-joe-kuo-6.21201, 2008).
/// </summary>
/// <remarks>
/// Каждая координата есть цифровая последовательность по основанию 2: номер точки в коде Грея
/// побитно складывается по модулю 2 с направляющими числами своей координаты. Первые 2^k точек
/// делят каждую ось на 2^k равных частей по одной точке в каждой, а числа Джо и Куо подобраны
/// так, чтобы и двумерные проекции были равномерны. Точка с номером 0 есть начало координат.
/// <para>
/// Точность 32 бита: номер точки меньше 2^32. Случайный цифровой сдвиг (побитное сложение
/// каждой координаты со своим случайным числом) делает оценку несмещенной и позволяет оценить
/// погрешность по нескольким независимым сдвигам, не портя равномерности.
/// </para>
/// </remarks>
public sealed class SobolSequence
{
    /// <summary>Наибольшая поддерживаемая размерность</summary>
    public const int MaxDimension = 21;

    private const int Bits = 32;

    /// <summary>
    /// Строки таблицы Джо и Куо для координат 2..21: степень примитивного многочлена, его внутренние
    /// коэффициенты a и начальные числа m
    /// </summary>
    private static readonly (int Degree, int Coefficients, int[] Initial)[] JoeKuo =
    [
        (1, 0, [1]),
        (2, 1, [1, 3]),
        (3, 1, [1, 3, 1]),
        (3, 2, [1, 1, 1]),
        (4, 1, [1, 1, 3, 3]),
        (4, 4, [1, 3, 5, 13]),
        (5, 2, [1, 1, 5, 5, 17]),
        (5, 4, [1, 1, 5, 5, 5]),
        (5, 7, [1, 1, 7, 11, 19]),
        (5, 11, [1, 1, 5, 1, 1]),
        (5, 13, [1, 1, 1, 3, 11]),
        (5, 14, [1, 3, 5, 5, 31]),
        (6, 1, [1, 3, 3, 9, 7, 49]),
        (6, 13, [1, 1, 1, 15, 21, 21]),
        (6, 16, [1, 3, 1, 13, 27, 49]),
        (6, 19, [1, 1, 1, 15, 7, 5]),
        (6, 22, [1, 3, 1, 15, 13, 25]),
        (6, 25, [1, 1, 5, 5, 19, 61]),
        (7, 1, [1, 3, 7, 11, 23, 15, 103]),
        (7, 4, [1, 3, 7, 13, 13, 15, 69]),
    ];

    private readonly uint[][] _directions;
    private readonly uint[] _shift;
    private readonly uint[] _state;
    private long _index;

    /// <summary>Создает последовательность</summary>
    /// <param name="dimension">Размерность точек, от 1 до <see cref="MaxDimension"/></param>
    /// <param name="skip">Сколько первых точек пропустить</param>
    /// <param name="shift">Генератор случайного цифрового сдвига; null: без сдвига</param>
    public SobolSequence(int dimension, long skip = 0, Random? shift = null)
    {
        if (dimension < 1 || dimension > MaxDimension)
            throw new ArgumentOutOfRangeException(nameof(dimension), $"Размерность должна быть от 1 до {MaxDimension}");

        if (skip < 0 || skip > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(skip), "Пропуск должен быть от 0 до 2^32 − 1");

        _directions = new uint[dimension][];
        _shift = new uint[dimension];

        for (int j = 0; j < dimension; j++)
        {
            _directions[j] = Directions(j);

            if (shift is not null)
                _shift[j] = (uint)shift.NextInt64(0, 1L << Bits);
        }

        _index = skip;
        _state = Raw(skip);
    }

    /// <summary>Размерность точек</summary>
    public int Dimension => _directions.Length;

    /// <summary>Номер точки, которую вернет следующий вызов <see cref="Next"/></summary>
    public long Index => _index;

    /// <summary>Следующая точка последовательности</summary>
    /// <exception cref="InvalidOperationException">Исчерпаны 2^32 точки</exception>
    public Vector Next()
    {
        if (_index > uint.MaxValue)
            throw new InvalidOperationException("Последовательность исчерпана: точек не больше 2^32");

        var point = ToPoint(_state);

        // Код Грея следующего номера отличается одним битом: младшим нулевым битом текущего номера
        if (_index < uint.MaxValue)
        {
            int bit = System.Numerics.BitOperations.TrailingZeroCount(_index + 1);

            for (int j = 0; j < _state.Length; j++)
                _state[j] ^= _directions[j][bit];
        }

        _index++;

        return point;
    }

    /// <summary>Точка с заданным номером, без сдвига текущего положения</summary>
    /// <param name="index">Номер точки, от 0 до 2^32 − 1</param>
    public Vector Point(long index)
    {
        if (index < 0 || index > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index), "Номер точки должен быть от 0 до 2^32 − 1");

        return ToPoint(Raw(index));
    }

    /// <summary>Следующие count точек строками матрицы count × <see cref="Dimension"/></summary>
    /// <param name="count">Число точек</param>
    public Matrix Generate(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Число точек не может быть отрицательным");

        var result = new Matrix(count, Dimension);

        for (int i = 0; i < count; i++)
        {
            var point = Next();

            for (int j = 0; j < point.Count; j++)
                result[i, j] = point[j];
        }

        return result;
    }

    /// <summary>Целочисленные координаты точки: сумма направляющих чисел по битам кода Грея номера</summary>
    private uint[] Raw(long index)
    {
        ulong gray = (ulong)index ^ ((ulong)index >> 1);
        var raw = new uint[Dimension];

        for (int bit = 0; gray != 0; bit++, gray >>= 1)
        {
            if ((gray & 1) == 0)
                continue;

            for (int j = 0; j < raw.Length; j++)
                raw[j] ^= _directions[j][bit];
        }

        return raw;
    }

    private Vector ToPoint(uint[] raw)
    {
        var point = new Vector(raw.Length);

        for (int j = 0; j < raw.Length; j++)
            point[j] = (raw[j] ^ _shift[j]) * (1.0 / (1L << Bits));

        return point;
    }

    /// <summary>Направляющие числа координаты по рекуррентному соотношению примитивного многочлена</summary>
    private static uint[] Directions(int coordinate)
    {
        var v = new uint[Bits];

        if (coordinate == 0)
        {
            for (int k = 0; k < Bits; k++)
                v[k] = 1u << (Bits - 1 - k);

            return v;
        }

        var (s, a, m) = JoeKuo[coordinate - 1];

        for (int k = 0; k < s; k++)
            v[k] = (uint)m[k] << (Bits - 1 - k);

        for (int k = s; k < Bits; k++)
        {
            v[k] = v[k - s] ^ (v[k - s] >> s);

            for (int i = 1; i < s; i++)
            {
                if (((a >> (s - 1 - i)) & 1) != 0)
                    v[k] ^= v[k - i];
            }
        }

        return v;
    }
}
