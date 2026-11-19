namespace AI.Agents.Contracts.Models;

/// <summary>
/// Что умеет модель, которую порт ставит на роль: по этому движок выбирает способ разговора с ней.
/// </summary>
/// <remarks>
/// Признаки обязательны: реализация порта должна сказать явно, умеет ли модель. Иначе «не задано» молча
/// превращалось бы в «не умеет» или «умеет», и движок выбирал бы протокол наугад.
/// </remarks>
public sealed record ModelCapabilities
{
    /// <summary>Нативный вызов инструментов (<see cref="ModelAsk.Tools"/>).</summary>
    public required bool ToolCalling { get; init; }

    /// <summary>Картинки на входе (<see cref="MessagePart.Image"/>).</summary>
    public required bool Images { get; init; }

    /// <summary>Ответ строго по JSON Schema (<see cref="ModelAsk.Schema"/>).</summary>
    public required bool StructuredOutput { get; init; }

    /// <summary>Размер окна в токенах; <see langword="null"/>: неизвестен.</summary>
    public int? ContextTokens { get; init; }
}
