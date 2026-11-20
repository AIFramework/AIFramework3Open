using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Memory;

/// <summary>Операция записи. Набор один для всех путей записи.</summary>
[JsonConverter(typeof(StrictEnumConverter<MemoryOp>))]
public enum MemoryOp
{
    /// <summary>Операция не задана.</summary>
    Unknown = 0,

    /// <summary>
    /// Новый факт. Если такой факт уже есть, новой строки не будет: реализация отвечает
    /// <see cref="MemoryVerdict.Confirmed"/> и добавляет эпизод в опоры.
    /// </summary>
    Add,

    /// <summary>
    /// Новое значение вместо старого: старый факт (<see cref="MemoryChange.Target"/>) закрывается сроком
    /// <see cref="MemoryFact.ValidTo"/>, новый (<see cref="MemoryChange.Fact"/>) ссылается на него. Старый не удаляется.
    /// </summary>
    Revise,

    /// <summary>Повтор факта <see cref="MemoryChange.Target"/>: эпизод добавляется в опоры, новой строки нет.</summary>
    Confirm,

    /// <summary>
    /// Факт <see cref="MemoryChange.Target"/> перестал быть верным: срок закрывается, причина — в
    /// <see cref="MemoryChange.Reason"/>. Факт остается в истории.
    /// </summary>
    Retire,
}
