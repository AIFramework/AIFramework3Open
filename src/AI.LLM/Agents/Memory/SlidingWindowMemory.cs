using AI.LLM.Core.Models.Common.Messages;

namespace AI.LLM.Agents.Memory;

/// <summary>
/// Память на основе скользящего окна — хранит последние N сообщений.
/// Потокобезопасна для конкурентных вызовов.
/// </summary>
public sealed class SlidingWindowMemory : IAgentMemory
{
    private readonly int _maxMessages;
    private readonly List<LLMMessage> _history = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <param name="maxMessages">Максимальное число сообщений в истории (не считая system).</param>
    public SlidingWindowMemory(int maxMessages = 20)
    {
        _maxMessages = Math.Max(2, maxMessages);
    }

    /// <inheritdoc />
    public async Task<List<LLMMessage>> BuildContextAsync(string query, string systemPrompt)
    {
        var messages = new List<LLMMessage> { LLMMessage.CreateMessage(Roles.System, systemPrompt) };

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try { messages.AddRange(_history); }
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

            // Удаляем парами (user + assistant), чтобы история не начиналась с ответа ассистента.
            while (_history.Count > _maxMessages && _history.Count >= 2)
                _history.RemoveRange(0, 2);
        }
        finally { _semaphore.Release(); }
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try { _history.Clear(); }
        finally { _semaphore.Release(); }
    }
}
