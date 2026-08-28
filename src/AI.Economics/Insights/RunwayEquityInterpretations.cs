using System;
using System.Linq;
using AI.Economics.Insights;

namespace AI.Economics.Runway;

/// <summary>Разбор стохастической оценки запаса прочности.</summary>
public sealed partial record RunwayResult : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        string Months(double v) => double.IsPositiveInfinity(v) ? "за горизонтом" : Fmt.Num(v, 1);

        bool deterministicOptimistic = double.IsFinite(DeterministicRunwayMonths)
                                       && double.IsFinite(CashOutP10)
                                       && DeterministicRunwayMonths > CashOutP10 * 1.2;

        return new InterpretationBuilder("Запас прочности: стохастическая оценка")
            .Summary($"Детерминированный расчёт «касса делить на burn» даёт " +
                     $"{Months(DeterministicRunwayMonths)} месяцев. Симуляция показывает разброс: " +
                     $"в неудачном сценарии деньги кончаются к {Months(CashOutP10)} месяцу, " +
                     $"медианно — к {Months(CashOutP50)}. Вероятность дожить до конца горизонта " +
                     $"{Fmt.Pct(SurvivalProbability)}.")
            .Metric("Детерминированный runway", Months(DeterministicRunwayMonths), "месяцев",
                "то, что обычно называют runway", MetricQuality.Warning)
            .Metric("P10", Months(CashOutP10), "месяцев",
                "в одном случае из десяти деньги кончатся к этому сроку", MetricQuality.Critical)
            .Metric("Медиана", Months(CashOutP50), "месяцев", "половина траекторий кончается раньше")
            .Metric("P90", Months(CashOutP90), "месяцев", "оптимистичный сценарий")
            .Metric("Дожили до горизонта", Fmt.Pct(SurvivalProbability), null,
                "доля траекторий без кассового разрыва",
                SurvivalProbability > 0.8 ? MetricQuality.Good
                    : SurvivalProbability > 0.5 ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("Риск разрыва за год", Fmt.Pct(ProbabilityCashOutIn12), null,
                "вероятность остаться без денег в ближайшие 12 месяцев",
                ProbabilityCashOutIn12 < 0.1 ? MetricQuality.Good
                    : ProbabilityCashOutIn12 < 0.3 ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("Выход в плюс", Fmt.Pct(ProbabilityBreakEven), null,
                double.IsNaN(MedianBreakEvenMonth)
                    ? "большинство траекторий в плюс не выходит"
                    : $"медианно на {Fmt.Num(MedianBreakEvenMonth, 0)} месяце",
                ProbabilityBreakEven > 0.5 ? MetricQuality.Good : MetricQuality.Warning)
            .Finding("Решение о выходе на раунд принимается по левому хвосту распределения. " +
                     "Важно не то, когда деньги кончатся «в среднем», а когда они кончатся " +
                     "в неудачном сценарии — потому что именно тогда придётся идти к инвестору " +
                     "с плохой переговорной позицией.")
            .FindingIf(deterministicOptimistic,
                $"Детерминированная оценка оптимистичнее пессимистичного сценария на " +
                $"{Fmt.Num(DeterministicRunwayMonths - CashOutP10, 1)} месяцев. Планирование по ней " +
                "оставляет компанию без запаса на переговоры.")
            .FindingIf(ProbabilityBreakEven > 0.5 && !double.IsNaN(MedianBreakEvenMonth),
                $"Больше половины траекторий выходит на положительный поток к " +
                $"{Fmt.Num(MedianBreakEvenMonth, 0)} месяцу — это сильный аргумент в переговорах.")
            .WarningIf(ProbabilityCashOutIn6 > 0.1,
                $"Вероятность кассового разрыва в ближайшие полгода {Fmt.Pct(ProbabilityCashOutIn6)}. " +
                "Начинать раунд надо было вчера.")
            .Warning("Модель предполагает независимость месячных приращений. Реальные шоки " +
                     "автокоррелированы: плохой квартал идёт целиком, поэтому настоящий " +
                     "левый хвост тяжелее модельного.")
            .Warning("Затраты в модели растут по заданному темпу и не реагируют на падение " +
                     "выручки. Это сознательно консервативно: опции сокращения расходов в модели нет.")
            .Recommendation("Проверьте чувствительность к волатильности: если разброс " +
                            "определяет решение сильнее, чем средний темп роста, " +
                            "уточняйте именно волатильность.")
            .Build();
    }
}
