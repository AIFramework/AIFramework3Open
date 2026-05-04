using AI.DataStructs.Algebraic;
using AI.DSP.Multiray.Sources;
using System.Collections.Generic;

namespace AI.DSP.Multiray;

/// <summary>
/// Среда распространения сигналов с источниками и детекторами
/// </summary>
public class SignalEnvironment
{
    /// <summary>
    /// Список источников сигналов
    /// </summary>
    public List<Source> Sources { get; set; } = new List<Source>();

    /// <summary>
    /// Список детекторов (приёмников)
    /// </summary>
    public List<Detector> Detectors { get; set; } = new List<Detector>();

    /// <summary>
    /// Частота дискретизации (Гц)
    /// </summary>
    public double SR { get; set; } = 10000;

    /// <summary>
    /// Скорость распространения волны (м/с)
    /// </summary>
    public double WaveSpeed { get; set; } = 300;

    /// <summary>
    /// Вычисление сигналов на всех детекторах
    /// </summary>
    /// <returns>Список сигналов для каждого детектора</returns>
    public List<Vector> GetSignals()
    {
        var signals = new List<Vector>(Detectors.Count);

        foreach (var detector in Detectors)
            signals.Add(detector.GetSignal(Sources, WaveSpeed));

        return signals;
    }
}
