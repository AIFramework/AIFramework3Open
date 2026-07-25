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
        if (memory == null)
            return [];

        List<LLMMessage> context = await memory.BuildContextAsync(query, systemPrompt).ConfigureAwait(false);
        if (context is not { Count: > 0 })
            return [];

        var history = new List<LLMMessage>(context.Count);
        foreach (LLMMessage message in context)
        {
            if (message == null || message.Role == LLMMessage.SystemRole)
                continue;

            history.Add(message);
        }

        if (history.Count > 0
            && history[^1].Role == LLMMessage.UserRole
            && string.Equals(history[^1].Content?.ToString(), query, StringComparison.Ordinal))
            history.RemoveAt(history.Count - 1);

        return history;
    }
}
