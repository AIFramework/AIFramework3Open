#nullable enable
using System;
using AI.DataStructs.Algebraic;

namespace AI.Statistics;

/// <summary>
/// Последовательность Холтона: квазислучайные точки единичного куба любой размерности.
/// </summary>
/// <remarks>
/// Координата с номером j точки с номером n равна обращению цифр n в системе счисления с j-м простым
/// основанием (2, 3, 5, 7, ...). Точки заполняют куб равномернее псевдослучайных: погрешность
/// квазиметода Монте-Карло для гладкой функции убывает почти как 1/N вместо 1/√N.
/// <para>
/// В больших размерностях у соседних простых оснований первые точки лежат на немногих прямых.
/// Перемешивание это лечит: цифры каждой координаты переставляются своей случайной перестановкой,
/// ноль остается нулем, поэтому точки не выходят из [0; 1) и сохраняют равномерное расслоение по каждой оси.
/// </para>
/// Точка с номером 0 есть начало координат; пропуск первых точек задается параметром skip.
/// </remarks>
public sealed class HaltonSequence
{
    private readonly int[] _bases;
    private readonly int[][]? _permutations;
    private long _index;

    /// <summary>Создает последовательность</summary>
    /// <param name="dimension">Размерность точек</param>
    /// <param name="skip">Сколько первых точек пропустить</param>
    /// <param name="scramble">Генератор для перемешивания цифр; null: без перемешивания</param>
    public HaltonSequence(int dimension, long skip = 0, Random? scramble = null)
    {
        if (dimension < 1)
            throw new ArgumentOutOfRangeException(nameof(dimension), "Размерность должна быть положительной");

        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip), "Пропуск не может быть отрицательным");

        _bases = FirstPrimes(dimension);
        _index = skip;

        if (scramble is null)
            return;

        _permutations = new int[dimension][];

        for (int j = 0; j < dimension; j++)
        {
            int radix = _bases[j];
            var permutation = new int[radix];

            for (int d = 0; d < radix; d++)
                permutation[d] = d;

            // Фишер-Йетс по цифрам 1..radix−1: ноль остается на месте
            for (int d = radix - 1; d > 1; d--)
            {
                int k = 1 + scramble.Next(d);
                (permutation[d], permutation[k]) = (permutation[k], permutation[d]);
            }

            _permutations[j] = permutation;
        }
    }

    /// <summary>Размерность точек</summary>
    public int Dimension => _bases.Length;

    /// <summary>Номер точки, которую вернет следующий вызов <see cref="Next"/></summary>
    public long Index => _index;

    /// <summary>Следующая точка последовательности</summary>
    public Vector Next() => Point(_index++);

    /// <summary>Точка с заданным номером, без сдвига текущего положения</summary>
    /// <param name="index">Номер точки, от нуля</param>
    public Vector Point(long index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Номер точки не может быть отрицательным");

        var point = new Vector(_bases.Length);

        for (int j = 0; j < _bases.Length; j++)
            point[j] = Inverse(index, _bases[j], _permutations?[j]);

        return point;
    }

    /// <summary>Следующие count точек строками матрицы count × <see cref="Dimension"/></summary>
    /// <param name="count">Число точек</param>
    public Matrix Generate(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Число точек не может быть отрицательным");

        var result = new Matrix(count, _bases.Length);

        for (int i = 0; i < count; i++)
        {
            var point = Next();

            for (int j = 0; j < point.Count; j++)
                result[i, j] = point[j];
        }

        return result;
    }

    /// <summary>
    /// Обращение цифр (радикальная обратная функция, последовательность ван дер Корпута): цифры номера
    /// в системе с основанием radix записываются после запятой в обратном порядке
    /// </summary>
    /// <param name="index">Номер, неотрицательный</param>
    /// <param name="radix">Основание, не меньше 2</param>
    /// <returns>Число из [0; 1)</returns>
    public static double RadicalInverse(long index, int radix)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Номер не может быть отрицательным");

        if (radix < 2)
            throw new ArgumentOutOfRangeException(nameof(radix), "Основание должно быть не меньше 2");

        return Inverse(index, radix, null);
    }

    private static double Inverse(long index, int radix, int[]? permutation)
    {
        // Обращенные цифры и степень основания копятся точно, пока меньше 2^53, и делятся один раз
        double reversed = 0;
        double denominator = 1;

        while (index > 0)
        {
            int digit = (int)(index % radix);
            reversed = (reversed * radix) + (permutation is null ? digit : permutation[digit]);
            denominator *= radix;
            index /= radix;
        }

        return Math.Min(reversed / denominator, 1 - 1e-16);
    }

    private static int[] FirstPrimes(int count)
    {
        var primes = new int[count];
        int found = 0;

        for (int candidate = 2; found < count; candidate++)
        {
            bool prime = true;

            for (int i = 0; i < found && primes[i] * primes[i] <= candidate; i++)
            {
                if (candidate % primes[i] == 0)
                {
                    prime = false;
                    break;
                }
            }

            if (prime)
                primes[found++] = candidate;
        }

        return primes;
    }
}
