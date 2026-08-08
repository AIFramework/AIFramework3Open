using AI.LLM.Core.Models.Common.Messages;
using Serilog;

namespace AI.LLM.Agents.Memory;

/// <summary>
/// Композитная память — объединяет краткосрочную (скользящее окно) и долгосрочную (векторную).
/// Контекст строится из долгосрочных воспоминаний + недавней истории.
/// </summary>
/// <remarks>
/// Долгосрочной памяти достаточно реализовать <see cref="IRecallMemory"/> — тогда её вклад
/// забирается напрямую. Для реализаций без него остаётся запасной путь: он сравнивает системный
/// промпт до и после и берёт то, что память в него дописала. Путь работает не с любой
/// реализацией, поэтому о его безрезультатности сообщается в лог, а не проходит незаметно.
/// </remarks>
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
        var augmentedSystemPrompt = await BuildAugmentedPromptAsync(query, systemPrompt).ConfigureAwait(false);
        return await _shortTerm.BuildContextAsync(query, augmentedSystemPrompt).ConfigureAwait(false);
    }

    /// <summary>Системный промпт, дополненный вкладом долгосрочной памяти.</summary>
    private async Task<string> BuildAugmentedPromptAsync(string query, string systemPrompt)
    {
        if (_longTerm is IRecallMemory recall)
        {
            var memories = await recall.RecallAsync(query).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(memories)
                ? systemPrompt
                : $"{systemPrompt}\n\n{IRecallMemory.SectionHeader}\n{memories}";
        }

        // Память не умеет отдавать воспоминания отдельно. Собрать из двух готовых контекстов
        // один нельзя — вышло бы два системных промпта и два запроса, — поэтому берём то
        // единственное, что можно взять: как долгосрочная память переписала системный промпт.
        var context = await _longTerm.BuildContextAsync(query, systemPrompt).ConfigureAwait(false);

        var produced = context is { Count: > 0 } && context[0].Role == LLMMessage.SystemRole
            ? context[0].Content?.ToString()
            : null;

        if (!string.IsNullOrEmpty(produced) && !string.Equals(produced, systemPrompt, StringComparison.Ordinal))
            return produced;

        // Промпт не изменился — значит от долгосрочной памяти в контекст не попало НИЧЕГО, и
        // композит молча выродился в одну краткосрочную. Раньше это выглядело как рабочая
        // конфигурация: ошибки нет, результата тоже.
        Log.Warning(
            "CompositeMemory: долгосрочная память {Type} не реализует IRecallMemory и ничего не "
            + "добавила к системному промпту — её содержимое в контекст не попало.",
            _longTerm.GetType().Name);

        return systemPrompt;
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
