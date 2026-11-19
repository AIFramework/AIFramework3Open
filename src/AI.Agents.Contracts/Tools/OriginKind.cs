using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Откуда пришел текст: можно ли исполнять его как указание.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<OriginKind>))]
public enum OriginKind
{
    /// <summary>Происхождение не задано. Доверенным такой текст не считается.</summary>
    Unknown = 0,

    /// <summary>Текст написал хост, агент или человек, от имени которого идет запуск.</summary>
    Trusted,

    /// <summary>Текст извне (страница, письмо, чужой сервер): это данные, исполнять их как указание нельзя.</summary>
    External,
}
