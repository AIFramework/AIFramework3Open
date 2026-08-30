using System;
using AI.Statistics;

namespace AI.Econometrics.Numerics;

/// <summary>
/// Функции распределений, нужные для проверки гипотез в эконометрике.
/// </summary>
/// <remarks>
/// Нормальное, стьюдентово и хи-квадрат берутся из <see cref="StatInference"/>
/// фреймворка; распределение Фишера и неполная бета-функция реализованы здесь,
/// поскольку в базовой библиотеке их нет.
/// </remarks>
internal static class Distributions
{
    /// <summary>Двустороннее p-значение по нормальному закону.</summary>
    public static double NormalPValue(double z) =>
        2.0 * (1.0 - StatInference.NormalCdf(Math.Abs(z)));

    /// <summary>Двустороннее p-значение по распределению Стьюдента.</summary>
    public static double TPValue(double t, int df)
    {
        if (double.IsNaN(t) || df <= 0) return double.NaN;
        return 2.0 * (1.0 - StatInference.TCdf(Math.Abs(t), df));
    }

    /// <summary>Правый хвост распределения хи-квадрат.</summary>
    public static double ChiSquarePValue(double statistic, int df)
    {
        if (double.IsNaN(statistic) || df <= 0) return double.NaN;
        if (statistic <= 0) return 1.0;

        return Math.Clamp(1.0 - StatInference.ChiSquaredCdf(statistic, df), 0, 1);
    }

    /// <summary>Правый хвост распределения Фишера.</summary>
    /// <param name="statistic">Значение статистики.</param>
    /// <param name="df1">Число степеней свободы числителя.</param>
    /// <param name="df2">Число степеней свободы знаменателя.</param>
    public static double FPValue(double statistic, int df1, int df2)
    {
        if (double.IsNaN(statistic) || statistic <= 0 || df1 <= 0 || df2 <= 0) return double.NaN;

        double x = df1 * statistic / ((df1 * statistic) + df2);
        return Math.Clamp(1.0 - RegularizedBeta(x, df1 / 2.0, df2 / 2.0), 0, 1);
    }

    /// <summary>Квантиль нормального распределения.</summary>
    public static double NormalQuantile(double p) => StatInference.NormalQuantile(p);

    /// <summary>
    /// Регуляризованная неполная бета-функция через цепную дробь.
    /// </summary>
    /// <remarks>
    /// Ряд сходится быстро только при <c>x &lt; (a+1)/(a+b+2)</c>, поэтому для
    /// больших аргументов используется симметрия <c>I_x(a,b) = 1 - I_{1-x}(b,a)</c>.
    /// </remarks>
    public static double RegularizedBeta(double x, double a, double b)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;

        double front = Math.Exp((a * Math.Log(x)) + (b * Math.Log(1 - x)) - EconMath.LogBeta(a, b));

        return x < (a + 1) / (a + b + 2)
            ? front * ContinuedFraction(x, a, b) / a
            : 1 - (Math.Exp((b * Math.Log(1 - x)) + (a * Math.Log(x)) - EconMath.LogBeta(b, a))
                   * ContinuedFraction(1 - x, b, a) / b);
    }

    /// <summary>Цепная дробь Лентца для неполной бета-функции.</summary>
    private static double ContinuedFraction(double x, double a, double b)
    {
        const double tiny = 1e-300;
        const int maxIterations = 300;

        double f = 1, c = 1, d = 0;

        for (int i = 0; i <= maxIterations; i++)
        {
            int m = i / 2;
            double numerator;

            if (i == 0) numerator = 1;
            else if (i % 2 == 0) numerator = m * (b - m) * x / ((a + (2 * m) - 1) * (a + (2 * m)));
            else numerator = -(a + m) * (a + b + m) * x / ((a + (2 * m)) * (a + (2 * m) + 1));

            d = 1 + (numerator * d);
            if (Math.Abs(d) < tiny) d = tiny;
            d = 1 / d;

            c = 1 + (numerator / c);
            if (Math.Abs(c) < tiny) c = tiny;

            double step = c * d;
            f *= step;

            if (Math.Abs(1 - step) < 1e-12) break;
        }

        return f - 1;
    }
}
