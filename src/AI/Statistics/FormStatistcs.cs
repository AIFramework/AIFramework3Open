using AI.DataStructs.Algebraic;
using System;
using System.ComponentModel;

namespace AI.Statistics;

/// <summary>
/// Статистики формы сигнала (безразмерные характеристики, не
/// зависящие от масштабирования по амплитуде).
/// </summary>
[Serializable]
public static class FormStatistics
{
    /// <summary>
    /// Пик-фактор (crest factor) = max|x| / RMS. Безразмерная метрика
    /// «пикообразности» сигнала.
    /// </summary>
    public static double CrestFactor(Vector vector)
    {
        if (vector == null || vector.Count == 0)
            throw new ArgumentException("Пустой вектор", nameof(vector));

        double rms = Statistic.RMS(vector);
        if (rms < double.Epsilon) return 0.0;

        double maxAbs = vector.MaxAbs();
        return maxAbs / rms;
    }

    /// <summary>
    /// Коэффициент формы = RMS / среднее(|x|). Безразмерная.
    /// </summary>
    public static double ShapeFactor(Vector vector)
    {
        if (vector == null || vector.Count == 0)
            throw new ArgumentException("Пустой вектор", nameof(vector));

        double rms = Statistic.RMS(vector);
        double meanAbs = Statistic.ExpectedValueAbs(vector);
        return meanAbs < double.Epsilon ? 0.0 : rms / meanAbs;
    }

    /// <summary>
    /// Импульсный фактор = max|x| / среднее(|x|).
    /// </summary>
    public static double ImpulseFactor(Vector vector)
    {
        if (vector == null || vector.Count == 0)
            throw new ArgumentException("Пустой вектор", nameof(vector));

        double meanAbs = Statistic.ExpectedValueAbs(vector);
        if (meanAbs < double.Epsilon) return 0.0;

        return vector.MaxAbs() / meanAbs;
    }
}

/// <summary>
/// Устаревшее имя из-за опечатки в слове «Statistics». Класс
/// сохранён как прокси к <see cref="FormStatistics"/> для обратной
/// совместимости.
/// </summary>
[Obsolete("Используйте FormStatistics (без опечатки).", false)]
[EditorBrowsable(EditorBrowsableState.Never)]
[Serializable]
public static class FormStatistcs
{
    /// <summary>Алиас <see cref="FormStatistics.CrestFactor"/>.</summary>
    public static double CrestFactor(Vector vector) => FormStatistics.CrestFactor(vector);
}
