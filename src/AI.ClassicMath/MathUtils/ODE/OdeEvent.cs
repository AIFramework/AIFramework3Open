#nullable enable

using AI.DataStructs.Algebraic;
using System;

namespace AI.MathUtils.ODE;

/// <summary>
/// Событие при интегрировании: момент смены знака функции g(t, y).
/// </summary>
public sealed class OdeEvent
{
    /// <summary>
    /// Создает событие.
    /// </summary>
    /// <param name="function">Функция события g(t, y); событие наступает при смене ее знака.</param>
    /// <param name="isTerminal">Останавливать ли интегрирование в момент события.</param>
    /// <param name="direction">
    /// Направление пересечения: 1 - только рост через ноль, -1 - только спад, 0 - любое.
    /// </param>
    public OdeEvent(Func<double, Vector, double> function, bool isTerminal = false, int direction = 0)
    {
        Function = function ?? throw new ArgumentNullException(nameof(function));
        IsTerminal = isTerminal;
        Direction = Math.Sign(direction);
    }

    /// <summary>
    /// Функция события g(t, y).
    /// </summary>
    public Func<double, Vector, double> Function { get; }

    /// <summary>
    /// Останавливать ли интегрирование в момент события.
    /// </summary>
    public bool IsTerminal { get; }

    /// <summary>
    /// Направление пересечения: 1 - рост, -1 - спад, 0 - любое.
    /// </summary>
    public int Direction { get; }
}
