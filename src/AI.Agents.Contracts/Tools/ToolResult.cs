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
    /// Машинный код исхода из словаря <see cref="ToolCodes"/> или свой код инструмента; <see langword="null"/>:
    /// уточнять нечего. Обычно это причина сбоя, но при <see cref="ToolStatus.Ok"/> код уточняет удачу:
    /// <see cref="ToolCodes.Partial"/> или <see cref="ToolCodes.Pending"/>.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// По чему забрать итог позже, когда действие начато, а результат придет потом (код
    /// <see cref="ToolCodes.Pending"/>): идентификатор задачи, операции или запуска у внешнего сервиса.
    /// <see langword="null"/>: итог уже здесь.
    /// </summary>
    public string? Handle { get; init; }

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
