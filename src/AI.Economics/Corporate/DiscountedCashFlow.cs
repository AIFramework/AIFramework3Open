using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;
using AI.Economics.Numerics;
using AI.Statistics;

namespace AI.Economics.Corporate;

/// <summary>Способ расчёта продлённой стоимости.</summary>
public enum TerminalValueMethod
{
    /// <summary>Модель Гордона: рост потока с постоянным темпом вечно.</summary>
    Gordon,

    /// <summary>Мультипликатор выхода к прибыли до амортизации последнего года.</summary>
    ExitMultiple,
}

/// <summary>Прогноз одного года для оценки методом дисконтированных потоков.</summary>
/// <param name="Revenue">Выручка.</param>
/// <param name="EbitMargin">Рентабельность по операционной прибыли.</param>
/// <param name="TaxRate">Эффективная ставка налога.</param>
/// <param name="Depreciation">Амортизация.</param>
/// <param name="CapitalExpenditures">Капитальные затраты.</param>
/// <param name="WorkingCapitalChange">Прирост оборотного капитала.</param>
public sealed record ForecastYear(
    double Revenue, double EbitMargin, double TaxRate,
    double Depreciation, double CapitalExpenditures, double WorkingCapitalChange)
{
    /// <summary>Операционная прибыль.</summary>
    public double Ebit => Revenue * EbitMargin;

    /// <summary>Прибыль до процентов и после налога.</summary>
    public double Nopat => Ebit * (1 - TaxRate);

    /// <summary>Свободный денежный поток фирмы.</summary>
    public double FreeCashFlow => Nopat + Depreciation - CapitalExpenditures - WorkingCapitalChange;

    /// <summary>Прибыль до процентов, налогов и амортизации.</summary>
    public double Ebitda => Ebit + Depreciation;
}

/// <summary>Входные данные оценки методом дисконтированных потоков.</summary>
public sealed record DcfInput
{
    /// <summary>Название компании или проекта.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Прогноз по годам.</summary>
    public IReadOnlyList<ForecastYear> Forecast { get; init; } = [];

    /// <summary>Ставка дисконтирования.</summary>
    public double DiscountRate { get; init; } = 0.15;

    /// <summary>Способ расчёта продлённой стоимости.</summary>
    public TerminalValueMethod TerminalMethod { get; init; } = TerminalValueMethod.Gordon;

    /// <summary>Темп роста в постпрогнозном периоде для модели Гордона.</summary>
    public double TerminalGrowth { get; init; } = 0.03;

    /// <summary>Мультипликатор выхода к прибыли до амортизации.</summary>
    public double ExitMultiple { get; init; } = 6;

    /// <summary>Чистый долг на дату оценки.</summary>
    public double NetDebt { get; init; }

    /// <summary>Неоперационные активы, добавляемые к стоимости бизнеса.</summary>
    public double NonOperatingAssets { get; init; }

    /// <summary>Дисконтировать ли потоки к середине года.</summary>
    public bool MidYearConvention { get; init; } = true;
}

/// <summary>Вклад одного фактора в разброс оценки.</summary>
/// <param name="Factor">Название фактора.</param>
/// <param name="LowValue">Стоимость при нижнем значении фактора.</param>
/// <param name="HighValue">Стоимость при верхнем значении фактора.</param>
/// <param name="Swing">Размах влияния.</param>
public sealed record SensitivityBar(string Factor, double LowValue, double HighValue, double Swing);

/// <summary>Результат оценки методом дисконтированных потоков.</summary>
public sealed record DcfResult : IInterpretable
{
    /// <summary>Название компании или проекта.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Свободные денежные потоки прогнозного периода.</summary>
    public Vector CashFlows { get; init; } = new(0);

    /// <summary>Дисконтированные потоки прогнозного периода.</summary>
    public Vector DiscountedCashFlows { get; init; } = new(0);

    /// <summary>Продлённая стоимость на конец прогнозного периода.</summary>
    public double TerminalValue { get; init; }

    /// <summary>Дисконтированная продлённая стоимость.</summary>
    public double DiscountedTerminalValue { get; init; }

    /// <summary>Стоимость бизнеса.</summary>
    public double EnterpriseValue { get; init; }

    /// <summary>Стоимость собственного капитала.</summary>
    public double EquityValue { get; init; }

    /// <summary>Доля продлённой стоимости в стоимости бизнеса.</summary>
    public double TerminalShare =>
        EnterpriseValue > 0 ? DiscountedTerminalValue / EnterpriseValue : 0;

    /// <summary>Ставка дисконтирования.</summary>
    public double DiscountRate { get; init; }

    /// <summary>Использованный способ расчёта продлённой стоимости.</summary>
    public TerminalValueMethod TerminalMethod { get; init; }

    /// <summary>Неявный мультипликатор продлённой стоимости к прибыли последнего года.</summary>
    public double ImpliedExitMultiple { get; init; }

    /// <summary>Неявный темп роста, соответствующий заданному мультипликатору выхода.</summary>
    public double ImpliedGrowth { get; init; }

    /// <summary>Вклад факторов в разброс оценки.</summary>
    public IReadOnlyList<SensitivityBar> Tornado { get; init; } = [];

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        SensitivityBar? driver = Tornado.FirstOrDefault();
        bool terminalHeavy = TerminalShare > 0.75;

        var builder = new InterpretationBuilder($"Оценка методом дисконтированных потоков: {Name}")
            .Summary($"Стоимость бизнеса {Fmt.Money(EnterpriseValue)}, собственного капитала " +
                     $"{Fmt.Money(EquityValue)} при ставке {Fmt.Pct(DiscountRate, 2)}. " +
                     $"На продлённую стоимость приходится {Fmt.Pct(TerminalShare, 0)} оценки. " +
                     $"Неявный мультипликатор выхода {Fmt.Num(ImpliedExitMultiple, 1)}, " +
                     $"неявный темп роста {Fmt.Pct(ImpliedGrowth, 2)}.")
            .Metric("Стоимость бизнеса", Fmt.Money(EnterpriseValue), null,
                "дисконтированные потоки плюс продлённая стоимость")
            .Metric("Стоимость капитала", Fmt.Money(EquityValue), null,
                "за вычетом чистого долга")
            .Metric("Доля продлённой стоимости", TerminalShare, null,
                terminalHeavy ? "оценка держится на постпрогнозном периоде" : "прогнозный период весом",
                terminalHeavy ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Metric("Неявный мультипликатор", ImpliedExitMultiple, "×",
                "продлённая стоимость к прибыли до амортизации последнего года",
                MetricQuality.Neutral, 2)
            .Metric("Неявный темп роста", ImpliedGrowth, null,
                "рост, при котором модель Гордона даёт ту же продлённую стоимость",
                ImpliedGrowth < DiscountRate - 0.02 ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("Ставка дисконтирования", DiscountRate, null,
                "средневзвешенная стоимость капитала", MetricQuality.Neutral, 4);

        foreach (SensitivityBar bar in Tornado)
        {
            builder.Metric($"Чувствительность: {bar.Factor}", bar.Swing, null,
                $"от {Fmt.Money(bar.LowValue)} до {Fmt.Money(bar.HighValue)}",
                MetricQuality.Unknown, 0);
        }

        return builder
            .FindingIf(driver is not null,
                $"Сильнее всего оценку двигает «{driver?.Factor}»: размах " +
                $"{Fmt.Money(driver?.Swing ?? 0)}, то есть " +
                $"{Fmt.Pct(EnterpriseValue > 0 ? (driver?.Swing ?? 0) / EnterpriseValue : 0, 0)} " +
                "стоимости бизнеса. Обсуждать в первую очередь нужно этот фактор, а не прогноз выручки.")
            .Finding($"Неявный мультипликатор {Fmt.Num(ImpliedExitMultiple, 1)} и неявный темп роста " +
                     $"{Fmt.Pct(ImpliedGrowth, 2)} — взаимная проверка двух способов расчёта " +
                     "продлённой стоимости. Если один из них выглядит неправдоподобно, " +
                     "неправдоподобна и вся оценка.")
            .FindingIf(!terminalHeavy,
                $"На прогнозный период приходится {Fmt.Pct(1 - TerminalShare, 0)} стоимости — " +
                "оценка опирается на детальный прогноз, а не на предпосылку о вечном росте.")
            .WarningIf(terminalHeavy,
                $"Продлённая стоимость даёт {Fmt.Pct(TerminalShare, 0)} результата. " +
                "Детальный прогноз при этом почти не влияет на оценку: спорить о выручке " +
                "третьего года бессмысленно, спорить нужно о ставке и темпе вечного роста.")
            .WarningIf(ImpliedGrowth >= DiscountRate - 0.01,
                $"Неявный темп роста {Fmt.Pct(ImpliedGrowth, 2)} почти сравнялся со ставкой " +
                $"{Fmt.Pct(DiscountRate, 2)}. Модель Гордона в этой области взрывается: " +
                "малое изменение любой из величин меняет оценку в разы.")
            .Warning("Темп вечного роста не может превышать долгосрочный рост экономики: " +
                     "компания, растущая быстрее, в пределе становится больше её. " +
                     "Разумный потолок — уровень инфляции плюс реальный рост ВВП.")
            .Recommendation("Показывайте диаграмму чувствительности вместе с оценкой: она " +
                            "переводит спор о стоимости в спор о конкретных предпосылках.")
            .Recommendation("Считайте продлённую стоимость обоими способами и сверяйте. " +
                            "Расхождение больше четверти означает, что одна из предпосылок " +
                            "не согласована с остальной моделью.")
            .Build();
    }
}

/// <summary>Результат стохастической оценки методом Монте-Карло.</summary>
public sealed record DcfSimulationResult : IInterpretable
{
    /// <summary>Название компании или проекта.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Средняя стоимость капитала по симуляциям.</summary>
    public double MeanEquityValue { get; init; }

    /// <summary>Медианная стоимость капитала.</summary>
    public double MedianEquityValue { get; init; }

    /// <summary>Нижний процентиль оценки.</summary>
    public double LowerPercentile { get; init; }

    /// <summary>Верхний процентиль оценки.</summary>
    public double UpperPercentile { get; init; }

    /// <summary>Стандартное отклонение оценки.</summary>
    public double StandardDeviation { get; init; }

    /// <summary>Доля симуляций с отрицательной стоимостью капитала.</summary>
    public double ProbabilityOfLoss { get; init; }

    /// <summary>Детерминированная оценка на базовых предпосылках.</summary>
    public double BaseCase { get; init; }

    /// <summary>Отсортированные результаты симуляций.</summary>
    public Vector Distribution { get; init; } = new(0);

    /// <summary>Число симуляций.</summary>
    public int Simulations { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double skew = MedianEquityValue > 0 ? (MeanEquityValue - MedianEquityValue) / MedianEquityValue : 0;
        double width = MedianEquityValue > 0 ? (UpperPercentile - LowerPercentile) / MedianEquityValue : 0;

        return new InterpretationBuilder($"Монте-Карло вокруг оценки: {Name}")
            .Summary($"По {Simulations} симуляциям медианная стоимость капитала " +
                     $"{Fmt.Money(MedianEquityValue)}, интервал от {Fmt.Money(LowerPercentile)} " +
                     $"до {Fmt.Money(UpperPercentile)}. Детерминированная оценка " +
                     $"{Fmt.Money(BaseCase)}. Вероятность отрицательной стоимости " +
                     $"{Fmt.Pct(ProbabilityOfLoss, 1)}.")
            .Metric("Медиана", Fmt.Money(MedianEquityValue), null, "половина исходов выше этого значения")
            .Metric("Базовый расчёт", Fmt.Money(BaseCase), null,
                Math.Abs(BaseCase - MedianEquityValue) > 0.1 * Math.Abs(MedianEquityValue)
                    ? "заметно отличается от медианы симуляций"
                    : "близок к медиане симуляций")
            .Metric("Интервал 10-90", $"{Fmt.Money(LowerPercentile)} — {Fmt.Money(UpperPercentile)}", null,
                $"ширина {Fmt.Pct(width, 0)} от медианы")
            .Metric("Разброс", Fmt.Money(StandardDeviation), null, "стандартное отклонение оценки")
            .Metric("Вероятность отрицательной стоимости", ProbabilityOfLoss, null,
                "доля сценариев, в которых долг превышает стоимость бизнеса",
                ProbabilityOfLoss > 0.1 ? MetricQuality.Critical
                    : ProbabilityOfLoss > 0.02 ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Metric("Асимметрия", skew, null,
                skew > 0.05 ? "распределение вытянуто вправо" : "распределение симметрично",
                MetricQuality.Neutral, 3)
            .Finding("Точечная оценка стоимости всегда ложна: неопределённость предпосылок " +
                     "переносится в оценку нелинейно. Интервал показывает, насколько " +
                     "уверенно вообще можно говорить о цене.")
            .FindingIf(Math.Abs(skew) > 0.05,
                $"Среднее отличается от медианы на {Fmt.Pct(skew, 1)}: распределение " +
                "несимметрично. Ориентироваться в переговорах стоит на медиану, " +
                "а не на среднее.")
            .FindingIf(width > 1,
                $"Интервал шире самой медианы ({Fmt.Pct(width, 0)}). При такой " +
                "неопределённости диапазон цены — это и есть результат оценки.")
            .Warning("Распределения предпосылок заданы вручную и обычно независимы, " +
                     "тогда как в реальности выручка, маржа и капитальные затраты " +
                     "коррелированы. Игнорирование корреляций занижает разброс оценки.")
            .Warning("Симуляция не уменьшает неопределённость, а показывает её. Узкий " +
                     "интервал означает узкие заданные распределения, а не точную модель.")
            .Recommendation("Задавайте распределения по историческим отклонениям прогнозов " +
                            "от факта, а не по интуиции: это единственный способ сделать " +
                            "интервал осмысленным.")
            .Build();
    }
}

/// <summary>
/// Оценка методом дисконтированных денежных потоков.
/// </summary>
/// <remarks>
/// <para>
/// Свободный поток фирмы строится из операционной прибыли:
/// </para>
/// <code>
/// FCFF = EBIT * (1 - tax) + D&amp;A - Capex - dWC
/// EV = sum_t FCFF_t / (1 + WACC)^(t - 0.5) + TV / (1 + WACC)^(T - 0.5)
/// Equity = EV - NetDebt + NonOperatingAssets
/// </code>
/// <para>
/// Поправка на середину года отражает то, что деньги поступают равномерно,
/// а не одномоментно в конце периода. Она повышает оценку примерно на половину
/// ставки и в практике оценки применяется по умолчанию.
/// </para>
/// <para>
/// Продлённая стоимость считается двумя способами — моделью Гордона и
/// мультипликатором выхода — и они служат взаимной проверкой. Модель выдаёт
/// неявный мультипликатор для заданного темпа роста и неявный темп роста для
/// заданного мультипликатора: если хотя бы одно из этих чисел выглядит
/// неправдоподобно, неправдоподобна и вся оценка.
/// </para>
/// </remarks>
public static class DiscountedCashFlow
{
    /// <summary>Оценивает бизнес методом дисконтированных потоков.</summary>
    /// <param name="input">Прогноз, ставка и предпосылки продлённой стоимости.</param>
    /// <returns>Стоимость бизнеса, собственного капитала и диаграмма чувствительности.</returns>
    /// <exception cref="ArgumentNullException">Входные данные не заданы.</exception>
    /// <exception cref="ArgumentException">Прогноз пуст или темп роста не меньше ставки.</exception>
    public static DcfResult Value(DcfInput input)
    {
        DcfResult result = Core(input);
        return result with { Tornado = Tornado(input, result.EnterpriseValue) };
    }

    /// <summary>Оценка без построения диаграммы чувствительности.</summary>
    /// <remarks>
    /// Диаграмма чувствительности сама переоценивает модель на сдвинутых
    /// предпосылках, поэтому она вынесена из основного расчёта: иначе
    /// вызовы уходили бы в бесконечную рекурсию.
    /// </remarks>
    private static DcfResult Core(DcfInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Forecast.Count == 0)
            throw new ArgumentException("Прогноз не задан.", nameof(input));
        if (input.DiscountRate <= 0)
            throw new ArgumentException("Ставка дисконтирования должна быть положительной.", nameof(input));
        if (input.TerminalMethod == TerminalValueMethod.Gordon && input.TerminalGrowth >= input.DiscountRate)
            throw new ArgumentException(
                "Темп вечного роста должен быть меньше ставки дисконтирования.", nameof(input));

        int years = input.Forecast.Count;
        var flows = new Vector(years);
        var discounted = new Vector(years);
        double shift = input.MidYearConvention ? 0.5 : 0;

        for (int t = 0; t < years; t++)
        {
            flows[t] = input.Forecast[t].FreeCashFlow;
            discounted[t] = flows[t] / Math.Pow(1 + input.DiscountRate, t + 1 - shift);
        }

        ForecastYear last = input.Forecast[^1];

        double terminal = input.TerminalMethod == TerminalValueMethod.Gordon
            ? last.FreeCashFlow * (1 + input.TerminalGrowth) / (input.DiscountRate - input.TerminalGrowth)
            : last.Ebitda * input.ExitMultiple;

        double discountedTerminal = terminal / Math.Pow(1 + input.DiscountRate, years - shift);
        double enterprise = discounted.Sum() + discountedTerminal;

        double impliedMultiple = last.Ebitda > 0 ? terminal / last.Ebitda : 0;
        double impliedGrowth = last.FreeCashFlow > 0 && terminal > 0
            ? ((input.DiscountRate * terminal) - last.FreeCashFlow) / (terminal + last.FreeCashFlow)
            : 0;

        return new DcfResult
        {
            Name = input.Name,
            CashFlows = flows,
            DiscountedCashFlows = discounted,
            TerminalValue = terminal,
            DiscountedTerminalValue = discountedTerminal,
            EnterpriseValue = enterprise,
            EquityValue = enterprise - input.NetDebt + input.NonOperatingAssets,
            DiscountRate = input.DiscountRate,
            TerminalMethod = input.TerminalMethod,
            ImpliedExitMultiple = impliedMultiple,
            ImpliedGrowth = impliedGrowth,
        };
    }

    /// <summary>Строит диаграмму чувствительности оценки к ключевым предпосылкам.</summary>
    /// <param name="input">Базовые предпосылки.</param>
    /// <param name="baseValue">Оценка на базовых предпосылках.</param>
    /// <returns>Факторы по убыванию размаха влияния.</returns>
    /// <exception cref="ArgumentNullException">Входные данные не заданы.</exception>
    public static IReadOnlyList<SensitivityBar> Tornado(DcfInput input, double baseValue)
    {
        ArgumentNullException.ThrowIfNull(input);

        var bars = new List<SensitivityBar>();

        void Add(string factor, DcfInput low, DcfInput high)
        {
            try
            {
                double lowValue = Core(low with { Name = input.Name }).EnterpriseValue;
                double highValue = Core(high with { Name = input.Name }).EnterpriseValue;

                bars.Add(new SensitivityBar(factor, lowValue, highValue, Math.Abs(highValue - lowValue)));
            }
            catch (ArgumentException)
            {
                // Предпосылка вышла за область определения модели: фактор пропускается
            }
        }

        Add("Ставка дисконтирования",
            input with { DiscountRate = input.DiscountRate + 0.02 },
            input with { DiscountRate = Math.Max(input.DiscountRate - 0.02, input.TerminalGrowth + 0.005) });

        Add("Темп вечного роста",
            input with { TerminalGrowth = Math.Max(input.TerminalGrowth - 0.01, -0.02) },
            input with { TerminalGrowth = Math.Min(input.TerminalGrowth + 0.01, input.DiscountRate - 0.005) });

        Add("Рентабельность",
            input with { Forecast = [.. input.Forecast.Select(y => y with { EbitMargin = y.EbitMargin - 0.02 })] },
            input with { Forecast = [.. input.Forecast.Select(y => y with { EbitMargin = y.EbitMargin + 0.02 })] });

        Add("Выручка",
            input with { Forecast = [.. input.Forecast.Select(y => y with { Revenue = y.Revenue * 0.9 })] },
            input with { Forecast = [.. input.Forecast.Select(y => y with { Revenue = y.Revenue * 1.1 })] });

        Add("Капитальные затраты",
            input with
            {
                Forecast = [.. input.Forecast.Select(y => y with { CapitalExpenditures = y.CapitalExpenditures * 1.2 })],
            },
            input with
            {
                Forecast = [.. input.Forecast.Select(y => y with { CapitalExpenditures = y.CapitalExpenditures * 0.8 })],
            });

        return [.. bars.OrderByDescending(b => b.Swing)];
    }

    /// <summary>Проводит стохастическую оценку методом Монте-Карло.</summary>
    /// <param name="input">Базовые предпосылки.</param>
    /// <param name="revenueVolatility">Годовая волатильность отклонения выручки от прогноза.</param>
    /// <param name="marginVolatility">Разброс рентабельности в процентных пунктах.</param>
    /// <param name="rateVolatility">Разброс ставки дисконтирования в процентных пунктах.</param>
    /// <param name="simulations">Число симуляций.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Распределение стоимости капитала и его характеристики.</returns>
    /// <exception cref="ArgumentNullException">Входные данные не заданы.</exception>
    public static DcfSimulationResult Simulate(
        DcfInput input, double revenueVolatility = 0.12, double marginVolatility = 0.02,
        double rateVolatility = 0.015, int simulations = 5000, int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfLessThan(simulations, 100);

        Random rng = RandomEngine.Create(seed);
        var values = new List<double>(simulations);
        double baseCase = Core(input).EquityValue;

        for (int s = 0; s < simulations; s++)
        {
            double rate = Math.Max(
                input.TerminalGrowth + 0.01,
                input.DiscountRate + (RandomEngine.NextGaussian(rng) * rateVolatility));

            var forecast = new List<ForecastYear>(input.Forecast.Count);
            double cumulative = 1;

            for (int t = 0; t < input.Forecast.Count; t++)
            {
                // Отклонения выручки накапливаются: ошибка первого года переносится дальше
                cumulative *= 1 + (RandomEngine.NextGaussian(rng) * revenueVolatility);
                ForecastYear year = input.Forecast[t];

                forecast.Add(year with
                {
                    Revenue = year.Revenue * cumulative,
                    EbitMargin = year.EbitMargin + (RandomEngine.NextGaussian(rng) * marginVolatility),
                });
            }

            try
            {
                values.Add(Core(input with { Forecast = forecast, DiscountRate = rate }).EquityValue);
            }
            catch (ArgumentException)
            {
                // Сценарий вышел за область определения модели и в выборку не входит
            }
        }

        double[] sorted = [.. values.OrderBy(v => v)];
        var distribution = new Vector(sorted.Length);
        for (int i = 0; i < sorted.Length; i++) distribution[i] = sorted[i];

        double mean = sorted.Length > 0 ? sorted.Average() : 0;
        double variance = sorted.Length > 1
            ? sorted.Sum(v => (v - mean) * (v - mean)) / (sorted.Length - 1)
            : 0;

        return new DcfSimulationResult
        {
            Name = input.Name,
            MeanEquityValue = mean,
            MedianEquityValue = sorted.Length > 0 ? EconMath.Quantile(sorted, 0.5) : 0,
            LowerPercentile = sorted.Length > 0 ? EconMath.Quantile(sorted, 0.1) : 0,
            UpperPercentile = sorted.Length > 0 ? EconMath.Quantile(sorted, 0.9) : 0,
            StandardDeviation = Math.Sqrt(Math.Max(variance, 0)),
            ProbabilityOfLoss = sorted.Length > 0 ? (double)sorted.Count(v => v < 0) / sorted.Length : 0,
            BaseCase = baseCase,
            Distribution = distribution,
            Simulations = sorted.Length,
        };
    }
}
