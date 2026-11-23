using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Машинный код сбоя вызова модели. Решения принимаются по коду; текст для человека едет рядом, в
/// <see cref="LlmAnswer.FailureText"/>.
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

    /// <summary>
    /// Ответ не того вида, что просили: при <see cref="ModelAsk.Schema"/> текст не разбирается как JSON.
    /// </summary>
    InvalidOutput,

    /// <summary>
    /// Модель замолчала: первый фрагмент потока не пришел в срок или поток встал посреди ответа (сторож потока).
    /// Действие на стороне провайдера могло начаться; повтор решает вызывающий.
    /// </summary>
    Stalled,
}
