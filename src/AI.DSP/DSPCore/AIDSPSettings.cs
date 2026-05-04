using AI.DataStructs.WithComplexElements;
using System;
using System.Threading;

namespace AI.DSP.DSPCore;

/// <summary>
/// Глобальные настройки DSP-модуля
/// </summary>
[Serializable]
public static class AIDSPSettings
{
    private static Func<ComplexVector, bool, ComplexVector> _fftCore = FFT.BaseStaticFFT;

    /// <summary>
    /// Базовая функция БПФ. 
    /// Замена делегата потокобезопасна, но не гарантирует атомарности смены между чтением и записью из разных потоков.
    /// </summary>
    public static Func<ComplexVector, bool, ComplexVector> FFTCore
    {
        get => Volatile.Read(ref _fftCore);
        set => Volatile.Write(ref _fftCore, value ?? throw new ArgumentNullException(nameof(value)));
    }
}
