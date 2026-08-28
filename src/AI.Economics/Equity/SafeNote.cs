using System;

namespace AI.Economics.Equity;

/// <summary>
/// Конвертируемый инструмент: SAFE либо конвертируемый заём.
/// </summary>
/// <remarks>
/// Оба инструмента откладывают оценку компании до следующего ценового раунда
/// и конвертируются в акции по цене, которая не выше цены раунда. Разница
/// в деталях: у займа накапливаются проценты, у SAFE — нет; SAFE бывает
/// pre-money и post-money, и это меняет, кто именно размывается.
/// </remarks>
public sealed record SafeNote
{
    /// <summary>Имя инвестора.</summary>
    public string Holder { get; init; } = string.Empty;

    /// <summary>Сумма вложения.</summary>
    public double Amount { get; init; }

    /// <summary>Оценочный потолок. <c>NaN</c> — потолка нет.</summary>
    public double ValuationCap { get; init; } = double.NaN;

    /// <summary>Скидка к цене раунда, доля (0,2 — скидка 20 %).</summary>
    public double Discount { get; init; }

    /// <summary>
    /// Post-money SAFE (стандарт YC 2018 года): потолок задаёт долю инвестора
    /// от капитала <b>после</b> раунда, и размывают его только последующие
    /// раунды, но не другие SAFE того же раунда.
    /// </summary>
    public bool PostMoney { get; init; }

    /// <summary>
    /// Оговорка о наиболее благоприятных условиях: инструмент получает лучшие
    /// условия среди всех конвертируемых в этом раунде.
    /// </summary>
    public bool MostFavoredNation { get; init; }

    /// <summary>Годовая ставка процента для конвертируемого займа.</summary>
    public double InterestRate { get; init; }

    /// <summary>Число лет, за которые начислены проценты.</summary>
    public double YearsAccrued { get; init; }

    /// <summary>Сумма к конвертации с учётом накопленных процентов.</summary>
    public double AmountWithInterest => Amount * (1.0 + (InterestRate * YearsAccrued));
}

/// <summary>Итог конвертации одного инструмента в акции раунда.</summary>
public sealed record NoteConversion
{
    /// <summary>Имя инвестора.</summary>
    public string Holder { get; init; } = string.Empty;

    /// <summary>Сконвертированная сумма с процентами.</summary>
    public double Amount { get; init; }

    /// <summary>Цена, по которой прошла конвертация.</summary>
    public double ConversionPrice { get; init; }

    /// <summary>Полученное число акций.</summary>
    public double Shares { get; init; }

    /// <summary>Что определило цену: потолок оценки, скидка или цена раунда.</summary>
    public string PriceDriver { get; init; } = string.Empty;

    /// <summary>Эффективная оценка, по которой вошёл инвестор.</summary>
    public double EffectiveValuation { get; init; }

    /// <summary>Доля инвестора после раунда.</summary>
    public double OwnershipAfter { get; init; }
}
