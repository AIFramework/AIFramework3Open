namespace AI.Agents.Interop.Engines;

/// <summary>
/// Машинные коды остановки (<c>StopCode</c>), которые адаптеры движков пишут сверх известных кодов контракта.
/// </summary>
public static class EngineInteropCodes
{
    /// <summary>
    /// Продолжение с места не поддерживается: на входе <c>EngineInput.State</c> или ответ человека
    /// (<c>EngineTask.Answer</c>), а старые движки состояния не принимают (до шага Ф6).
    /// </summary>
    public const string ContinuationNotSupported = "continuation_not_supported";

    /// <summary>Параметры движка (<c>EngineInput.Options</c>) не разобраны: неизвестный ключ или значение.</summary>
    public const string InvalidOptions = "invalid_options";

    /// <summary>
    /// Порт модели не настроен под запуск: роль, которую требуют параметры движка, в порте не настроена. Код из общего
    /// словаря (<see cref="AI.Agents.Contracts.Tools.ToolCodes.NotConfigured"/>): параметры разобраны верно, не хватает
    /// настройки окружения, и ни одного вызова модели не было.
    /// </summary>
    public const string NotConfigured = AI.Agents.Contracts.Tools.ToolCodes.NotConfigured;

    /// <summary>План не утвержден (<c>PlanningAgentResult.Rejected</c>).</summary>
    public const string PlanRejected = "plan_rejected";
}
