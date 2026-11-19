using System.Text.Json;
using System.Text.Json.Nodes;
using AI.Agents.Contracts.Citations;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Итог прогона движка. Приходит единственным способом — последним событием потока
/// (<see cref="EngineEvent.Finished"/>).
/// </summary>
public sealed record EngineResult
{
    /// <summary>Чем кончился прогон.</summary>
    public required EngineStatus Status { get; init; }

    /// <summary>Машинный код причины остановки; тот же словарь, что у <see cref="IRunBudget"/>.</summary>
    public required StopCode StopCode { get; init; }

    /// <summary>Ответ текстом; <see langword="null"/>: текста нет.</summary>
    public string? Text { get; init; }

    /// <summary>Структурный итог инструмента-финала; <see langword="null"/>: его нет.</summary>
    public JsonElement? Payload { get; init; }

    /// <summary>
    /// Непроверенные подтверждения, собранные за прогон. Проверяет их приемка, не движок;
    /// <see langword="null"/>: подтверждений нет.
    /// </summary>
    public IReadOnlyList<Citation>? Evidence { get; init; }

    /// <summary>Файлы, созданные за прогон; <see langword="null"/>: файлов нет.</summary>
    public IReadOnlyList<ToolFile>? Files { get; init; }

    /// <summary>Что не сделано (при <see cref="EngineStatus.Incomplete"/>); <see langword="null"/>: недоделанного нет.</summary>
    public IReadOnlyList<string>? Unfinished { get; init; }

    /// <summary>Вопрос человеку при <see cref="EngineStatus.AwaitsUser"/>; иначе <see langword="null"/>.</summary>
    public Question? Question { get; init; }

    /// <summary>Расход прогона на модель; <see langword="null"/>: движок расход не считал.</summary>
    public ModelUsage? Usage { get; init; }

    /// <summary>
    /// Состояние движка для продолжения (передается в <see cref="EngineInput.State"/>); <see langword="null"/>:
    /// продолжать нечего.
    /// </summary>
    public JsonObject? State { get; init; }
}
