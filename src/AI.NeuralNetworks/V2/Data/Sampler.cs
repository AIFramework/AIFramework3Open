using System;
using System.Collections.Generic;

namespace AI.ML.NeuralNetworks.V2.Data;

/// <summary>
/// Sampler выдаёт последовательность индексов для DataLoader.
/// Аналог <c>torch.utils.data.Sampler</c>.
/// </summary>
public interface ISampler
{
    /// <summary>Перечислить индексы (одна эпоха).</summary>
    IEnumerable<int> Iterate();
    /// <summary>Число элементов за эпоху (если известно).</summary>
    int Count { get; }
}

/// <summary>Sequential sampler: 0..N-1.</summary>
public sealed class SequentialSampler : ISampler
{
    /// <summary>Размер датасета.</summary>
    public int N { get; }
    /// <summary>Создать.</summary>
    public SequentialSampler(int n) { N = n; }
    /// <inheritdoc/>
    public int Count => N;
    /// <inheritdoc/>
    public IEnumerable<int> Iterate()
    {
        for (int i = 0; i < N; i++) yield return i;
    }
}

/// <summary>Random sampler без замещения. Reproducible через seed.</summary>
public sealed class RandomSampler : ISampler
{
    /// <summary>Размер.</summary>
    public int N { get; }
    private readonly Random _rng;
    /// <summary>Создать.</summary>
    public RandomSampler(int n, int? seed = null)
    { N = n; _rng = seed.HasValue ? new Random(seed.Value) : new Random(); }
    /// <inheritdoc/>
    public int Count => N;
    /// <inheritdoc/>
    public IEnumerable<int> Iterate()
    {
        var indices = new int[N];
        for (int i = 0; i < N; i++) indices[i] = i;
        // Fisher–Yates
        for (int i = N - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        return indices;
    }
}

/// <summary>
/// Batch sampler: группирует индексы из базового sampler в батчи.
/// </summary>
public sealed class BatchSampler : ISampler
{
    /// <summary>Внутренний sampler.</summary>
    public ISampler Inner { get; }
    /// <summary>Размер батча.</summary>
    public int BatchSize { get; }
    /// <summary>Отбрасывать ли последний неполный батч.</summary>
    public bool DropLast { get; }
    /// <summary>Создать.</summary>
    public BatchSampler(ISampler inner, int batchSize, bool dropLast = false)
    {
        Inner = inner; BatchSize = batchSize; DropLast = dropLast;
    }
    /// <inheritdoc/>
    public int Count => DropLast ? Inner.Count / BatchSize : (Inner.Count + BatchSize - 1) / BatchSize;
    /// <summary>Перечислить батчи как массивы индексов.</summary>
    public IEnumerable<int[]> IterateBatches()
    {
        var batch = new List<int>(BatchSize);
        foreach (var i in Inner.Iterate())
        {
            batch.Add(i);
            if (batch.Count == BatchSize)
            {
                yield return batch.ToArray();
                batch.Clear();
            }
        }
        if (batch.Count > 0 && !DropLast) yield return batch.ToArray();
    }
    /// <inheritdoc/>
    public IEnumerable<int> Iterate()
    {
        foreach (var b in IterateBatches())
            foreach (var i in b) yield return i;
    }
}
