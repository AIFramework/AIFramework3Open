using AI.LLM.Core.Models.Common.Responses;

namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Решение одного шага: рассуждение и либо действия, либо сигнал завершения.
/// </summary>
/// <remarks>
/// Состояний три, и «не разобрали ответ» — отдельное от «модель закончила». Смешивать их нельзя:
/// именно на этом ломались прежние реализации, где неразобранный JSON молча означал «готово»,
/// и цикл завершался с пустыми руками, ничего не сообщив ни модели, ни пользователю.
/// </remarks>
public sealed class ReActDecision
{
    /// <summary>Рассуждение шага. Может быть <c>null</c>.</summary>
    public string Thought { get; init; }

    /// <summary>
    /// Запрошенные действия. Никогда не <c>null</c>. Список, а не одно действие: нативный
    /// function calling позволяет модели запросить несколько вызовов сразу, и на каждый
    /// протокол требует отдельный ответ.
    /// </summary>
    public IReadOnlyList<ReActAction> Actions { get; init; } = [];

    /// <summary>Модель считает, что собранных данных достаточно.</summary>
    public bool IsFinal { get; init; }

    /// <summary>Ответ модели не удалось разобрать. Это НЕ то же самое, что <see cref="IsFinal"/>.</summary>
    public bool IsMalformed { get; init; }

    /// <summary>
    /// Текст, присланный вместе с сигналом завершения. При включённом синтезе это ЧЕРНОВИК:
    /// решение шага генерируется в урезанном бюджете и часто в JSON-режиме, поэтому итоговый
    /// текст пишет отдельный полнобюджетный вызов, получая черновик как основу.
    /// Может быть <c>null</c>.
    /// </summary>
    public string FinalText { get; init; }

    /// <summary>Расход токенов на этот вызов. Может быть <c>null</c>.</summary>
    public Usage Usage { get; init; }

    /// <summary>Сырой ответ модели — для диагностики и для повторной попытки разбора. Может быть <c>null</c>.</summary>
    public string RawResponse { get; init; }

    /// <summary>Есть ли у решения хотя бы одно действие.</summary>
    public bool HasActions => Actions.Count > 0;

    /// <summary>Завершение цикла.</summary>
    /// <param name="finalText">Текст ответа или черновик.</param>
    /// <param name="thought">Рассуждение шага.</param>
    /// <param name="usage">Расход токенов.</param>
    /// <param name="rawResponse">Сырой ответ модели.</param>
    public static ReActDecision Final(
        string finalText, string thought = null, Usage usage = null, string rawResponse = null) =>
        new() { IsFinal = true, FinalText = finalText, Thought = thought, Usage = usage, RawResponse = rawResponse };

    /// <summary>Одно действие.</summary>
    /// <param name="action">Действие.</param>
    /// <param name="thought">Рассуждение шага.</param>
    /// <param name="usage">Расход токенов.</param>
    /// <param name="rawResponse">Сырой ответ модели.</param>
    public static ReActDecision Act(
        ReActAction action, string thought = null, Usage usage = null, string rawResponse = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new ReActDecision
        {
            Actions = [action],
            Thought = thought,
            Usage = usage,
            RawResponse = rawResponse,
        };
    }

    /// <summary>Несколько действий одного шага.</summary>
    /// <param name="actions">Действия; пустой список равнозначен отсутствию решения.</param>
    /// <param name="thought">Рассуждение шага.</param>
    /// <param name="usage">Расход токенов.</param>
    /// <param name="rawResponse">Сырой ответ модели.</param>
    public static ReActDecision Act(
        IReadOnlyList<ReActAction> actions, string thought = null, Usage usage = null, string rawResponse = null) =>
        new()
        {
            Actions = actions ?? [],
            Thought = thought,
            Usage = usage,
            RawResponse = rawResponse,
        };

    /// <summary>Ответ модели не разобран. Движок подскажет модели формат и повторит попытку.</summary>
    /// <param name="rawResponse">Сырой ответ модели.</param>
    /// <param name="usage">Расход токенов.</param>
    public static ReActDecision Malformed(string rawResponse, Usage usage = null) =>
        new() { IsMalformed = true, RawResponse = rawResponse, Usage = usage };
}
