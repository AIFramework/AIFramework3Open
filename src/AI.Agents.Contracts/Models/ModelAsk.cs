using System.Text.Json;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Запрос к модели через <see cref="IModelPort"/>: роль вместо имени модели, сообщения частями, инструменты
/// описаниями <see cref="ToolSpec"/>.
/// </summary>
public sealed record ModelAsk
{
    private readonly double? _temperature;
    private readonly ReasoningLevel? _reasoning;

    /// <summary>Роль модели; саму модель на роль выбирает реализация порта.</summary>
    public required ModelRole Role { get; init; }

    /// <summary>Сообщения разговора по порядку.</summary>
    public required IReadOnlyList<ModelMessage> Messages { get; init; }

    /// <summary>
    /// Как обращаться с инструментами. Задается на каждый запрос явно: синтез идет с <see cref="ToolMode.None"/>,
    /// шаг цикла — с <see cref="ToolMode.Auto"/>, принудительный финал — с <see cref="ToolMode.Required"/> или
    /// <see cref="ToolMode.Named"/>.
    /// </summary>
    public required ToolMode ToolMode { get; init; }

    /// <summary>Инструменты для нативного вызова; <see langword="null"/>: без инструментов.</summary>
    public IReadOnlyList<ToolSpec>? Tools { get; init; }

    /// <summary>Какой инструмент звать при <see cref="ToolMode.Named"/> (<see cref="ToolSpec.Name"/>); иначе <see langword="null"/>.</summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// JSON Schema, по которой модель обязана ответить; <see langword="null"/>: ответ свободный. При схеме итог
    /// <see cref="LlmStatus.Ok"/> значит, что <see cref="LlmAnswer.Text"/> — разбираемый JSON; ответ не JSON — итог
    /// <see cref="LlmStatus.Failed"/> с кодом <see cref="LlmFailure.InvalidOutput"/>, пустой ответ —
    /// <see cref="LlmStatus.Empty"/>, сбой провайдера — <see cref="LlmStatus.Failed"/> со своим кодом. Расход едет в
    /// <see cref="LlmAnswer.Usage"/>, как у свободного ответа.
    /// </summary>
    public JsonElement? Schema { get; init; }

    /// <summary>Предел токенов ответа; <see langword="null"/>: предел выбирает реализация порта.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Температура выборки, от 0 (самый вероятный ответ: классификация, ответ по схеме); <see langword="null"/>:
    /// выбирает реализация порта.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Значение отрицательное или не число.</exception>
    public double? Temperature
    {
        get => _temperature;
        init => _temperature = value is null || (double.IsFinite(value.Value) && value.Value >= 0)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Temperature), value, "Температура — конечное число не меньше 0.");
    }

    /// <summary>Сколько модели размышлять; <see langword="null"/>: выбирает реализация порта.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Задано <see cref="ReasoningLevel.Unknown"/>: «не выбрано» — это
    /// <see langword="null"/>, а не значение.</exception>
    public ReasoningLevel? Reasoning
    {
        get => _reasoning;
        init => _reasoning = value != ReasoningLevel.Unknown
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Reasoning), value, "Уровень размышления не задан: вместо Unknown передайте null.");
    }
}
