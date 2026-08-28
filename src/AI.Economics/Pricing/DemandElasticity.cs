using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Numerics;

namespace AI.Economics.Pricing;

/// <summary>
/// Оценка ценовой эластичности спроса тремя способами: наивным МНК,
/// панельной моделью с фиксированными эффектами и двухшаговым МНК.
/// </summary>
/// <remarks>
/// <para>
/// Ключевая проблема — эндогенность цены. Цену поднимают тогда, когда спрос
/// и так высок: перед праздниками, при дефиците, на растущем рынке. В данных
/// это выглядит так, будто высокая цена сама вызывает высокий спрос, и
/// наивная регрессия занижает модуль эластичности, а в тяжёлых случаях даёт
/// положительный знак — «чем дороже, тем больше покупают».
/// </para>
/// <para>
/// Два способа справиться. Панельная модель убирает всё постоянное внутри
/// товара и внутри периода, но не спасает от факторов, меняющихся вместе
/// с ценой. Инструментальные переменные решают задачу полностью, если
/// найдётся величина, влияющая на цену и не влияющая на спрос напрямую:
/// себестоимость, курс валюты, логистическая ставка.
/// </para>
/// </remarks>
public static class DemandElasticity
{
    /// <summary>Оценивает эластичность выбранным способом.</summary>
    /// <param name="observations">Наблюдения «цена — объём».</param>
    /// <param name="estimator">Способ оценки.</param>
    /// <returns>Оценка вместе с наивной для сравнения.</returns>
    /// <exception cref="ArgumentNullException">Наблюдения не заданы.</exception>
    /// <exception cref="ArgumentException">Мало наблюдений или отсутствует инструмент.</exception>
    public static ElasticityResult Estimate(
        IReadOnlyList<PriceObservation> observations,
        ElasticityEstimator estimator = ElasticityEstimator.LogLogOls)
    {
        ArgumentNullException.ThrowIfNull(observations);

        List<PriceObservation> data = [.. observations.Where(o => o.Price > 0 && o.Quantity > 0)];
        if (data.Count < 5)
            throw new ArgumentException(
                "Нужно минимум пять наблюдений с положительными ценой и объёмом.", nameof(observations));

        double naive = EstimateLogLog(data).Elasticity;

        return estimator switch
        {
            ElasticityEstimator.LogLogOls => EstimateLogLog(data) with { NaiveElasticity = naive },
            ElasticityEstimator.PanelFixedEffects => EstimatePanel(data) with { NaiveElasticity = naive },
            ElasticityEstimator.InstrumentalVariables => EstimateIv(data) with { NaiveElasticity = naive },
            _ => throw new ArgumentException("Неизвестный способ оценки.", nameof(estimator)),
        };
    }

    /// <summary>
    /// Оценивает эластичность всеми тремя способами — так видно величину
    /// смещения наивной регрессии.
    /// </summary>
    /// <param name="observations">Наблюдения «цена — объём».</param>
    /// <returns>Оценки в порядке нарастания требований к данным.</returns>
    public static IReadOnlyList<ElasticityResult> EstimateAll(IReadOnlyList<PriceObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var results = new List<ElasticityResult> { Estimate(observations, ElasticityEstimator.LogLogOls) };

        if (observations.Select(o => o.Unit).Distinct().Count() > 1)
            results.Add(Estimate(observations, ElasticityEstimator.PanelFixedEffects));

        if (observations.All(o => !double.IsNaN(o.Instrument)))
            results.Add(Estimate(observations, ElasticityEstimator.InstrumentalVariables));

        return results;
    }

    /// <summary>
    /// Прогноз спроса при заданной цене по модели постоянной эластичности.
    /// </summary>
    /// <param name="basePrice">Базовая цена.</param>
    /// <param name="baseQuantity">Спрос при базовой цене.</param>
    /// <param name="elasticity">Эластичность.</param>
    /// <param name="newPrice">Новая цена.</param>
    /// <returns>Ожидаемый объём продаж.</returns>
    public static double DemandAt(double basePrice, double baseQuantity, double elasticity, double newPrice)
        => basePrice > 0 && newPrice > 0
            ? baseQuantity * Math.Pow(newPrice / basePrice, elasticity)
            : 0;

    /// <summary>Кривая спроса по сетке цен — для графика и проверки формы.</summary>
    /// <param name="basePrice">Базовая цена.</param>
    /// <param name="baseQuantity">Спрос при базовой цене.</param>
    /// <param name="elasticity">Эластичность.</param>
    /// <param name="from">Начало сетки цен.</param>
    /// <param name="to">Конец сетки цен.</param>
    /// <param name="points">Число точек.</param>
    /// <returns>Цены и соответствующие объёмы.</returns>
    public static (Vector Prices, Vector Quantities) DemandCurve(
        double basePrice, double baseQuantity, double elasticity, double from, double to, int points = 60)
    {
        if (points < 2) points = 2;

        var prices = new Vector(points);
        var quantities = new Vector(points);

        for (int i = 0; i < points; i++)
        {
            double price = from + ((to - from) * i / (points - 1));
            prices[i] = price;
            quantities[i] = DemandAt(basePrice, baseQuantity, elasticity, price);
        }

        return (prices, quantities);
    }

    /// <summary>Лог-логарифмическая регрессия обычным МНК.</summary>
    private static ElasticityResult EstimateLogLog(List<PriceObservation> data)
    {
        int controls = ControlCount(data);
        int k = 2 + controls;

        var x = new double[data.Count, k];
        var y = new double[data.Count];

        for (int i = 0; i < data.Count; i++)
        {
            x[i, 0] = 1.0;
            x[i, 1] = Math.Log(data[i].Price);
            for (int c = 0; c < controls; c++) x[i, 2 + c] = data[i].Controls![c];
            y[i] = Math.Log(data[i].Quantity);
        }

        OlsFit fit = Ols.Fit(x, y) ?? throw new ArgumentException("Матрица регрессоров вырождена.");
        return FromFit(ElasticityEstimator.LogLogOls, fit, 1);
    }

    /// <summary>
    /// Панельная модель: двустороннее внутригрупповое преобразование убирает
    /// постоянные эффекты товара и периода.
    /// </summary>
    private static ElasticityResult EstimatePanel(List<PriceObservation> data)
    {
        int controls = ControlCount(data);
        int n = data.Count;

        var logPrice = new double[n];
        var logQuantity = new double[n];
        for (int i = 0; i < n; i++)
        {
            logPrice[i] = Math.Log(data[i].Price);
            logQuantity[i] = Math.Log(data[i].Quantity);
        }

        int[] units = [.. data.Select(o => o.Unit)];
        int[] periods = [.. data.Select(o => o.Period)];

        Demean(logPrice, units);
        Demean(logQuantity, units);

        bool hasTimeVariation = periods.Distinct().Count() > 1;
        if (hasTimeVariation)
        {
            Demean(logPrice, periods);
            Demean(logQuantity, periods);
        }

        var controlColumns = new double[controls][];
        for (int c = 0; c < controls; c++)
        {
            var column = new double[n];
            for (int i = 0; i < n; i++) column[i] = data[i].Controls![c];
            Demean(column, units);
            if (hasTimeVariation) Demean(column, periods);
            controlColumns[c] = column;
        }

        // После двустороннего преобразования свободный член тождественно нулевой
        var x = new double[n, 1 + controls];
        for (int i = 0; i < n; i++)
        {
            x[i, 0] = logPrice[i];
            for (int c = 0; c < controls; c++) x[i, 1 + c] = controlColumns[c][i];
        }

        OlsFit fit = Ols.Fit(x, logQuantity)
            ?? throw new ArgumentException("Панель вырождена: цена не меняется внутри групп.");

        int lostDegrees = units.Distinct().Count() + (hasTimeVariation ? periods.Distinct().Count() : 0);
        return FromFit(ElasticityEstimator.PanelFixedEffects, fit, 0) with
        {
            Observations = n - lostDegrees,
        };
    }

    /// <summary>Двухшаговый МНК с инструментом для логарифма цены.</summary>
    private static ElasticityResult EstimateIv(List<PriceObservation> data)
    {
        if (data.Any(o => double.IsNaN(o.Instrument) || o.Instrument <= 0))
            throw new ArgumentException(
                "Для оценки IV каждое наблюдение должно иметь положительный инструмент.", nameof(data));

        int controls = ControlCount(data);
        int n = data.Count;

        var endogenous = new double[n];
        var y = new double[n];
        var exogenous = new double[n, 1 + controls];
        var instruments = new double[n, 1];

        for (int i = 0; i < n; i++)
        {
            endogenous[i] = Math.Log(data[i].Price);
            y[i] = Math.Log(data[i].Quantity);
            exogenous[i, 0] = 1.0;
            for (int c = 0; c < controls; c++) exogenous[i, 1 + c] = data[i].Controls![c];
            instruments[i, 0] = Math.Log(data[i].Instrument);
        }

        var result = Ols.TwoStage(endogenous, exogenous, instruments, y)
            ?? throw new ArgumentException("Двухшаговый МНК не сошёлся: проверьте инструмент.");

        return FromFit(ElasticityEstimator.InstrumentalVariables, result.Second, 0) with
        {
            FirstStageF = result.InstrumentF,
        };
    }

    private static ElasticityResult FromFit(ElasticityEstimator estimator, OlsFit fit, int index) => new()
    {
        Estimator = estimator,
        Elasticity = fit.Beta[index],
        StandardError = fit.StandardErrors[index],
        TStatistic = fit.TStatistic(index),
        PValue = fit.PValue(index),
        ConfidenceLow = fit.ConfidenceLow(index),
        ConfidenceHigh = fit.ConfidenceHigh(index),
        RSquared = fit.RSquared,
        Observations = fit.Observations,
        NaiveElasticity = fit.Beta[index],
    };

    private static int ControlCount(List<PriceObservation> data)
    {
        int count = data[0].Controls?.Count ?? 0;
        if (count > 0 && data.Any(o => (o.Controls?.Count ?? 0) != count))
            throw new ArgumentException("Число контрольных переменных должно совпадать во всех наблюдениях.");
        return count;
    }

    /// <summary>Вычитает групповые средние — внутригрупповое преобразование.</summary>
    private static void Demean(double[] values, int[] groups)
    {
        var sums = new Dictionary<int, double>();
        var counts = new Dictionary<int, int>();

        for (int i = 0; i < values.Length; i++)
        {
            sums.TryGetValue(groups[i], out double s);
            counts.TryGetValue(groups[i], out int c);
            sums[groups[i]] = s + values[i];
            counts[groups[i]] = c + 1;
        }

        for (int i = 0; i < values.Length; i++)
            values[i] -= sums[groups[i]] / counts[groups[i]];
    }
}
