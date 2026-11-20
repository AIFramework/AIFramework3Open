using System.Text.Json.Serialization;

namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Безопасен ли повтор вызова инструмента с теми же аргументами. Ось отдельная от вида действия
/// (<see cref="ToolAction"/>): удаление идемпотентно, но необратимо, а отправка письма обратима не больше,
/// чем удаление, и при этом не идемпотентна.
/// </summary>
/// <remarks>
/// Ноль — <see cref="Unknown"/>: незаданное значение не должно молча стать «повтор безопасен». Везде, где
/// решается повтор, <see cref="Unknown"/> трактуется как <see cref="NotIdempotent"/>; трактовку дает один
/// метод <see cref="ToolIdempotencies.Effective(ToolIdempotency)"/>.
/// </remarks>
[JsonConverter(typeof(StrictEnumConverter<ToolIdempotency>))]
public enum ToolIdempotency
{
    /// <summary>Не объявлено. Трактуется как <see cref="NotIdempotent"/>: повторять нельзя.</summary>
    Unknown = 0,

    /// <summary>Повтор безопасен всегда: второй вызов не меняет мир сверх первого (удалить, поставить значение).</summary>
    Idempotent,

    /// <summary>
    /// Повтор безопасен только с тем же ключом эффекта (<see cref="ToolCall.EffectKey"/>): инструмент передает
    /// ключ внешнему сервису (например, заголовком <c>Idempotency-Key</c>), и сервис сам не исполняет второй раз.
    /// </summary>
    WithKey,

    /// <summary>Повтор небезопасен: второй вызов делает действие еще раз (отправить письмо, создать запись, оплатить).</summary>
    NotIdempotent,
}
