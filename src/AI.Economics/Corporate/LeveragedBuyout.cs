using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;

namespace AI.Economics.Corporate;

/// <summary>Транш долга в структуре финансирования сделки.</summary>
/// <param name="Name">Название транша.</param>
/// <param name="Amount">Сумма привлечения.</param>
/// <param name="Rate">Ставка.</param>
/// <param name="AmortizationRate">Доля тела, погашаемая ежегодно по графику.</param>
/// <param name="Seniority">Очерёдность: меньше — старше.</param>
/// <param name="CashSweep">Участвует ли транш в досрочном погашении свободным потоком.</param>
public sealed record DebtTranche(
    string Name, double Amount, double Rate, double AmortizationRate, int Seniority, bool CashSweep = true);

/// <summary>Состояние сделки в конкретном году.</summary>
/// <param name="Year">Номер года.</param>
/// <param name="Ebitda">Прибыль до процентов, налогов и амортизации.</param>
/// <param name="Interest">Начисленные проценты.</param>
/// <param name="Amortization">Плановое погашение тела.</param>
/// <param name="CashSweep">Досрочное погашение свободным потоком.</param>
/// <param name="ClosingDebt">Долг на конец года.</param>
/// <param name="Leverage">Долг к прибыли до амортизации.</param>
/// <param name="InterestCoverage">Покрытие процентов.</param>
public sealed record LboYear(
    int Year, double Ebitda, double Interest, double Amortization,
    double CashSweep, double ClosingDebt, double Leverage, double InterestCoverage);

/// <summary>Нарушение ковенанта.</summary>
/// <param name="Year">Год нарушения.</param>
/// <param name="Covenant">Название ковенанта.</param>
/// <param name="Value">Фактическое значение.</param>
/// <param name="Limit">Предельное значение.</param>
public sealed record CovenantBreach(int Year, string Covenant, double Value, double Limit);

/// <summary>Входные данные модели выкупа за счёт долга.</summary>
public sealed record LboInput
{
    /// <summary>Название сделки.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Прибыль до амортизации на входе в сделку.</summary>
    public double EntryEbitda { get; init; } = 100;

    /// <summary>Мультипликатор входа.</summary>
    public double EntryMultiple { get; init; } = 7;

    /// <summary>Мультипликатор выхода.</summary>
    public double ExitMultiple { get; init; } = 7;

    /// <summary>Срок владения в годах.</summary>
    public int HoldingPeriod { get; init; } = 5;

    /// <summary>Годовой темп роста прибыли.</summary>
    public double EbitdaGrowth { get; init; } = 0.08;

    /// <summary>Доля прибыли, уходящая на капитальные затраты и оборотный капитал.</summary>
    public double CashConversionDrag { get; init; } = 0.25;

    /// <summary>Эффективная ставка налога.</summary>
    public double TaxRate { get; init; } = 0.2;

    /// <summary>Транши долга.</summary>
    public IReadOnlyList<DebtTranche> Tranches { get; init; } = [];

    /// <summary>Предельная долговая нагрузка по ковенанту.</summary>
    public double MaxLeverage { get; init; } = 5.0;

    /// <summary>Минимальное покрытие процентов по ковенанту.</summary>
    public double MinInterestCoverage { get; init; } = 2.0;
}

/// <summary>Результат моделирования выкупа за счёт долга.</summary>
public sealed record LboResult : IInterpretable
{
    /// <summary>Название сделки.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Динамика сделки по годам.</summary>
    public IReadOnlyList<LboYear> Schedule { get; init; } = [];

    /// <summary>Цена входа.</summary>
    public double EntryValue { get; init; }

    /// <summary>Вложение собственных средств.</summary>
    public double EquityInvested { get; init; }

    /// <summary>Стоимость бизнеса на выходе.</summary>
    public double ExitValue { get; init; }

    /// <summary>Поступление собственнику на выходе.</summary>
    public double EquityProceeds { get; init; }

    /// <summary>Мультипликатор возврата на вложенный капитал.</summary>
    public double MoneyMultiple => EquityInvested > 0 ? EquityProceeds / EquityInvested : 0;

    /// <summary>Внутренняя норма доходности сделки.</summary>
    public double Irr { get; init; }

    /// <summary>Нарушенные ковенанты.</summary>
    public IReadOnlyList<CovenantBreach> Breaches { get; init; } = [];

    /// <summary>Вклад роста прибыли в прирост стоимости капитала.</summary>
    public double GrowthContribution { get; init; }

    /// <summary>Вклад погашения долга в прирост стоимости капитала.</summary>
    public double DeleveragingContribution { get; init; }

    /// <summary>Вклад изменения мультипликатора в прирост стоимости капитала.</summary>
    public double MultipleContribution { get; init; }

    /// <summary>Долговая нагрузка на входе.</summary>
    public double EntryLeverage { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double total = Math.Abs(GrowthContribution) + Math.Abs(DeleveragingContribution)
            + Math.Abs(MultipleContribution);

        string mainDriver = total <= 0 ? "не определён"
            : Math.Abs(GrowthContribution) >= Math.Abs(DeleveragingContribution)
              && Math.Abs(GrowthContribution) >= Math.Abs(MultipleContribution) ? "рост прибыли"
            : Math.Abs(DeleveragingContribution) >= Math.Abs(MultipleContribution) ? "погашение долга"
            : "изменение мультипликатора";

        var builder = new InterpretationBuilder($"Выкуп за счёт долга: {Name}")
            .Summary($"Вход по {Fmt.Money(EntryValue)}, собственных средств " +
                     $"{Fmt.Money(EquityInvested)} при нагрузке {Fmt.Num(EntryLeverage, 1)}x. " +
                     $"Выход через {Schedule.Count} лет по {Fmt.Money(ExitValue)}, " +
                     $"поступление собственнику {Fmt.Money(EquityProceeds)}: " +
                     $"{Fmt.Num(MoneyMultiple, 2)}x за {Fmt.Pct(Irr, 1)} годовых. " +
                     $"Главный источник доходности — {mainDriver}. " +
                     $"Нарушений ковенантов: {Breaches.Count}.")
            .Metric("Внутренняя доходность", Irr, null,
                $"{Fmt.Num(MoneyMultiple, 2)}x на вложенный капитал",
                Irr > 0.2 ? MetricQuality.Good : Irr > 0.12 ? MetricQuality.Neutral : MetricQuality.Warning, 4)
            .Metric("Мультипликатор возврата", MoneyMultiple, "×",
                "во сколько раз выросли вложенные средства",
                MoneyMultiple > 2 ? MetricQuality.Good : MetricQuality.Neutral, 2)
            .Metric("Нагрузка на входе", EntryLeverage, "×",
                "долг к прибыли до амортизации",
                EntryLeverage > 6 ? MetricQuality.Critical
                    : EntryLeverage > 4.5 ? MetricQuality.Warning : MetricQuality.Good, 2)
            .Metric("Вклад роста", Fmt.Money(GrowthContribution), null, "прирост прибыли за период владения")
            .Metric("Вклад погашения долга", Fmt.Money(DeleveragingContribution), null,
                "сокращение долга свободным потоком")
            .Metric("Вклад мультипликатора", Fmt.Money(MultipleContribution), null,
                MultipleContribution > 0 ? "выход дороже входа" : "выход дешевле входа",
                Math.Abs(MultipleContribution) > Math.Abs(GrowthContribution)
                    ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Нарушений ковенантов", Breaches.Count, null,
                Breaches.Count == 0 ? "график обслуживается" : "нужна реструктуризация или доп. капитал",
                Breaches.Count == 0 ? MetricQuality.Good : MetricQuality.Critical, 0);

        foreach (LboYear year in Schedule)
        {
            builder.Metric($"Год {year.Year}", year.Leverage, "×",
                $"прибыль {Fmt.Money(year.Ebitda)}, проценты {Fmt.Money(year.Interest)}, " +
                $"долг на конец {Fmt.Money(year.ClosingDebt)}, покрытие {Fmt.Num(year.InterestCoverage, 1)}",
                year.Leverage > 5 ? MetricQuality.Warning : MetricQuality.Neutral, 2);
        }

        foreach (CovenantBreach breach in Breaches)
        {
            builder.Metric($"Нарушение, год {breach.Year}", breach.Value, null,
                $"{breach.Covenant}: предел {Fmt.Num(breach.Limit, 2)}", MetricQuality.Critical, 2);
        }

        return builder
            .Finding($"Доходность сделки раскладывается на три источника: рост прибыли " +
                     $"({Fmt.Money(GrowthContribution)}), погашение долга " +
                     $"({Fmt.Money(DeleveragingContribution)}) и изменение мультипликатора " +
                     $"({Fmt.Money(MultipleContribution)}). Только первые два зависят " +
                     "от работы с активом.")
            .FindingIf(Math.Abs(MultipleContribution) > Math.Abs(GrowthContribution),
                "Основную часть доходности даёт изменение мультипликатора, то есть ставка " +
                "на рынок, а не на компанию. Такая сделка выигрывает или проигрывает " +
                "вместе с циклом оценок.")
            .FindingIf(Breaches.Count == 0,
                "График обслуживания долга выдерживается на всём горизонте, ковенанты " +
                "не нарушаются.")
            .WarningIf(Breaches.Count > 0,
                $"Ковенанты нарушаются в {Breaches.Select(b => b.Year).Distinct().Count()} годах. " +
                "На практике это означает переговоры с банками, дополнительное вложение " +
                "капитала или потерю контроля над активом.")
            .WarningIf(EntryLeverage > 5.5,
                $"Нагрузка на входе {Fmt.Num(EntryLeverage, 1)}x. При таком уровне сделка " +
                "чувствительна к падению прибыли: снижение на четверть съедает весь " +
                "запас по ковенантам.")
            .WarningIf(Irr > 0.35,
                $"Доходность {Fmt.Pct(Irr, 1)} выглядит завышенной. Проверьте предпосылки " +
                "о росте и мультипликаторе выхода: обычно такая цифра получается " +
                "из оптимистичного сценария по обеим сразу.")
            .Warning("Модель предполагает, что весь свободный поток идёт на погашение долга " +
                     "и что рефинансирование доступно. В спад оба допущения нарушаются " +
                     "одновременно, и именно это разрушает сделки такого типа.")
            .Recommendation("Считайте сценарий с равными мультипликаторами входа и выхода: " +
                            "он показывает доходность, которая зависит только от работы " +
                            "с компанией.")
            .Recommendation("Проверьте запас по ковенантам при падении прибыли на треть. " +
                            "Это и есть настоящая мера риска сделки, а не расчётная доходность.")
            .Build();
    }
}

/// <summary>
/// Модель выкупа компании за счёт заёмных средств.
/// </summary>
/// <remarks>
/// <para>
/// Сделка строится на трёх источниках доходности: рост прибыли за период
/// владения, погашение долга свободным потоком и изменение мультипликатора
/// между входом и выходом. Разложение доходности на эти составляющие —
/// главный аналитический результат модели.
/// </para>
/// <para>
/// График долга рассчитывается по годам: начисляются проценты, гасится плановая
/// амортизация, а остаток свободного потока направляется на досрочное
/// погашение в порядке старшинства траншей.
/// </para>
/// <code>
/// FCF = EBITDA * (1 - drag) - Interest * (1 - tax)
/// Debt_{t+1} = Debt_t - Amortization_t - CashSweep_t
/// Equity_exit = EBITDA_T * ExitMultiple - Debt_T
/// </code>
/// <para>
/// Ковенанты проверяются каждый год: предельная долговая нагрузка и минимальное
/// покрытие процентов. Нарушение любого из них означает не арифметическую
/// ошибку, а необходимость переговоров с кредиторами — и именно запас по
/// ковенантам, а не расчётная доходность, определяет риск сделки.
/// </para>
/// </remarks>
public static class LeveragedBuyout
{
    /// <summary>Рассчитывает сделку и график обслуживания долга.</summary>
    /// <param name="input">Параметры сделки и структура долга.</param>
    /// <returns>График по годам, доходность и разложение её источников.</returns>
    /// <exception cref="ArgumentNullException">Входные данные не заданы.</exception>
    /// <exception cref="ArgumentException">Прибыль или срок владения неположительны.</exception>
    public static LboResult Run(LboInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.EntryEbitda <= 0)
            throw new ArgumentException("Прибыль на входе должна быть положительной.", nameof(input));
        if (input.HoldingPeriod < 1)
            throw new ArgumentException("Срок владения должен быть не меньше года.", nameof(input));

        double entryValue = input.EntryEbitda * input.EntryMultiple;
        var balances = input.Tranches.OrderBy(t => t.Seniority).Select(t => t.Amount).ToArray();
        var tranches = input.Tranches.OrderBy(t => t.Seniority).ToArray();

        double totalDebt = balances.Sum();
        double equityInvested = Math.Max(entryValue - totalDebt, 0);
        double entryLeverage = totalDebt / input.EntryEbitda;

        var schedule = new List<LboYear>(input.HoldingPeriod);
        var breaches = new List<CovenantBreach>();
        double ebitda = input.EntryEbitda;

        for (int year = 1; year <= input.HoldingPeriod; year++)
        {
            ebitda *= 1 + input.EbitdaGrowth;

            double interest = 0;
            for (int i = 0; i < tranches.Length; i++) interest += balances[i] * tranches[i].Rate;

            double amortization = 0;
            for (int i = 0; i < tranches.Length; i++)
            {
                double payment = Math.Min(tranches[i].Amount * tranches[i].AmortizationRate, balances[i]);
                balances[i] -= payment;
                amortization += payment;
            }

            // Свободный поток после процентов с учётом налогового щита
            double available = (ebitda * (1 - input.CashConversionDrag))
                - (interest * (1 - input.TaxRate)) - amortization;

            double sweep = 0;
            if (available > 0)
            {
                double remaining = available;

                for (int i = 0; i < tranches.Length && remaining > 0; i++)
                {
                    if (!tranches[i].CashSweep) continue;

                    double payment = Math.Min(remaining, balances[i]);
                    balances[i] -= payment;
                    remaining -= payment;
                    sweep += payment;
                }
            }

            double closing = balances.Sum();
            double leverage = closing / ebitda;
            double coverage = interest > 0 ? ebitda / interest : 99;

            schedule.Add(new LboYear(year, ebitda, interest, amortization, sweep, closing, leverage, coverage));

            if (leverage > input.MaxLeverage)
                breaches.Add(new CovenantBreach(year, "долг к прибыли", leverage, input.MaxLeverage));
            if (coverage < input.MinInterestCoverage)
                breaches.Add(new CovenantBreach(year, "покрытие процентов", coverage, input.MinInterestCoverage));
        }

        double exitValue = ebitda * input.ExitMultiple;
        double exitDebt = balances.Sum();
        double proceeds = Math.Max(exitValue - exitDebt, 0);

        // Разложение прироста стоимости капитала на три источника
        double growth = (ebitda - input.EntryEbitda) * input.EntryMultiple;
        double multiple = ebitda * (input.ExitMultiple - input.EntryMultiple);
        double deleveraging = totalDebt - exitDebt;

        return new LboResult
        {
            Name = input.Name,
            Schedule = schedule,
            EntryValue = entryValue,
            EquityInvested = equityInvested,
            ExitValue = exitValue,
            EquityProceeds = proceeds,
            Irr = equityInvested > 0 && proceeds > 0
                ? Math.Pow(proceeds / equityInvested, 1.0 / input.HoldingPeriod) - 1
                : -1,
            Breaches = breaches,
            GrowthContribution = growth,
            DeleveragingContribution = deleveraging,
            MultipleContribution = multiple,
            EntryLeverage = entryLeverage,
        };
    }

    /// <summary>Подбирает максимальную цену входа при целевой доходности.</summary>
    /// <param name="input">Параметры сделки.</param>
    /// <param name="targetIrr">Целевая внутренняя доходность.</param>
    /// <param name="maxMultiple">Верхняя граница поиска мультипликатора входа.</param>
    /// <returns>Мультипликатор входа, при котором доходность равна целевой.</returns>
    /// <exception cref="ArgumentNullException">Входные данные не заданы.</exception>
    public static double MaximumEntryMultiple(LboInput input, double targetIrr = 0.20, double maxMultiple = 20)
    {
        ArgumentNullException.ThrowIfNull(input);

        double low = 0.5, high = maxMultiple;

        // Доходность монотонно убывает по цене входа: решается делением отрезка
        for (int i = 0; i < 60; i++)
        {
            double mid = (low + high) / 2;
            double irr = Run(input with { EntryMultiple = mid }).Irr;

            if (irr > targetIrr) low = mid;
            else high = mid;
        }

        return (low + high) / 2;
    }

    /// <summary>Строит сетку доходности по мультипликаторам входа и выхода.</summary>
    /// <param name="input">Параметры сделки.</param>
    /// <param name="entryMultiples">Мультипликаторы входа.</param>
    /// <param name="exitMultiples">Мультипликаторы выхода.</param>
    /// <returns>Матрица доходности: строка — вход, столбец — выход.</returns>
    /// <exception cref="ArgumentNullException">Аргументы не заданы.</exception>
    public static Matrix Sensitivity(
        LboInput input, IReadOnlyList<double> entryMultiples, IReadOnlyList<double> exitMultiples)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(entryMultiples);
        ArgumentNullException.ThrowIfNull(exitMultiples);

        var grid = new Matrix(entryMultiples.Count, exitMultiples.Count);

        for (int i = 0; i < entryMultiples.Count; i++)
            for (int j = 0; j < exitMultiples.Count; j++)
                grid[i, j] = Run(input with
                {
                    EntryMultiple = entryMultiples[i],
                    ExitMultiple = exitMultiples[j],
                }).Irr;

        return grid;
    }
}
