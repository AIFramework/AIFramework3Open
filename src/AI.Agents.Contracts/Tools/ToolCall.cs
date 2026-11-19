using System.Text.Json;

namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Вызов инструмента: какой инструмент и с какими аргументами. Приходит от модели или от движка.
/// </summary>
/// <remarks>
/// Имя здесь не проверяется: модель может назвать несуществующий инструмент, и такой вызов должен
/// дойти до набора, чтобы тот ответил понятной ошибкой.
/// </remarks>
public sealed record ToolCall
{
    /// <summary>Идентификатор вызова: по нему результат сопоставляется с вызовом.</summary>
    public required string Id { get; init; }

    /// <summary>Имя вызываемого инструмента (<see cref="ToolSpec.Name"/>).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Аргументы как JSON. Обычно объект; если модель прислала текст, который не разбирается как JSON,
    /// он приходит строкой JSON (<see cref="JsonValueKind.String"/>), а не теряется.
    /// </summary>
    public required JsonElement Arguments { get; init; }

    /// <summary>
    /// Ключ действия для журнала: одно намерение — одно исполнение. Считает хост по имени и каноническим
    /// аргументам; <see langword="null"/>: ключ не назначен.
    /// </summary>
    public string? EffectKey { get; init; }
}
