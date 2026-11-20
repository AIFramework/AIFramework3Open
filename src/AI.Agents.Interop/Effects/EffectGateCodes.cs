namespace AI.Agents.Interop.Effects;

/// <summary>
/// Машинные коды, которые пишет гейт эффектов (<see cref="EffectGate"/>) в <c>ToolResult.ErrorCode</c>.
/// </summary>
public static class EffectGateCodes
{
    /// <summary>
    /// Повтор не исполнен: вызов с этим ключом уже начинался и не кончился успехом, а инструмент не объявлен
    /// идемпотентным. Повторить может только человек или хост, зная, что первое действие не состоялось.
    /// </summary>
    public const string RepeatUnsafe = "repeat_unsafe";

    /// <summary>Поток инструмента кончился без итога; исход неизвестен (контракт <c>ToolUpdate</c>).</summary>
    public const string NoResult = "no_result";

    /// <summary>Вызов прерван отменой или остановкой чтения потока до итога; исход неизвестен.</summary>
    public const string Interrupted = "interrupted";

    /// <summary>Инструмент бросил исключение; исход неизвестен.</summary>
    public const string Exception = "exception";
}
