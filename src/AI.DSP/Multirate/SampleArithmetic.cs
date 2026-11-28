using System;
using System.Numerics;

namespace AI.DSP.Multirate;

/// <summary>
/// Операции над отсчётами, общие для double и Q16: блоки пишутся один раз и работают в обеих арифметиках.
/// </summary>
/// <typeparam name="T">double или long (целочисленное представление Q16).</typeparam>
internal interface ISampleArithmetic<T>
    where T : struct
{
    /// <summary>Перевод входного отсчёта.</summary>
    T FromInput(double value);

    /// <summary>Перевод коэффициента фильтра.</summary>
    T Coefficient(double value);

    /// <summary>Выходной отсчёт (с насыщением, если оно включено).</summary>
    double ToOutput(T value);

    /// <summary>a + b.</summary>
    T Add(T a, T b);

    /// <summary>a − b.</summary>
    T Subtract(T a, T b);

    /// <summary>|a|.</summary>
    T Abs(T a);

    /// <summary>Накопление произведения отсчёта на коэффициент.</summary>
    T Accumulate(T acc, T x, T c);

    /// <summary>Завершение накопления (округление широкого накопителя).</summary>
    T Finish(T acc);

    /// <summary>Произведение с округлением (схема Горнера).</summary>
    T Multiply(T a, T b);

    /// <summary>Произведение на целое число.</summary>
    T Scale(T a, int factor);

    /// <summary>(w1·a + w2·b) / (w1 + w2); при w1 + w2 = 0 — null.</summary>
    T? WeightedMean(T w1, T a, T w2, T b);

    /// <summary>Половина значения.</summary>
    T Half(T a);

    /// <summary>Нормировка накопителя ИГФ на коэффициент передачи.</summary>
    T Normalize(T acc, BigInteger gain);

    /// <summary>Дробная часть положения из 32-битной дробной части фазового накопителя.</summary>
    T Fraction(ulong fraction32);
}

/// <summary>
/// Вещественная арифметика.
/// </summary>
internal sealed class DoubleArithmetic : ISampleArithmetic<double>
{
    public static DoubleArithmetic Instance { get; } = new();

    public double FromInput(double value) => value;

    public double Coefficient(double value) => value;

    public double ToOutput(double value) => value;

    public double Add(double a, double b) => a + b;

    public double Subtract(double a, double b) => a - b;

    public double Abs(double a) => Math.Abs(a);

    public double Accumulate(double acc, double x, double c) => acc + (x * c);

    public double Finish(double acc) => acc;

    public double Multiply(double a, double b) => a * b;

    public double Scale(double a, int factor) => a * factor;

    public double? WeightedMean(double w1, double a, double w2, double b)
    {
        double w = w1 + w2;
        return w == 0 ? null : ((w1 * a) + (w2 * b)) / w;
    }

    public double Half(double a) => 0.5 * a;

    public double Normalize(double acc, BigInteger gain) => gain.IsOne ? acc : acc / (double)gain;

    public double Fraction(ulong fraction32) => fraction32 / 4294967296.0;
}

/// <summary>
/// Арифметика Q16: целочисленное представление в long, 64-битные накопители с переносом по модулю 2^64.
/// </summary>
internal sealed class Q16Arithmetic : ISampleArithmetic<long>
{
    private readonly Q16Options _options;

    public Q16Arithmetic(Q16Options options) => _options = options;

    public long FromInput(double value) => Q16.ConvertRaw(value, _options.Convert);

    public long Coefficient(double value) => Q16.ConvertRaw(value, _options.Convert);

    public double ToOutput(long value) => (_options.Saturate ? Q16.Saturate(value) : value) / (double)Q16.One;

    public long Add(long a, long b) => unchecked(a + b);

    public long Subtract(long a, long b) => unchecked(a - b);

    public long Abs(long a) => Math.Abs(a);

    public long Accumulate(long acc, long x, long c)
        => unchecked(_options.WideAccumulator ? acc + (x * c) : acc + Q16.MultiplyRaw(x, c, _options.Multiply));

    public long Finish(long acc) => _options.WideAccumulator ? Q16.Shift(acc, _options.Multiply) : acc;

    public long Multiply(long a, long b) => Q16.MultiplyRaw(a, b, _options.Multiply);

    public long Scale(long a, int factor) => unchecked(a * factor);

    public long? WeightedMean(long w1, long a, long w2, long b)
    {
        long w = w1 + w2;
        if (w == 0)
        {
            return null;
        }

        long num = (w1 * a) + (w2 * b);
        return _options.Multiply switch
        {
            Q16Rounding.Floor => Q16.FloorDivide(num, w),
            Q16Rounding.Nearest => Q16.FloorDivide((2 * num) + w, 2 * w),
            _ => num / w,
        };
    }

    public long Half(long a) => _options.Multiply == Q16Rounding.Nearest ? (a + 1) >> 1 : a >> 1;

    public long Normalize(long acc, BigInteger gain)
    {
        if (gain.IsOne)
        {
            return acc;
        }

        return gain.IsPowerOfTwo ? acc >> (int)(gain.GetBitLength() - 1) : Q16.FloorDivide(acc, (long)gain);
    }

    public long Fraction(ulong fraction32) => (long)(fraction32 >> 16);
}
