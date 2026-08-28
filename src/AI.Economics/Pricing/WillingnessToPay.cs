using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;

namespace AI.Economics.Pricing;

/// <summary>Ответы одного респондента на четыре вопроса Ван Вестендорпа.</summary>
/// <param name="TooCheap">Цена, ниже которой качество вызывает сомнения.</param>
/// <param name="Cheap">Цена, которую респондент считает выгодной.</param>
/// <param name="Expensive">Цена, которую респондент считает дорогой, но приемлемой.</param>
/// <param name="TooExpensive">Цена, выше которой он не купит ни при каких условиях.</param>
public sealed record VanWestendorpAnswer(double TooCheap, double Cheap, double Expensive, double TooExpensive);

/// <summary>Результат анализа по методу Ван Вестендорпа.</summary>
public sealed record VanWestendorpResult : IInterpretable
{
    /// <summary>Сетка цен, на которой построены кривые.</summary>
    public Vector Prices { get; init; } = new Vector(0);

    /// <summary>Доля считающих цену слишком низкой.</summary>
    public Vector TooCheapCurve { get; init; } = new Vector(0);

    /// <summary>Доля считающих цену выгодной.</summary>
    public Vector CheapCurve { get; init; } = new Vector(0);

    /// <summary>Доля считающих цену дорогой.</summary>
    public Vector ExpensiveCurve { get; init; } = new Vector(0);

    /// <summary>Доля считающих цену неприемлемо высокой.</summary>
    public Vector TooExpensiveCurve { get; init; } = new Vector(0);

    /// <summary>Точка предельной дешевизны — нижняя граница приемлемого диапазона.</summary>
    public double PointOfMarginalCheapness { get; init; }

    /// <summary>Точка предельной дороговизны — верхняя граница диапазона.</summary>
    public double PointOfMarginalExpensiveness { get; init; }

    /// <summary>
    /// Оптимальная точка цены: доля отвергающих товар как слишком дешёвый
    /// равна доле отвергающих его как слишком дорогой.
    /// </summary>
    public double OptimalPricePoint { get; init; }

    /// <summary>Точка безразличия: столько же считают цену выгодной, сколько дорогой.</summary>
    public double IndifferencePricePoint { get; init; }

    /// <summary>Число респондентов.</summary>
    public int Respondents { get; init; }

    /// <summary>Число ответов, отброшенных из-за нарушения порядка цен.</summary>
    public int InconsistentAnswers { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double width = PointOfMarginalExpensiveness - PointOfMarginalCheapness;
        double relativeWidth = OptimalPricePoint > 0 ? width / OptimalPricePoint : 0;
        double inconsistentShare = Respondents + InconsistentAnswers > 0
            ? (double)InconsistentAnswers / (Respondents + InconsistentAnswers)
            : 0;

        return new InterpretationBuilder("Готовность платить: метод Ван Вестендорпа")
            .Summary($"Приемлемый диапазон цен — от {Fmt.Money(PointOfMarginalCheapness)} до " +
                     $"{Fmt.Money(PointOfMarginalExpensiveness)}. Оптимальная точка " +
                     $"{Fmt.Money(OptimalPricePoint)}: на ней одинаково мало тех, кто отвергает товар " +
                     $"как слишком дешёвый, и тех, кто отвергает его как слишком дорогой.")
            .Metric("Оптимальная цена", Fmt.Money(OptimalPricePoint), null,
                "минимум суммарного отказа", MetricQuality.Good)
            .Metric("Точка безразличия", Fmt.Money(IndifferencePricePoint), null,
                "обычно соответствует цене лидера рынка")
            .Metric("Нижняя граница", Fmt.Money(PointOfMarginalCheapness), null,
                "ниже неё покупатель сомневается в качестве")
            .Metric("Верхняя граница", Fmt.Money(PointOfMarginalExpensiveness), null,
                "выше неё отказ становится массовым")
            .Metric("Ширина диапазона", Fmt.Pct(relativeWidth), null,
                "относительно оптимальной цены",
                relativeWidth > 0.8 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Респондентов", Respondents, null, null, MetricQuality.Unknown, 0)
            .FindingIf(IndifferencePricePoint > OptimalPricePoint * 1.05,
                "Точка безразличия выше оптимальной: у аудитории есть привычная референсная цена, " +
                "и запас для повышения существует.")
            .FindingIf(IndifferencePricePoint < OptimalPricePoint * 0.95,
                "Точка безразличия ниже оптимальной: аудитория ориентируется на более дешёвые " +
                "аналоги, повышение цены потребует объяснения ценности.")
            .FindingIf(relativeWidth > 0.8,
                "Широкий диапазон приемлемых цен означает неоднородную аудиторию — вероятно, " +
                "в выборке смешаны сегменты с разной готовностью платить.")
            .WarningIf(Respondents < 50,
                $"Респондентов всего {Respondents}. Пересечения кривых на малой выборке " +
                "смещаются на десятки процентов от замены нескольких ответов.")
            .WarningIf(inconsistentShare > 0.1,
                $"Отброшено {Fmt.Pct(inconsistentShare)} ответов с нарушенным порядком цен. " +
                "Высокая доля означает, что вопрос был непонятен респондентам.")
            .Warning("Метод измеряет декларируемую, а не фактическую готовность платить: " +
                     "респондент ничего не платит и склонен занижать верхнюю границу.")
            .Warning("Метод не даёт объёма продаж. Он очерчивает коридор, внутри которого " +
                     "цену надо выбирать по экономике, а не по опросу.")
            .Recommendation("Проверьте найденный коридор методом Габора — Грейнджера: " +
                            "он даёт кривую спроса и позволяет посчитать выручку.")
            .RecommendationIf(relativeWidth > 0.8,
                "Разделите выборку на сегменты и постройте кривые отдельно: единая цена " +
                "для неоднородной аудитории оставляет деньги на столе.")
            .Build();
    }
}

/// <summary>Результат анализа по методу Габора — Грейнджера.</summary>
public sealed record GaborGrangerResult : IInterpretable
{
    /// <summary>Протестированные цены.</summary>
    public Vector Prices { get; init; } = new Vector(0);

    /// <summary>Доля согласных купить по каждой цене.</summary>
    public Vector AcceptanceRates { get; init; } = new Vector(0);

    /// <summary>Ожидаемая выручка на респондента по каждой цене.</summary>
    public Vector Revenue { get; init; } = new Vector(0);

    /// <summary>Ожидаемая прибыль на респондента по каждой цене.</summary>
    public Vector Profit { get; init; } = new Vector(0);

    /// <summary>Цена, максимизирующая выручку.</summary>
    public double RevenueOptimalPrice { get; init; }

    /// <summary>Цена, максимизирующая прибыль.</summary>
    public double ProfitOptimalPrice { get; init; }

    /// <summary>Доля согласных купить по цене, максимизирующей прибыль.</summary>
    public double AcceptanceAtProfitOptimum { get; init; }

    /// <summary>Эластичность спроса в окрестности оптимума по прибыли.</summary>
    public double ElasticityAtOptimum { get; init; }

    /// <summary>Переменные издержки, использованные в расчёте прибыли.</summary>
    public double UnitCost { get; init; }

    /// <summary>Число респондентов.</summary>
    public int Respondents { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool priceOptimumAboveRevenue = ProfitOptimalPrice > RevenueOptimalPrice * 1.01;

        return new InterpretationBuilder("Готовность платить: метод Габора — Грейнджера")
            .Summary($"Прибыль максимальна при цене {Fmt.Money(ProfitOptimalPrice)}: по ней готовы " +
                     $"купить {Fmt.Pct(AcceptanceAtProfitOptimum)} респондентов. Выручка максимальна " +
                     $"при {Fmt.Money(RevenueOptimalPrice)} — это другая цена, и ориентироваться " +
                     $"надо на первую.")
            .Metric("Цена по прибыли", Fmt.Money(ProfitOptimalPrice), null,
                "максимум ожидаемой прибыли", MetricQuality.Good)
            .Metric("Цена по выручке", Fmt.Money(RevenueOptimalPrice), null,
                "максимум ожидаемой выручки, обычно ниже")
            .Metric("Конверсия в оптимуме", Fmt.Pct(AcceptanceAtProfitOptimum), null,
                "доля согласившихся купить")
            .Metric("Эластичность в оптимуме", ElasticityAtOptimum, null,
                "локальная чувствительность спроса к цене",
                Math.Abs(ElasticityAtOptimum) > 1 ? MetricQuality.Neutral : MetricQuality.Warning)
            .Metric("Респондентов", Respondents, null, null, MetricQuality.Unknown, 0)
            .FindingIf(priceOptimumAboveRevenue,
                "Оптимум по прибыли выше оптимума по выручке — обычная ситуация: " +
                "при наличии переменных издержек часть дешёвых продаж не окупается.")
            .FindingIf(AcceptanceAtProfitOptimum < 0.3,
                $"В оптимуме покупает лишь {Fmt.Pct(AcceptanceAtProfitOptimum)} аудитории. " +
                "Это нормально для премиального позиционирования и опасно там, где важен охват.")
            .WarningIf(Respondents < 50,
                $"Респондентов всего {Respondents}: кривая согласия оценена грубо.")
            .WarningIf(Prices.Count < 4,
                "Протестировано меньше четырёх цен: оптимум определяется сеткой, а не данными.")
            .Warning("Метод завышает готовность платить: согласие в опросе ничего не стоит " +
                     "респонденту. Ожидайте, что фактическая конверсия будет ниже.")
            .Warning("Порядок предъявления цен влияет на ответы. Убедитесь, что цены " +
                     "предъявлялись в случайном порядке, а не по возрастанию.")
            .Recommendation("Сопоставьте результат с реальной конверсией на текущей цене " +
                            "и откалибруйте кривую на это отношение.")
            .Build();
    }
}

/// <summary>
/// Исследование готовности платить методами Ван Вестендорпа и
/// Габора — Грейнджера.
/// </summary>
/// <remarks>
/// Оба метода отвечают на разные вопросы. Ван Вестендорп очерчивает коридор
/// приемлемых цен и хорошо работает для нового продукта, у которого нет
/// аналогов. Габор — Грейнджер строит кривую спроса и позволяет посчитать
/// выручку и прибыль, но требует, чтобы респондент понимал, что именно
/// покупает.
/// </remarks>
public static class WillingnessToPay
{
    /// <summary>Анализ по методу Ван Вестендорпа.</summary>
    /// <param name="answers">Ответы респондентов.</param>
    /// <param name="gridPoints">Число точек ценовой сетки.</param>
    /// <returns>Кривые, четыре характерные точки и их разбор.</returns>
    /// <exception cref="ArgumentNullException">Ответы не заданы.</exception>
    /// <exception cref="ArgumentException">Нет ни одного согласованного ответа.</exception>
    public static VanWestendorpResult VanWestendorp(
        IReadOnlyList<VanWestendorpAnswer> answers, int gridPoints = 120)
    {
        ArgumentNullException.ThrowIfNull(answers);

        // Ответ считается согласованным, если цены упорядочены по смыслу вопросов
        List<VanWestendorpAnswer> valid =
        [
            .. answers.Where(a => a.TooCheap > 0
                                  && a.TooCheap <= a.Cheap
                                  && a.Cheap <= a.Expensive
                                  && a.Expensive <= a.TooExpensive),
        ];

        if (valid.Count == 0)
            throw new ArgumentException(
                "Нет ответов с корректным порядком цен: слишком дёшево <= выгодно <= дорого <= слишком дорого.",
                nameof(answers));

        double min = valid.Min(a => a.TooCheap);
        double max = valid.Max(a => a.TooExpensive);
        if (max <= min) max = min * 1.5 + 1;

        if (gridPoints < 10) gridPoints = 10;

        var prices = new Vector(gridPoints);
        var tooCheap = new Vector(gridPoints);
        var cheap = new Vector(gridPoints);
        var expensive = new Vector(gridPoints);
        var tooExpensive = new Vector(gridPoints);

        for (int i = 0; i < gridPoints; i++)
        {
            double p = min + ((max - min) * i / (gridPoints - 1));
            prices[i] = p;

            // Убывающие кривые: «при цене p товар кажется слишком дешёвым»
            tooCheap[i] = valid.Count(a => a.TooCheap >= p) / (double)valid.Count;
            cheap[i] = valid.Count(a => a.Cheap >= p) / (double)valid.Count;

            // Возрастающие: «при цене p товар кажется дорогим»
            expensive[i] = valid.Count(a => a.Expensive <= p) / (double)valid.Count;
            tooExpensive[i] = valid.Count(a => a.TooExpensive <= p) / (double)valid.Count;
        }

        return new VanWestendorpResult
        {
            Prices = prices,
            TooCheapCurve = tooCheap,
            CheapCurve = cheap,
            ExpensiveCurve = expensive,
            TooExpensiveCurve = tooExpensive,
            PointOfMarginalCheapness = Crossing(prices, tooCheap, expensive),
            PointOfMarginalExpensiveness = Crossing(prices, cheap, tooExpensive),
            OptimalPricePoint = Crossing(prices, tooCheap, tooExpensive),
            IndifferencePricePoint = Crossing(prices, cheap, expensive),
            Respondents = valid.Count,
            InconsistentAnswers = answers.Count - valid.Count,
        };
    }

    /// <summary>Анализ по методу Габора — Грейнджера.</summary>
    /// <param name="prices">Протестированные цены по возрастанию.</param>
    /// <param name="acceptanceRates">Доля согласных купить по каждой цене.</param>
    /// <param name="unitCost">Переменные издержки на единицу.</param>
    /// <param name="respondents">Число респондентов; используется в предупреждениях.</param>
    /// <returns>Кривые выручки и прибыли с оптимальными ценами.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Длины не совпадают или цен слишком мало.</exception>
    public static GaborGrangerResult GaborGranger(
        Vector prices, Vector acceptanceRates, double unitCost = 0, int respondents = 0)
    {
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(acceptanceRates);

        if (prices.Count != acceptanceRates.Count)
            throw new ArgumentException("Длины векторов цен и долей согласия должны совпадать.",
                nameof(acceptanceRates));
        if (prices.Count < 2)
            throw new ArgumentException("Нужно минимум две цены.", nameof(prices));

        int n = prices.Count;
        var revenue = new Vector(n);
        var profit = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            revenue[i] = prices[i] * acceptanceRates[i];
            profit[i] = (prices[i] - unitCost) * acceptanceRates[i];
        }

        int revenueBest = ArgMax(revenue);
        int profitBest = ArgMax(profit);

        return new GaborGrangerResult
        {
            Prices = prices,
            AcceptanceRates = acceptanceRates,
            Revenue = revenue,
            Profit = profit,
            RevenueOptimalPrice = prices[revenueBest],
            ProfitOptimalPrice = prices[profitBest],
            AcceptanceAtProfitOptimum = acceptanceRates[profitBest],
            ElasticityAtOptimum = LocalElasticity(prices, acceptanceRates, profitBest),
            UnitCost = unitCost,
            Respondents = respondents,
        };
    }

    /// <summary>
    /// Абсцисса пересечения двух кривых. Кривые ступенчатые, поэтому точка
    /// ищется по смене знака разности с линейной интерполяцией внутри шага.
    /// </summary>
    private static double Crossing(Vector prices, Vector first, Vector second)
    {
        for (int i = 1; i < prices.Count; i++)
        {
            double previous = first[i - 1] - second[i - 1];
            double current = first[i] - second[i];

            if (previous == 0) return prices[i - 1];
            if (previous * current < 0)
            {
                double t = previous / (previous - current);
                return prices[i - 1] + (t * (prices[i] - prices[i - 1]));
            }
        }

        return prices[prices.Count - 1];
    }

    private static int ArgMax(Vector v)
    {
        int best = 0;
        for (int i = 1; i < v.Count; i++)
            if (v[i] > v[best]) best = i;
        return best;
    }

    /// <summary>Эластичность по соседним точкам сетки в логарифмических приращениях.</summary>
    private static double LocalElasticity(Vector prices, Vector quantities, int index)
    {
        int low = Math.Max(index - 1, 0);
        int high = Math.Min(index + 1, prices.Count - 1);
        if (low == high) return double.NaN;

        double dLogP = Math.Log(prices[high]) - Math.Log(prices[low]);
        double dLogQ = Math.Log(Math.Max(quantities[high], 1e-9)) - Math.Log(Math.Max(quantities[low], 1e-9));

        return Math.Abs(dLogP) > 1e-12 ? dLogQ / dLogP : double.NaN;
    }
}
