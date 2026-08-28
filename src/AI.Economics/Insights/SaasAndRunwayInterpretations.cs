using System;
using System.Collections.Generic;
using System.Linq;
using AI.Economics.Insights;

namespace AI.Economics.Saas;

/// <summary>Разбор MRR-мостика.</summary>
public sealed partial record MrrBridgeResult : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double growth = StartingMrr > 0 ? NetNewMrr / StartingMrr : double.NaN;
        double losses = ChurnedMrr + ContractionMrr;
        double gains = NewMrr + ExpansionMrr + ReactivationMrr;
        double churnShareOfGains = gains > 0 ? losses / gains : double.NaN;

        return new InterpretationBuilder("MRR-мостик за период")
            .Summary($"Выручка изменилась с {Fmt.Money(StartingMrr)} до {Fmt.Money(EndingMrr)} " +
                     $"({Fmt.Pct(growth)}). Прирост дали новые ({Fmt.Money(NewMrr)}) и расширение " +
                     $"({Fmt.Money(ExpansionMrr)}), потери — отток ({Fmt.Money(ChurnedMrr)}) " +
                     $"и сжатие ({Fmt.Money(ContractionMrr)}). NDR {Fmt.Pct(NetDollarRetention)}, " +
                     $"GRR {Fmt.Pct(GrossRevenueRetention)}.")
            .Metric("NDR", Fmt.Pct(NetDollarRetention), null,
                "удержание выручки с расширениями, без новых клиентов",
                NetDollarRetention >= 1.1 ? MetricQuality.Good
                    : NetDollarRetention >= 1.0 ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("GRR", Fmt.Pct(GrossRevenueRetention), null,
                "удержание без расширений — потолок честности",
                GrossRevenueRetention >= 0.9 ? MetricQuality.Good
                    : GrossRevenueRetention >= 0.8 ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("Quick ratio", QuickRatio, null,
                "прирост делить на потери; ниже единицы — база сжимается",
                QuickRatio >= 4 ? MetricQuality.Good
                    : QuickRatio >= 1 ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("Отток логотипов", Fmt.Pct(LogoChurnRate), null,
                $"{ChurnedCustomers} клиентов из {StartingCustomers}",
                LogoChurnRate < 0.02 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Чистый прирост", Fmt.Money(NetNewMrr), null, Fmt.Pct(growth) + " к началу периода",
                NetNewMrr > 0 ? MetricQuality.Good : MetricQuality.Critical)
            .FindingIf(NetDollarRetention >= 1.0,
                "NDR не ниже единицы: база растёт сама, даже если продажи полностью остановятся. " +
                "Это самое ценное свойство подписной модели.")
            .FindingIf(NetDollarRetention < 1.0 && growth > 0,
                "Рост держится только на новых продажах: существующая база сокращается. " +
                "Каждый следующий процент роста будет обходиться дороже предыдущего.")
            .FindingIf(!double.IsNaN(churnShareOfGains) && churnShareOfGains > 0.5,
                $"Потери съедают {Fmt.Pct(churnShareOfGains)} прироста. Коммерческая машина " +
                "работает во многом на компенсацию оттока.")
            .FindingIf(ExpansionMrr > NewMrr,
                "Расширение существующих клиентов приносит больше новых продаж — признак " +
                "зрелого продукта с работающей моделью роста внутри аккаунта.")
            .WarningIf(GrossRevenueRetention < 0.8,
                $"GRR {Fmt.Pct(GrossRevenueRetention)}: база теряет более пятой части выручки " +
                "за период. Расширение крупных клиентов маскирует это в NDR.")
            .WarningIf(StartingCustomers < 30,
                $"Клиентов на начало периода {StartingCustomers}: показатели удержания " +
                "определяются поведением единиц и сильно шумят.")
            .Warning("Разовые платежи и услуги внедрения не должны входить в MRR: они дают " +
                     "ложное расширение с последующим ложным сжатием.")
            .Recommendation("Следите за GRR отдельно от NDR: первый показывает качество продукта, " +
                            "второй — способность продавать больше тем же клиентам.")
            .Build();
    }
}

/// <summary>Разбор набора метрик здоровья SaaS-бизнеса.</summary>
public static class SaasMetricsInsights
{
    /// <summary>Разбирает набор метрик как единую картину.</summary>
    /// <param name="metrics">Метрики, посчитанные по показателям периода.</param>
    /// <returns>Итог, оценки и рекомендации.</returns>
    /// <exception cref="ArgumentNullException">Метрики не заданы.</exception>
    public static Interpretation Interpret(this IReadOnlyList<SaasMetric> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var problems = metrics.Where(m => m.Verdict == MetricVerdict.Poor).ToList();
        var good = metrics.Where(m => m.Verdict == MetricVerdict.Good).ToList();
        SaasMetric? growth = metrics.FirstOrDefault(m => m.Name.Contains("рост", StringComparison.OrdinalIgnoreCase));
        SaasMetric? burn = metrics.FirstOrDefault(m => m.Name == "Burn multiple");

        var builder = new InterpretationBuilder("Здоровье SaaS-бизнеса")
            .Summary($"В норме {good.Count} метрик из {metrics.Count}, за пределами нормы " +
                     $"{problems.Count}. " +
                     (problems.Count == 0
                         ? "Показатели согласованы: рост оплачивается эффективно."
                         : $"Проблемные показатели: {string.Join(", ", problems.Select(m => m.Name))}."));

        foreach (SaasMetric metric in metrics)
        {
            builder.Metric(metric.Name, metric.Value, metric.Unit, metric.Comment, metric.Verdict switch
            {
                MetricVerdict.Good => MetricQuality.Good,
                MetricVerdict.Warning => MetricQuality.Warning,
                _ => MetricQuality.Critical,
            }, metric.Unit == "%" ? 1 : 2);
        }

        return builder
            .Finding("Метрики намеренно противоречат друг другу: рост можно купить бюджетом, " +
                     "но тогда испортятся magic number и burn multiple. Одновременно нарисовать " +
                     "все четыре невозможно.")
            .FindingIf(burn is not null && burn.Verdict == MetricVerdict.Poor,
                $"Burn multiple {Fmt.Num(burn?.Value ?? 0)}: каждый рубль нового ARR обходится " +
                "слишком дорого. Это главный сигнал для инвестора о качестве роста.")
            .FindingIf(growth is not null && growth.Verdict == MetricVerdict.Good
                       && problems.Count > 0,
                "Рост в норме, но оплачен неэффективно. При следующем раунде вопросы будут " +
                "именно к эффективности, а не к темпу.")
            .WarningIf(problems.Count >= 3,
                "Три и более показателей за пределами нормы. Это не отдельные проблемы, " +
                "а признак того, что модель роста не работает.")
            .Warning("Пороговые значения взяты из практики венчурного рынка для SaaS " +
                     "и не применимы напрямую к маркетплейсам, аппаратным продуктам " +
                     "и агентскому бизнесу.")
            .Recommendation("Сравнивайте показатели с собственной динамикой за год, " +
                            "а не только с отраслевыми порогами.")
            .Build();
    }
}
