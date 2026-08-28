using System;
using System.Collections.Generic;
using System.Linq;

using AI.Economics.Insights;

namespace AI.Economics.UnitEconomics;

/// <summary>Канал привлечения со своими затратами, конверсией и качеством трафика.</summary>
public sealed record ChannelInput
{
    /// <summary>Название канала.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Затраты на канал за период.</summary>
    public double Spend { get; init; }

    /// <summary>Привлечено клиентов за период.</summary>
    public double NewCustomers { get; init; }

    /// <summary>Средний доход с клиента этого канала за период.</summary>
    public double RevenuePerPeriod { get; init; }

    /// <summary>Доля валовой маржи.</summary>
    public double GrossMarginRate { get; init; } = 1.0;

    /// <summary>Отток клиентов этого канала за период.</summary>
    public double ChurnRate { get; init; }

    /// <summary>Ставка дисконтирования за период.</summary>
    public double DiscountRate { get; init; }

    /// <summary>Горизонт расчёта в периодах; 0 — бесконечный.</summary>
    public int Horizon { get; init; } = 36;
}

/// <summary>Юнит-экономика одного канала вместе с его долей в миксе.</summary>
public sealed record ChannelResult
{
    /// <summary>Название канала.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Метрики канала.</summary>
    public UnitEconomicsResult Economics { get; init; } = new();

    /// <summary>Доля канала в общих затратах на привлечение.</summary>
    public double SpendShare { get; init; }

    /// <summary>Доля канала в привлечённых клиентах.</summary>
    public double CustomerShare { get; init; }

    /// <summary>Суммарный маржинальный вклад всех клиентов канала за вычетом затрат.</summary>
    public double TotalNetContribution { get; init; }
}

/// <summary>Сводка по всему миксу каналов привлечения.</summary>
public sealed partial record ChannelMixResult
{
    /// <summary>Разбивка по каналам, отсортированная по убыванию LTV/CAC.</summary>
    public IReadOnlyList<ChannelResult> Channels { get; init; } = [];

    /// <summary>Совокупные затраты на привлечение.</summary>
    public double TotalSpend { get; init; }

    /// <summary>Совокупное число привлечённых клиентов, включая органику.</summary>
    public double TotalCustomers { get; init; }

    /// <summary>
    /// Blended CAC: все затраты делённые на всех клиентов, включая пришедших
    /// бесплатно. Занижает стоимость привлечения и потому не годится для
    /// решений о бюджете — приводится ради сопоставимости с отчётностью.
    /// </summary>
    public double BlendedCac { get; init; }

    /// <summary>Paid CAC: затраты платных каналов на клиентов платных каналов.</summary>
    public double PaidCac { get; init; }

    /// <summary>Средневзвешенный по числу клиентов LTV.</summary>
    public double WeightedLtv { get; init; }

    /// <summary>Отношение средневзвешенного LTV к Paid CAC.</summary>
    public double LtvToPaidCac { get; init; }

    /// <summary>Суммарный маржинальный вклад микса за вычетом всех затрат.</summary>
    public double TotalNetContribution { get; init; }

    /// <summary>Лучший канал по LTV/CAC.</summary>
    public string? BestChannel { get; init; }

    /// <summary>Худший канал по LTV/CAC.</summary>
    public string? WorstChannel { get; init; }
}

/// <summary>
/// Юнит-экономика в разрезе каналов привлечения.
/// </summary>
/// <remarks>
/// Ключевая причина считать по каналам, а не «в среднем»: смешанный CAC
/// прячет убыточные каналы за прибыльными. Канал с LTV/CAC = 0,8 при доле
/// 30 % бюджета выглядит незаметно, если общий показатель равен 3,1.
/// </remarks>
public static class ChannelEconomics
{
    /// <summary>Считает юнит-экономику по каждому каналу и сводку по миксу.</summary>
    /// <param name="channels">Каналы привлечения.</param>
    /// <returns>Сводка микса с разбивкой по каналам.</returns>
    /// <exception cref="ArgumentNullException">Список каналов не задан.</exception>
    public static ChannelMixResult Analyze(IReadOnlyList<ChannelInput> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);

        double totalSpend = channels.Sum(c => c.Spend);
        double totalCustomers = channels.Sum(c => c.NewCustomers);
        double paidSpend = channels.Where(c => c.Spend > 0).Sum(c => c.Spend);
        double paidCustomers = channels.Where(c => c.Spend > 0).Sum(c => c.NewCustomers);

        var results = new List<ChannelResult>(channels.Count);
        double weightedLtv = 0;
        double totalNet = 0;

        foreach (ChannelInput c in channels)
        {
            var economics = UnitEconomicsCalculator.Compute(new UnitEconomicsInput
            {
                MarketingSpend = c.Spend,
                NewCustomers = c.NewCustomers,
                RevenuePerPeriod = c.RevenuePerPeriod,
                GrossMarginRate = c.GrossMarginRate,
                ChurnRate = c.ChurnRate,
                DiscountRate = c.DiscountRate,
                Horizon = c.Horizon,
            });

            double net = (economics.Ltv * c.NewCustomers) - c.Spend;
            totalNet += net;
            weightedLtv += economics.Ltv * c.NewCustomers;

            results.Add(new ChannelResult
            {
                Name = c.Name,
                Economics = economics,
                SpendShare = totalSpend > 0 ? c.Spend / totalSpend : 0,
                CustomerShare = totalCustomers > 0 ? c.NewCustomers / totalCustomers : 0,
                TotalNetContribution = net,
            });
        }

        List<ChannelResult> ordered = [.. results.OrderByDescending(r => r.Economics.LtvToCac)];
        double paidCac = paidCustomers > 0 ? paidSpend / paidCustomers : 0;
        double avgLtv = totalCustomers > 0 ? weightedLtv / totalCustomers : 0;

        return new ChannelMixResult
        {
            Channels = ordered,
            TotalSpend = totalSpend,
            TotalCustomers = totalCustomers,
            BlendedCac = totalCustomers > 0 ? totalSpend / totalCustomers : 0,
            PaidCac = paidCac,
            WeightedLtv = avgLtv,
            LtvToPaidCac = paidCac > 0 ? avgLtv / paidCac : double.PositiveInfinity,
            TotalNetContribution = totalNet,
            BestChannel = ordered.Count > 0 ? ordered[0].Name : null,
            WorstChannel = ordered.Count > 0 ? ordered[^1].Name : null,
        };
    }
}
