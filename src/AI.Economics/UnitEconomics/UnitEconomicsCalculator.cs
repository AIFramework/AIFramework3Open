using System;
using AI.DataStructs.Algebraic;

namespace AI.Economics.UnitEconomics;

/// <summary>
/// Расчёт юнит-экономики: CAC, LTV, LTV/CAC, срок окупаемости привлечения.
/// </summary>
/// <remarks>
/// Отличия от «формулы из блога» — ровно те три, на которых обычно ошибаются:
/// <list type="number">
/// <item>LTV считается по маржинальному вкладу, а не по выручке;</item>
/// <item>поток дисконтируется, иначе рубль через три года равен сегодняшнему;</item>
/// <item>удержание может задаваться кривой, а не единственным числом оттока —
/// на реальных данных отток убывает со временем, и формула
/// <c>ARPU / churn</c> занижает LTV в разы.</item>
/// </list>
/// </remarks>
public static class UnitEconomicsCalculator
{
    /// <summary>Горизонт по умолчанию для бесконечной постановки, периодов.</summary>
    private const int DefaultInfiniteHorizon = 600;

    /// <summary>Рассчитывает юнит-экономику по входным параметрам.</summary>
    /// <param name="input">Параметры сегмента.</param>
    /// <returns>Метрики юнит-экономики.</returns>
    /// <exception cref="ArgumentNullException">Вход не задан.</exception>
    public static UnitEconomicsResult Compute(UnitEconomicsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        double cac = double.IsNaN(input.CacOverride)
            ? (input.NewCustomers > 0 ? (input.MarketingSpend + input.SalesSpend) / input.NewCustomers : 0)
            : input.CacOverride;

        double arpu = input.RevenuePerPeriod;
        double contribution = (arpu * input.GrossMarginRate) - input.VariableCostPerPeriod;
        double marginRate = arpu > 0 ? contribution / arpu : 0;

        double[] survival = BuildSurvival(input);
        int horizon = survival.Length;

        double discount = input.DiscountRate;
        double ltv = 0, undiscounted = 0, lifetime = 0;
        var cumulativeNet = new double[horizon];
        double payback = double.NaN;
        double prevCum = -cac;

        for (int t = 0; t < horizon; t++)
        {
            double df = discount > 0 ? Math.Pow(1.0 + discount, -t) : 1.0;
            double step = contribution * survival[t];

            ltv += step * df;
            undiscounted += step;
            lifetime += survival[t];

            double cum = ltv - cac;
            cumulativeNet[t] = cum;

            if (double.IsNaN(payback) && cum >= 0)
            {
                // Линейная интерполяция внутри периода: округление срока
                // окупаемости до целых месяцев даёт ошибку почти в месяц.
                // prevCum отвечает t платежам, cum — (t + 1) платежу.
                double gain = cum - prevCum;
                payback = gain > 0 ? t + (-prevCum / gain) : t + 1.0;
            }

            prevCum = cum;
        }

        return new UnitEconomicsResult
        {
            Cac = cac,
            Arpu = arpu,
            ContributionPerPeriod = contribution,
            ContributionMarginRate = marginRate,
            Ltv = ltv,
            UndiscountedLtv = undiscounted,
            LtvToCac = cac > 0 ? ltv / cac : double.PositiveInfinity,
            NetContribution = ltv - cac,
            CacPaybackPeriods = payback,
            ExpectedLifetimePeriods = lifetime,
            HorizonUsed = horizon,
            Survival = new Vector(survival),
            CumulativeNet = new Vector(cumulativeNet),
        };
    }

    /// <summary>
    /// Классическая формула LTV при постоянном оттоке и бесконечном горизонте:
    /// <c>LTV = m (1 + d) / (c + d)</c>.
    /// </summary>
    /// <param name="contributionPerPeriod">Маржинальный вклад за период.</param>
    /// <param name="churnRate">Отток за период.</param>
    /// <param name="discountRate">Ставка дисконтирования за период.</param>
    /// <returns>Пожизненная ценность клиента.</returns>
    public static double LtvFromChurn(double contributionPerPeriod, double churnRate, double discountRate = 0)
    {
        if (churnRate <= 0 && discountRate <= 0) return double.PositiveInfinity;
        if (discountRate <= 0) return contributionPerPeriod / churnRate;
        return contributionPerPeriod * (1.0 + discountRate) / (churnRate + discountRate);
    }

    /// <summary>
    /// LTV по произвольной кривой удержания:
    /// <c>LTV = m * sum S(t) / (1 + d)^t</c>.
    /// </summary>
    /// <param name="contributionPerPeriod">Маржинальный вклад за период.</param>
    /// <param name="survival">Кривая удержания, где <c>S(0) = 1</c>.</param>
    /// <param name="discountRate">Ставка дисконтирования за период.</param>
    /// <returns>Пожизненная ценность клиента на горизонте кривой.</returns>
    public static double LtvFromCurve(double contributionPerPeriod, Vector survival, double discountRate = 0)
    {
        ArgumentNullException.ThrowIfNull(survival);

        double sum = 0;
        for (int t = 0; t < survival.Count; t++)
            sum += survival[t] * (discountRate > 0 ? Math.Pow(1.0 + discountRate, -t) : 1.0);

        return contributionPerPeriod * sum;
    }

    /// <summary>Строит кривую удержания из входа: явную либо геометрическую.</summary>
    private static double[] BuildSurvival(UnitEconomicsInput input)
    {
        if (input.Survival is { Count: > 0 } curve)
        {
            int len = input.Horizon > 0 ? Math.Min(input.Horizon, curve.Count) : curve.Count;
            var s = new double[len];
            for (int t = 0; t < len; t++) s[t] = curve[t];
            return s;
        }

        int horizon = input.Horizon > 0 ? input.Horizon : DefaultInfiniteHorizon;
        double keep = 1.0 - input.ChurnRate;
        var result = new double[horizon];

        for (int t = 0; t < horizon; t++)
            result[t] = keep <= 0 ? (t == 0 ? 1 : 0) : Math.Pow(keep, t);

        return result;
    }
}
