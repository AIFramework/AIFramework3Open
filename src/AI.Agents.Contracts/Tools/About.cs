namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Подробное описание исполнителя по блокам: что делает, чего не делает, как к нему обращаться, примеры.
/// Сейчас его несет <see cref="ToolSpec.About"/>; тот же тип возьмет запись агента.
/// </summary>
/// <remarks>
/// Одна строка описания плохо отвечает на вопрос «мой ли это случай», поэтому описание разбито на блоки с
/// разными ролями. Обязателен только <see cref="Does"/>: у инструментов MCP и OpenAI описание — одна строка,
/// и <see cref="FromText(string)"/> переводит ее в этот тип.
/// </remarks>
public sealed record About
{
    /// <summary>Назначение: какие задачи решает, что принимает и что отдает.</summary>
    public required string Does { get; init; }

    /// <summary>Что вне назначения и куда с этим идти; <see langword="null"/>: не указано.</summary>
    public string? DoesNot { get; init; }

    /// <summary>Как составлять обращение: формат аргументов, обязательные сведения; <see langword="null"/>: не указано.</summary>
    public string? Rules { get; init; }

    /// <summary>Образцы обращений; <see langword="null"/>: образцов нет.</summary>
    public IReadOnlyList<string>? Examples { get; init; }

    /// <summary>
    /// Чему в результате нельзя верить без проверки и как проверять; <see langword="null"/>: оговорок нет.
    /// </summary>
    public string? Guardrails { get; init; }

    /// <summary>
    /// Описание из одной строки, как у инструментов MCP и OpenAI.
    /// </summary>
    /// <param name="description">Строка описания; пустая не допускается.</param>
    /// <returns>Описание, в котором заполнен только <see cref="Does"/>.</returns>
    /// <exception cref="ArgumentException">Строка пустая или из пробелов.</exception>
    public static About FromText(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new About { Does = description.Trim() };
    }
}
