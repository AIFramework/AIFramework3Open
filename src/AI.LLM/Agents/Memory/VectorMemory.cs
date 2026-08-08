using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;

namespace AI.LLM.Agents.Memory;

/// <summary>
/// Долгосрочная память на основе векторного поиска (эмбеддинги + косинусное сходство).
/// Потокобезопасна для конкурентных вызовов.
/// </summary>
public sealed class VectorMemory : IAgentMemory, IRecallMemory
{
    private readonly IEmbedderService _embedder;
    private readonly int _topK;
    private readonly double _minScore;
    private readonly List<MemoryEntry> _entries = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <param name="embedder">Сервис эмбеддингов.</param>
    /// <param name="topK">Количество релевантных воспоминаний для вставки в контекст.</param>
    /// <param name="minScore">Минимальный порог косинусного сходства (0..1).</param>
    public VectorMemory(IEmbedderService embedder, int topK = 5, double minScore = 0.3)
    {
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        _topK = Math.Max(1, topK);
        _minScore = Math.Clamp(minScore, 0, 1);
    }

    /// <inheritdoc />
    public async Task<List<LLMMessage>> BuildContextAsync(string query, string systemPrompt)
    {
        var memories = await RecallAsync(query).ConfigureAwait(false);

        var systemContent = string.IsNullOrEmpty(memories)
            ? systemPrompt
            : $"{systemPrompt}\n\n{IRecallMemory.SectionHeader}\n{memories}";

        return
        [
            LLMMessage.CreateMessage(Roles.System, systemContent),
            LLMMessage.CreateMessage(Roles.User, query)
        ];
    }

    /// <inheritdoc />
    public async Task SaveInteractionAsync(string query, string answer, List<LLMMessage> fullHistory)
    {
        var text = $"Вопрос: {query}\nОтвет: {answer}";
        var vector = await _embedder.EncodeAsync(text).ConfigureAwait(false);

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try { _entries.Add(new MemoryEntry(text, vector)); }
        finally { _semaphore.Release(); }
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try { _entries.Clear(); }
        finally { _semaphore.Release(); }
    }

    /// <inheritdoc />
    public async Task<string> RecallAsync(string query)
    {
        List<MemoryEntry> snapshot;
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_entries.Count == 0) return null;
            snapshot = [.. _entries];
        }
        finally { _semaphore.Release(); }

        var queryVec = await _embedder.EncodeQuestionAsync(query).ConfigureAwait(false);

        var scored = snapshot
            .Select(e => (e.Text, Score: CosineSimilarity(queryVec, e.Vector)))
            .Where(x => x.Score > _minScore)
            .OrderByDescending(x => x.Score)
            .Take(_topK)
            .ToList();

        return scored.Count == 0 ? null : string.Join("\n---\n", scored.Select(s => s.Text));
    }

    private static double CosineSimilarity(AI.DataStructs.Algebraic.Vector a, AI.DataStructs.Algebraic.Vector b)
    {
        if (a.Count != b.Count) return 0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Count; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = Math.Sqrt(normA * normB);
        return denom < 1e-12 ? 0 : dot / denom;
    }

    private sealed record MemoryEntry(string Text, AI.DataStructs.Algebraic.Vector Vector);
}
