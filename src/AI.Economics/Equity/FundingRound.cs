using System;
using System.Collections.Generic;
using System.Linq;

using AI.Economics.Insights;

namespace AI.Economics.Equity;

/// <summary>Условия ценового раунда.</summary>
public sealed record RoundInput
{
    /// <summary>Название раунда, оно же имя нового класса акций.</summary>
    public string RoundName { get; init; } = "Series A";

    /// <summary>Имя нового инвестора.</summary>
    public string InvestorName { get; init; } = "Инвестор";

    /// <summary>Оценка до денег.</summary>
    public double PreMoneyValuation { get; init; }

    /// <summary>Сумма инвестиции.</summary>
    public double Investment { get; init; }

    /// <summary>
    /// Целевая доля опционного пула после раунда, от 0 до 1. Ноль означает,
    /// что пул не пополняется.
    /// </summary>
    public double TargetOptionPoolPost { get; init; }

    /// <summary>Конвертируемые инструменты, срабатывающие в этом раунде.</summary>
    public IReadOnlyList<SafeNote>? ConvertingNotes { get; init; }

    /// <summary>Кратность ликвидационной преференции нового класса.</summary>
    public double LiquidationMultiple { get; init; } = 1.0;

    /// <summary>Тип участия нового класса в остатке.</summary>
    public PreferenceType Preference { get; init; } = PreferenceType.NonParticipating;

    /// <summary>Потолок участия нового класса, в кратностях вложения.</summary>
    public double ParticipationCap { get; init; } = double.NaN;

    /// <summary>Старшинство нового класса; по умолчанию старше всех существующих.</summary>
    public int Seniority { get; init; } = int.MinValue;
}

/// <summary>Изменение доли одного держателя в результате раунда.</summary>
/// <param name="Holder">Держатель.</param>
/// <param name="Before">Доля до раунда.</param>
/// <param name="After">Доля после раунда.</param>
public sealed record DilutionRow(string Holder, double Before, double After)
{
    /// <summary>Изменение доли в процентных пунктах.</summary>
    public double Delta => After - Before;

    /// <summary>Относительное разводнение: какая часть доли потеряна.</summary>
    public double RelativeDilution => Before > 0 ? (Before - After) / Before : 0;
}

/// <summary>Итог ценового раунда.</summary>
public sealed partial record RoundResult
{
    /// <summary>Таблица капитализации после раунда.</summary>
    public CapTable CapTable { get; init; } = new();

    /// <summary>Цена акции раунда.</summary>
    public double PricePerShare { get; init; }

    /// <summary>Оценка после денег.</summary>
    public double PostMoneyValuation { get; init; }

    /// <summary>Акции, полученные новым инвестором.</summary>
    public double InvestorShares { get; init; }

    /// <summary>Доля нового инвестора после раунда.</summary>
    public double InvestorOwnership { get; init; }

    /// <summary>Вновь созданные опционы.</summary>
    public double NewPoolShares { get; init; }

    /// <summary>Полностью разводнённое число акций после раунда.</summary>
    public double TotalSharesAfter { get; init; }

    /// <summary>Итоги конвертации SAFE и займов.</summary>
    public IReadOnlyList<NoteConversion> Conversions { get; init; } = [];

    /// <summary>Разводнение по держателям.</summary>
    public IReadOnlyList<DilutionRow> Dilution { get; init; } = [];

    /// <summary>
    /// Эффективная оценка до денег с точки зрения существующих акционеров:
    /// их доля после раунда, умноженная на оценку после денег.
    /// </summary>
    /// <remarks>
    /// Расходится с заявленной <see cref="RoundInput.PreMoneyValuation"/> ровно
    /// на стоимость пополнения пула и скидок конвертируемых инструментов —
    /// это и есть цена «pool shuffle», которую основатели платят молча.
    /// </remarks>
    public double EffectivePreMoneyForFounders { get; init; }
}

/// <summary>
/// Расчёт ценового раунда: конвертация SAFE и займов, пополнение опционного
/// пула, цена акции и разводнение.
/// </summary>
/// <remarks>
/// <para>
/// Три эффекта, из-за которых доля основателей после раунда почти всегда
/// меньше ожидаемой:
/// </para>
/// <list type="number">
/// <item>
/// <b>Pool shuffle.</b> Пополнение опционного пула по условиям сделки
/// делается «до денег», то есть размывает только существующих акционеров,
/// а не инвестора. Пул в 10 % при инвесторской доле 20 % стоит основателям
/// примерно 8 процентных пунктов.
/// </item>
/// <item>
/// <b>Конвертация SAFE.</b> Инструменты конвертируются по цене ниже
/// раундовой, поэтому дают больше акций, чем «на ту же сумму по цене раунда».
/// </item>
/// <item>
/// <b>Циклическая зависимость.</b> Число акций SAFE зависит от цены раунда,
/// а цена раунда — от общего числа акций. Уравнение решается итеративно;
/// «прикидка на салфетке» систематически занижает разводнение.
/// </item>
/// </list>
/// </remarks>
public static class FundingRound
{
    private const int FixedPointIterations = 200;

    /// <summary>Проводит раунд и возвращает новую таблицу капитализации.</summary>
    /// <param name="table">Таблица до раунда.</param>
    /// <param name="round">Условия раунда.</param>
    /// <returns>Итог раунда.</returns>
    /// <exception cref="ArgumentNullException">Аргументы не заданы.</exception>
    /// <exception cref="ArgumentException">Условия раунда несовместимы.</exception>
    public static RoundResult Execute(CapTable table, RoundInput round)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(round);

        double postMoney = round.PreMoneyValuation + round.Investment;
        if (postMoney <= 0)
            throw new ArgumentException("Оценка после денег должна быть положительной.", nameof(round));

        double investorFraction = round.Investment / postMoney;
        double poolFraction = round.TargetOptionPoolPost;
        bool topUpPool = poolFraction > 0;

        double denominator = topUpPool
            ? 1.0 - poolFraction - investorFraction
            : 1.0 - investorFraction;

        if (denominator <= 1e-9)
            throw new ArgumentException(
                "Доли инвестора и пула не оставляют места существующим акционерам.", nameof(round));

        double issued = table.IssuedShares;
        double poolExisting = table.UnallocatedPool;
        double preRoundFd = issued + poolExisting;

        List<SafeNote> notes = ApplyMostFavoredNation(round.ConvertingNotes);
        double baseShares = topUpPool ? issued : issued + poolExisting;

        // Число акций SAFE зависит от цены раунда, цена раунда — от числа акций.
        // Неподвижная точка ищется простой итерацией: отображение сжимающее,
        // сходимость на практике за десяток шагов
        double total = baseShares / denominator;
        for (int i = 0; i < FixedPointIterations; i++)
        {
            double pps = postMoney / total;
            double safeShares = notes.Sum(n => NoteShares(n, pps, preRoundFd, total, investorFraction));
            double next = (baseShares + safeShares) / denominator;
            if (Math.Abs(next - total) < 1e-9 * Math.Max(1, total)) { total = next; break; }
            total = next;
        }

        double pricePerShare = postMoney / total;
        double investorShares = investorFraction * total;
        double poolTotal = topUpPool ? poolFraction * total : poolExisting;
        double newPool = Math.Max(0, poolTotal - poolExisting);

        var before = table.Ownership().ToDictionary(r => r.Holder, r => r.Ownership);

        CapTable after = table.Clone();
        after.UnallocatedPool = poolTotal;

        int seniority = round.Seniority == int.MinValue
            ? (table.Classes.Count > 0 ? table.Classes.Max(c => c.Seniority) + 1 : 1)
            : round.Seniority;

        after.AddClass(new ShareClass
        {
            Name = round.RoundName,
            IsCommon = false,
            Seniority = seniority,
            LiquidationMultiple = round.LiquidationMultiple,
            Preference = round.Preference,
            ParticipationCap = round.ParticipationCap,
        });

        after.AddHolding(round.InvestorName, investorShares, round.RoundName, round.Investment);

        var conversions = new List<NoteConversion>(notes.Count);
        if (notes.Count > 0)
        {
            string safeClass = round.RoundName + " (SAFE)";
            after.AddClass(new ShareClass
            {
                Name = safeClass,
                IsCommon = false,
                Seniority = seniority,
                LiquidationMultiple = 1.0,
                Preference = PreferenceType.NonParticipating,
            });

            foreach (SafeNote note in notes)
            {
                double shares = NoteShares(note, pricePerShare, preRoundFd, total, investorFraction);
                double amount = note.AmountWithInterest;
                double price = shares > 0 ? amount / shares : pricePerShare;

                after.AddHolding(note.Holder, shares, safeClass, amount);

                conversions.Add(new NoteConversion
                {
                    Holder = note.Holder,
                    Amount = amount,
                    ConversionPrice = price,
                    Shares = shares,
                    PriceDriver = PriceDriver(note, pricePerShare, preRoundFd),
                    EffectiveValuation = price * total,
                    OwnershipAfter = total > 0 ? shares / total : 0,
                });
            }
        }

        var afterOwnership = after.Ownership().ToDictionary(r => r.Holder, r => r.Ownership);
        var dilution = new List<DilutionRow>();
        foreach ((string holder, double share) in before)
            dilution.Add(new DilutionRow(holder, share,
                afterOwnership.TryGetValue(holder, out double a) ? a : 0));

        double foundersAfter = before.Keys
            .Where(h => afterOwnership.ContainsKey(h))
            .Sum(h => afterOwnership[h]);

        return new RoundResult
        {
            CapTable = after,
            PricePerShare = pricePerShare,
            PostMoneyValuation = postMoney,
            InvestorShares = investorShares,
            InvestorOwnership = investorFraction,
            NewPoolShares = newPool,
            TotalSharesAfter = after.FullyDilutedShares,
            Conversions = conversions,
            Dilution = [.. dilution.OrderByDescending(d => d.Before)],
            EffectivePreMoneyForFounders = foundersAfter * postMoney,
        };
    }

    /// <summary>
    /// Число акций, получаемых по конвертируемому инструменту.
    /// </summary>
    /// <remarks>
    /// Post-money SAFE трактуется по стандарту YC: доля инвестора считается от
    /// капитала после конвертации всех инструментов, но <b>до</b> новых денег,
    /// поэтому размывается только ценовым раундом.
    /// </remarks>
    private static double NoteShares(
        SafeNote note, double pricePerShare, double preRoundFd, double total, double investorFraction)
    {
        double amount = note.AmountWithInterest;
        if (amount <= 0) return 0;

        if (note.PostMoney && !double.IsNaN(note.ValuationCap) && note.ValuationCap > 0)
            return amount / note.ValuationCap * total * (1.0 - investorFraction);

        double price = ConversionPrice(note, pricePerShare, preRoundFd);
        return price > 0 ? amount / price : 0;
    }

    /// <summary>Цена конвертации: минимум из цены по потолку и цены со скидкой.</summary>
    private static double ConversionPrice(SafeNote note, double pricePerShare, double preRoundFd)
    {
        double capPrice = !double.IsNaN(note.ValuationCap) && note.ValuationCap > 0 && preRoundFd > 0
            ? note.ValuationCap / preRoundFd
            : double.PositiveInfinity;

        double discountPrice = note.Discount > 0
            ? pricePerShare * (1.0 - note.Discount)
            : double.PositiveInfinity;

        double price = Math.Min(capPrice, discountPrice);
        return double.IsPositiveInfinity(price) ? pricePerShare : price;
    }

    private static string PriceDriver(SafeNote note, double pricePerShare, double preRoundFd)
    {
        if (note.PostMoney && !double.IsNaN(note.ValuationCap)) return "Потолок (post-money)";

        double capPrice = !double.IsNaN(note.ValuationCap) && note.ValuationCap > 0 && preRoundFd > 0
            ? note.ValuationCap / preRoundFd
            : double.PositiveInfinity;
        double discountPrice = note.Discount > 0 ? pricePerShare * (1.0 - note.Discount) : double.PositiveInfinity;

        if (double.IsPositiveInfinity(capPrice) && double.IsPositiveInfinity(discountPrice)) return "Цена раунда";
        return capPrice <= discountPrice ? "Потолок оценки" : "Скидка к раунду";
    }

    /// <summary>
    /// Раскрывает оговорку о наиболее благоприятных условиях: инструмент с MFN
    /// получает минимальный потолок и максимальную скидку среди остальных.
    /// </summary>
    private static List<SafeNote> ApplyMostFavoredNation(IReadOnlyList<SafeNote>? notes)
    {
        if (notes is null || notes.Count == 0) return [];
        if (!notes.Any(n => n.MostFavoredNation)) return [.. notes];

        double bestCap = notes
            .Where(n => !n.MostFavoredNation && !double.IsNaN(n.ValuationCap))
            .Select(n => n.ValuationCap)
            .DefaultIfEmpty(double.NaN)
            .Min();

        double bestDiscount = notes
            .Where(n => !n.MostFavoredNation)
            .Select(n => n.Discount)
            .DefaultIfEmpty(0)
            .Max();

        return [.. notes.Select(n => n.MostFavoredNation
            ? n with
            {
                ValuationCap = double.IsNaN(bestCap) ? n.ValuationCap : Math.Min(BestOr(n.ValuationCap), bestCap),
                Discount = Math.Max(n.Discount, bestDiscount),
            }
            : n)];
    }

    private static double BestOr(double cap) => double.IsNaN(cap) ? double.PositiveInfinity : cap;
}
