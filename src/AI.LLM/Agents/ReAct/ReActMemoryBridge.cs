using AI.LLM.Agents.Memory;
using AI.LLM.Core.Models.Common.Messages;

namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Превращает контекст <see cref="IAgentMemory"/> в историю для цикла.
/// </summary>
/// <remarks>
/// Память не подключается к движку напрямую: её контракт владеет всем списком сообщений,
/// включая системное, и, будучи подключённой, перекрыла бы системный промпт цикла — тот
/// собирается из промпта вызывающей стороны, навыков и списка инструментов. Поэтому память
/// вызывается до прогона, а сюда попадает только результат.
/// <para>
/// Своё память дописывает к системному промпту: сводку прежнего диалога, найденные
/// воспоминания. Эта приписка возвращается отдельно — движок добавляет её к своему промпту.
/// Отбросив её вместе с системным сообщением, цикл молча терял бы и сводку, и воспоминания.
/// </para>
/// </remarks>
public static class ReActMemoryBridge
{
    /// <summary>Строит историю диалога из памяти.</summary>
    /// <param name="memory">Память; при <c>null</c> возвращается пустая история.</param>
    /// <param name="systemPrompt">Системный промпт, который память ожидает получить.</param>
    /// <param name="query">Текущий запрос.</param>
    /// <returns>
    /// Сообщения истории без системного и без хвостового дубля текущего запроса: первое
    /// перекрыло бы промпт цикла, второе привело бы к тому, что запрос попал бы в переписку дважды.
    /// </returns>
    public static async Task<IReadOnlyList<LLMMessage>> BuildHistoryAsync(
        IAgentMemory memory, string systemPrompt, string query)
    {
        return (await BuildAsync(memory, systemPrompt, query).ConfigureAwait(false)).History;
    }

    /// <summary>
    /// История диалога из памяти и то, что память дописала к системному промпту.
    /// </summary>
    /// <param name="memory">Память; при <c>null</c> — пустая история и пустая приписка.</param>
    /// <param name="systemPrompt">Системный промпт, который память ожидает получить.</param>
    /// <param name="query">Текущий запрос.</param>
    /// <returns>
    /// История — как у <see cref="BuildHistoryAsync"/>; приписка — текст системного сообщения
    /// памяти сверх <paramref name="systemPrompt"/>, пустая строка, если память ничего не добавила.
    /// </returns>
    public static async Task<(IReadOnlyList<LLMMessage> History, string SystemAddition)> BuildAsync(
        IAgentMemory memory, string systemPrompt, string query)
    {
        if (memory == null)
            return ([], string.Empty);

        List<LLMMessage> context = await memory.BuildContextAsync(query, systemPrompt).ConfigureAwait(false);
        if (context is not { Count: > 0 })
            return ([], string.Empty);

        var history = new List<LLMMessage>(context.Count);
        var addition = new System.Text.StringBuilder();
        foreach (LLMMessage message in context)
        {
            if (message == null)
                continue;

            if (message.Role == LLMMessage.SystemRole)
            {
                AppendAddition(addition, message.Content?.ToString(), systemPrompt);
                continue;
            }

            history.Add(message);
        }

        if (history.Count > 0
            && history[^1].Role == LLMMessage.UserRole
            && string.Equals(history[^1].Content?.ToString(), query, StringComparison.Ordinal))
            history.RemoveAt(history.Count - 1);

        return (history, addition.ToString());
    }

    /// <summary>Добавляет к приписке системный текст памяти без исходного промпта в его начале.</summary>
    private static void AppendAddition(System.Text.StringBuilder addition, string text, string systemPrompt)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (!string.IsNullOrEmpty(systemPrompt) && text.StartsWith(systemPrompt, StringComparison.Ordinal))
            text = text[systemPrompt.Length..];

        text = text.Trim();
        if (text.Length == 0)
            return;

        if (addition.Length > 0)
            addition.Append("\n\n");
        addition.Append(text);
    }
}
