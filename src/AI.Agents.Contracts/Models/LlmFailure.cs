using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Машинный код сбоя вызова модели. Текст для человека из него строит продукт, в контракте его нет.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<LlmFailure>))]
public enum LlmFailure
{
    /// <summary>Не задано.</summary>
    Unknown = 0,

    /// <summary>Запрос не помещается в окно модели.</summary>
    ContextOverflow,

    /// <summary>Превышен предел частоты запросов.</summary>
    RateLimit,

    /// <summary>Провайдер отказал: модель недоступна, запрос отклонен.</summary>
    ProviderRefusal,

    /// <summary>Нет доступа: ключ, права, баланс.</summary>
    Access,

    /// <summary>Сеть: соединение, тайм-аут.</summary>
    Network,

    /// <summary>
    /// Запрос отменен не вызывающим (провайдером, промежуточным узлом). Отмену токеном вызывающего порт
    /// сообщает исключением <see cref="OperationCanceledException"/>, как принято в .NET.
    /// </summary>
    Cancelled,

    /// <summary>Прочий сбой.</summary>
    Other,
}
