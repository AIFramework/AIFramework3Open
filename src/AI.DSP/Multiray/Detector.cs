using AI.DataStructs.Algebraic;
using AI.DSP.Multiray.Sources;
using System.Collections.Generic;

namespace AI.DSP.Multiray;

/// <summary>
/// Детектор (приёмник) сигналов
/// </summary>
public class Detector : GeometrySignalObject
{
    /// <summary>
    /// Конструктор по умолчанию
    /// </summary>
    public Detector() : base() { }

    /// <summary>
    /// Конструктор с координатами
    /// </summary>
    public Detector(params double[] data) : base(data) { }

    /// <summary>
    /// Получение суммарного сигнала от набора источников
    /// </summary>
    /// <param name="signals">Источники сигналов</param>
    /// <param name="waveSpeed">Скорость распространения волны</param>
    /// <returns>Суммарный сигнал</returns>
    public Vector GetSignal(IEnumerable<Source> signals, double waveSpeed)
        => MultiRayTools.CollectSignals(signals, this, waveSpeed);
}
