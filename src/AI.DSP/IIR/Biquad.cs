using System;

namespace AI.DSP.IIR;

/// <summary>
/// Биквадратные звенья по формулам Р. Бристоу-Джонсона (Audio EQ Cookbook) — тем же типом
/// <see cref="SecondOrderSections"/>, что и фильтры по ТЗ.
/// </summary>
public static class Biquad
{
    /// <summary>ФНЧ второго порядка с добротностью q.</summary>
    public static SecondOrderSections LowPass(double sampleRate, double frequency, double q = 0.7071067811865476)
    {
        (double cos, double alpha) = Prepare(sampleRate, frequency, q);
        return Make((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
    }

    /// <summary>ФВЧ второго порядка.</summary>
    public static SecondOrderSections HighPass(double sampleRate, double frequency, double q = 0.7071067811865476)
    {
        (double cos, double alpha) = Prepare(sampleRate, frequency, q);
        return Make((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
    }

    /// <summary>Полосовой с единичным усилением на центральной частоте.</summary>
    public static SecondOrderSections BandPass(double sampleRate, double frequency, double q)
    {
        (double cos, double alpha) = Prepare(sampleRate, frequency, q);
        return Make(alpha, 0, -alpha, 1 + alpha, -2 * cos, 1 - alpha);
    }

    /// <summary>Режекторный (нуль АЧХ на центральной частоте).</summary>
    public static SecondOrderSections Notch(double sampleRate, double frequency, double q)
    {
        (double cos, double alpha) = Prepare(sampleRate, frequency, q);
        return Make(1, -2 * cos, 1, 1 + alpha, -2 * cos, 1 - alpha);
    }

    /// <summary>Фазовый (всепропускающий).</summary>
    public static SecondOrderSections AllPass(double sampleRate, double frequency, double q)
    {
        (double cos, double alpha) = Prepare(sampleRate, frequency, q);
        return Make(1 - alpha, -2 * cos, 1 + alpha, 1 + alpha, -2 * cos, 1 - alpha);
    }

    /// <summary>Пиковый корректор: усиление gainDb на центральной частоте.</summary>
    public static SecondOrderSections Peaking(double sampleRate, double frequency, double q, double gainDb)
    {
        (double cos, double alpha) = Prepare(sampleRate, frequency, q);
        double a = Math.Pow(10, gainDb / 40);
        return Make(1 + (alpha * a), -2 * cos, 1 - (alpha * a), 1 + (alpha / a), -2 * cos, 1 - (alpha / a));
    }

    /// <summary>Полочный корректор нижних частот: усиление gainDb ниже частоты перегиба.</summary>
    public static SecondOrderSections LowShelf(double sampleRate, double frequency, double gainDb, double q = 0.7071067811865476)
    {
        (double cos, double alpha) = Prepare(sampleRate, frequency, q);
        double a = Math.Pow(10, gainDb / 40);
        double s = 2 * Math.Sqrt(a) * alpha;
        return Make(a * ((a + 1) - ((a - 1) * cos) + s), 2 * a * ((a - 1) - ((a + 1) * cos)), a * ((a + 1) - ((a - 1) * cos) - s),
            (a + 1) + ((a - 1) * cos) + s, -2 * ((a - 1) + ((a + 1) * cos)), (a + 1) + ((a - 1) * cos) - s);
    }

    /// <summary>Полочный корректор верхних частот: усиление gainDb выше частоты перегиба.</summary>
    public static SecondOrderSections HighShelf(double sampleRate, double frequency, double gainDb, double q = 0.7071067811865476)
    {
        (double cos, double alpha) = Prepare(sampleRate, frequency, q);
        double a = Math.Pow(10, gainDb / 40);
        double s = 2 * Math.Sqrt(a) * alpha;
        return Make(a * ((a + 1) + ((a - 1) * cos) + s), -2 * a * ((a - 1) + ((a + 1) * cos)), a * ((a + 1) + ((a - 1) * cos) - s),
            (a + 1) - ((a - 1) * cos) + s, 2 * ((a - 1) - ((a + 1) * cos)), (a + 1) - ((a - 1) * cos) - s);
    }

    private static (double Cos, double Alpha) Prepare(double sampleRate, double frequency, double q)
    {
        if (!(sampleRate > 0) || !(frequency > 0 && frequency < sampleRate / 2) || !(q > 0))
        {
            throw new ArgumentException($"Нужно 0 < f < Fs/2 и Q > 0; дано f = {frequency}, Fs = {sampleRate}, Q = {q}.");
        }

        double w0 = 2 * Math.PI * frequency / sampleRate;
        return (Math.Cos(w0), Math.Sin(w0) / (2 * q));
    }

    private static SecondOrderSections Make(double b0, double b1, double b2, double a0, double a1, double a2)
        => new(new double[,] { { b0 / a0, b1 / a0, b2 / a0, 1, a1 / a0, a2 / a0 } });
}
