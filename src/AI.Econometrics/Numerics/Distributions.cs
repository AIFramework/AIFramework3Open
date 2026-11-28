using System;
using AI.Statistics;

namespace AI.Econometrics.Numerics;

/// <summary>
/// P-значения для проверки гипотез в эконометрике.
/// </summary>
/// <remarks>
/// Распределения берутся из <see cref="StatInference"/>; здесь только правила, общие для всех
/// оценок: двусторонний тест удваивает правый хвост модуля статистики, недопустимые степени
/// свободы дают <c>NaN</c>. Хвост считается напрямую, а не как <c>1 − CDF</c>: разность
/// обращается в ноль уже при p около 1e-16, и сильные эффекты становились бы неразличимы.
/// </remarks>
internal static class Distributions
{
    /// <summary>Двустороннее p-значение по нормальному закону.</summary>
    public static double NormalPValue(double z) => 2.0 * StatInference.NormalSf(Math.Abs(z));

    /// <summary>Двустороннее p-значение по распределению Стьюдента.</summary>
    public static double TPValue(double t, int df)
    {
        if (double.IsNaN(t) || df <= 0) return double.NaN;

        return 2.0 * StatInference.TSf(Math.Abs(t), df);
    }

    /// <summary>Правый хвост распределения хи-квадрат.</summary>
    public static double ChiSquarePValue(double statistic, int df)
    {
        if (double.IsNaN(statistic) || df <= 0) return double.NaN;
        if (statistic <= 0) return 1.0;

        return StatInference.ChiSquaredSf(statistic, df);
    }

    /// <summary>Правый хвост распределения Фишера.</summary>
    /// <param name="statistic">Значение статистики.</param>
    /// <param name="df1">Число степеней свободы числителя.</param>
    /// <param name="df2">Число степеней свободы знаменателя.</param>
    public static double FPValue(double statistic, int df1, int df2)
    {
        if (double.IsNaN(statistic) || statistic <= 0 || df1 <= 0 || df2 <= 0) return double.NaN;

        return StatInference.FSf(statistic, df1, df2);
    }

    /// <summary>Квантиль нормального распределения.</summary>
    public static double NormalQuantile(double p) => StatInference.NormalQuantile(p);
}
