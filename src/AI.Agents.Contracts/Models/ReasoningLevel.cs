using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Сколько модели размышлять перед ответом (<see cref="ModelAsk.Reasoning"/>). Как уровень ложится на провайдера
/// (поле запроса, бюджет токенов рассуждения), решает реализация порта.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<ReasoningLevel>))]
public enum ReasoningLevel
{
    /// <summary>Не задано; в запросе не допускается: «выбирает порт» — это <see langword="null"/>.</summary>
    Unknown = 0,

    /// <summary>Без размышления, если модель это позволяет.</summary>
    None,

    /// <summary>Самое короткое размышление.</summary>
    Minimal,

    /// <summary>Короткое размышление.</summary>
    Low,

    /// <summary>Обычное размышление.</summary>
    Medium,

    /// <summary>Долгое размышление.</summary>
    High,
}
