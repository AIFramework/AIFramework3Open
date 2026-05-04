using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;

namespace AI.LLM.Agents.Memory;

/// <summary>
/// Память с автоматическим сжатием через LLM при переполнении.
/// Потокобезопасна — <see cref="SemaphoreSlim"/> гарантирует,
/// что суммаризация не запускается конкурентно.
/// </summary>
public sealed class SummarizationMemory : IAgentMemory
{
    private readonly ILLMClient _llm;
    private readonly int _maxMessages;
    private readonly List<LLMMessage> _history = [];
    private string _summary;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <param name="llm">LLM-клиент для суммаризации.</param>
    /// <param name="maxMessages">Порог, после которого происходит сжатие.</param>
    public SummarizationMemory(ILLMClient llm, int maxMessages = 20)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _maxMessages = Math.Max(4, maxMessages);
    }

    /// <inheritdoc />
    public async Task<List<LLMMessage>> BuildContextAsync(string query, string systemPrompt)
    {
        var messages = new List<LLMMessage>();

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var prompt = string.IsNullOrEmpty(_summary)
                ? systemPrompt
                : $"{systemPrompt}\n\n### Краткое содержание предыдущего диалога:\n{_summary}";

            messages.Add(LLMMessage.CreateMessage(Roles.System, prompt));
            messages.AddRange(_history);
        }
        finally { _semaphore.Release(); }

        messages.Add(LLMMessage.CreateMessage(Roles.User, query));
        return messages;
    }

    /// <inheritdoc />
    public async Task SaveInteractionAsync(string query, string answer, List<LLMMessage> fullHistory)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _history.Add(LLMMessage.CreateMessage(Roles.User, query));
            _history.Add(LLMMessage.CreateMessage(Roles.Assistant, answer));

            if (_history.Count > _maxMessages)
                await SummarizeLockedAsync().ConfigureAwait(false);
        }
        finally { _semaphore.Release(); }
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _history.Clear();
            _summary = null;
        }
        finally { _semaphore.Release(); }
    }

    /// <summary>Вызывается ТОЛЬКО под семафором — гонка исключена.</summary>
    private async Task SummarizeLockedAsync()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var msg in _history)
            sb.AppendLine($"{msg.Role}: {msg.Content}");

        var prompt = $"Сожми следующий диалог в краткое содержание (3-5 предложений), " +
                     $"сохранив ключевые факты и решения:\n\n{sb}";

        var newSummary = await _llm.SendAsync(prompt).ConfigureAwait(false);

        _summary = string.IsNullOrEmpty(_summary)
            ? newSummary
            : $"{_summary}\n\n{newSummary}";

        _history.Clear();
    }
}
