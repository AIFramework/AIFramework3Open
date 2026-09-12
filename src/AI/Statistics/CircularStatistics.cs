#nullable enable
using AI.DataStructs.Algebraic;
using System;

namespace AI.Statistics;

/// <summary>
/// Углы и статистика направлений. Обычное среднее для углов неверно: среднее 350° и 10° — это 0°,
/// а не 180°. Здесь направления усредняются как единичные векторы.
/// </summary>
/// <remarks>
/// Основная единица — радианы, как в <c>AI.Geometry</c>. Для градусов есть нормализация и разность:
/// именно ими пользуются при разборе «северо-запад», «на 30° левее».
/// </remarks>
public static class CircularStatistics
{
    private const double FullTurn = 2.0 * Math.PI;

    /// <summary>Угол, приведённый к [0; 2π)</summary>
    /// <param name="angle">Угол, радианы</param>
    public static double WrapRadians(double angle) => Wrap(angle, FullTurn);

    /// <summary>Угол, приведённый к [0; 360)</summary>
    /// <param name="angle">Угол, градусы</param>
    public static double WrapDegrees(double angle) => Wrap(angle, 360.0);

    /// <summary>Кратчайший поворот от <paramref name="from"/> к <paramref name="to"/>, радианы на (−π; π]</summary>
    /// <param name="from">Начальное направление, радианы</param>
    /// <param name="to">Конечное направление, радианы</param>
    public static double DifferenceRadians(double from, double to) => Difference(from, to, FullTurn);

    /// <summary>Кратчайший поворот от <paramref name="from"/> к <paramref name="to"/>, градусы на (−180; 180]</summary>
    /// <param name="from">Начальное направление, градусы</param>
    /// <param name="to">Конечное направление, градусы</param>
    public static double DifferenceDegrees(double from, double to) => Difference(from, to, 360.0);

    /// <summary>
    /// Среднее направление — направление суммы единичных векторов, радианы на [0; 2π).
    /// При средней длине результирующего вектора около нуля направление не определено
    /// </summary>
    /// <param name="angles">Углы, радианы</param>
    public static double MeanDirection(Vector angles) => WrapRadians(Resultant(angles, null).Direction);

    /// <summary>Взвешенное среднее направление, радианы на [0; 2π)</summary>
    /// <param name="angles">Углы, радианы</param>
    /// <param name="weights">Неотрицательные веса</param>
    public static double MeanDirection(Vector angles, Vector weights) => WrapRadians(Resultant(angles, weights).Direction);

    /// <summary>
    /// Средняя длина результирующего вектора R ∈ [0; 1]: 1 — все направления совпадают,
    /// около 0 — выраженного направления нет
    /// </summary>
    /// <param name="angles">Углы, радианы</param>
    public static double MeanResultantLength(Vector angles) => Resultant(angles, null).Length;

    /// <summary>Взвешенная средняя длина результирующего вектора R ∈ [0; 1]</summary>
    /// <param name="angles">Углы, радианы</param>
    /// <param name="weights">Неотрицательные веса</param>
    public static double MeanResultantLength(Vector angles, Vector weights) => Resultant(angles, weights).Length;

    /// <summary>Направление и средняя длина результирующего вектора; без весов — равные</summary>
    internal static (double Direction, double Length) Resultant(Vector angles, Vector? weights)
    {
        ArgumentNullException.ThrowIfNull(angles);
        if (angles.Count == 0)
            throw new ArgumentException("Нет ни одного направления", nameof(angles));

        double total = weights is null ? angles.Count : WeightedStatistics.TotalWeight(angles, weights);
        double sin = 0;
        double cos = 0;

        for (int i = 0; i < angles.Count; i++)
        {
            double weight = weights is null ? 1.0 : weights[i];
            sin += weight * Math.Sin(angles[i]);
            cos += weight * Math.Cos(angles[i]);
        }

        return (Math.Atan2(sin, cos), Math.Sqrt((sin * sin) + (cos * cos)) / total);
    }

    private static double Wrap(double angle, double period)
    {
        double wrapped = angle % period;
        if (wrapped < 0)
            wrapped += period;

        // Крошечный отрицательный угол после сдвига округляется ровно до периода
        return wrapped >= period ? 0.0 : wrapped;
    }

    private static double Difference(double from, double to, double period)
    {
        double delta = Wrap(to - from, period);
        return delta > period / 2 ? delta - period : delta;
    }
}
