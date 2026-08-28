using System;

namespace AI.Economics.Statements;

/// <summary>
/// Финансовая отчётность компании за период: баланс, отчёт о прибылях и убытках
/// и денежный поток в объёме, достаточном для коэффициентного анализа,
/// моделей банкротства и форензики.
/// </summary>
/// <remarks>
/// Все суммы задаются в одной валюте и одном масштабе. Производные величины —
/// валовая прибыль, прибыль до амортизации, рабочий капитал, свободный
/// денежный поток — вычисляются, а не задаются, чтобы отчётность оставалась
/// внутренне согласованной.
/// </remarks>
public sealed record FinancialStatement
{
    /// <summary>Название компании.</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>Отчётный период.</summary>
    public string Period { get; init; } = string.Empty;

    // --- Баланс: активы ---

    /// <summary>Совокупные активы.</summary>
    public double TotalAssets { get; init; }

    /// <summary>Оборотные активы.</summary>
    public double CurrentAssets { get; init; }

    /// <summary>Денежные средства и эквиваленты.</summary>
    public double Cash { get; init; }

    /// <summary>Краткосрочные финансовые вложения.</summary>
    public double ShortTermInvestments { get; init; }

    /// <summary>Дебиторская задолженность.</summary>
    public double AccountsReceivable { get; init; }

    /// <summary>Запасы.</summary>
    public double Inventory { get; init; }

    /// <summary>Основные средства по остаточной стоимости.</summary>
    public double PropertyPlantEquipment { get; init; }

    /// <summary>Нематериальные активы, включая гудвил.</summary>
    public double IntangibleAssets { get; init; }

    // --- Баланс: пассивы ---

    /// <summary>Совокупные обязательства.</summary>
    public double TotalLiabilities { get; init; }

    /// <summary>Краткосрочные обязательства.</summary>
    public double CurrentLiabilities { get; init; }

    /// <summary>Кредиторская задолженность.</summary>
    public double AccountsPayable { get; init; }

    /// <summary>Краткосрочный долг.</summary>
    public double ShortTermDebt { get; init; }

    /// <summary>Долгосрочный долг.</summary>
    public double LongTermDebt { get; init; }

    /// <summary>Нераспределённая прибыль.</summary>
    public double RetainedEarnings { get; init; }

    // --- Отчёт о прибылях и убытках ---

    /// <summary>Выручка.</summary>
    public double Revenue { get; init; }

    /// <summary>Себестоимость продаж.</summary>
    public double CostOfGoodsSold { get; init; }

    /// <summary>Коммерческие и управленческие расходы.</summary>
    public double OperatingExpenses { get; init; }

    /// <summary>Амортизация.</summary>
    public double Depreciation { get; init; }

    /// <summary>Процентные расходы.</summary>
    public double InterestExpense { get; init; }

    /// <summary>Налог на прибыль.</summary>
    public double IncomeTax { get; init; }

    /// <summary>Чистая прибыль.</summary>
    public double NetIncome { get; init; }

    // --- Денежный поток ---

    /// <summary>Денежный поток от операционной деятельности.</summary>
    public double OperatingCashFlow { get; init; }

    /// <summary>Капитальные затраты, положительным числом.</summary>
    public double CapitalExpenditures { get; init; }

    /// <summary>Выплаченные дивиденды, положительным числом.</summary>
    public double DividendsPaid { get; init; }

    // --- Рыночные данные ---

    /// <summary>Рыночная капитализация; нужна для модели Альтмана для публичных компаний.</summary>
    public double MarketCapitalization { get; init; }

    // --- Производные величины ---

    /// <summary>Собственный капитал: активы за вычетом обязательств.</summary>
    public double Equity => TotalAssets - TotalLiabilities;

    /// <summary>Валовая прибыль.</summary>
    public double GrossProfit => Revenue - CostOfGoodsSold;

    /// <summary>Операционная прибыль до процентов и налогов.</summary>
    public double OperatingIncome => GrossProfit - OperatingExpenses - Depreciation;

    /// <summary>Прибыль до процентов, налогов и амортизации.</summary>
    public double Ebitda => OperatingIncome + Depreciation;

    /// <summary>Прибыль до налогообложения.</summary>
    public double PretaxIncome => OperatingIncome - InterestExpense;

    /// <summary>Совокупный долг.</summary>
    public double TotalDebt => ShortTermDebt + LongTermDebt;

    /// <summary>Чистый долг за вычетом денежных средств и вложений.</summary>
    public double NetDebt => TotalDebt - Cash - ShortTermInvestments;

    /// <summary>Рабочий капитал.</summary>
    public double WorkingCapital => CurrentAssets - CurrentLiabilities;

    /// <summary>Свободный денежный поток.</summary>
    public double FreeCashFlow => OperatingCashFlow - CapitalExpenditures;

    /// <summary>Внеоборотные активы.</summary>
    public double NonCurrentAssets => TotalAssets - CurrentAssets;

    /// <summary>Долгосрочные обязательства.</summary>
    public double NonCurrentLiabilities => TotalLiabilities - CurrentLiabilities;

    /// <summary>Проверяет базовую согласованность отчётности.</summary>
    /// <returns>Список выявленных нарушений; пустой список означает, что грубых ошибок нет.</returns>
    public string[] Validate()
    {
        var problems = new System.Collections.Generic.List<string>();

        if (TotalAssets <= 0) problems.Add("совокупные активы неположительны");
        if (CurrentAssets > TotalAssets) problems.Add("оборотные активы превышают совокупные");
        if (CurrentLiabilities > TotalLiabilities) problems.Add("краткосрочные обязательства превышают совокупные");
        if (Revenue < 0) problems.Add("выручка отрицательна");
        if (Math.Abs(Cash) > TotalAssets) problems.Add("денежные средства превышают активы");
        if (Equity < 0) problems.Add("собственный капитал отрицателен");

        return [.. problems];
    }
}
