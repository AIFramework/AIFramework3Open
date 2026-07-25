using System.Text;
using AI.LLM.Agents.ReAct.Tools;

namespace AI.LLM.Agents.ReAct.Rendering;

/// <summary>
/// Тексты по умолчанию: только механика цикла, без единого названия продукта, поставщика
/// или модели. Всё предметное задаётся базовым промптом и навыками вызывающей стороны.
/// </summary>
public sealed class DefaultReActPromptTemplate : IReActPromptTemplate
{
    /// <inheritdoc />
    public string BuildSystemPrompt(
        string basePrompt,
        IReadOnlyList<IReActTool> tools,
        IReadOnlyList<IReActSkill> skills,
        ReActRunContext context)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(basePrompt))
            sb.Append(basePrompt.TrimEnd()).Append("\n\n");

        sb.Append("Ты работаешь по циклу ReAct: рассуждение → действие → наблюдение → рассуждение.\n");

        if (tools is { Count: > 0 })
        {
            sb.Append("\nДоступные инструменты:\n");
            foreach (IReActTool tool in tools)
            {
                sb.Append("- ").Append(tool.Name);
                if (!string.IsNullOrWhiteSpace(tool.Description))
                    sb.Append(": ").Append(tool.Description);
                sb.Append('\n');
            }
        }

        if (skills is { Count: > 0 })
        {
            sb.Append("\nИнструкции:\n");
            foreach (IReActSkill skill in skills)
                sb.Append("- ").Append(skill.Name).Append(": ").Append(skill.Instruction).Append('\n');
        }

        sb.Append("\nПравила:\n");
        sb.Append("- На каждом шаге сначала коротко рассуждай, затем выбирай действие.\n");
        sb.Append("- Вызывай инструмент только тогда, когда он реально нужен. Если ответ известен — завершай.\n");
        sb.Append("- Не повторяй уже выполненные действия: их результаты перечислены в наблюдениях.\n");
        sb.Append("- Инструмент вернул ошибку — не повторяй тот же вызов. Выбери другой путь "
                  + "или честно скажи, что не получилось.\n");
        sb.Append("- Не подменяй задачу: если подходящего инструмента нет, так и скажи, "
                  + "а не делай вместо этого что-то другое.\n");
        sb.Append("- Когда наблюдений достаточно — завершай работу.\n");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string BuildUnknownToolNote(string requestedName, IReadOnlyList<string> availableNames)
    {
        var sb = new StringBuilder();
        sb.Append("Инструмента «").Append(requestedName).Append("» не существует, вызов не выполнен. ");

        if (availableNames is { Count: > 0 })
        {
            sb.Append("Доступны только: ");
            for (int i = 0; i < availableNames.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(availableNames[i]);
            }

            sb.Append(". ");
        }

        sb.Append("Выбери инструмент из списка либо завершай работу.");
        return sb.ToString();
    }

    /// <inheritdoc />
    public string BuildRepeatedActionNote(string toolName) =>
        $"Инструмент «{toolName}» уже вызывался с теми же аргументами — повторный вызов не выполнен, "
        + "результат приведён выше. Сделай следующий шаг или завершай работу.";

    /// <inheritdoc />
    public string BuildRepeatedFailureNote(string toolName) =>
        $"Инструмент «{toolName}» не сработал несколько раз подряд. Не пытайся снова: "
        + "объясни, что именно не получилось, либо выбери другой путь.";

    /// <inheritdoc />
    public string BuildMalformedDecisionNote() =>
        "Предыдущий ответ не удалось разобрать. Ответь строго в требуемом формате, без текста вне него.";
}
