namespace AI.Agents.Interop.ReAct;

/// <summary>
/// Машинные коды ошибок, которые адаптеры пишут в <c>ReActToolOutcome.ErrorCode</c> и <c>ToolResult.ErrorCode</c>.
/// </summary>
/// <remarks>
/// Старый движок ReAct знает только «удалось» и «не удалось» (<c>Ok</c>). Статусы контракта, которых у него нет,
/// приходят в старую форму как <c>Ok = false</c> с одним из этих кодов, чтобы потеря статуса была видна машине.
/// </remarks>
public static class ReActInteropCodes
{
    /// <summary>Вызов отклонен (<c>ToolStatus.Denied</c>).</summary>
    public const string Denied = "denied";

    /// <summary>Инструменту не хватает данных от человека (<c>ToolStatus.NeedsInput</c>).</summary>
    public const string NeedsInput = "needs_input";

    /// <summary>Инструмент ждет решения человека (<c>ToolStatus.AwaitsUser</c>).</summary>
    public const string AwaitsUser = "awaits_user";

    /// <summary>Статус неизвестен (<c>ToolStatus.Unknown</c>): исход вызова не установлен.</summary>
    public const string UnknownStatus = "unknown_status";

    /// <summary>Инструмента с таким именем в наборе нет; тот же код, что у движка ReAct.</summary>
    public const string UnknownTool = "unknown_tool";
}
