namespace AI.DSP.Multirate;

/// <summary>
/// Правила целочисленных вычислений Q16 в блоках передискретизации.
/// </summary>
/// <param name="Convert">Округление при переводе отсчётов и коэффициентов в Q16.</param>
/// <param name="Multiply">Округление произведений.</param>
/// <param name="WideAccumulator">
/// true — произведения КИХ-фильтра копятся в 64 битах и округляются один раз (resampler_mpy, resampler.c);
/// false — каждое произведение округляется отдельно (Mul автора, эксперименты главы 3).
/// </param>
/// <param name="Saturate">Насыщение выходных отсчётов блоков диапазоном int32.</param>
public sealed record Q16Options(Q16Rounding Convert, Q16Rounding Multiply, bool WideAccumulator, bool Saturate)
{
    /// <summary>
    /// Как в экспериментах главы 3 (таблица 3.4): усечение при переводе, умножение вниз, каждое произведение отдельно.
    /// </summary>
    public static Q16Options Author { get; } = new(Q16Rounding.TowardZero, Q16Rounding.Floor, false, false);

    /// <summary>
    /// Рекомендуемые (правило 3, п. 2.5.4) и как в синтезаторе: округление к ближайшему, 64-битный накопитель.
    /// </summary>
    public static Q16Options Recommended { get; } = new(Q16Rounding.Nearest, Q16Rounding.Nearest, true, false);
}
