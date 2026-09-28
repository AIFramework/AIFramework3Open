using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.NLP.Similarity;

/// <summary>
/// MinHash: короткая подпись множества, по которой оценивается коэффициент Жаккара двух
/// множеств без их сравнения целиком (A. Broder, 1997).
/// </summary>
/// <remarks>
/// <para>
/// Для каждой из <see cref="Size"/> хеш-функций подпись хранит наименьший хеш элементов
/// множества. Вероятность совпадения минимумов у двух множеств равна их коэффициенту
/// Жаккара, поэтому доля совпавших позиций подписи — несмещённая оценка сходства с
/// погрешностью порядка 1/√<see cref="Size"/>.
/// </para>
/// <para>
/// Элементы подаются уже хешированными в 64 бита (<see cref="Hash(string)"/>, <see cref="Combine"/>):
/// так один класс годится и для слов текста, и для шинглов кода.
/// </para>
/// </remarks>
public sealed class MinHash
{
    private const ulong FnvOffset = 14695981039346656037;
    private const ulong FnvPrime = 1099511628211;

    private readonly ulong[] _seeds;

    /// <summary>Создаёт набор хеш-функций</summary>
    /// <param name="size">Длина подписи; 128 даёт погрешность оценки около 0.09</param>
    /// <param name="seed">Зерно: подписи сравнимы только при одинаковых размере и зерне</param>
    public MinHash(int size = 128, int seed = 1)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "Длина подписи должна быть положительной.");

        var random = new Random(seed);
        _seeds = new ulong[size];

        for (int i = 0; i < size; i++)
            _seeds[i] = ((ulong)(uint)random.Next() << 32) | (uint)random.Next();
    }

    /// <summary>Длина подписи</summary>
    public int Size => _seeds.Length;

    /// <summary>Подпись множества; у пустого множества все позиции равны <see cref="ulong.MaxValue"/></summary>
    public ulong[] Signature(IEnumerable<ulong> items)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));

        var signature = new ulong[_seeds.Length];
        Array.Fill(signature, ulong.MaxValue);

        foreach (ulong item in items)
        {
            for (int i = 0; i < _seeds.Length; i++)
            {
                ulong h = Mix(item ^ _seeds[i]);
                if (h < signature[i]) signature[i] = h;
            }
        }

        return signature;
    }

    /// <summary>Оценка коэффициента Жаккара по двум подписям одной длины</summary>
    public static double Similarity(ulong[] a, ulong[] b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        if (a.Length != b.Length) throw new ArgumentException("Подписи разной длины несравнимы.", nameof(b));

        int equal = 0;
        for (int i = 0; i < a.Length; i++)
            if (a[i] == b[i]) equal++;

        return (double)equal / a.Length;
    }

    /// <summary>Точный коэффициент Жаккара двух множеств: для проверки оценки и для малых множеств</summary>
    public static double Jaccard<T>(IReadOnlySet<T> a, IReadOnlySet<T> b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        if (a.Count == 0 && b.Count == 0) return 0;

        int common = a.Count(b.Contains);
        return (double)common / (a.Count + b.Count - common);
    }

    /// <summary>64-битный хеш строки (FNV-1a)</summary>
    public static ulong Hash(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));

        ulong h = FnvOffset;
        foreach (char c in text)
        {
            h ^= c;
            h *= FnvPrime;
        }

        return h;
    }

    /// <summary>Хеш последовательности хешей: шингл из нескольких токенов</summary>
    public static ulong Combine(ReadOnlySpan<ulong> parts)
    {
        ulong h = FnvOffset;
        foreach (ulong part in parts)
        {
            h ^= part;
            h *= FnvPrime;
        }

        return Mix(h);
    }

    // Финализатор SplitMix64: перемешивает биты так, что xor с зерном даёт независимую функцию
    private static ulong Mix(ulong x)
    {
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EB;
        return x ^ (x >> 31);
    }
}

/// <summary>
/// Поиск пар похожих множеств по подписям MinHash без перебора всех пар (locality-sensitive hashing).
/// </summary>
/// <remarks>
/// Подпись режется на <c>bands</c> полос по <c>rows</c> позиций; два множества становятся
/// кандидатами, если совпала хотя бы одна полоса целиком. Вероятность этого при сходстве s
/// равна 1 − (1 − s^rows)^bands — резкий переход около <see cref="Threshold"/>.
/// </remarks>
public sealed class MinHashLsh
{
    private readonly int _bands;
    private readonly int _rows;
    private readonly Dictionary<(int Band, ulong Key), List<int>> _buckets = new();

    /// <summary>Создаёт индекс</summary>
    /// <param name="bands">Число полос</param>
    /// <param name="rows">Позиций в полосе</param>
    public MinHashLsh(int bands = 32, int rows = 4)
    {
        if (bands <= 0) throw new ArgumentOutOfRangeException(nameof(bands));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));

        _bands = bands;
        _rows = rows;
    }

    /// <summary>Сходство, при котором пара становится кандидатом с вероятностью около половины</summary>
    public double Threshold => Math.Pow(1.0 / _bands, 1.0 / _rows);

    /// <summary>Добавляет подпись</summary>
    public void Add(int id, ulong[] signature)
    {
        foreach ((int band, ulong key) in Keys(signature))
        {
            if (!_buckets.TryGetValue((band, key), out List<int> ids)) _buckets[(band, key)] = ids = new List<int>();
            ids.Add(id);
        }
    }

    /// <summary>Кто совпал с подписью хотя бы одной полосой</summary>
    public IReadOnlyCollection<int> Query(ulong[] signature)
    {
        var found = new HashSet<int>();

        foreach ((int, ulong) key in Keys(signature))
            if (_buckets.TryGetValue(key, out List<int> ids)) found.UnionWith(ids);

        return found;
    }

    /// <summary>
    /// Все пары-кандидаты. Корзины крупнее <paramref name="maxBucket"/> пропускаются: так
    /// выглядят тысячи одинаковых тривиальных множеств, и перебор их пар ничего не даёт.
    /// </summary>
    public IReadOnlyCollection<(int A, int B)> CandidatePairs(int maxBucket = 64)
    {
        var pairs = new HashSet<(int, int)>();

        foreach (List<int> ids in _buckets.Values)
        {
            if (ids.Count < 2 || ids.Count > maxBucket) continue;

            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++)
                    if (ids[i] != ids[j]) pairs.Add((Math.Min(ids[i], ids[j]), Math.Max(ids[i], ids[j])));
        }

        return pairs;
    }

    private IEnumerable<(int Band, ulong Key)> Keys(ulong[] signature)
    {
        if (signature is null) throw new ArgumentNullException(nameof(signature));
        if (signature.Length < _bands * _rows)
            throw new ArgumentException($"Подпись короче {_bands * _rows} позиций.", nameof(signature));

        for (int band = 0; band < _bands; band++)
            yield return (band, MinHash.Combine(signature.AsSpan(band * _rows, _rows)));
    }
}
