namespace AI.DSP.Multirate;

/// <summary>
/// Округление при переводе в Q16 и при умножении.
/// </summary>
public enum Q16Rounding
{
    /// <summary>К нулю (усечение): ToInt автора.</summary>
    TowardZero,

    /// <summary>Вниз (отбрасывание младших разрядов в дополнительном коде): Mul автора (2.21).</summary>
    Floor,

    /// <summary>К ближайшему: MulRound, правило 3 п. 2.5.4.</summary>
    Nearest,
}
