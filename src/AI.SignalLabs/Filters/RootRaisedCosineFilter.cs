using AI.DataStructs.Algebraic;
using AI.DSP.DSPCore;
using AI.DSP.FIR;
using System;

namespace AI.SignalLab.Filters;

/// <summary>
/// Корневой фильтр приподнятого косинуса (Square Root Raised Cosine, SRRC).
///
/// Используется парами: один — на передатчике (формирование импульса),
/// второй — на приёмнике (согласованный фильтр).
/// Вместе они образуют полный фильтр приподнятого косинуса (RRC × RRC = RC),
/// обеспечивая нулевую МСИ в моменты выборки символов (критерий Найквиста).
///
/// Импульсная характеристика (по формуле из Прокиса/Скляра):
///
///   t = 0:          h(0) = (1 + β(4/π − 1)) / T
///   t = ±T/(4β):   h = (β/T√2) · [(1+2/π)sin(π/4β) + (1−2/π)cos(π/4β)]
///   иначе:         h(t) = (1/T) · [sin(πt/T·(1−β)) + 4βt/T·cos(πt/T·(1+β))]
///                         / [πt/T·(1 − (4βt/T)²)]
/// </summary>
public class RootRaisedCosineFilter : IFilter
{
    private readonly FIRFilter _fir;

    /// <summary>
    /// Длина фильтра в отсчётах (нечётное число — для симметричности)
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Коэффициент скатывания (roll-off factor), 0 &lt; β ≤ 1
    /// </summary>
    public double RollOff { get; }

    /// <summary>
    /// Длительность символа, с
    /// </summary>
    public double SymbolPeriod { get; }

    public string Name { get; set; } = "SRRC";

    /// <param name="symbolPeriod">Длительность символа T, с</param>
    /// <param name="sampleRate">Частота дискретизации, Гц</param>
    /// <param name="rollOff">Коэффициент скатывания β (0 &lt; β ≤ 1), по умолч. 0.35</param>
    /// <param name="spanSymbols">Длина фильтра в символах (по обе стороны от 0), по умолч. 8</param>
    public RootRaisedCosineFilter(double symbolPeriod, double sampleRate,
                                  double rollOff = 0.35, int spanSymbols = 8)
    {
        RollOff = rollOff;
        SymbolPeriod = symbolPeriod;

        Vector kernel = ComputeKernel(symbolPeriod, sampleRate, rollOff, spanSymbols);
        Length = kernel.Count;
        // Sectional — секционная свёртка через FFT, O(N·log N) вместо O(N²)
        _fir = new FIRFilter(kernel, (int)sampleRate, FIRCalcConvType.Sectional);
    }

    /// <summary>
    /// Фильтрация всего вектора (потоковый режим по одному отсчёту).
    /// Это гарантирует правильную задержку и отсутствие багов с обрезкой хвостов,
    /// которые есть в пакетной свёртке FIRFilter.
    /// </summary>
    public Vector FilterOutp(Vector signal)
    {
        Vector outp = new Vector(signal.Count);
        for (int i = 0; i < signal.Count; i++)
        {
            outp[i] = _fir.FilterOutp(signal[i]);
        }
        return outp;
    }

    /// <summary>
    /// Фильтрация одного отсчёта (потоковый режим).
    /// </summary>
    public double FilterOutp(double sample) => _fir.FilterOutp(sample);

    /// <summary>
    /// Вычисляет коэффициенты импульсной характеристики SRRC.
    /// </summary>
    public static Vector ComputeKernel(double T, double sr, double beta, int span)
    {
        int samplesPerSymbol = (int)Math.Round(T * sr);
        int half = span * samplesPerSymbol;
        int len = 2 * half + 1;
        Vector h = new Vector(len);

        double dt = 1.0 / sr;
        double eps = 1e-10;

        for (int i = 0; i < len; i++)
        {
            double t = (i - half) * dt;
            double tNorm = t / T; // нормированное время t/T

            double val;

            if (Math.Abs(t) < eps)
            {
                // t = 0
                val = (1.0 + beta * (4.0 / Math.PI - 1.0)) / T;
            }
            else if (Math.Abs(Math.Abs(tNorm) - 1.0 / (4.0 * beta)) < eps)
            {
                // t = ±T/(4β) — особая точка
                double s = Math.Sin(Math.PI / (4.0 * beta));
                double c = Math.Cos(Math.PI / (4.0 * beta));
                val = (beta / (T * Math.Sqrt(2.0))) *
                      ((1.0 + 2.0 / Math.PI) * s + (1.0 - 2.0 / Math.PI) * c);
            }
            else
            {
                double num = Math.Sin(Math.PI * tNorm * (1.0 - beta))
                           + 4.0 * beta * tNorm * Math.Cos(Math.PI * tNorm * (1.0 + beta));
                double den = Math.PI * tNorm * (1.0 - Math.Pow(4.0 * beta * tNorm, 2.0));
                val = num / (den * T);
            }

            h[i] = val;
        }

        // Нормировка — единичная энергия ядра
        double norm = 0;
        for (int i = 0; i < len; i++) norm += h[i] * h[i];
        norm = Math.Sqrt(norm);
        for (int i = 0; i < len; i++) h[i] /= norm;

        return h;
    }
}
