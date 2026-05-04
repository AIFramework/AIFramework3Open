using AI.DataStructs.Algebraic;
using AI.HighLevelFunctions;
using System;

namespace AI.DSP.DSPCore;

/// <summary>
/// Оконные функции для БПФ
/// </summary>
[Serializable]
public static class WindowForFFT
{
    /// <summary>
    /// Окно Ханна (Хеннинга) (-31 дб)
    /// </summary>
    /// <param name="windowSize">Размер окна</param>
    public static Vector HannWindow(int windowSize)
    {
        if (windowSize <= 1)
            return new Vector(windowSize) + 1;

        Vector n = Vector.SeqBeginsWithZero(1, windowSize);
        Vector w = 0.5 * (1 - FunctionsForEachElements.Cos(Math.PI * 2.0 * n / (windowSize - 1)));
        return w;
    }

    /// <summary>
    /// Окно Хэмминга (-42 дб)
    /// </summary>
    /// <param name="windowSize">Размер окна</param>
    public static Vector HammingWindow(int windowSize)
    {
        if (windowSize <= 1)
            return new Vector(windowSize) + 1;

        Vector n = Vector.SeqBeginsWithZero(1, windowSize);
        Vector w = 0.53836 - 0.46164 * FunctionsForEachElements.Cos(Math.PI * 2.0 * n / (windowSize - 1));
        return w;
    }

    /// <summary>
    /// Прямоугольное окно (-13 дб)
    /// </summary>
    /// <param name="windowSize">Размер окна</param>
    public static Vector RectWindow(int windowSize)
    {
        return new Vector(windowSize) + 1;
    }

    /// <summary>
    /// Окно Блэкмана (-58 дб)
    /// </summary>
    /// <param name="windowSize">Размер окна</param>
    public static Vector BlackmanWindow(int windowSize)
    {
        if (windowSize <= 1)
            return new Vector(windowSize) + 1;

        Vector n = Vector.SeqBeginsWithZero(1, windowSize);
        const double a = 0.16, a1 = 0.5;
        double a0 = (1 - a) / 2.0, a2 = a / 2.0;

        Vector cos1 = FunctionsForEachElements.Cos(Math.PI * 2.0 * n / (windowSize - 1)), cos2 = FunctionsForEachElements.Cos(Math.PI * 4.0 * n / (windowSize - 1));
        Vector w = a0 - a1 * cos1 + a2 * cos2;
        return w;
    }
}
