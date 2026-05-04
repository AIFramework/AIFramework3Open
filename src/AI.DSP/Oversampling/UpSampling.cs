using AI.DataStructs.Algebraic;
using AI.DSP.DSPCore;
using System;

namespace AI.DSP.Oversampling;

/// <summary>
/// Увеличение частоты дискретизации (upsampling)
/// </summary>
public static class UpSampling
{
    /// <summary>
    /// Увеличение частоты дискретизации с прямоугольным ФНЧ (zero-stuff + rectangular LPF)
    /// </summary>
    /// <param name="signal">Сигнал</param>
    /// <param name="fd">Частота дискретизации</param>
    /// <param name="factor">Во сколько раз увеличить</param>
    /// <returns>Сигнал с увеличенной частотой дискретизации</returns>
    public static Vector UpSamplingRectFilter(Vector signal, int fd, int factor)
    {
        if (factor <= 0) throw new ArgumentException("Factor must be positive", nameof(factor));

        int newFd = fd * factor;
        Vector newSignal = signal.UnPooling(factor);
        int fFilter = newFd / (2 * factor);
        return Filters.FilterLow(newSignal, fFilter, newFd) * factor;
    }

    /// <summary>
    /// Увеличение частоты дискретизации с ФНЧ Баттерворта (zero-stuff + Butterworth LPF)
    /// </summary>
    /// <param name="signal">Сигнал</param>
    /// <param name="fd">Частота дискретизации</param>
    /// <param name="factor">Во сколько раз увеличить</param>
    /// <param name="order">Порядок фильтра</param>
    /// <returns>Сигнал с увеличенной частотой дискретизации</returns>
    public static Vector UpSamplingButterworthFilter(Vector signal, int fd, int factor, int order = 7)
    {
        if (factor <= 0) throw new ArgumentException("Factor must be positive", nameof(factor));

        int newFd = fd * factor;
        Vector newSignal = signal.UnPooling(factor);
        int fFilter = newFd / (2 * factor);
        return Filters.FilterLowButterworthCFH(newSignal, fFilter, newFd, order) * factor;
    }

    /// <summary>
    /// Увеличение частоты дискретизации с ФНЧ Баттерворта и оконной коррекцией (zero-stuff + windowed Butterworth LPF)
    /// </summary>
    /// <param name="signal">Сигнал</param>
    /// <param name="fd">Частота дискретизации</param>
    /// <param name="factor">Во сколько раз увеличить</param>
    /// <param name="order">Порядок фильтра</param>
    /// <returns>Сигнал с увеличенной частотой дискретизации</returns>
    public static Vector UpSamplingButterworthFilterW(Vector signal, int fd, int factor, int order = 7)
    {
        if (factor <= 0) throw new ArgumentException("Factor must be positive", nameof(factor));

        int newFd = fd * factor;
        Vector newSignal = signal.UnPooling(factor);
        Vector blac = WindowForFFT.HannWindow(newSignal.Count) + 1e-2;
        newSignal *= blac;
        int fFilter = newFd / (2 * factor);
        newSignal = Filters.FilterLowButterworthCFH(newSignal, fFilter, newFd, order) * factor;
        return newSignal / blac;
    }

    /// <summary>
    /// Увеличение частоты дискретизации квадратичными сплайнами (полиномиальная интерполяция, без ФНЧ)
    /// </summary>
    /// <param name="signal">Исходный сигнал</param>
    /// <param name="factor">Во сколько раз увеличить</param>
    /// <returns>Сигнал с увеличенной частотой дискретизации</returns>
    public static Vector UpSamplingQuadratic(Vector signal, int factor)
    {
        if (factor <= 0) throw new ArgumentException("Factor must be positive", nameof(factor));
        if (signal == null || signal.Count < 2) throw new ArgumentException("Signal must have at least 2 samples", nameof(signal));

        int origCount = signal.Count;
        int newCount = (origCount - 1) * factor + 1;
        Vector newSignal = new Vector(newCount);

        for (int n = 0; n < origCount - 1; n++)
        {
            double prev = (n == 0) ? signal[0] : signal[n - 1];
            double cur = signal[n];
            double next = signal[n + 1];

            for (int i = 0; i < factor; i++)
            {
                double a = i / (double)factor;
                double interp = cur
                    + 0.5 * (next - prev) * a
                    + 0.5 * (next + prev - 2 * cur) * a * a;
                newSignal[n * factor + i] = interp;
            }
        }

        newSignal[newCount - 1] = signal[origCount - 1];

        return newSignal;
    }
}
