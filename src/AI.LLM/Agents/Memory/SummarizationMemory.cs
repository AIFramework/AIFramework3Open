using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using Serilog;

namespace AI.LLM.Agents.Memory;

/// <summary>
/// Память с автоматическим сжатием через LLM при переполнении.
/// </summary>
/// <remarks>
/// Сжатие ЗАМЕЩАЕТ прежнюю сводку, а не приписывается к ней: прошлая сводка уходит в запрос
/// вместе с новыми сообщениями и возвращается одним текстом. Иначе память растёт линейно и через
/// несколько сжатий «краткое содержание» оказывается длиннее исходного диалога — ровно то, ради
/// чего сжатие и делалось.
/// <para>
/// Обращение к модели выполняется ВНЕ замка: оно занимает секунды, а под замком стоят и запись
/// новых реплик, и построение контекста. Пока сжатие идёт, память продолжает работать со старой
/// сводкой и полной историей — хуже от этого только объём одного запроса.
/// </para>
/// </remarks>
public sealed class SummarizationMemory : IAgentMemory
{
    private readonly ILLMClient _llm;
    private readonly int _maxMessages;
    private readonly List<LLMMessage> _history = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private string _summary;

    /// <summary>Сжатие уже идёт — второе запускать не нужно.</summary>
    private bool _compacting;

    /// <summary>
    /// Номер поколения памяти; растёт на каждый <see cref="ClearAsync"/>. Сжатие, начатое до
    /// очистки, не должно записывать свой результат в уже очищенную память.
    /// </summary>
    private int _generation;

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
        List<LLMMessage> pending = null;
        string previous = null;
        int generation = 0;

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _history.Add(LLMMessage.CreateMessage(Roles.User, query));
            _history.Add(LLMMessage.CreateMessage(Roles.Assistant, answer));

            if (_history.Count > _maxMessages && !_compacting)
            {
                _compacting = true;
                pending = [.. _history];
                previous = _summary;
                generation = _generation;
            }
        }
        finally { _semaphore.Release(); }

        if (pending == null)
            return;

        await CompactAsync(pending, previous, generation).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _history.Clear();
            _summary = null;

            // Идущее сжатие относится к прежнему содержимому — его результат сюда не попадёт.
            _generation++;
            _compacting = false;
        }
        finally { _semaphore.Release(); }
    }

    /// <summary>
    /// Просит модель свернуть прежнюю сводку вместе со снятыми сообщениями в одну новую
    /// и заменяет ею прежнюю.
    /// </summary>
    private async Task CompactAsync(List<LLMMessage> pending, string previous, int generation)
    {
        string summary;
        try
        {
            summary = await RequestSummaryAsync(previous, pending).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Не сжали — не беда: история просто останется длинной и попытка повторится
            // на следующей реплике.
            Log.Warning(ex, "SummarizationMemory: не удалось сжать историю");
            await ReleaseCompactionAsync(generation).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            Log.Warning("SummarizationMemory: модель вернула пустую сводку, история оставлена как есть");
            await ReleaseCompactionAsync(generation).ConfigureAwait(false);
            return;
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (generation != _generation)
                return; // Память очищена, пока шло сжатие.

            _summary = summary.Trim();

            // Снимаем ровно то, что ушло в сводку: реплики, добавленные за время обращения
            // к модели, ещё не сжаты и должны остаться.
            _history.RemoveRange(0, Math.Min(pending.Count, _history.Count));
            _compacting = false;
        }
        finally { _semaphore.Release(); }
    }

    private async Task ReleaseCompactionAsync(int generation)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (generation == _generation)
                _compacting = false;
        }
        finally { _semaphore.Release(); }
    }

    private async Task<string> RequestSummaryAsync(string previous, List<LLMMessage> messages)
    {
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(previous))
        {
            sb.AppendLine("Краткое содержание более раннего разговора:");
            sb.AppendLine(previous);
            sb.AppendLine();
            sb.AppendLine("Продолжение диалога:");
        }

        foreach (var msg in messages)
            sb.AppendLine($"{msg.Role}: {msg.Content}");

        var prompt =
            "Сожми всё нижеследующее в ОДНО краткое содержание (3-5 предложений), сохранив ключевые " +
            "факты и решения. Ответь только текстом содержания, без пояснений.\n\n" + sb;

        return await _llm.SendAsync(prompt).ConfigureAwait(false);
    }
}
