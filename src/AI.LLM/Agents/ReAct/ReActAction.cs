namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Одно действие, запрошенное моделью: какой инструмент вызвать и с чем.
/// </summary>
/// <remarks>
/// <see cref="Id"/> обязателен и должен быть уникален в пределах шага: при нативном
/// function calling протокол требует, чтобы на каждый вызов пришёл ровно один ответ с тем же
/// идентификатором. Потеря идентификатора ломает следующий запрос к модели.
/// </remarks>
public sealed class ReActAction
{
    /// <summary>Идентификатор вызова. Никогда не пуст.</summary>
    public string Id { get; }

    /// <summary>Имя инструмента.</summary>
    public string ToolName { get; }

    /// <summary>
    /// Аргумент: JSON-объект либо простая строка — зависит от инструмента и от того,
    /// каким образом получено решение. Никогда не <c>null</c>, но может быть пустым.
    /// </summary>
    public string Arguments { get; }

    /// <summary>Создаёт действие.</summary>
    /// <param name="toolName">Имя инструмента; обязательно.</param>
    /// <param name="arguments">Аргумент инструмента; допускается <c>null</c>.</param>
    /// <param name="id">Идентификатор вызова; при отсутствии генерируется.</param>
    public ReActAction(string toolName, string arguments, string id = null)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("Имя инструмента не может быть пустым.", nameof(toolName));

        ToolName = toolName.Trim();
        Arguments = arguments ?? string.Empty;
        Id = string.IsNullOrWhiteSpace(id) ? "call_" + Guid.NewGuid().ToString("N") : id;
    }
}
