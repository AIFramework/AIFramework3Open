using System;

namespace AI.DSP.Multirate;

/// <summary>
/// Число в формате с фиксированной точкой Q16 (16 дробных бит, 32-битное слово), диссертация, п. 2.4.
/// </summary>
/// <remarks>
/// Произведение считается в 64 битах. Умножение автора (2.21) — раздельное умножение целых и дробных частей —
/// в точной целочисленной арифметике совпадает с ⌊ab/2^16⌋, то есть с <see cref="Q16Rounding.Floor"/>;
/// в 32 битах оно переполняется (0,9 × 0,9 даёт −0,19), поэтому здесь не воспроизводится.
/// </remarks>
public readonly struct Q16 : IEquatable<Q16>
{
    /// <summary>Число дробных бит.</summary>
    public const int FractionBits = 16;

    /// <summary>Единица в формате Q16 (2^16).</summary>
    public const int One = 1 << FractionBits;

    /// <summary>
    /// Создаёт число по его целочисленному представлению.
    /// </summary>
    /// <param name="raw">Целое, равное 2^16·v.</param>
    public Q16(int raw) => Raw = raw;

    /// <summary>Целочисленное представление (2^16·v).</summary>
    public int Raw { get; }

    /// <summary>Вещественное значение.</summary>
    public double ToDouble() => Raw / (double)One;

    /// <summary>
    /// Переводит вещественное число в Q16.
    /// </summary>
    /// <param name="value">Значение.</param>
    /// <param name="rounding">Округление: к нулю — как ToInt автора, к ближайшему — как q16 синтезатора.</param>
    /// <param name="saturate">Насыщение при выходе за диапазон; иначе исключение.</param>
    public static Q16 FromDouble(double value, Q16Rounding rounding = Q16Rounding.Nearest, bool saturate = true)
        => new(Narrow(ConvertRaw(value, rounding), saturate));

    /// <summary>
    /// Сложение; при переполнении — насыщение или перенос по модулю 2^32.
    /// </summary>
    public static Q16 Add(Q16 a, Q16 b, bool saturate = false) => new(Narrow((long)a.Raw + b.Raw, saturate, wrap: true));

    /// <summary>
    /// Вычитание; при переполнении — насыщение или перенос по модулю 2^32.
    /// </summary>
    public static Q16 Subtract(Q16 a, Q16 b, bool saturate = false) => new(Narrow((long)a.Raw - b.Raw, saturate, wrap: true));

    /// <summary>
    /// Умножение через 64-битное произведение.
    /// </summary>
    /// <param name="a">Первый сомножитель.</param>
    /// <param name="b">Второй сомножитель.</param>
    /// <param name="rounding">Вниз — как Mul автора (2.21), к ближайшему — как MulRound (правило 3, п. 2.5.4).</param>
    /// <param name="saturate">Насыщение при выходе за диапазон; иначе перенос по модулю 2^32.</param>
    public static Q16 Multiply(Q16 a, Q16 b, Q16Rounding rounding = Q16Rounding.Nearest, bool saturate = false)
        => new(Narrow(MultiplyRaw(a.Raw, b.Raw, rounding), saturate, wrap: true));

    /// <summary>Сложение с переносом по модулю 2^32.</summary>
    public static Q16 operator +(Q16 a, Q16 b) => Add(a, b);

    /// <summary>Вычитание с переносом по модулю 2^32.</summary>
    public static Q16 operator -(Q16 a, Q16 b) => Subtract(a, b);

    /// <summary>Умножение с округлением к ближайшему.</summary>
    public static Q16 operator *(Q16 a, Q16 b) => Multiply(a, b);

    /// <summary>Равенство.</summary>
    public static bool operator ==(Q16 a, Q16 b) => a.Raw == b.Raw;

    /// <summary>Неравенство.</summary>
    public static bool operator !=(Q16 a, Q16 b) => a.Raw != b.Raw;

    /// <inheritdoc/>
    public bool Equals(Q16 other) => Raw == other.Raw;

    /// <inheritdoc/>
    public override bool Equals(object obj) => obj is Q16 q && Equals(q);

    /// <inheritdoc/>
    public override int GetHashCode() => Raw;

    /// <inheritdoc/>
    public override string ToString() => ToDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Произведение целочисленных представлений Q16 в 64 битах со сдвигом на 16 разрядов.
    /// </summary>
    internal static long MultiplyRaw(long a, long b, Q16Rounding rounding) => Shift(a * b, rounding);

    /// <summary>
    /// Сдвиг накопителя с 32 дробными битами к 16 дробным битам с заданным округлением.
    /// </summary>
    internal static long Shift(long product, Q16Rounding rounding) => rounding switch
    {
        Q16Rounding.Floor => product >> FractionBits,
        Q16Rounding.Nearest => (product + (One >> 1)) >> FractionBits,
        _ => product / One,
    };

    /// <summary>
    /// Перевод вещественного в целочисленное представление без ограничения разрядности.
    /// </summary>
    internal static long ConvertRaw(double value, Q16Rounding rounding)
    {
        double scaled = value * One;
        return (long)(rounding switch
        {
            Q16Rounding.Floor => Math.Floor(scaled),
            Q16Rounding.Nearest => Math.Round(scaled, MidpointRounding.AwayFromZero),
            _ => Math.Truncate(scaled),
        });
    }

    /// <summary>
    /// Деление с округлением вниз (как // в Python); в C# деление целых округляет к нулю.
    /// </summary>
    internal static long FloorDivide(long a, long b)
    {
        long q = a / b;
        return (a % b != 0) && ((a < 0) != (b < 0)) ? q - 1 : q;
    }

    /// <summary>
    /// Ограничение 64-битного значения диапазоном int32.
    /// </summary>
    internal static long Saturate(long value) => Math.Clamp(value, int.MinValue, int.MaxValue);

    private static int Narrow(long value, bool saturate, bool wrap = false)
    {
        if (saturate)
        {
            return (int)Saturate(value);
        }

        if (wrap)
        {
            return unchecked((int)value);
        }

        return value is < int.MinValue or > int.MaxValue
            ? throw new OverflowException($"Значение {value / (double)One} вне диапазона Q16.")
            : (int)value;
    }
}
