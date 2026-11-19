using System.Text.Json.Nodes;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Engines;

/// <summary>
/// Вход прогона: задача, контекст, порты (инструменты, модель, бюджет, другие агенты) и состояние для
/// продолжения.
/// </summary>
/// <remarks>
/// Порты — живые объекты хоста, поэтому вход целиком в JSON не пишется; данные входа (<see cref="EngineTask"/>,
/// <see cref="EngineContext"/>, состояние, параметры) — отдельные записи и пишутся. Срока здесь нет: он живет
/// в <see cref="IRunBudget"/>.
/// </remarks>
public sealed record EngineInput
{
    /// <summary>Идентификатор запуска (непрозрачная строка хоста).</summary>
    public required string RunId { get; init; }

    /// <summary>Задача.</summary>
    public required EngineTask Task { get; init; }

    /// <summary>Набор инструментов: единственный путь движка к инструментам.</summary>
    public required IToolSet Tools { get; init; }

    /// <summary>Порт модели: единственный путь движка к модели.</summary>
    public required IModelPort Model { get; init; }

    /// <summary>Бюджет: шаги, токены, деньги, срок.</summary>
    public required IRunBudget Budget { get; init; }

    /// <summary>
    /// Запуск других агентов шагом; <see langword="null"/>: в этом запуске звать других агентов нельзя, и
    /// движок, которому это нужно, кончает прогон итогом со статусом <see cref="EngineStatus.Failed"/>.
    /// </summary>
    public IStepRunner? Steps { get; init; }

    /// <summary>Контекст: инструкции, история, файлы; <see langword="null"/>: кроме задачи агент ничего не видит.</summary>
    public EngineContext? Context { get; init; }

    /// <summary>Параметры движка из записи агента; <see langword="null"/>: параметров нет, движок работает по своим правилам.</summary>
    public JsonObject? Options { get; init; }

    /// <summary>
    /// Состояние для продолжения (из <see cref="EngineResult.State"/> или <see cref="EngineEvent.Saved"/>);
    /// <see langword="null"/>: новый прогон.
    /// </summary>
    public JsonObject? State { get; init; }
}
