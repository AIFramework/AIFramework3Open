using AI.DataStructs.Algebraic;
using System;

namespace AI.DSP.DSPCore;

/// <summary>
/// Базовый интерфейс реализации спектров
/// </summary>
public interface ISpectr
{
    /// <summary>
    /// Отсчеты частоты
    /// </summary>
    Vector Freq { get; set; }
    /// <summary>
    /// Данные спектра
    /// </summary>
    Vector Data { get; set; }
    /// <summary>
    /// Имя
    /// </summary>
    string Name { get; set; }
    /// <summary>
    /// Название шкалы Y
    /// </summary>
    string YLabel { get; set; }
    /// <summary>
    /// Название шкалы X
    /// </summary>
    string XLabel { get; set; }
    /// <summary>
    /// Выводится ли шкала данные по Y в децибелах
    /// </summary>
    bool IsDbScale { get; set; }
    /// <summary>
    /// Логарифмическая ли шкала частот
    /// </summary>
    bool LogScaleX { get; set; }
}

/// <summary>
/// Амплитудный спектр
/// </summary>
public class AmplitudeSpectr : ISpectr
{
    /// <summary>
    /// Отсчеты частоты
    /// </summary>
    public Vector Freq { get; set; }
    /// <summary>
    /// Амплитуды спектра
    /// </summary>
    public Vector Data { get; set; }
    /// <summary>
    /// Имя
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Название шкалы Y
    /// </summary>
    public string YLabel { get; set; }
    /// <summary>
    /// Название шкалы X
    /// </summary>
    public string XLabel { get; set; }
    /// <summary>
    /// Выводится ли шкала данные по Y в децибелах
    /// </summary>
    public bool IsDbScale { get; set; }
    /// <summary>
    /// Логарифмическая ли шкала частот
    /// </summary>
    public bool LogScaleX { get; set; }

    /// <summary>
    /// Амплитудный спектр
    /// </summary>
    /// <param name="channel">Channel</param>
    /// <param name="isDbScale">Выражать ли в db, 20log(x)</param>
    public AmplitudeSpectr(Channel channel, bool isDbScale = false)
    {
        IsDbScale = isDbScale;
        Freq = channel.Freq();
        Data = IsDbScale ? channel.GetSpectr().Transform(x => 20 * Math.Log10(Math.Max(x, 1e-30))) : channel.GetSpectr();
        Name = "Спектр [\"" + channel.Name + "\"]";
        XLabel = "Частота [Гц]";
        YLabel = "Амплитуда " + (IsDbScale ? "[db]" : channel.YName());
    }

    /// <summary>
    /// Амплитудный спектр
    /// </summary>
    /// <param name="channel">Channel</param>
    /// <param name="windowWFunc">Оконная функция</param>
    /// <param name="isDbScale">Выражать ли в db, 20log(x)</param>
    public AmplitudeSpectr(Channel channel, Func<int, Vector> windowWFunc, bool isDbScale = false)
    {
        IsDbScale = isDbScale;
        Freq = channel.Freq();
        Data = IsDbScale ? channel.GetSpectr(windowWFunc).Transform(x => 20 * Math.Log10(Math.Max(x, 1e-30))) : channel.GetSpectr(windowWFunc);
        Name = "Спектр [\"" + channel.Name + "\"]";
        XLabel = "Частота [Гц]";
        YLabel = "Амплитуда " + (IsDbScale ? "[db]" : channel.YName());
    }
}
