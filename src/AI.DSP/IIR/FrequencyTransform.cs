using System;
using System.Linq;
using System.Numerics;

namespace AI.DSP.IIR;

/// <summary>
/// Преобразования аналогового ФНЧ-прототипа (нули, полюсы, усиление) в ФНЧ, ФВЧ, ПФ, РФ с заданной частотой
/// и билинейное преобразование в цифровой фильтр (формулы lp2lp_zpk … bilinear_zpk из scipy.signal).
/// </summary>
public static class FrequencyTransform
{
    /// <summary>ФНЧ → ФНЧ с частотой среза ω₀: s → s/ω₀.</summary>
    public static ZeroPoleGain LowPassToLowPass(ZeroPoleGain prototype, double cutoff)
    {
        ArgumentNullException.ThrowIfNull(prototype);
        int degree = prototype.PoleArray.Length - prototype.ZeroArray.Length;
        return new ZeroPoleGain(prototype.ZeroArray.Select(z => z * cutoff).ToArray(), prototype.PoleArray.Select(p => p * cutoff).ToArray(),
            prototype.Gain * Math.Pow(cutoff, degree));
    }

    /// <summary>ФНЧ → ФВЧ с частотой среза ω₀: s → ω₀/s.</summary>
    public static ZeroPoleGain LowPassToHighPass(ZeroPoleGain prototype, double cutoff)
    {
        ArgumentNullException.ThrowIfNull(prototype);
        int degree = prototype.PoleArray.Length - prototype.ZeroArray.Length;
        Complex[] z = prototype.ZeroArray.Select(x => cutoff / x).Concat(Enumerable.Repeat(Complex.Zero, degree)).ToArray();
        Complex[] p = prototype.PoleArray.Select(x => cutoff / x).ToArray();
        double k = prototype.Gain * (Product(prototype.ZeroArray.Select(x => -x)) / Product(prototype.PoleArray.Select(x => -x))).Real;
        return new ZeroPoleGain(z, p, k);
    }

    /// <summary>ФНЧ → ПФ с центром ω₀ и шириной полосы Δω: s → (s² + ω₀²)/(Δω·s).</summary>
    public static ZeroPoleGain LowPassToBandPass(ZeroPoleGain prototype, double center, double bandwidth)
    {
        ArgumentNullException.ThrowIfNull(prototype);
        int degree = prototype.PoleArray.Length - prototype.ZeroArray.Length;
        Complex[] z = Split(prototype.ZeroArray.Select(x => x * bandwidth / 2), center).Concat(Enumerable.Repeat(Complex.Zero, degree)).ToArray();
        Complex[] p = Split(prototype.PoleArray.Select(x => x * bandwidth / 2), center).ToArray();
        return new ZeroPoleGain(z, p, prototype.Gain * Math.Pow(bandwidth, degree));
    }

    /// <summary>ФНЧ → РФ с центром ω₀ и шириной полосы Δω: s → Δω·s/(s² + ω₀²).</summary>
    public static ZeroPoleGain LowPassToBandStop(ZeroPoleGain prototype, double center, double bandwidth)
    {
        ArgumentNullException.ThrowIfNull(prototype);
        int degree = prototype.PoleArray.Length - prototype.ZeroArray.Length;
        Complex[] z = Split(prototype.ZeroArray.Select(x => bandwidth / 2 / x), center)
            .Concat(Enumerable.Repeat(new Complex(0, center), degree))
            .Concat(Enumerable.Repeat(new Complex(0, -center), degree)).ToArray();
        Complex[] p = Split(prototype.PoleArray.Select(x => bandwidth / 2 / x), center).ToArray();
        double k = prototype.Gain * (Product(prototype.ZeroArray.Select(x => -x)) / Product(prototype.PoleArray.Select(x => -x))).Real;
        return new ZeroPoleGain(z, p, k);
    }

    /// <summary>
    /// Билинейное преобразование s = 2F·(z − 1)/(z + 1); недостающие нули уходят в z = −1.
    /// </summary>
    /// <param name="analog">Аналоговый фильтр.</param>
    /// <param name="sampleRate">F — частота дискретизации преобразования.</param>
    public static ZeroPoleGain Bilinear(ZeroPoleGain analog, double sampleRate)
    {
        ArgumentNullException.ThrowIfNull(analog);
        int degree = analog.PoleArray.Length - analog.ZeroArray.Length;
        double fs2 = 2.0 * sampleRate;
        Complex[] z = analog.ZeroArray.Select(x => (fs2 + x) / (fs2 - x)).Concat(Enumerable.Repeat(new Complex(-1, 0), degree)).ToArray();
        Complex[] p = analog.PoleArray.Select(x => (fs2 + x) / (fs2 - x)).ToArray();
        double k = analog.Gain * (Product(analog.ZeroArray.Select(x => fs2 - x)) / Product(analog.PoleArray.Select(x => fs2 - x))).Real;
        return new ZeroPoleGain(z, p, k);
    }

    /// <summary>
    /// Предыскажение частоты для билинейного преобразования: ω_a = 2F·tan(πf/F_s) при F = 2 и нормированной f.
    /// </summary>
    /// <param name="frequency">Частота, Гц.</param>
    /// <param name="sampleRate">Частота дискретизации, Гц.</param>
    public static double Prewarp(double frequency, double sampleRate) => 4.0 * Math.Tan(Math.PI * (2 * frequency / sampleRate) / 2.0);

    private static Complex Product(System.Collections.Generic.IEnumerable<Complex> values) => values.Aggregate(Complex.One, (a, b) => a * b);

    private static System.Collections.Generic.IEnumerable<Complex> Split(System.Collections.Generic.IEnumerable<Complex> scaled, double center)
    {
        Complex[] s = scaled.ToArray();
        Complex[] roots = s.Select(x => Complex.Sqrt((x * x) - (center * center))).ToArray();
        return s.Select((x, i) => x + roots[i]).Concat(s.Select((x, i) => x - roots[i]));
    }
}
