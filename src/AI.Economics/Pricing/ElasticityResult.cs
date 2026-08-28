using System;
using AI.Economics.Insights;

namespace AI.Economics.Pricing;

/// <summary>Оценка ценовой эластичности спроса.</summary>
public sealed record ElasticityResult : IInterpretable
{
    /// <summary>Использованный способ оценки.</summary>
    public ElasticityEstimator Estimator { get; init; }

    /// <summary>
    /// Эластичность: на сколько процентов меняется спрос при росте цены на
    /// один процент. У нормального товара отрицательна.
    /// </summary>
    public double Elasticity { get; init; }

    /// <summary>Стандартная ошибка оценки.</summary>
    public double StandardError { get; init; }

    /// <summary>Статистика Стьюдента.</summary>
    public double TStatistic { get; init; }

    /// <summary>Двустороннее p-значение.</summary>
    public double PValue { get; init; }

    /// <summary>Нижняя граница 95-процентного доверительного интервала.</summary>
    public double ConfidenceLow { get; init; }

    /// <summary>Верхняя граница 95-процентного доверительного интервала.</summary>
    public double ConfidenceHigh { get; init; }

    /// <summary>Коэффициент детерминации модели.</summary>
    public double RSquared { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>
    /// Наивная оценка обычным МНК — приводится всегда для сравнения.
    /// Расхождение с основной оценкой показывает величину смещения.
    /// </summary>
    public double NaiveElasticity { get; init; }

    /// <summary>
    /// F-статистика исключённых инструментов на первой ступени.
    /// Значение ниже 10 означает слабый инструмент.
    /// </summary>
    public double FirstStageF { get; init; } = double.NaN;

    /// <summary>Эластичен ли спрос: модуль эластичности больше единицы.</summary>
    public bool IsElastic => Math.Abs(Elasticity) > 1.0;

    /// <summary>
    /// Оптимальная наценка к переменным издержкам по правилу Лернера:
    /// <c>p = c e / (1 + e)</c>. Определена только при эластичном спросе.
    /// </summary>
    public double OptimalMarkup => IsElastic && Elasticity < 0
        ? (Elasticity / (1.0 + Elasticity)) - 1.0
        : double.NaN;

    /// <summary>
    /// Валовая маржа, при которой снижение цены на процент окупается ростом
    /// объёма: <c>1 / |e|</c>.
    /// </summary>
    public double BreakEvenMargin => Math.Abs(Elasticity) > 1e-9 ? 1.0 / Math.Abs(Elasticity) : double.NaN;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        string estimatorName = Estimator switch
        {
            ElasticityEstimator.LogLogOls => "лог-логарифмическая регрессия (МНК)",
            ElasticityEstimator.PanelFixedEffects => "панельная модель с фиксированными эффектами",
            ElasticityEstimator.InstrumentalVariables => "двухшаговый МНК с инструментом",
            _ => Estimator.ToString(),
        };

        double bias = Elasticity - NaiveElasticity;
        bool wrongSign = Elasticity > 0;
        bool insignificant = PValue > 0.05;
        bool weakInstrument = Estimator == ElasticityEstimator.InstrumentalVariables
                              && !double.IsNaN(FirstStageF) && FirstStageF < 10;

        var builder = new InterpretationBuilder("Эластичность спроса по цене")
            .Summary($"Способ оценки: {estimatorName}. Эластичность {Fmt.Num(Elasticity)} " +
                     $"означает, что рост цены на 1 % меняет объём продаж на {Fmt.Num(Elasticity)} %. " +
                     (IsElastic
                         ? "Спрос эластичный: выручка растёт при снижении цены."
                         : "Спрос неэластичный: выручка растёт при повышении цены."))
            .Metric("Эластичность", Elasticity, null,
                "процентов изменения спроса на 1 % изменения цены",
                wrongSign ? MetricQuality.Critical : MetricQuality.Neutral)
            .Metric("95 % интервал", $"[{Fmt.Num(ConfidenceLow)}; {Fmt.Num(ConfidenceHigh)}]", null,
                "диапазон, совместимый с данными",
                ConfidenceLow < 0 && ConfidenceHigh < 0 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("p-значение", PValue, null, "вероятность увидеть такой эффект при его отсутствии",
                insignificant ? MetricQuality.Warning : MetricQuality.Good, 4)
            .Metric("Наивная оценка МНК", NaiveElasticity, null,
                "что дала бы регрессия без поправок", MetricQuality.Unknown)
            .Metric("R2", RSquared, null, "доля объяснённой дисперсии логарифма спроса", MetricQuality.Unknown)
            .Metric("Наблюдений", Observations, null, null, MetricQuality.Unknown, 0);

        if (!double.IsNaN(FirstStageF))
        {
            builder.Metric("F первой ступени", FirstStageF, null,
                "сила инструмента, порог надёжности — 10",
                weakInstrument ? MetricQuality.Critical : MetricQuality.Good);
        }

        builder
            .FindingIf(Estimator != ElasticityEstimator.LogLogOls && Math.Abs(bias) > 0.15,
                $"Поправка на смещение изменила оценку на {Fmt.Num(Math.Abs(bias))} " +
                $"({Fmt.Num(NaiveElasticity)} у наивного МНК против {Fmt.Num(Elasticity)}). " +
                "Наивная регрессия занижает модуль эластичности, потому что цену поднимают " +
                "тогда, когда спрос и так высок.")
            .FindingIf(IsElastic && Elasticity < 0,
                $"Оптимальная наценка к переменным издержкам по правилу Лернера: " +
                $"{Fmt.Pct(OptimalMarkup)}.")
            .FindingIf(Elasticity < 0,
                $"Снижение цены окупается объёмом только при валовой марже выше " +
                $"{Fmt.Pct(BreakEvenMargin)}.")
            .FindingIf(!IsElastic && Elasticity < 0,
                "Спрос неэластичен: повышение цены увеличит и выручку, и прибыль — " +
                "ограничением становится не экономика, а восприятие клиентов.")
            .WarningIf(wrongSign,
                "Эластичность положительна. Это либо товар Веблена, либо, что вероятнее, " +
                "нерешённая эндогенность цены: оценке в таком виде верить нельзя.")
            .WarningIf(insignificant,
                "Эффект статистически незначим: данных не хватает, чтобы отличить его от нуля.")
            .WarningIf(weakInstrument,
                $"Слабый инструмент (F = {Fmt.Num(FirstStageF)} при пороге 10). " +
                "Оценка IV в этом случае смещена сильнее наивного МНК.")
            .WarningIf(Estimator == ElasticityEstimator.LogLogOls,
                "Обычный МНК не решает проблему эндогенности цены. Оценка пригодна " +
                "для описания, но не для решения об изменении цены.")
            .WarningIf(Observations < 30,
                $"Наблюдений всего {Observations}: доверительный интервал по нормальному " +
                "приближению слишком узок.")
            .Warning("Оценка описывает связь в наблюдавшемся диапазоне цен. Перенос вывода " +
                     "на цены за его пределами — уже допущение, а не измерение.")
            .RecommendationIf(Estimator == ElasticityEstimator.LogLogOls,
                "Повторите оценку с инструментом для цены (себестоимость, курс, цена конкурента) " +
                "или на панели с фиксированными эффектами.")
            .RecommendationIf(IsElastic && Elasticity < 0 && !insignificant,
                "Считайте оптимальную цену: при эластичном спросе текущая цена почти наверняка " +
                "не максимизирует прибыль.")
            .RecommendationIf(weakInstrument,
                "Найдите более сильный инструмент либо переходите к панельной модели.");

        return builder.Build();
    }
}
