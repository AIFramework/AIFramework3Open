#nullable enable

using AI.DataStructs.Algebraic;

namespace AI.MathUtils.ODE;

/// <summary>
/// Наступившее событие: номер события, момент и состояние в этот момент.
/// </summary>
public sealed class OdeEventHit
{
    /// <summary>
    /// Создает запись о событии.
    /// </summary>
    /// <param name="eventIndex">Номер события в переданном списке.</param>
    /// <param name="time">Момент события.</param>
    /// <param name="state">Состояние в момент события.</param>
    public OdeEventHit(int eventIndex, double time, Vector state)
    {
        EventIndex = eventIndex;
        Time = time;
        State = state;
    }

    /// <summary>
    /// Номер события в переданном списке.
    /// </summary>
    public int EventIndex { get; }

    /// <summary>
    /// Момент события.
    /// </summary>
    public double Time { get; }

    /// <summary>
    /// Состояние в момент события (по плотной выдаче интегратора).
    /// </summary>
    public Vector State { get; }
}
