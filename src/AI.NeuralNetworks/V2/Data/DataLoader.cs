using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AI.ML.NeuralNetworks.V2.Data;

/// <summary>
/// Многопоточный DataLoader: воркеры читают элементы из <see cref="IDataset{T}"/>
/// и отправляют батчи в канал. Аналог <c>torch.utils.data.DataLoader</c>.
/// </summary>
/// <remarks>
/// <para>
/// Реализован через <see cref="System.Threading.Channels.Channel{T}"/>: лёгкий
/// pipe с натуральным backpressure через <see cref="BoundedChannelOptions"/>.
/// </para>
/// <para>
/// <b>Многопоточность.</b> NumWorkers ≥ 1 запускает фоновые воркеры,
/// которые обрабатывают батчи из BatchSampler. Порядок батчей в выходе
/// сохраняется через монотонный seq-id.
/// </para>
/// <para>
/// <b>Порядок и детерминизм.</b> Если NumWorkers=0, обработка синхронна на
/// потоке потребителя. Если NumWorkers&gt;0 — порядок выдачи батчей детерминирован
/// (результаты буферизуются и выдаются по seq-id).
/// </para>
/// </remarks>
public sealed class DataLoader<TItem, TBatch> : IAsyncEnumerable<TBatch>, IEnumerable<TBatch>
{
    /// <summary>Источник данных.</summary>
    public IDataset<TItem> Dataset { get; }
    /// <summary>Sampler батчей.</summary>
    public BatchSampler BatchSampler { get; }
    /// <summary>Collate.</summary>
    public CollateFn<TItem, TBatch> CollateFn { get; }
    /// <summary>Число рабочих потоков (0 — синхронно).</summary>
    public int NumWorkers { get; }
    /// <summary>Размер канала-буфера (prefetch).</summary>
    public int PrefetchFactor { get; }

    /// <summary>Создать.</summary>
    public DataLoader(IDataset<TItem> dataset, int batchSize,
        CollateFn<TItem, TBatch> collateFn,
        bool shuffle = false, bool dropLast = false,
        int numWorkers = 0, int prefetchFactor = 2, int? seed = null)
    {
        Dataset = dataset;
        var inner = shuffle
            ? (ISampler)new RandomSampler(dataset.Count, seed)
            : new SequentialSampler(dataset.Count);
        BatchSampler = new BatchSampler(inner, batchSize, dropLast);
        CollateFn = collateFn ?? throw new ArgumentNullException(nameof(collateFn));
        if (numWorkers < 0) throw new ArgumentOutOfRangeException(nameof(numWorkers));
        NumWorkers = numWorkers;
        PrefetchFactor = Math.Max(1, prefetchFactor);
    }

    /// <summary>Число батчей в эпохе.</summary>
    public int Count => BatchSampler.Count;

    /// <inheritdoc/>
    public IEnumerator<TBatch> GetEnumerator()
    {
        if (NumWorkers == 0)
        {
            foreach (var batch in BatchSampler.IterateBatches())
            {
                var items = new TItem[batch.Length];
                for (int i = 0; i < batch.Length; i++) items[i] = Dataset.Get(batch[i]);
                yield return CollateFn(items);
            }
            yield break;
        }

        // Многопоточный режим.
        var channel = Channel.CreateBounded<(int seq, TBatch batch)>(
            new BoundedChannelOptions(NumWorkers * PrefetchFactor)
            { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });

        var batches = BatchSampler.IterateBatches().GetEnumerator();
        var lockObj = new object();
        int nextSeq = 0;
        var cts = new CancellationTokenSource();

        var workerTasks = new Task[NumWorkers];
        for (int w = 0; w < NumWorkers; w++)
        {
            workerTasks[w] = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    int seq;
                    int[] batchIdx;
                    lock (lockObj)
                    {
                        if (!batches.MoveNext()) return;
                        seq = nextSeq++;
                        batchIdx = batches.Current;
                    }
                    var items = new TItem[batchIdx.Length];
                    for (int i = 0; i < batchIdx.Length; i++) items[i] = Dataset.Get(batchIdx[i]);
                    var collated = CollateFn(items);
                    await channel.Writer.WriteAsync((seq, collated), cts.Token).ConfigureAwait(false);
                }
            }, cts.Token);
        }

        // Закрытие канала после завершения всех воркеров.
        var closeTask = Task.Run(async () =>
        {
            try { await Task.WhenAll(workerTasks).ConfigureAwait(false); }
            finally { channel.Writer.TryComplete(); }
        });

        // Реordering buffer: выдавать строго по возрастанию seq.
        var pending = new SortedDictionary<int, TBatch>();
        int expected = 0;
        try
        {
            while (true)
            {
                if (pending.TryGetValue(expected, out var ready))
                {
                    pending.Remove(expected);
                    expected++;
                    yield return ready;
                    continue;
                }
                if (!channel.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
                {
                    // Канал закрыт.
                    while (pending.TryGetValue(expected, out var rest))
                    {
                        pending.Remove(expected);
                        expected++;
                        yield return rest;
                    }
                    yield break;
                }
                while (channel.Reader.TryRead(out var item))
                    pending[item.seq] = item.batch;
            }
        }
        finally
        {
            cts.Cancel();
            try { closeTask.GetAwaiter().GetResult(); } catch { /* ignore */ }
            cts.Dispose();
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public async IAsyncEnumerator<TBatch> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (NumWorkers == 0)
        {
            foreach (var b in this)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return b;
            }
            yield break;
        }
        // Простой async-обход через синхронный итератор (нагрузка ввода/вывода уже на воркерах).
        foreach (var b in this)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return b;
            await Task.Yield();
        }
    }
}
