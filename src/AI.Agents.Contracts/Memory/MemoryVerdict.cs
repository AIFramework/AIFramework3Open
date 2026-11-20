using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Memory;

/// <summary>Исход одного изменения памяти. Причина — в <see cref="MemoryOutcome.Reason"/>.</summary>
[JsonConverter(typeof(StrictEnumConverter<MemoryVerdict>))]
public enum MemoryVerdict
{
    /// <summary>Исход не задан.</summary>
    Unknown = 0,

    /// <summary>Записан новый факт.</summary>
    Written,

    /// <summary>Записано новое значение, старый факт закрыт сроком (<see cref="MemoryOutcome.Existing"/>).</summary>
    Revised,

    /// <summary>
    /// Повтор: эпизод добавлен в опоры существующего факта (<see cref="MemoryOutcome.Existing"/>), новой строки нет.
    /// </summary>
    Confirmed,

    /// <summary>Не записан, а предложен человеку на решение (очередь «Ждут решения»).</summary>
    Proposed,

    /// <summary>Факт закрыт сроком: снят (Retire) или остался без опор после снятия эпизода.</summary>
    Archived,

    /// <summary>
    /// Не записан: секрет, содержимое источника, нет прав, полный слой, неполное изменение. Причина — в
    /// <see cref="MemoryOutcome.Reason"/>.
    /// </summary>
    Rejected,
}
