using AI.LLM.Core.Models.Common.Messages;

namespace AI.LLM.Agents.Memory;

/// <summary>
/// Композитная память — объединяет краткосрочную (скользящее окно) и долгосрочную (векторную).
/// Контекст строится из долгосрочных воспоминаний + недавней истории.
/// </summary>
public sealed class CompositeMemory : IAgentMemory
{
    private readonly IAgentMemory _shortTerm;
    private readonly IAgentMemory _longTerm;

    /// <summary>
    /// Композитная память.
    /// </summary>
    /// <param name="shortTerm">Краткосрочная память (например <see cref="SlidingWindowMemory"/>).</param>
    /// <param name="longTerm">Долгосрочная память (например <see cref="VectorMemory"/>).</param>
    public CompositeMemory(IAgentMemory shortTerm, IAgentMemory longTerm)
    {
        _shortTerm = shortTerm ?? throw new ArgumentNullException(nameof(shortTerm));
        _longTerm = longTerm ?? throw new ArgumentNullException(nameof(longTerm));
    }

    /// <inheritdoc />
    public async Task<List<LLMMessage>> BuildContextAsync(string query, string systemPrompt)
    {
        var longTermCtx = await _longTerm.BuildContextAsync(query, systemPrompt).ConfigureAwait(false);

        var augmentedSystemPrompt = systemPrompt;
        if (longTermCtx.Count > 0 && longTermCtx[0].Role == LLMMessage.SystemRole)
            augmentedSystemPrompt = longTermCtx[0].Content?.ToString() ?? systemPrompt;

        return await _shortTerm.BuildContextAsync(query, augmentedSystemPrompt).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveInteractionAsync(string query, string answer, List<LLMMessage> fullHistory)
    {
        await Task.WhenAll(
            _shortTerm.SaveInteractionAsync(query, answer, fullHistory),
            _longTerm.SaveInteractionAsync(query, answer, fullHistory)
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        await Task.WhenAll(
            _shortTerm.ClearAsync(),
            _longTerm.ClearAsync()
        ).ConfigureAwait(false);
    }
}
