using System;
using System.Collections.Generic;

using AI.Economics.Insights;

namespace AI.Economics.Saas;

/// <summary>Оценка метрики относительно принятых в отрасли порогов.</summary>
public enum MetricVerdict
{
    /// <summary>Значение в норме.</summary>
    Good,

    /// <summary>Пограничное значение — требует внимания.</summary>
    Warning,

    /// <summary>Значение за пределами допустимого.</summary>
    Poor,
}

/// <summary>Метрика с оценкой и пояснением.</summary>
/// <param name="Name">Название метрики.</param>
/// <param name="Value">Числовое значение.</param>
/// <param name="Unit">Единица измерения.</param>
/// <param name="Verdict">Оценка относительно порогов.</param>
/// <param name="Comment">Как читать значение.</param>
public sealed record SaasMetric(string Name, double Value, string Unit, MetricVerdict Verdict, string Comment);

/// <summary>Вход расчёта здоровья SaaS-бизнеса за период.</summary>
public sealed record SaasHealthInput
{
    /// <summary>ARR на начало периода.</summary>
    public double ArrStart { get; init; }

    /// <summary>ARR на конец периода.</summary>
    public double ArrEnd { get; init; }

    /// <summary>ARR годом ранее — для расчёта годового темпа роста.</summary>
    public double ArrYearAgo { get; init; }

    /// <summary>Затраты на продажи и маркетинг за период.</summary>
    public double SalesAndMarketing { get; init; }

    /// <summary>Чистое сжигание денег за период (положительное — тратим больше, чем зарабатываем).</summary>
    public double NetBurn { get; init; }

    /// <summary>Маржа свободного денежного потока, доля от выручки.</summary>
    public double FreeCashFlowMargin { get; init; }

    /// <summary>Доля валовой маржи.</summary>
    public double GrossMarginRate { get; init; } = 0.8;

    /// <summary>Средняя выручка на клиента в месяц.</summary>
    public double ArpaMonthly { get; init; }

    /// <summary>Стоимость привлечения клиента.</summary>
    public double Cac { get; init; }

    /// <summary>Net Dollar Retention за период, доля (1,15 означает 115 %).</summary>
    public double NetDollarRetention { get; init; } = double.NaN;
}

/// <summary>
/// Сводные метрики здоровья SaaS-бизнеса: Rule of 40, magic number,
/// burn multiple и окупаемость привлечения.
/// </summary>
/// <remarks>
/// Эти четыре показателя закрывают четыре разных вопроса — растём ли мы
/// достаточно быстро относительно убыточности, эффективно ли работает
/// коммерческая машина, дорого ли обходится каждый рубль нового ARR и
/// как быстро возвращаются деньги за привлечение. По отдельности каждый
/// легко «нарисовать», вместе они противоречат друг другу и потому
/// показательны.
/// </remarks>
public static class SaasMetrics
{
    /// <summary>
    /// Rule of 40: сумма темпа роста и маржи в процентах. Ориентир — не ниже 40.
    /// </summary>
    /// <param name="growthRatePercent">Темп роста выручки, процентов.</param>
    /// <param name="marginPercent">Маржа (обычно FCF или EBITDA), процентов.</param>
    /// <returns>Значение показателя.</returns>
    public static double RuleOf40(double growthRatePercent, double marginPercent)
        => growthRatePercent + marginPercent;

    /// <summary>
    /// Magic number: сколько нового ARR приносит рубль, вложенный в продажи
    /// и маркетинг предыдущего периода.
    /// </summary>
    /// <param name="arrStart">ARR на начало периода.</param>
    /// <param name="arrEnd">ARR на конец периода.</param>
    /// <param name="salesAndMarketing">Затраты на продажи и маркетинг за период.</param>
    /// <returns>Значение показателя; выше 0,75 принято считать хорошим.</returns>
    public static double MagicNumber(double arrStart, double arrEnd, double salesAndMarketing)
        => salesAndMarketing > 0 ? (arrEnd - arrStart) / salesAndMarketing : double.NaN;

    /// <summary>
    /// Burn multiple: сколько денег сожжено на каждый рубль нового ARR.
    /// </summary>
    /// <param name="netBurn">Чистое сжигание за период.</param>
    /// <param name="netNewArr">Прирост ARR за тот же период.</param>
    /// <returns>Значение показателя; ниже 1,5 считается хорошим, выше 3 — тревожным.</returns>
    public static double BurnMultiple(double netBurn, double netNewArr)
        => netNewArr > 0 ? netBurn / netNewArr : double.PositiveInfinity;

    /// <summary>
    /// Срок окупаемости привлечения в месяцах по марже, а не по выручке.
    /// </summary>
    /// <param name="cac">Стоимость привлечения клиента.</param>
    /// <param name="arpaMonthly">Средняя выручка с клиента в месяц.</param>
    /// <param name="grossMarginRate">Доля валовой маржи.</param>
    /// <returns>Число месяцев до возврата затрат на привлечение.</returns>
    public static double CacPaybackMonths(double cac, double arpaMonthly, double grossMarginRate)
    {
        double monthly = arpaMonthly * grossMarginRate;
        return monthly > 0 ? cac / monthly : double.PositiveInfinity;
    }

    /// <summary>Считает набор метрик и оценивает каждую по отраслевым порогам.</summary>
    /// <param name="input">Показатели бизнеса за период.</param>
    /// <returns>Список метрик с оценками.</returns>
    /// <exception cref="ArgumentNullException">Вход не задан.</exception>
    public static IReadOnlyList<SaasMetric> Evaluate(SaasHealthInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        double growth = input.ArrYearAgo > 0
            ? (input.ArrEnd - input.ArrYearAgo) / input.ArrYearAgo * 100.0
            : input.ArrStart > 0 ? (input.ArrEnd - input.ArrStart) / input.ArrStart * 100.0 : double.NaN;

        double rule40 = RuleOf40(growth, input.FreeCashFlowMargin * 100.0);
        double magic = MagicNumber(input.ArrStart, input.ArrEnd, input.SalesAndMarketing);
        double burn = BurnMultiple(input.NetBurn, input.ArrEnd - input.ArrStart);
        double payback = CacPaybackMonths(input.Cac, input.ArpaMonthly, input.GrossMarginRate);

        var metrics = new List<SaasMetric>
        {
            new("Темп роста ARR", growth, "%",
                Band(growth, 40, 20),
                "Годовой рост выручки. Ниже 20 % при убыточности инвесторы не финансируют."),

            new("Rule of 40", rule40, "",
                Band(rule40, 40, 20),
                "Рост плюс маржа. Позволяет быть убыточным, если растёшь быстро, и наоборот."),

            new("Magic number", magic, "",
                Band(magic, 0.75, 0.5),
                "Новый ARR на рубль коммерческих затрат. Ниже 0,5 — масштабировать продажи рано."),

            new("Burn multiple", burn, "",
                BandInverse(burn, 1.5, 3.0),
                "Сожжено денег на рубль нового ARR. Главная метрика эффективности сжигания."),

            new("CAC payback", payback, "мес.",
                BandInverse(payback, 12, 18),
                "Месяцев до возврата затрат на привлечение по валовой марже."),
        };

        if (!double.IsNaN(input.NetDollarRetention))
        {
            metrics.Add(new SaasMetric("NDR", input.NetDollarRetention * 100.0, "%",
                Band(input.NetDollarRetention * 100.0, 110, 100),
                "Удержание выручки с расширениями. Выше 100 % — база растёт сама."));
        }

        return metrics;
    }

    /// <summary>Оценка «больше — лучше».</summary>
    private static MetricVerdict Band(double value, double good, double warning)
    {
        if (double.IsNaN(value)) return MetricVerdict.Warning;
        return value >= good ? MetricVerdict.Good
             : value >= warning ? MetricVerdict.Warning
             : MetricVerdict.Poor;
    }

    /// <summary>Оценка «меньше — лучше».</summary>
    private static MetricVerdict BandInverse(double value, double good, double warning)
    {
        if (double.IsNaN(value)) return MetricVerdict.Warning;
        return value <= good ? MetricVerdict.Good
             : value <= warning ? MetricVerdict.Warning
             : MetricVerdict.Poor;
    }
}
