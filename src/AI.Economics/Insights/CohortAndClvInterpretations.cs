using System;
using System.Linq;
using AI.Economics.Insights;

namespace AI.Economics.Cohorts;

/// <summary>Разбор подгонки кривой удержания.</summary>
public sealed partial record RetentionFitResult : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int horizon = Survival.Count - 1;
        double tailWidth = horizon >= 0 && SurvivalUpper.Count > horizon
            ? SurvivalUpper[horizon] - SurvivalLower[horizon]
            : double.NaN;

        double firstChurn = Observed.Count > 1 ? 1 - Observed[1] : double.NaN;
        double naiveLifetime = firstChurn > 0 ? 1 / firstChurn : double.NaN;
        bool risingRetention = RetentionRates.Count > 12 && RetentionRates[12] > RetentionRates[2] + 0.02;

        return new InterpretationBuilder("Кривая удержания клиентов")
            .Summary($"Лучше всего данные описывает модель «{Model}». Ожидаемый срок жизни клиента — " +
                     $"{Fmt.Num(ExpectedLifetime, 1)} периодов " +
                     $"(интервал {Fmt.Num(ExpectedLifetimeLower, 1)}–{Fmt.Num(ExpectedLifetimeUpper, 1)}). " +
                     $"К концу горизонта доживает {Fmt.Pct(Survival[horizon])} когорты." +
                     (double.IsNaN(naiveLifetime)
                         ? string.Empty
                         : $" Расчёт «единица делить на отток первого периода» дал бы " +
                           $"{Fmt.Num(naiveLifetime, 1)} — сравните."))
            .Metric("Модель", Model.ToString(), null, "выбрана по минимуму AIC")
            .Metric("Срок жизни", ExpectedLifetime, "периодов",
                $"интервал {Fmt.Pct(ConfidenceLevel, 0)}: " +
                $"{Fmt.Num(ExpectedLifetimeLower, 1)}–{Fmt.Num(ExpectedLifetimeUpper, 1)}")
            .Metric("Доживание на горизонте", Fmt.Pct(Survival[horizon]), null,
                $"интервал {Fmt.Pct(SurvivalLower[horizon])}–{Fmt.Pct(SurvivalUpper[horizon])}")
            .Metric("RMSE", Rmse, null, "отклонение подгонки от наблюдений",
                Rmse < 0.02 ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("AIC", Aic, null, "для сравнения с другими семействами кривых",
                MetricQuality.Unknown, 1)
            .Metric("Наблюдений", ObservedPeriods, "периодов",
                "дальше начинается экстраполяция", MetricQuality.Unknown, 0)
            .FindingIf(risingRetention,
                $"Мгновенное удержание растёт со временем: {Fmt.Pct(RetentionRates[2])} на втором " +
                $"периоде против {Fmt.Pct(RetentionRates[12])} на двенадцатом. Это следствие " +
                "неоднородности клиентов, и именно поэтому единая ставка оттока даёт неверный LTV.")
            .FindingIf(!double.IsNaN(naiveLifetime) && naiveLifetime < ExpectedLifetime * 0.7,
                $"Наивная оценка срока жизни занижает его в " +
                $"{Fmt.Num(ExpectedLifetime / Math.Max(naiveLifetime, 1e-9))} раза.")
            .FindingIf(horizon > ObservedPeriods * 2,
                $"Кривая продлена в {Fmt.Num((double)horizon / Math.Max(ObservedPeriods, 1), 1)} раза " +
                "дальше наблюдений — интервал на хвосте отражает цену этой экстраполяции.")
            .WarningIf(!double.IsNaN(tailWidth) && tailWidth > 0.25,
                $"Ширина интервала на конце горизонта {Fmt.Pct(tailWidth)}: данных не хватает " +
                "для уверенного суждения о дальнем хвосте.")
            .WarningIf(ObservedPeriods < 6,
                $"Наблюдений всего {ObservedPeriods} периодов. Любая параметрическая экстраполяция " +
                "на таком объёме — гипотеза, а не измерение.")
            .Warning("Подгонка предполагает однородность условий. Смена продукта, тарифов " +
                     "или каналов внутри периода наблюдения нарушает модель.")
            .Recommendation("Подставьте полученную кривую в расчёт LTV вместо постоянного оттока.")
            .Recommendation("Стройте кривые отдельно по каналам и тарифам: агрегированная " +
                            "кривая усредняет разные по качеству когорты.")
            .Build();
    }
}

/// <summary>Разбор когортной матрицы.</summary>
public sealed partial class CohortMatrix : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var pooled = PooledRetention();
        var observations = ObservationBase();
        int last = pooled.Count - 1;

        double total = CohortSizes().Sum();
        double coverage = total > 0 ? observations[last] / total : 0;

        // Дрейф качества: сравнение удержания первых и последних когорт
        // на одном и том же возрасте
        double early = 0, late = 0;
        int earlyCount = 0, lateCount = 0;
        int age = Math.Min(2, MaxAge);
        int half = Math.Max(CohortCount / 2, 1);

        for (int c = 0; c < CohortCount; c++)
        {
            if (!IsObserved(c, age) || this[c, 0] <= 0) continue;
            double value = this[c, age] / this[c, 0];
            if (c < half) { early += value; earlyCount++; }
            else { late += value; lateCount++; }
        }

        double drift = earlyCount > 0 && lateCount > 0 ? (late / lateCount) - (early / earlyCount) : double.NaN;

        return new InterpretationBuilder("Когортная матрица удержания")
            .Summary($"{CohortCount} когорт, всего {Fmt.Int(total)} клиентов. Сводное удержание " +
                     $"на возрасте {last} составляет {Fmt.Pct(pooled[last])}, за ним стоит " +
                     $"{Fmt.Int(observations[last])} клиентов — {Fmt.Pct(coverage)} базы." +
                     (double.IsNaN(drift)
                         ? string.Empty
                         : $" Поздние когорты удерживаются на {Fmt.Pct(Math.Abs(drift))} " +
                           (drift > 0 ? "лучше ранних." : "хуже ранних.")))
            .Metric("Когорт", CohortCount, null, "строк в треугольнике", MetricQuality.Unknown, 0)
            .Metric("Всего клиентов", Fmt.Int(total), null, "сумма размеров когорт")
            .Metric("Сводное удержание", Fmt.Pct(pooled[last]), null,
                $"на возрасте {last} периодов")
            .Metric("База последней точки", Fmt.Int(observations[last]), "клиентов",
                "столько данных стоит за правым концом кривой",
                coverage < 0.2 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Дрейф качества", double.IsNaN(drift) ? "не оценён" : Fmt.Pct(drift), null,
                "разница удержания поздних и ранних когорт",
                double.IsNaN(drift) ? MetricQuality.Unknown
                    : drift < -0.05 ? MetricQuality.Critical
                    : drift > 0.05 ? MetricQuality.Good : MetricQuality.Neutral)
            .Finding("Сводная кривая считается только по когортам, дожившим до соответствующего " +
                     "возраста. Суммирование непронаблюдённых ячеек как нулей обрушило бы " +
                     "правый конец кривой из-за нехватки данных, а не из-за оттока.")
            .FindingIf(!double.IsNaN(drift) && drift < -0.05,
                "Качество привлечения падает: поздние когорты удерживаются заметно хуже. " +
                "Проверьте, не изменился ли микс каналов.")
            .FindingIf(!double.IsNaN(drift) && drift > 0.05,
                "Качество привлечения растёт: поздние когорты удерживаются лучше. " +
                "Средняя по всей базе кривая занижает текущее положение дел.")
            .WarningIf(coverage < 0.2,
                $"Правый конец кривой опирается на {Fmt.Pct(coverage)} базы: одна-две когорты. " +
                "Не делайте по нему выводов о сроке жизни.")
            .WarningIf(CohortCount < 4,
                "Когорт слишком мало, чтобы отличить дрейф качества от случайных колебаний.")
            .Warning("Матрица описывает клиента как активного или неактивного. Частичное " +
                     "снижение выручки — переход на младший тариф, сокращение объёма — " +
                     "в ней не видно: для этого нужен разрез по деньгам, а не по логотипам.")
            .Recommendation("Передайте сводную кривую в подгонку — она даст срок жизни " +
                            "с доверительным интервалом.")
            .Build();
    }
}
