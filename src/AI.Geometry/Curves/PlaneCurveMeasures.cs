#nullable enable

using System;
using AI.MathUtils.Integration;

namespace AI.Geometry.Curves;

/// <summary>
/// Меры параметрической кривой на плоскости x(t), y(t), заданной делегатами: длина дуги, ориентированная площадь
/// и кривизна.
/// </summary>
/// <remarks>
/// Интегралы берутся адаптивной квадратурой <see cref="GaussKronrod"/> с относительной точностью около 1e-10 для
/// гладкой кривой. Производные передает вызывающий: символьные дают точный ответ, а численные ограничивают точность
/// шагом разности. График функции y = f(x) это кривая x(t) = t, y(t) = f(t).
/// </remarks>
public static class PlaneCurveMeasures
{
    /// <summary>
    /// Длина дуги от <paramref name="from"/> до <paramref name="to"/>: интеграл √(x'² + y'²) по параметру.
    /// </summary>
    /// <param name="dx">Производная x по параметру</param>
    /// <param name="dy">Производная y по параметру</param>
    /// <param name="from">Начальное значение параметра</param>
    /// <param name="to">Конечное значение параметра; при to &lt; from длина отрицательна</param>
    /// <param name="relativeTolerance">Допустимая относительная погрешность</param>
    /// <returns>Длина дуги в единицах координат</returns>
    public static double ArcLength(
        Func<double, double> dx, Func<double, double> dy, double from, double to, double relativeTolerance = 1e-10)
    {
        ArgumentNullException.ThrowIfNull(dx);
        ArgumentNullException.ThrowIfNull(dy);
        return GaussKronrod.Integrate(
            t =>
            {
                double u = dx(t), v = dy(t);
                return Math.Sqrt((u * u) + (v * v));
            },
            from,
            to,
            relativeTolerance);
    }

    /// <summary>
    /// Ориентированная площадь по формуле Грина: ½·∫(x·y' − y·x') по параметру. У замкнутой кривой это площадь
    /// внутри нее, положительная при обходе против часовой стрелки; у незамкнутой это площадь, заметенная отрезком
    /// из начала координат в точку кривой.
    /// </summary>
    /// <param name="x">Координата x</param>
    /// <param name="y">Координата y</param>
    /// <param name="dx">Производная x по параметру</param>
    /// <param name="dy">Производная y по параметру</param>
    /// <param name="from">Начальное значение параметра</param>
    /// <param name="to">Конечное значение параметра</param>
    /// <param name="relativeTolerance">Допустимая относительная погрешность</param>
    /// <returns>Ориентированная площадь в квадратных единицах координат</returns>
    public static double SignedArea(
        Func<double, double> x, Func<double, double> y, Func<double, double> dx, Func<double, double> dy,
        double from, double to, double relativeTolerance = 1e-10)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(dx);
        ArgumentNullException.ThrowIfNull(dy);
        return GaussKronrod.Integrate(t => ((x(t) * dy(t)) - (y(t) * dx(t))) / 2, from, to, relativeTolerance);
    }

    /// <summary>
    /// Ориентированная кривизна в точке: (x'·y'' − y'·x'') / (x'² + y'²)^{3/2}. Положительна, когда кривая
    /// поворачивает против часовой стрелки; у окружности радиуса r по модулю равна 1/r.
    /// </summary>
    /// <param name="dx">Первая производная x</param>
    /// <param name="dy">Первая производная y</param>
    /// <param name="ddx">Вторая производная x</param>
    /// <param name="ddy">Вторая производная y</param>
    /// <returns>Кривизна в обратных единицах координат; NaN в особой точке, где скорость нулевая</returns>
    public static double Curvature(double dx, double dy, double ddx, double ddy)
    {
        double speedSquared = (dx * dx) + (dy * dy);
        return speedSquared == 0 ? double.NaN : ((dx * ddy) - (dy * ddx)) / Math.Pow(speedSquared, 1.5);
    }
}
