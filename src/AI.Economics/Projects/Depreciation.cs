using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;

namespace AI.Economics.Projects;

/// <summary>Способ начисления амортизации.</summary>
public enum DepreciationMethod
{
    /// <summary>Линейный: равные суммы каждый период.</summary>
    StraightLine,

    /// <summary>Уменьшаемого остатка с заданным коэффициентом ускорения.</summary>
    DecliningBalance,

    /// <summary>По сумме чисел лет полезного использования.</summary>
    SumOfYearsDigits,

    /// <summary>Нелинейный по налоговому кодексу: фиксированная норма к остаточной стоимости.</summary>
    TaxNonLinear,
}

/// <summary>Начисление амортизации за один период.</summary>
/// <param name="Period">Номер периода.</param>
/// <param name="OpeningValue">Остаточная стоимость на начало.</param>
/// <param name="Charge">Начисленная амортизация.</param>
/// <param name="ClosingValue">Остаточная стоимость на конец.</param>
/// <param name="TaxShield">Экономия на налоге.</param>
/// <param name="DiscountedShield">Приведённая экономия на налоге.</param>
public sealed record DepreciationPeriod(
    int Period, double OpeningValue, double Charge, double ClosingValue,
    double TaxShield, double DiscountedShield);

/// <summary>График амортизации и связанный с ним налоговый щит.</summary>
public sealed record DepreciationSchedule : IInterpretable
{
    /// <summary>Способ начисления.</summary>
    public DepreciationMethod Method { get; init; }

    /// <summary>Начисления по периодам.</summary>
    public IReadOnlyList<DepreciationPeriod> Periods { get; init; } = [];

    /// <summary>Первоначальная стоимость.</summary>
    public double Cost { get; init; }

    /// <summary>Ликвидационная стоимость.</summary>
    public double Salvage { get; init; }

    /// <summary>Срок полезного использования.</summary>
    public int UsefulLife { get; init; }

    /// <summary>Ставка налога на прибыль.</summary>
    public double TaxRate { get; init; }

    /// <summary>Приведённая стоимость налогового щита.</summary>
    public double PresentValueOfShield { get; init; }

    /// <summary>Доля первоначальной стоимости, списанная в первой трети срока.</summary>
    public double FrontLoading { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool accelerated = Method != DepreciationMethod.StraightLine;
        double shieldShare = Cost > 0 ? PresentValueOfShield / Cost : 0;

        var builder = new InterpretationBuilder($"График амортизации: {MethodName()}")
            .Summary($"Стоимость {Fmt.Money(Cost)} списывается за {UsefulLife} периодов " +
                     $"до ликвидационной {Fmt.Money(Salvage)}. Приведённая стоимость " +
                     $"налогового щита {Fmt.Money(PresentValueOfShield)} — " +
                     $"{Fmt.Pct(shieldShare, 1)} первоначальных вложений. " +
                     $"В первой трети срока списывается {Fmt.Pct(FrontLoading, 0)} стоимости.")
            .Metric("Приведённый налоговый щит", Fmt.Money(PresentValueOfShield), null,
                $"{Fmt.Pct(shieldShare, 1)} стоимости актива", MetricQuality.Neutral)
            .Metric("Списано в первой трети срока", FrontLoading, null,
                accelerated ? "ускоренное списание сдвигает щит вперёд" : "равномерное списание",
                MetricQuality.Neutral, 3)
            .Metric("Срок полезного использования", UsefulLife, "периодов",
                $"ставка налога {Fmt.Pct(TaxRate, 0)}", MetricQuality.Neutral, 0);

        foreach (DepreciationPeriod period in Periods)
        {
            builder.Metric($"Период {period.Period}", period.Charge, null,
                $"остаток {Fmt.Money(period.ClosingValue)}, щит {Fmt.Money(period.TaxShield)}, " +
                $"приведённый {Fmt.Money(period.DiscountedShield)}",
                MetricQuality.Unknown, 0);
        }

        return builder
            .Finding("Амортизация не движение денег, но она уменьшает налог. Именно этот " +
                     "щит и есть её экономический смысл в оценке проекта: чем раньше " +
                     "списана стоимость, тем дороже стоит экономия.")
            .FindingIf(accelerated,
                $"Ускоренное списание переносит {Fmt.Pct(FrontLoading, 0)} стоимости " +
                "в первую треть срока. Общая сумма щита не меняется, но его приведённая " +
                "стоимость растёт — это и есть выгода от выбора метода.")
            .FindingIf(!accelerated,
                "Линейный метод даёт равномерный щит. Он проще в учёте, но приведённая " +
                "стоимость экономии при нём наименьшая среди допустимых способов.")
            .WarningIf(Salvage > 0 && Method == DepreciationMethod.DecliningBalance,
                "При методе уменьшаемого остатка ликвидационная стоимость достигается " +
                "не автоматически. График переключается на линейное досписание, " +
                "чтобы остаток не ушёл ниже неё.")
            .Warning("Налоговый щит реализуется только при наличии прибыли. У убыточного " +
                     "проекта он переносится на будущее и обесценивается, а при отсутствии " +
                     "прибыли в течение всего срока не реализуется вовсе.")
            .Recommendation("Сравнивайте методы по приведённой стоимости щита, а не по " +
                            "суммарной амортизации: она одинакова у всех способов.")
            .Recommendation("Проверьте, допускает ли учётная политика и налоговое " +
                            "законодательство выбранный метод для этой группы активов: " +
                            "экономия существует только в рамках разрешённого.")
            .Build();
    }

    /// <summary>Читаемое название метода.</summary>
    private string MethodName() => Method switch
    {
        DepreciationMethod.StraightLine => "линейный",
        DepreciationMethod.DecliningBalance => "уменьшаемого остатка",
        DepreciationMethod.SumOfYearsDigits => "по сумме чисел лет",
        _ => "нелинейный налоговый",
    };
}

/// <summary>
/// Начисление амортизации и связанный с ней налоговый щит.
/// </summary>
/// <remarks>
/// <para>
/// Суммарная амортизация одинакова при любом методе, поэтому выбор влияет не на
/// величину экономии, а на её распределение во времени:
/// </para>
/// <code>
/// линейный:         (Cost - Salvage) / N
/// уменьшаемого:     rate * OpeningValue,  rate = factor / N
/// сумма чисел лет:  (Cost - Salvage) * (N - t + 1) / (N * (N + 1) / 2)
/// щит:              Charge_t * tax / (1 + r)^t
/// </code>
/// <para>
/// Ускоренные методы сдвигают списание вперёд и повышают приведённую стоимость
/// щита. Это единственная причина, по которой имеет смысл выбирать метод: сама
/// прибыль от него не меняется, а денежный поток меняется.
/// </para>
/// <para>
/// Метод уменьшаемого остатка не доходит до ликвидационной стоимости за
/// конечное число шагов, поэтому в конце срока он переключается на линейное
/// досписание остатка — так поступают и учётные стандарты.
/// </para>
/// </remarks>
public static class Depreciation
{
    /// <summary>Строит график амортизации и налогового щита.</summary>
    /// <param name="cost">Первоначальная стоимость.</param>
    /// <param name="usefulLife">Срок полезного использования в периодах.</param>
    /// <param name="method">Способ начисления.</param>
    /// <param name="salvage">Ликвидационная стоимость.</param>
    /// <param name="taxRate">Ставка налога на прибыль.</param>
    /// <param name="discountRate">Ставка дисконтирования налогового щита.</param>
    /// <param name="factor">Коэффициент ускорения для метода уменьшаемого остатка.</param>
    /// <returns>График начислений и приведённая стоимость щита.</returns>
    /// <exception cref="ArgumentException">Стоимость или срок неположительны.</exception>
    public static DepreciationSchedule Build(
        double cost, int usefulLife, DepreciationMethod method = DepreciationMethod.StraightLine,
        double salvage = 0, double taxRate = 0.2, double discountRate = 0.12, double factor = 2)
    {
        if (cost <= 0) throw new ArgumentException("Стоимость должна быть положительной.", nameof(cost));
        if (usefulLife < 1) throw new ArgumentException("Срок должен быть не меньше периода.", nameof(usefulLife));
        if (salvage < 0 || salvage >= cost)
            throw new ArgumentException("Ликвидационная стоимость должна быть в пределах от нуля до стоимости.", nameof(salvage));

        var periods = new List<DepreciationPeriod>(usefulLife);
        double book = cost;
        double depreciable = cost - salvage;
        double digits = usefulLife * (usefulLife + 1) / 2.0;
        double shield = 0;

        for (int t = 1; t <= usefulLife; t++)
        {
            double charge = method switch
            {
                DepreciationMethod.StraightLine => depreciable / usefulLife,
                DepreciationMethod.SumOfYearsDigits => depreciable * (usefulLife - t + 1) / digits,
                DepreciationMethod.TaxNonLinear => book * (2.0 / usefulLife),
                _ => book * (factor / usefulLife),
            };

            if (method is DepreciationMethod.DecliningBalance or DepreciationMethod.TaxNonLinear)
            {
                // Ближе к концу срока ускоренное списание уступает линейному досписанию
                double remaining = usefulLife - t + 1;
                double linear = (book - salvage) / remaining;
                charge = Math.Max(charge, linear);
            }

            charge = Math.Min(charge, book - salvage);
            charge = Math.Max(charge, 0);

            double opening = book;
            book -= charge;

            double periodShield = charge * taxRate;
            double discounted = periodShield / Math.Pow(1 + discountRate, t);
            shield += discounted;

            periods.Add(new DepreciationPeriod(t, opening, charge, book, periodShield, discounted));
        }

        int third = Math.Max(1, usefulLife / 3);
        double frontLoaded = periods.Take(third).Sum(p => p.Charge) / depreciable;

        return new DepreciationSchedule
        {
            Method = method,
            Periods = periods,
            Cost = cost,
            Salvage = salvage,
            UsefulLife = usefulLife,
            TaxRate = taxRate,
            PresentValueOfShield = shield,
            FrontLoading = frontLoaded,
        };
    }

    /// <summary>Сравнивает приведённую стоимость щита по всем методам.</summary>
    /// <param name="cost">Первоначальная стоимость.</param>
    /// <param name="usefulLife">Срок полезного использования.</param>
    /// <param name="salvage">Ликвидационная стоимость.</param>
    /// <param name="taxRate">Ставка налога.</param>
    /// <param name="discountRate">Ставка дисконтирования.</param>
    /// <returns>Графики по всем методам, отсортированные по убыванию приведённого щита.</returns>
    public static IReadOnlyList<DepreciationSchedule> CompareMethods(
        double cost, int usefulLife, double salvage = 0, double taxRate = 0.2, double discountRate = 0.12)
    {
        var schedules = new List<DepreciationSchedule>();

        foreach (DepreciationMethod method in Enum.GetValues<DepreciationMethod>())
            schedules.Add(Build(cost, usefulLife, method, salvage, taxRate, discountRate));

        return [.. schedules.OrderByDescending(s => s.PresentValueOfShield)];
    }

    /// <summary>Ряд начислений для подстановки в модель денежного потока.</summary>
    /// <param name="schedule">График амортизации.</param>
    /// <returns>Начисления по периодам.</returns>
    /// <exception cref="ArgumentNullException">График не задан.</exception>
    public static Vector Charges(DepreciationSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var charges = new Vector(schedule.Periods.Count);
        for (int i = 0; i < schedule.Periods.Count; i++) charges[i] = schedule.Periods[i].Charge;

        return charges;
    }
}
