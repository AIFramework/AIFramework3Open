using AI.Agents.Contracts.Models;

namespace AI.Agents.Interop.Models;

/// <summary>Модель цепочки роли: имя модели у провайдера и порт, который к ней ходит.</summary>
/// <param name="Model">Имя модели; оно попадает в <see cref="LlmAnswer.Model"/>, если порт модель не сообщил.</param>
/// <param name="Port">Порт одной модели (обычно <c>ModelPort.Fixed</c> над клиентом этой модели).</param>
public sealed record ModelSlot(string Model, IModelPort Port)
{
    /// <summary>Имя модели; пустое не допускается.</summary>
    public string Model { get; } = !string.IsNullOrWhiteSpace(Model) ? Model : throw new ArgumentException("Имя модели не задано.", nameof(Model));

    /// <summary>Порт модели.</summary>
    public IModelPort Port { get; } = Port ?? throw new ArgumentNullException(nameof(Port));
}

/// <summary>
/// Правило перехода по цепочке запасных моделей — одно для всех ролей: следующая модель берется, когда сбой
/// вызвала сама модель или ее провайдер и другая модель может ответить.
/// </summary>
/// <remarks>
/// Переход: лимит частоты, отказ провайдера, нет доступа к модели, сеть, модель замолчала, ответ не того вида,
/// прочий сбой. Не переход: переполнение окна (менять надо запрос, а не модель), отмена не вызывающим, ответ без
/// сбоя (в том числе пустой и обрезанный: модель ответила). Незнакомое значение кода — отказ
/// (<see cref="ArgumentOutOfRangeException"/>), а не догадка.
/// </remarks>
public static class ModelFallback
{
    /// <summary>Поможет ли другая модель при этом коде сбоя.</summary>
    /// <param name="failure">Код сбоя.</param>
    /// <exception cref="ArgumentOutOfRangeException">Код не задан или незнаком.</exception>
    public static bool Cures(LlmFailure failure) => failure switch
    {
        LlmFailure.RateLimit or LlmFailure.ProviderRefusal or LlmFailure.Access or LlmFailure.Network
            or LlmFailure.Stalled or LlmFailure.InvalidOutput or LlmFailure.Other => true,
        LlmFailure.ContextOverflow or LlmFailure.Cancelled => false,
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "Код сбоя модели не задан или незнаком."),
    };

    /// <summary>Брать ли следующую модель цепочки после этого итога.</summary>
    /// <param name="answer">Итог модели.</param>
    public static bool TryNext(LlmAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return answer.Status == LlmStatus.Failed && Cures(answer.Failure ?? LlmFailure.Unknown);
    }
}
