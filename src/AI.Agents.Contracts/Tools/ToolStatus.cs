using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Чем кончился вызов инструмента.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<ToolStatus>))]
public enum ToolStatus
{
    /// <summary>
    /// Исход неизвестен: обрыв, тайм-аут, поток без итога. Действие могло состояться, поэтому такой вызов
    /// сам не повторяется. Этот статус ставит вызывающий, а не инструмент.
    /// </summary>
    Unknown = 0,

    /// <summary>Вызов удался.</summary>
    Ok,

    /// <summary>Вызов не удался; причина в <see cref="ToolResult.Text"/> и <see cref="ToolResult.ErrorCode"/>.</summary>
    Failed,

    /// <summary>Вызов запрещен: гейт или правила не разрешили действие.</summary>
    Denied,

    /// <summary>Не хватает данных; что спросить в <see cref="ToolResult.Question"/>.</summary>
    NeedsInput,

    /// <summary>Нужен ответ или согласие человека; вопрос в <see cref="ToolResult.Question"/>.</summary>
    AwaitsUser,
}
