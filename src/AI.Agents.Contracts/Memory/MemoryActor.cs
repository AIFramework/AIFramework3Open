using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Memory;

/// <summary>Кто произвел эпизод. От актера зависит, чем станет значение: сказанным или предложением.</summary>
[JsonConverter(typeof(StrictEnumConverter<MemoryActor>))]
public enum MemoryActor
{
    /// <summary>Актер не задан.</summary>
    Unknown = 0,

    /// <summary>Человек: реплика, правка на экране памяти, одобрение предложения.</summary>
    User,

    /// <summary>Ассистент в разговоре с человеком.</summary>
    Assistant,

    /// <summary>Другой агент: работа без человека, результат рабочего процесса.</summary>
    Agent,

    /// <summary>Хост: импорт, обслуживание по расписанию, «забыть чат».</summary>
    Host,
}
