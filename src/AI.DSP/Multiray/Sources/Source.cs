using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.DSP.Multiray.Sources;

/// <summary>
/// Абстрактный источник сигнала с пространственными координатами
/// </summary>
public abstract class Source : GeometrySignalObject
{
    /// <summary>
    /// Длительность сигнала в секундах
    /// </summary>
    public double T { get; set; } = 1;

    /// <summary>
    /// Частота дискретизации
    /// </summary>
    public double SR { get; set; }

    /// <summary>
    /// Конструктор по координатам
    /// </summary>
    public Source(params double[] coords) : base(coords)
    {
    }

    /// <summary>
    /// Конструктор с частотой дискретизации
    /// </summary>
    /// <param name="sr">Частота дискретизации</param>
    public Source(double sr = 1000) : base()
    {
        SR = sr;
    }

    /// <summary>
    /// Конструктор с частотой дискретизации и координатами
    /// </summary>
    /// <param name="sr">Частота дискретизации</param>
    /// <param name="coords">Координаты</param>
    public Source(double sr, params double[] coords) : base(coords)
    {
        SR = sr;
    }

    /// <summary>
    /// Генерирует сигнал с учётом расстояния и скорости распространения
    /// </summary>
    /// <param name="dist">Расстояние до приёмника</param>
    /// <param name="speed">Скорость распространения волны</param>
    /// <param name="signals">Другие источники (для отражателей)</param>
    /// <returns>Вектор отсчётов сигнала</returns>
    public abstract Vector GetSignal(double dist, double speed, IEnumerable<Source> signals = null);

    /// <summary>
    /// Вычисляет общие параметры несущей: временной вектор, затухание и фазовый сдвиг
    /// </summary>
    /// <param name="f0">Несущая частота</param>
    /// <param name="dist">Расстояние до приёмника</param>
    /// <param name="speed">Скорость распространения</param>
    /// <param name="t">Вектор времени</param>
    /// <param name="attenuation">Затухание 1/r (0 если dist==0)</param>
    /// <param name="phaseShift">Фазовый сдвиг 2*pi*f0*dist/speed</param>
    protected void BuildCarrier(double f0, double dist, double speed, out Vector t, out double attenuation, out double phaseShift)
    {
        t = Vector.Time0(SR, T);
        attenuation = dist > 1e-30 ? 1.0 / dist : 1.0;
        phaseShift = 2 * Math.PI * f0 * dist / speed;
    }
}
