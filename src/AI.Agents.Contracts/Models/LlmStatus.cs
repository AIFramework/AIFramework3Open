using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Чем кончился вызов модели.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<LlmStatus>))]
public enum LlmStatus
{
    /// <summary>Не задано. Успехом не считается.</summary>
    Unknown = 0,

    /// <summary>Ответ дописан до конца.</summary>
    Ok,

    /// <summary>Ответ оборван пределом длины: текст есть, но неполный.</summary>
    Truncated,

    /// <summary>Модель не написала ответа: пустой текст, нет вызовов, одно рассуждение.</summary>
    Empty,

    /// <summary>Вызов не состоялся; код в <see cref="LlmAnswer.Failure"/>.</summary>
    Failed,
}
