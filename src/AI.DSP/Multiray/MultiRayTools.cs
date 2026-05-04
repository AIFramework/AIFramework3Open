using AI.DataStructs.Algebraic;
using AI.DSP.Multiray.Sources;
using AI.HighLevelFunctions;
using System;
using System.Collections.Generic;

namespace AI.DSP.Multiray;

/// <summary>
/// Утилиты для многолучевой модели распространения сигналов
/// </summary>
public class MultiRayTools
{
    /// <summary>
    /// Суперпозиция сигналов от источников на приёмнике
    /// </summary>
    /// <param name="signals">Источники сигналов</param>
    /// <param name="collectorCoords">Объект-приёмник</param>
    /// <param name="speed">Скорость распространения волны</param>
    /// <returns>Суммарный сигнал на приёмнике</returns>
    public static Vector CollectSignals(IEnumerable<Source> signals, GeometrySignalObject collectorCoords, double speed)
    {
        if (signals == null) throw new ArgumentNullException(nameof(signals));
        if (speed <= 0) throw new ArgumentException("Wave speed must be positive", nameof(speed));

        Vector signal = null;

        foreach (var source in signals)
        {
            Vector s = GetSignalOnDetector(collectorCoords, source, speed);
            if (signal == null)
                signal = s;
            else
                signal += s;
        }

        return signal ?? new Vector(0);
    }

    private static Vector GetSignalOnDetector(GeometrySignalObject collectorCoords, Source source, double speed)
    {
        double d = GetDist(collectorCoords, source);
        return source.GetSignal(d, speed);
    }

    /// <summary>
    /// Расстояние между двумя объектами
    /// </summary>
    public static double GetDist(GeometrySignalObject go1, GeometrySignalObject go2) =>
        AnalyticGeometryFunctions.DistanceFromAToB(go1.Coordinates, go2.Coordinates);

    /// <summary>
    /// Разность времени прихода сигнала от точки привязки до двух объектов
    /// </summary>
    /// <param name="go1">Первый объект</param>
    /// <param name="go2">Второй объект</param>
    /// <param name="anchor">Точка привязки (источник)</param>
    /// <param name="v">Скорость распространения</param>
    /// <returns>Разность задержек t2 - t1</returns>
    public static double GetDeltaT(GeometrySignalObject go1, GeometrySignalObject go2, GeometrySignalObject anchor, double v)
    {
        if (v <= 0) throw new ArgumentException("Wave speed must be positive", nameof(v));
        double t1 = GetDist(go1, anchor) / v;
        double t2 = GetDist(go2, anchor) / v;
        return t2 - t1;
    }
}
