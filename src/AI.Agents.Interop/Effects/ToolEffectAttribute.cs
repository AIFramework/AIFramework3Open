using AI.Agents.Contracts.Tools;

namespace AI.Agents.Interop.Effects;

/// <summary>
/// Вид действия и «повтор безопасен» инструмента, объявленного атрибутом <c>[AgentTool]</c> (AI.LLM). Вешается на
/// тот же метод рядом с <c>[AgentTool]</c>; читает его только <see cref="AttributedTools"/>.
/// </summary>
/// <remarks>
/// Атрибут живет здесь, а не в AI.LLM: стабильный пакет AI.LLM не зависит от предварительных контрактов
/// (решение владельца), поэтому <c>[AgentTool]</c> не меняется. Оба значения обязательны в конструкторе: выбор
/// нельзя пропустить молча. Метод без этого атрибута получает <see cref="ToolAction.Unknown"/> и
/// <see cref="ToolIdempotency.Unknown"/> (действуют как «необратимо» и «повторять нельзя») и одно предупреждение
/// в журнал. Атрибут без <c>[AgentTool]</c> на том же методе — ошибка при построении набора.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToolEffectAttribute : Attribute
{
    /// <summary>Объявляет вид действия и «повтор безопасен».</summary>
    /// <param name="action">Вид действия инструмента.</param>
    /// <param name="idempotency">Безопасен ли повтор вызова с теми же аргументами.</param>
    public ToolEffectAttribute(ToolAction action, ToolIdempotency idempotency)
    {
        Action = action;
        Idempotency = idempotency;
    }

    /// <summary>Вид действия инструмента (<see cref="ToolSpec.Action"/>).</summary>
    public ToolAction Action { get; }

    /// <summary>Безопасен ли повтор (<see cref="ToolSpec.Idempotency"/>).</summary>
    public ToolIdempotency Idempotency { get; }
}
