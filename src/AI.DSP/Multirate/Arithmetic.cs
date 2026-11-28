namespace AI.DSP.Multirate;

/// <summary>
/// Вид арифметики блока передискретизации.
/// </summary>
public enum Arithmetic
{
    /// <summary>Вещественная, double.</summary>
    Double,

    /// <summary>Фиксированная точка Q16 с 64-битными накопителями.</summary>
    Q16,
}
