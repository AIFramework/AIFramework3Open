using System;

namespace AI.Economics.Cohorts;

/// <summary>Семейство кривых удержания.</summary>
public enum RetentionModel
{
    /// <summary>Экспоненциальная: постоянный отток, <c>S(t) = exp(-lambda t)</c>.</summary>
    Exponential,

    /// <summary>Степенная: <c>S(t) = (1 + t)^(-alpha)</c>, тяжёлый хвост.</summary>
    PowerLaw,

    /// <summary>Вейбулла: <c>S(t) = exp(-(t / lambda)^k)</c>, монотонно меняющийся отток.</summary>
    Weibull,

    /// <summary>
    /// Сдвинутая бета-геометрическая (Fader — Hardie): гетерогенность клиентов
    /// по склонности к оттоку описывается бета-распределением.
    /// </summary>
    ShiftedBetaGeometric,
}

/// <summary>
/// Вычисление кривых удержания и начальных приближений для их подгонки.
/// </summary>
internal static class RetentionCurves
{
    /// <summary>Имена параметров модели — для отчётов.</summary>
    public static string[] ParameterNames(RetentionModel model) => model switch
    {
        RetentionModel.Exponential => ["lambda"],
        RetentionModel.PowerLaw => ["alpha"],
        RetentionModel.Weibull => ["k", "lambda"],
        RetentionModel.ShiftedBetaGeometric => ["alpha", "beta"],
        _ => [],
    };

    /// <summary>Человекочитаемое имя модели.</summary>
    public static string DisplayName(RetentionModel model) => model switch
    {
        RetentionModel.Exponential => "Экспоненциальная",
        RetentionModel.PowerLaw => "Степенная",
        RetentionModel.Weibull => "Вейбулла",
        RetentionModel.ShiftedBetaGeometric => "sBG (бета-геометрическая)",
        _ => model.ToString(),
    };

    /// <summary>
    /// Кривая доживания <c>S(0..horizon)</c>, где <c>S(0) = 1</c>.
    /// </summary>
    /// <param name="model">Семейство кривых.</param>
    /// <param name="p">Параметры модели, все строго положительные.</param>
    /// <param name="horizon">Максимальный возраст в периодах.</param>
    /// <returns>Массив длиной <c>horizon + 1</c>.</returns>
    public static double[] Survival(RetentionModel model, double[] p, int horizon)
    {
        var s = new double[horizon + 1];
        s[0] = 1.0;

        switch (model)
        {
            case RetentionModel.Exponential:
                for (int t = 1; t <= horizon; t++) s[t] = Math.Exp(-p[0] * t);
                break;

            case RetentionModel.PowerLaw:
                for (int t = 1; t <= horizon; t++) s[t] = Math.Pow(1.0 + t, -p[0]);
                break;

            case RetentionModel.Weibull:
                for (int t = 1; t <= horizon; t++) s[t] = Math.Exp(-Math.Pow(t / p[1], p[0]));
                break;

            case RetentionModel.ShiftedBetaGeometric:
                // S(t) = S(t-1) * (beta + t - 1) / (alpha + beta + t - 1)
                double a = p[0], b = p[1];
                for (int t = 1; t <= horizon; t++)
                    s[t] = s[t - 1] * (b + t - 1.0) / (a + b + t - 1.0);
                break;
        }

        // Численная защита: кривая обязана быть невозрастающей и лежать в (0; 1]
        for (int t = 1; t <= horizon; t++)
        {
            if (double.IsNaN(s[t]) || s[t] < 0) s[t] = 0;
            if (s[t] > s[t - 1]) s[t] = s[t - 1];
        }

        return s;
    }

    /// <summary>
    /// Мгновенная доля удержания <c>r(t) = S(t) / S(t-1)</c> — то, что в отчётах
    /// называют «месячным retention» и ошибочно считают константой.
    /// </summary>
    public static double[] RetentionRates(double[] survival)
    {
        var r = new double[survival.Length];
        r[0] = 1.0;
        for (int t = 1; t < survival.Length; t++)
            r[t] = survival[t - 1] > 0 ? survival[t] / survival[t - 1] : 0;
        return r;
    }

    /// <summary>
    /// Начальное приближение параметров по наблюдённой кривой.
    /// </summary>
    /// <param name="model">Семейство кривых.</param>
    /// <param name="observed">Наблюдённое доживание, <c>observed[0] = 1</c>.</param>
    public static double[] InitialGuess(RetentionModel model, double[] observed)
    {
        int last = observed.Length - 1;
        double sLast = Math.Max(observed[last], 1e-4);
        double s1 = observed.Length > 1 ? EconClamp(observed[1]) : 0.8;

        return model switch
        {
            RetentionModel.Exponential => [Math.Max(-Math.Log(sLast) / Math.Max(last, 1), 1e-3)],
            RetentionModel.PowerLaw => [Math.Max(-Math.Log(sLast) / Math.Log(1.0 + Math.Max(last, 1)), 1e-3)],
            RetentionModel.Weibull => [0.8, Math.Max(last / Math.Max(-Math.Log(sLast), 1e-3), 0.5)],

            // Из S(1) = beta / (alpha + beta) при alpha = 1 следует beta = s1 / (1 - s1)
            RetentionModel.ShiftedBetaGeometric => [1.0, Math.Max(s1 / Math.Max(1.0 - s1, 1e-3), 0.05)],
            _ => [1.0],
        };
    }

    private static double EconClamp(double v) => v <= 0 ? 0.01 : v >= 1 ? 0.99 : v;
}
