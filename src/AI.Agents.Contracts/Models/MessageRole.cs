using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Кто автор сообщения в разговоре с моделью.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<MessageRole>))]
public enum MessageRole
{
    /// <summary>Не задано.</summary>
    Unknown = 0,

    /// <summary>Инструкции.</summary>
    System,

    /// <summary>Человек или хост от его имени.</summary>
    User,

    /// <summary>Модель: текст и вызовы инструментов.</summary>
    Assistant,

    /// <summary>Результат инструмента; ссылается на вызов через <see cref="ModelMessage.ToolCallId"/>.</summary>
    Tool,
}
