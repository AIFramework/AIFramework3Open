using AI.DataStructs.Algebraic;
using AI.DSP.DSPCore;
using AI.HighLevelFunctions;
using System;

namespace AI.DSP.FIR;

/// <summary>
/// Фильтр с конечной импульсной характеристикой
/// </summary>
[Serializable]
public class FIRFilter : IFilter
{
    private readonly Vector _ht;
    private readonly int transientsInd;
    private readonly FIRCalcConvType convType;
    private readonly int _fd;
    private readonly Vector signalInp;
    /// <summary>
    /// Имя фильтра
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Инициализация фильтра
    /// </summary>
    /// <param name="ht">Импульсная характеристика фильтра</param>
    /// <param name="fd">Частота дискретизации</param>
    /// <param name="calcConvType">Метод расчета свертки</param>
    public FIRFilter(Vector ht, int fd, FIRCalcConvType calcConvType = FIRCalcConvType.WithFFT)
    {
        if (ht == null || ht.Count == 0) throw new ArgumentException("Impulse response must not be empty", nameof(ht));
        if (fd <= 0) throw new ArgumentException("Sample rate must be positive", nameof(fd));

        _ht = ht;
        convType = calcConvType;
        transientsInd = _ht.Count / 2;
        _fd = fd;

        signalInp = new Vector(_ht.Count);
    }

    /// <summary>
    /// Расчет отклика фильтра на сигнал
    /// </summary>
    /// <param name="input">Входной сигнал</param>
    /// <returns>Фильтрованный сигнал той же длины</returns>
    public Vector FilterOutp(Vector input)
    {
        if (input == null || input.Count == 0)
            throw new ArgumentException("Input signal must not be empty", nameof(input));

        Vector outp = convType switch
        {
            FIRCalcConvType.Simple => Convolution.DirectConvolution(input, _ht, _fd),
            FIRCalcConvType.WithFFT => FastConv.FastConvolution(input, _ht, _fd),
            FIRCalcConvType.Sectional => FastConv.SectionalConvolution(_ht, input) / _fd,
            FIRCalcConvType.Sectional4 => FastConv.SectionalConvolution(_ht, input, 4) / _fd,
            _ => throw new InvalidOperationException("Unknown convolution type"),
        };

        return outp.GetInterval(transientsInd, input.Count + transientsInd);
    }

    /// <summary>
    /// Фильтрация сигнала по одному отсчету
    /// </summary>
    /// <param name="signal">Входной отсчёт</param>
    /// <returns>Выходной отсчёт</returns>
    public double FilterOutp(double signal)
    {
        signalInp.AddCBE(signal);
        return AnalyticGeometryFunctions.Dot(signalInp, _ht);
    }
}

/// <summary>
/// Метод расчета свертки
/// </summary>
public enum FIRCalcConvType
{
    /// <summary>
    /// Простая свертка
    /// </summary>
    Simple,
    /// <summary>
    /// Быстрая с использованием БПФ
    /// </summary>
    WithFFT,
    /// <summary>
    /// Секционная свертка
    /// </summary>
    Sectional,
    /// <summary>
    /// Секционная 4-поточная свертка
    /// </summary>
    Sectional4
}
