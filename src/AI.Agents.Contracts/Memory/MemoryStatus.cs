using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Memory;

/// <summary>Статус обращения к памяти. Сбой приходит статусом, а не исключением и не пустым ответом.</summary>
[JsonConverter(typeof(StrictEnumConverter<MemoryStatus>))]
public enum MemoryStatus
{
    /// <summary>Статус не задан. Ни успехом, ни сбоем не считается.</summary>
    Unknown = 0,

    /// <summary>Все сделано.</summary>
    Ok,

    /// <summary>Сделано не все: часть хранилищ или каналов не ответила; причины — в списке проблем.</summary>
    Partial,

    /// <summary>Сбой: ничего не записано или не прочитано; причины — в списке проблем.</summary>
    Failed,
}
