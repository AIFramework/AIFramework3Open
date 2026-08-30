using System;
using System.Collections.Generic;
using System.Linq;
using AI.Insights;

namespace AI.Economics.Corporate;

/// <summary>Подразделение или направление бизнеса.</summary>
public sealed record BusinessUnit
{
    /// <summary>Название подразделения.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Выручка.</summary>
    public double Revenue { get; init; }

    /// <summary>Операционная прибыль до процентов и налогов.</summary>
    public double OperatingProfit { get; init; }

    /// <summary>Инвестированный капитал.</summary>
    public double InvestedCapital { get; init; }

    /// <summary>Эффективная ставка налога.</summary>
    public double TaxRate { get; init; } = 0.2;

    /// <summary>Стоимость капитала подразделения; при нуле берётся общая по компании.</summary>
    public double CostOfCapital { get; init; }
}

/// <summary>Результат по одному подразделению.</summary>
/// <param name="Name">Название подразделения.</param>
/// <param name="Nopat">Прибыль после налога до процентов.</param>
/// <param name="InvestedCapital">Инвестированный капитал.</param>
/// <param name="Roic">Рентабельность инвестированного капитала.</param>
/// <param name="CostOfCapital">Применённая стоимость капитала.</param>
/// <param name="Spread">Разница между рентабельностью и стоимостью капитала.</param>
/// <param name="EconomicProfit">Экономическая добавленная стоимость.</param>
/// <param name="CapitalShare">Доля подразделения в капитале компании.</param>
public sealed record UnitEconomicProfit(
    string Name, double Nopat, double InvestedCapital, double Roic,
    double CostOfCapital, double Spread, double EconomicProfit, double CapitalShare);

/// <summary>Свод экономической добавленной стоимости по компании.</summary>
public sealed record EconomicProfitResult : IInterpretable
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Результаты по подразделениям.</summary>
    public IReadOnlyList<UnitEconomicProfit> Units { get; init; } = [];

    /// <summary>Суммарная экономическая добавленная стоимость.</summary>
    public double TotalEconomicProfit { get; init; }

    /// <summary>Суммарная прибыль после налога.</summary>
    public double TotalNopat { get; init; }

    /// <summary>Суммарный инвестированный капитал.</summary>
    public double TotalCapital { get; init; }

    /// <summary>Рентабельность инвестированного капитала по компании.</summary>
    public double Roic => TotalCapital > 0 ? TotalNopat / TotalCapital : 0;

    /// <summary>Средневзвешенная стоимость капитала.</summary>
    public double Wacc { get; init; }

    /// <summary>Разница между рентабельностью и стоимостью капитала.</summary>
    public double Spread => Roic - Wacc;

    /// <summary>Капитал, размещённый в подразделениях с отрицательной добавленной стоимостью.</summary>
    public double CapitalDestroying =>
        Units.Where(u => u.EconomicProfit < 0).Sum(u => u.InvestedCapital);

    /// <summary>Стоимость, которую создало бы перераспределение капитала в лучшее подразделение.</summary>
    public double ReallocationUpside { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        UnitEconomicProfit? best = Units.OrderByDescending(u => u.Spread).FirstOrDefault();
        UnitEconomicProfit? worst = Units.OrderBy(u => u.Spread).FirstOrDefault();

        var destroyers = Units.Where(u => u.EconomicProfit < 0).ToList();
        double destroyingShare = TotalCapital > 0 ? CapitalDestroying / TotalCapital : 0;

        var builder = new InterpretationBuilder($"Экономическая добавленная стоимость: {Company}")
            .Summary($"Рентабельность инвестированного капитала {Fmt.Pct(Roic, 2)} против стоимости " +
                     $"капитала {Fmt.Pct(Wacc, 2)} — спред {Fmt.Pct(Spread, 2)}. Экономическая " +
                     $"прибыль {Fmt.Money(TotalEconomicProfit)} при капитале {Fmt.Money(TotalCapital)}. " +
                     $"Стоимость разрушают {destroyers.Count} из {Units.Count} подразделений, " +
                     $"на них приходится {Fmt.Pct(destroyingShare, 0)} капитала.")
            .Metric("Экономическая прибыль", Fmt.Money(TotalEconomicProfit), null,
                "прибыль сверх платы за капитал",
                TotalEconomicProfit > 0 ? MetricQuality.Good : MetricQuality.Critical)
            .Metric("Рентабельность капитала", Roic, null,
                $"стоимость капитала {Fmt.Pct(Wacc, 2)}",
                Roic > Wacc ? MetricQuality.Good : MetricQuality.Critical, 4)
            .Metric("Спред", Spread, null,
                Spread > 0 ? "компания создаёт стоимость" : "компания разрушает стоимость",
                Spread > 0.05 ? MetricQuality.Good : Spread > 0 ? MetricQuality.Neutral : MetricQuality.Critical, 4)
            .Metric("Капитал в убыточных подразделениях", Fmt.Money(CapitalDestroying), null,
                $"{Fmt.Pct(destroyingShare, 0)} инвестированного капитала",
                destroyingShare > 0.3 ? MetricQuality.Critical
                    : destroyingShare > 0 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Потенциал перераспределения", Fmt.Money(ReallocationUpside), null,
                "если перевести капитал из худшего подразделения в лучшее",
                ReallocationUpside > 0 ? MetricQuality.Warning : MetricQuality.Good);

        foreach (UnitEconomicProfit unit in Units.OrderByDescending(u => u.EconomicProfit))
        {
            builder.Metric(unit.Name, unit.EconomicProfit, null,
                $"ROIC {Fmt.Pct(unit.Roic, 1)} против {Fmt.Pct(unit.CostOfCapital, 1)}, " +
                $"спред {Fmt.Pct(unit.Spread, 1)}, капитал {Fmt.Money(unit.InvestedCapital)} " +
                $"({Fmt.Pct(unit.CapitalShare, 0)})",
                unit.EconomicProfit > 0 ? MetricQuality.Good : MetricQuality.Warning, 0);
        }

        return builder
            .Finding("Прибыль сама по себе ничего не говорит: подразделение может быть " +
                     "прибыльным и при этом разрушать стоимость, если не окупает капитал, " +
                     "который в нём заперт. Экономическая прибыль исправляет именно это.")
            .FindingIf(best is not null && worst is not null,
                $"Разрыв между «{best?.Name}» (спред {Fmt.Pct(best?.Spread ?? 0, 1)}) и " +
                $"«{worst?.Name}» ({Fmt.Pct(worst?.Spread ?? 0, 1)}) составляет " +
                $"{Fmt.Pct((best?.Spread ?? 0) - (worst?.Spread ?? 0), 1)}. Это и есть " +
                "внутренний резерв компании: перераспределение капитала не требует " +
                "ни роста рынка, ни новых вложений.")
            .FindingIf(TotalEconomicProfit > 0 && destroyers.Count > 0,
                "Компания в целом создаёт стоимость, но отдельные подразделения её " +
                "разрушают. На консолидированном уровне это не видно — сильные " +
                "направления компенсируют слабые.")
            .WarningIf(Spread <= 0,
                $"Рентабельность капитала {Fmt.Pct(Roic, 2)} ниже его стоимости " +
                $"{Fmt.Pct(Wacc, 2)}. Компания уничтожает стоимость: рост в такой " +
                "конфигурации только ускоряет потери.")
            .WarningIf(destroyingShare > 0.3,
                $"В подразделениях с отрицательной экономической прибылью заперто " +
                $"{Fmt.Pct(destroyingShare, 0)} капитала. Это первый кандидат " +
                "на продажу или закрытие.")
            .Warning("Инвестированный капитал по балансу отличается от экономического: " +
                     "он не учитывает арендованные активы, капитализированные затраты " +
                     "на исследования и накопленную амортизацию гудвила. Разные способы " +
                     "его расчёта меняют вывод по подразделению на противоположный.")
            .Recommendation("Ставьте цели подразделениям по экономической прибыли, а не " +
                            "по выручке или марже: только она делает капитал платным " +
                            "в глазах руководителя направления.")
            .Recommendation("Применяйте разную стоимость капитала к разным по риску " +
                            "направлениям. Единая ставка субсидирует рискованные " +
                            "подразделения за счёт стабильных.")
            .Build();
    }
}

/// <summary>
/// Экономическая добавленная стоимость и рентабельность инвестированного
/// капитала по подразделениям.
/// </summary>
/// <remarks>
/// <para>
/// Бухгалтерская прибыль не вычитает плату за собственный капитал, поэтому
/// прибыльное подразделение может разрушать стоимость. Экономическая прибыль
/// исправляет это:
/// </para>
/// <code>
/// NOPAT = EBIT * (1 - tax)
/// ROIC  = NOPAT / InvestedCapital
/// EVA   = (ROIC - WACC) * InvestedCapital
/// </code>
/// <para>
/// Спред между рентабельностью и стоимостью капитала — единственная величина,
/// которая определяет, создаёт ли направление стоимость. Рост при
/// отрицательном спреде ускоряет потери, а не улучшает результат.
/// </para>
/// <para>
/// Разложение по подразделениям обычно и даёт главный вывод: на
/// консолидированном уровне сильные направления компенсируют слабые, и
/// разрушение стоимости становится видно только после разнесения капитала.
/// </para>
/// </remarks>
public static class EconomicValueAdded
{
    /// <summary>Считает экономическую прибыль по компании и подразделениям.</summary>
    /// <param name="company">Название компании.</param>
    /// <param name="units">Подразделения.</param>
    /// <param name="wacc">Средневзвешенная стоимость капитала компании.</param>
    /// <returns>Экономическая прибыль, спреды и потенциал перераспределения капитала.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Список подразделений пуст.</exception>
    public static EconomicProfitResult Compute(
        string company, IReadOnlyList<BusinessUnit> units, double wacc)
    {
        ArgumentNullException.ThrowIfNull(units);
        if (units.Count == 0) throw new ArgumentException("Список подразделений пуст.", nameof(units));

        double totalCapital = units.Sum(u => u.InvestedCapital);
        if (totalCapital <= 0)
            throw new ArgumentException("Инвестированный капитал должен быть положительным.", nameof(units));

        var results = new List<UnitEconomicProfit>(units.Count);

        foreach (BusinessUnit unit in units)
        {
            double nopat = unit.OperatingProfit * (1 - Math.Clamp(unit.TaxRate, 0, 0.6));
            double capital = Math.Max(unit.InvestedCapital, 1e-9);
            double rate = unit.CostOfCapital > 0 ? unit.CostOfCapital : wacc;
            double roic = nopat / capital;

            results.Add(new UnitEconomicProfit(
                unit.Name, nopat, unit.InvestedCapital, roic, rate, roic - rate,
                (roic - rate) * unit.InvestedCapital, unit.InvestedCapital / totalCapital));
        }

        UnitEconomicProfit best = results.OrderByDescending(u => u.Spread).First();
        UnitEconomicProfit worst = results.OrderBy(u => u.Spread).First();

        // Сколько добавил бы перевод капитала худшего подразделения в лучшее
        double upside = worst.Spread < best.Spread
            ? (best.Spread - worst.Spread) * worst.InvestedCapital
            : 0;

        return new EconomicProfitResult
        {
            Company = company,
            Units = results,
            TotalEconomicProfit = results.Sum(u => u.EconomicProfit),
            TotalNopat = results.Sum(u => u.Nopat),
            TotalCapital = totalCapital,
            Wacc = wacc,
            ReallocationUpside = upside,
        };
    }

    /// <summary>Приведённая стоимость будущей экономической прибыли.</summary>
    /// <param name="currentEconomicProfit">Текущая экономическая прибыль.</param>
    /// <param name="growth">Темп роста экономической прибыли.</param>
    /// <param name="wacc">Стоимость капитала.</param>
    /// <param name="horizon">Горизонт в годах; при нуле считается вечная рента.</param>
    /// <returns>Приведённая стоимость экономической прибыли — премия к инвестированному капиталу.</returns>
    /// <exception cref="ArgumentException">Темп роста не меньше ставки при бесконечном горизонте.</exception>
    public static double MarketValueAdded(
        double currentEconomicProfit, double growth, double wacc, int horizon = 0)
    {
        if (horizon <= 0)
        {
            if (growth >= wacc)
                throw new ArgumentException("Темп роста должен быть меньше стоимости капитала.", nameof(growth));

            return currentEconomicProfit * (1 + growth) / (wacc - growth);
        }

        double total = 0;
        double profit = currentEconomicProfit;

        for (int year = 1; year <= horizon; year++)
        {
            profit *= 1 + growth;
            total += profit / Math.Pow(1 + wacc, year);
        }

        return total;
    }
}
