using System.Collections.Generic;

namespace AI.Economics.Pricing;

/// <summary>
/// Наблюдение «цена — объём» для оценки эластичности.
/// </summary>
/// <remarks>
/// Поля <see cref="Unit"/> и <see cref="Period"/> заполняются только для
/// панельных данных: они задают, по каким разрезам убирать ненаблюдаемые
/// постоянные эффекты. <see cref="Instrument"/> нужен там, где цена
/// эндогенна — обычно это себестоимость, курс валюты или цена конкурента.
/// </remarks>
public sealed record PriceObservation
{
    /// <summary>Цена. Должна быть строго положительной.</summary>
    public double Price { get; init; }

    /// <summary>Проданный объём. Должен быть строго положительным.</summary>
    public double Quantity { get; init; }

    /// <summary>
    /// Инструмент для цены: величина, влияющая на цену, но не на спрос
    /// напрямую. Обязателен для оценки методом инструментальных переменных.
    /// </summary>
    public double Instrument { get; init; } = double.NaN;

    /// <summary>Единица наблюдения панели: товар, магазин, регион.</summary>
    public int Unit { get; init; }

    /// <summary>Номер периода для панельных данных.</summary>
    public int Period { get; init; }

    /// <summary>
    /// Дополнительные регрессоры: промо-флаг, сезонность, дистрибуция.
    /// Входят в модель как есть, без логарифмирования.
    /// </summary>
    public IReadOnlyList<double>? Controls { get; init; }
}

/// <summary>Способ оценки эластичности.</summary>
public enum ElasticityEstimator
{
    /// <summary>
    /// Лог-логарифмическая регрессия обычным МНК. Простая и почти всегда
    /// смещённая: цена коррелирует с ненаблюдаемыми факторами спроса.
    /// </summary>
    LogLogOls,

    /// <summary>
    /// Панельная модель с фиксированными эффектами: убирает всё постоянное
    /// внутри товара и внутри периода.
    /// </summary>
    PanelFixedEffects,

    /// <summary>
    /// Двухшаговый МНК с инструментом для цены: единственный из трёх
    /// способов, дающий причинную оценку при эндогенной цене.
    /// </summary>
    InstrumentalVariables,
}
