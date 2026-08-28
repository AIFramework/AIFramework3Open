using System;
using System.Linq;
using AI.Economics.Insights;

namespace AI.Economics.UnitEconomics;

/// <summary>Разбор результата юнит-экономики.</summary>
public sealed partial record UnitEconomicsResult : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool payback = !double.IsNaN(CacPaybackPeriods);
        double overstatement = Ltv > 1e-9 ? UndiscountedLtv / Ltv : 1;

        return new InterpretationBuilder("Юнит-экономика клиента")
            .Summary($"Привлечение клиента стоит {Fmt.Money(Cac)}, приносит он {Fmt.Money(Ltv)} " +
                     $"дисконтированной маржи за {Fmt.Num(ExpectedLifetimePeriods, 1)} периодов жизни. " +
                     $"Отношение LTV к CAC равно {Fmt.Num(LtvToCac)}" +
                     (payback
                         ? $", вложения возвращаются за {Fmt.Num(CacPaybackPeriods, 1)} периодов."
                         : ", на заданном горизонте вложения не возвращаются."))
            .Metric("CAC", Fmt.Money(Cac), null, "затраты на привлечение одного клиента")
            .Metric("LTV", Fmt.Money(Ltv), null, "дисконтированная маржа за срок жизни")
            .Metric("LTV / CAC", LtvToCac, null, "рыночный ориентир — не ниже 3",
                LtvToCac >= 3 ? MetricQuality.Good : LtvToCac >= 1 ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("Окупаемость", payback ? Fmt.Num(CacPaybackPeriods, 1) : "не окупается", "периодов",
                "до этого момента клиент убыточен",
                !payback ? MetricQuality.Critical
                    : CacPaybackPeriods <= 12 ? MetricQuality.Good
                    : CacPaybackPeriods <= 18 ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("Маржинальный вклад", Fmt.Money(ContributionPerPeriod), null,
                $"{Fmt.Pct(ContributionMarginRate)} от выручки за период")
            .Metric("Срок жизни", ExpectedLifetimePeriods, "периодов", "сумма кривой удержания")
            .Metric("Прибыль с клиента", Fmt.Money(NetContribution), null, "LTV за вычетом CAC",
                NetContribution > 0 ? MetricQuality.Good : MetricQuality.Critical)
            .FindingIf(LtvToCac < 1,
                "Клиент не окупает собственное привлечение. При таком соотношении рост " +
                "объёма увеличивает убыток, а не прибыль.")
            .FindingIf(LtvToCac is >= 1 and < 3,
                "Экономика сходится, но запаса нет: отношение ниже трёх не оставляет места " +
                "на постоянные расходы и ошибки прогноза удержания.")
            .FindingIf(LtvToCac > 5,
                "Отношение выше пяти обычно означает недоинвестирование в привлечение: " +
                "можно позволить себе более дорогие каналы и вырасти быстрее.")
            .FindingIf(overstatement > 1.2,
                $"Без дисконтирования LTV оказался бы в {Fmt.Num(overstatement)} раза выше. " +
                "Именно на этой разнице строится большинство завышенных инвесторских моделей.")
            .WarningIf(!payback,
                "Срок окупаемости не достигается на горизонте расчёта — увеличьте горизонт " +
                "или пересмотрите экономику канала.")
            .WarningIf(payback && CacPaybackPeriods > 18,
                $"Окупаемость {Fmt.Num(CacPaybackPeriods, 0)} периодов требует финансирования " +
                "оборотного капитала: деньги за клиента вернутся сильно позже, чем потрачены.")
            .WarningIf(Survival.Count > 0 && Survival.Count < 12,
                "Кривая удержания короче года: оценка срока жизни опирается на экстраполяцию.")
            .Warning("Удержание измерено в прошлом. Изменения продукта, цен и конкуренции " +
                     "влияют на срок жизни клиента сильнее, чем выбор метода расчёта.")
            .Recommendation("Считайте это соотношение отдельно по каналам: смешанный показатель " +
                            "скрывает убыточные источники трафика.")
            .RecommendationIf(LtvToCac < 3,
                "Работайте с удержанием прежде, чем с привлечением: рост срока жизни на 20 % " +
                "дешевле, чем снижение стоимости привлечения на те же 20 %.")
            .Build();
    }
}

/// <summary>Разбор экономики каналов привлечения.</summary>
public sealed partial record ChannelMixResult : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var unprofitable = Channels.Where(c => c.Economics.LtvToCac < 1).ToList();
        double organicShare = TotalCustomers > 0
            ? Channels.Where(c => c.Economics.Cac <= 0).Sum(c => c.CustomerShare)
            : 0;
        double gap = PaidCac > 0 ? (PaidCac - BlendedCac) / PaidCac : 0;

        return new InterpretationBuilder("Экономика каналов привлечения")
            .Summary($"Смешанный CAC равен {Fmt.Money(BlendedCac)}, платный — {Fmt.Money(PaidCac)}: " +
                     $"разница {Fmt.Pct(gap)} создаётся органикой, которая даёт {Fmt.Pct(organicShare)} " +
                     $"клиентов бесплатно. Лучший канал — «{BestChannel}», худший — «{WorstChannel}».")
            .Metric("Blended CAC", Fmt.Money(BlendedCac), null,
                "все затраты на всех клиентов; для решений о бюджете непригоден",
                MetricQuality.Warning)
            .Metric("Paid CAC", Fmt.Money(PaidCac), null, "затраты платных каналов на их клиентов",
                MetricQuality.Good)
            .Metric("LTV / Paid CAC", LtvToPaidCac, null, "честное отношение для платного трафика",
                LtvToPaidCac >= 3 ? MetricQuality.Good : LtvToPaidCac >= 1 ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("Убыточных каналов", unprofitable.Count, null,
                "каналы с отношением ниже единицы",
                unprofitable.Count == 0 ? MetricQuality.Good : MetricQuality.Critical, 0)
            .Metric("Итог микса", Fmt.Money(TotalNetContribution), null,
                "суммарная маржа за вычетом всех затрат",
                TotalNetContribution > 0 ? MetricQuality.Good : MetricQuality.Critical)
            .FindingIf(unprofitable.Count > 0,
                $"Убыточные каналы: {string.Join(", ", unprofitable.Select(c => c.Name))}. " +
                "В смешанном показателе они не видны, потому что их закрывает органика.")
            .FindingIf(gap > 0.2,
                $"Смешанный CAC занижает стоимость привлечения на {Fmt.Pct(gap)}. Планирование " +
                "по нему приводит к финансированию каналов, которые не окупаются.")
            .FindingIf(organicShare > 0.4,
                $"Органика даёт {Fmt.Pct(organicShare)} клиентов. Это сильная позиция, " +
                "но масштабировать её напрямую бюджетом нельзя.")
            .Warning("Атрибуция клиента к каналу принята как данность. При атрибуции по " +
                     "последнему клику брендовый поиск забирает заслугу у верхних этапов воронки.")
            .Warning("Каналы считаются независимыми: каннибализация и отложенные конверсии " +
                     "не моделируются.")
            .RecommendationIf(unprofitable.Count > 0,
                "Сократите или перестройте убыточные каналы прежде, чем увеличивать общий бюджет.")
            .Recommendation("Сравнивайте каналы по итогу в деньгах, а не только по отношению " +
                            "LTV к CAC: канал с отличным отношением и десятком клиентов не масштабируется.")
            .Build();
    }
}
