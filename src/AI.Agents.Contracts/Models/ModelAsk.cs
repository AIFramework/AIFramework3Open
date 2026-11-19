using System.Text.Json;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Запрос к модели через <see cref="IModelPort"/>: роль вместо имени модели, сообщения частями, инструменты
/// описаниями <see cref="ToolSpec"/>.
/// </summary>
public sealed record ModelAsk
{
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

    /// <summary>JSON Schema, по которой модель обязана ответить; <see langword="null"/>: ответ свободный.</summary>
    public JsonElement? Schema { get; init; }

    /// <summary>Предел токенов ответа; <see langword="null"/>: предел выбирает реализация порта.</summary>
    public int? MaxTokens { get; init; }
}
