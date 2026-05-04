using AI.BackEnds.DSP.NWaves.Filters.Butterworth;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.DSP.DSPCore;
using AI.DSP.IIR;
using AI.SignalLab.Filters;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.SignalLab.Modulation.Demodulation;

/// <summary>
/// Квадратурный демодулятор (IQ-разложение).
///
/// Схема приёмника:
///   вход -> ×cos(2πf₀t) -> [SRRC или ФНЧ Баттерворта] -> I
///   вход -> ×sin(2πf₀t) -> [SRRC или ФНЧ Баттерворта] -> Q
///
/// При использовании SRRC фильтр является согласованным с передатчиком,
/// обеспечивая нулевую МСИ в моменты выборки символов.
/// </summary>
public class QudratureDemodulation
{
    IFilter _filterI, _filterQ;
    double _sr, _f0;
    double _dt;

    readonly bool _useSrrc;
    readonly int _samplesPerSymbol;
    readonly int _filterDelay; // задержка фильтра в отсчётах (половина длины ядра)

    /// <summary>
    /// Квадратурный демодулятор с фильтром Баттерворта.
    /// </summary>
    /// <param name="f0">Несущая частота, Гц</param>
    /// <param name="sr">Частота дискретизации, Гц</param>
    /// <param name="cutoffFrequency">
    /// Частота среза ФНЧ после смесителя, Гц (~половина скорости символов).
    /// По умолчанию f0/4.
    /// </param>
    public QudratureDemodulation(double f0, double sr, double cutoffFrequency = 0)
    {
        _f0 = f0;
        _sr = sr;
        _dt = 1.0 / sr;
        _useSrrc = false;

        double fCut = cutoffFrequency > 0 ? cutoffFrequency : f0 / 4.0;
        double fCNorm = fCut / sr;

        var filter = new LowPassFilter(fCNorm, 7);
        _filterI = new IIRFilter(filter._a, filter._b);
        _filterQ = new IIRFilter(filter._a, filter._b);
    }

    /// <summary>
    /// Квадратурный демодулятор с согласованным SRRC-фильтром.
    /// </summary>
    /// <param name="f0">Несущая частота, Гц</param>
    /// <param name="sr">Частота дискретизации, Гц</param>
    /// <param name="symbolPeriod">Длительность символа T, с</param>
    /// <param name="rollOff">Коэффициент скатывания β (0 &lt; β ≤ 1)</param>
    /// <param name="spanSymbols">Длина фильтра в символах (рекомендуется 4–6)</param>
    public QudratureDemodulation(double f0, double sr, double symbolPeriod,
                                 double rollOff = 0.35, int spanSymbols = 4)
    {
        _f0 = f0;
        _sr = sr;
        _dt = 1.0 / sr;
        _useSrrc = true;
        _samplesPerSymbol = (int)Math.Round(symbolPeriod * sr);

        var srrcI = new RootRaisedCosineFilter(symbolPeriod, sr, rollOff, spanSymbols);
        var srrcQ = new RootRaisedCosineFilter(symbolPeriod, sr, rollOff, spanSymbols);
        _filterI = srrcI;
        _filterQ = srrcQ;
        _filterDelay = srrcI.Length / 2;
    }

    /// <summary>
    /// Получение синфазной и квадратурной компонент (полный вектор, по отсчёту на сэмпл).
    /// </summary>
    public ComplexVector GetIQComponents(Vector signal)
    {
        int len = signal.Count;
        ComplexVector result = new ComplexVector(len);

        for (int i = 0; i < len; i++)
        {
            double arg = 2 * Math.PI * _f0 * i * _dt;
            result[i] = new Complex(
                _filterI.FilterOutp(Math.Cos(arg) * signal[i]),
                _filterQ.FilterOutp(Math.Sin(arg) * signal[i])
            );
        }

        return result;
    }

    public int FilterDelay => _filterDelay;

    /// <summary>
    /// За один проход возвращает полный IQ-вектор и отдельно точки созвездия
    /// (по одной на символ, выборка в центре символа с учётом задержки фильтра).
    /// Используйте вместо раздельных вызовов GetIQComponents + GetIQSymbols.
    /// Требует инициализации через конструктор с SRRC.
    /// </summary>
    public (ComplexVector Full, ComplexVector Symbols) GetIQBoth(Vector signal, int externalDelay = 0)
    {
        if (!_useSrrc)
            throw new InvalidOperationException(
                "GetIQBoth требует SRRC. Используйте конструктор с параметром symbolPeriod.");

        int len = signal.Count;
        ComplexVector full = new ComplexVector(len);
        var symbols = new List<Complex>();

        int totalDelay = _filterDelay + externalDelay;

        for (int i = 0; i < len; i++)
        {
            double arg = 2 * Math.PI * _f0 * i * _dt;
            // Умножаем на 2.0 для компенсации потери мощности (1/2) при умножении на косинус
            double iVal = _filterI.FilterOutp(2.0 * Math.Cos(arg) * signal[i]);
            double qVal = _filterQ.FilterOutp(2.0 * Math.Sin(arg) * signal[i]);
            full[i] = new Complex(iVal, qVal);

            // Выборка в центре символа с компенсацией общей задержки (передатчик + приёмник)
            if (i >= totalDelay && (i - totalDelay) % _samplesPerSymbol == 0)
                symbols.Add(new Complex(iVal, qVal));
        }

        return (full, new ComplexVector(symbols.ToArray()));
    }

    /// <summary>
    /// Получение IQ только в центрах символов (чистое созвездие без переходов).
    /// Требует инициализации через конструктор с SRRC.
    /// </summary>
    public ComplexVector GetIQSymbols(Vector signal)
    {
        return GetIQBoth(signal).Symbols;
    }
}
