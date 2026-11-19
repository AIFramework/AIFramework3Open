using System.Text.Json;
using AI.Agents.Contracts.Citations;
using AI.Agents.Contracts.Engines;

namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Итог вызова инструмента: статус, текст для модели и все, что раньше ехало текстом или меткой, — полями.
/// </summary>
public sealed record ToolResult
{
    /// <summary>Чем кончился вызов.</summary>
    public required ToolStatus Status { get; init; }

    /// <summary>Что увидит модель.</summary>
    public required string Text { get; init; }

    /// <summary>Откуда текст: доверенный или внешний.</summary>
    public required Origin Origin { get; init; }

    /// <summary>
    /// Машинный код ошибки (<c>timeout</c>, <c>no_result</c>, свой код инструмента); <see langword="null"/>:
    /// ошибки нет.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>Структурный результат; <see langword="null"/>: есть только <see cref="Text"/>.</summary>
    public JsonElement? Data { get; init; }

    /// <summary>Созданные, измененные и возвращенные файлы, в том числе картинки для модели; <see langword="null"/>: файлов нет.</summary>
    public IReadOnlyList<ToolFile>? Files { get; init; }

    /// <summary>
    /// Подтверждения, еще не сверенные (<see cref="Citation.Verified"/> = <see langword="false"/>): сверяет и
    /// расставляет их приемка, а не инструмент. <see langword="null"/>: подтверждений нет.
    /// </summary>
    public IReadOnlyList<Citation>? Evidence { get; init; }

    /// <summary>Вопрос человеку при <see cref="ToolStatus.NeedsInput"/> и <see cref="ToolStatus.AwaitsUser"/>; иначе <see langword="null"/>.</summary>
    public Question? Question { get; init; }

    /// <summary>
    /// Вид этого вызова, если он оказался выше объявленного (например, нажатие в браузере отправило форму);
    /// <see langword="null"/>: вид не поднимался. Понизить вид нельзя. Поле для журнала: решение до вызова
    /// принимается по <see cref="ITool.Classify(ToolCall)"/>.
    /// </summary>
    public ToolAction? Raised { get; init; }

    /// <summary>Инструмент-финал: его итог завершает запуск; <see langword="false"/>: запуск продолжается.</summary>
    public bool EndsRun { get; init; }
}
