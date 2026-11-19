using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Как модели обращаться с инструментами в этом запросе.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<ToolMode>))]
public enum ToolMode
{
    /// <summary>Не задано.</summary>
    Unknown = 0,

    /// <summary>Модель сама решает, звать ли инструмент.</summary>
    Auto,

    /// <summary>Не звать инструменты: нужен ответ текстом.</summary>
    None,

    /// <summary>Обязательно позвать какой-нибудь инструмент.</summary>
    Required,

    /// <summary>Обязательно позвать инструмент <see cref="ModelAsk.ToolName"/>.</summary>
    Named,
}
