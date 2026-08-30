using System;
using System.Linq;
using AI.Insights;

namespace AI.Economics.Valuation;

/// <summary>Разбор оценки методом венчурного капитала.</summary>
public sealed partial record VcMethodResult : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double investment = PostMoneyValuation * OwnershipNow;
        bool impossible = OwnershipNow >= 0.6;

        return new InterpretationBuilder("Оценка методом венчурного капитала")
            .Summary($"Чтобы вложение выросло в {Fmt.Num(MoneyMultiple)} раза к выходу за " +
                     $"{Fmt.Money(ExitValue)}, инвестору нужно {Fmt.Pct(OwnershipAtExit)} компании " +
                     $"на момент выхода и {Fmt.Pct(OwnershipNow)} сегодня с поправкой на будущее " +
                     $"разводнение. Отсюда оценка до денег {Fmt.Money(PreMoneyValuation)}.")
            .Metric("Оценка до денег", Fmt.Money(PreMoneyValuation), null,
                "то, о чём идёт торг",
                PreMoneyValuation > 0 ? MetricQuality.Neutral : MetricQuality.Critical)
            .Metric("Оценка после денег", Fmt.Money(PostMoneyValuation), null,
                $"инвестиция {Fmt.Money(investment)}")
            .Metric("Доля сегодня", Fmt.Pct(OwnershipNow), null,
                "с поправкой на разводнение будущими раундами",
                impossible ? MetricQuality.Critical : MetricQuality.Neutral)
            .Metric("Доля при выходе", Fmt.Pct(OwnershipAtExit), null,
                "после всех будущих раундов")
            .Metric("Множитель", MoneyMultiple, "раз", "во сколько вырастет вложение")
            .Metric("Стоимость выхода", Fmt.Money(ExitValue), null, "выручка умножить на мультипликатор")
            .Finding("Метод целиком определяется двумя допущениями: требуемой доходностью " +
                     "и величиной будущего разводнения. Их и надо обсуждать, а не итоговую цифру.")
            .FindingIf(OwnershipAtExit < OwnershipNow * 0.8,
                $"Будущие раунды съедят заметную часть доли: с {Fmt.Pct(OwnershipNow)} сегодня " +
                $"до {Fmt.Pct(OwnershipAtExit)} к выходу. Инвестор закладывает это в цену входа.")
            .WarningIf(impossible,
                $"Требуемая доля {Fmt.Pct(OwnershipNow)} несовместима с мотивацией основателей. " +
                "Либо оценка выхода занижена, либо требуемая доходность нереалистична " +
                "для этой стадии.")
            .WarningIf(PreMoneyValuation <= 0,
                "Оценка до денег отрицательна: при заданной доходности инвестиция не окупается " +
                "даже при полном владении компанией.")
            .Warning("Стоимость выхода — прогноз на несколько лет вперёд. Ошибка в мультипликаторе " +
                     "или выручке переносится в оценку линейно.")
            .Recommendation("Посчитайте оценку ещё тремя методами: расхождение покажет, " +
                            "какое допущение управляет ценой.")
            .Build();
    }
}

/// <summary>Разбор сценарной оценки.</summary>
public sealed partial record ScenarioValuationResult : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var breakdown = Breakdown.OrderByDescending(b => b.Contribution).ToList();
        var top = breakdown.FirstOrDefault();
        double failureProbability = Breakdown.Where(b => b.Valuation <= 0).Sum(b => b.Probability);
        double coefficientOfVariation = ExpectedValuation > 0 ? StandardDeviation / ExpectedValuation : double.NaN;

        var builder = new InterpretationBuilder("Сценарная оценка (First Chicago)")
            .Summary($"Ожидаемая оценка {Fmt.Money(ExpectedValuation)} при стандартном отклонении " +
                     $"{Fmt.Money(StandardDeviation)}. На сценарий «{top.Name}» приходится " +
                     $"{Fmt.Pct(BestCaseShare)} ожидаемой стоимости при его вероятности " +
                     $"{Fmt.Pct(top.Probability)}.")
            .Metric("Ожидаемая оценка", Fmt.Money(ExpectedValuation), null,
                "взвешенная по вероятностям")
            .Metric("Стандартное отклонение", Fmt.Money(StandardDeviation), null,
                "разброс между сценариями",
                coefficientOfVariation > 1 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Вклад лучшего сценария", Fmt.Pct(BestCaseShare), null,
                "какая часть оценки держится на одном исходе",
                BestCaseShare > 0.7 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Вероятность провала", Fmt.Pct(failureProbability), null,
                "сценарии с нулевой стоимостью",
                failureProbability > 0.5 ? MetricQuality.Warning : MetricQuality.Neutral);

        foreach ((string name, double probability, double valuation, double contribution) in breakdown)
            builder.Metric(name, Fmt.Money(valuation), null,
                $"вероятность {Fmt.Pct(probability)}, вклад {Fmt.Money(contribution)}");

        return builder
            .Finding("Метод показывает ожидание, а не типичный исход. При высокой вероятности " +
                     "провала медианный результат равен нулю, и ожидаемая оценка об этом молчит.")
            .FindingIf(BestCaseShare > 0.7,
                $"Оценка держится на одном сценарии: {Fmt.Pct(BestCaseShare)} стоимости даёт " +
                $"«{top.Name}» с вероятностью {Fmt.Pct(top.Probability)}. Спорить надо " +
                "об этой вероятности, а не о цифре оценки.")
            .FindingIf(failureProbability > 0.5,
                $"Более половины вероятностной массы приходится на нулевые сценарии. " +
                "Это нормально для венчурного портфеля и неприемлемо для единственной ставки.")
            .WarningIf(!double.IsNaN(coefficientOfVariation) && coefficientOfVariation > 1.5,
                "Разброс превышает ожидание в полтора раза: сценарии слишком различны, " +
                "чтобы их усреднение имело практический смысл.")
            .Warning("Вероятности сценариев назначаются экспертно и обычно смещены: " +
                     "лучший исход переоценивают, худший недооценивают.")
            .Recommendation("Проверьте оценку на чувствительность к вероятности лучшего сценария: " +
                            "если она меняет результат вдвое, договариваться надо именно о ней.")
            .Build();
    }
}

/// <summary>Разбор оценки реального опциона.</summary>
public sealed partial record RealOptionResult : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool negativeNpv = StaticNpv < 0;
        bool worthWaiting = FlexibilityPremium > 0.05 * Math.Abs(BinomialValue);
        double methodGap = BlackScholesValue > 0
            ? Math.Abs(BinomialValue - BlackScholesValue) / BlackScholesValue
            : double.NaN;

        return new InterpretationBuilder("Оценка проекта методом реальных опционов")
            .Summary($"Статический NPV равен {Fmt.Money(StaticNpv)}, а с учётом права отложить " +
                     $"решение проект стоит {Fmt.Money(BinomialValue)}. Премия за гибкость — " +
                     $"{Fmt.Money(FlexibilityPremium)}. Запускать сейчас имеет смысл, начиная " +
                     $"со стоимости проекта {Fmt.Money(ImmediateExerciseThreshold)}.")
            .Metric("Статический NPV", Fmt.Money(StaticNpv), null,
                "классический ответ «сейчас или никогда»",
                negativeNpv ? MetricQuality.Critical : MetricQuality.Good)
            .Metric("Стоимость с опционом", Fmt.Money(BinomialValue), null,
                "право запустить в любой момент до конца срока", MetricQuality.Good)
            .Metric("Премия за гибкость", Fmt.Money(FlexibilityPremium), null,
                "сколько стоит право подождать",
                worthWaiting ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Порог запуска", Fmt.Money(ImmediateExerciseThreshold), null,
                "выше этой стоимости ждать невыгодно")
            .Metric("Дельта", Delta, null, "чувствительность к стоимости проекта")
            .Metric("Вероятность запуска", Fmt.Pct(ExerciseProbability), null,
                "риск-нейтральная оценка")
            .FindingIf(negativeNpv && BinomialValue > 0,
                "Проект с отрицательным NPV имеет положительную стоимость. Это не парадокс: " +
                "обязательства пока не приняты, а через год станет известно больше. " +
                "Классический расчёт систематически недооценивает такие проекты.")
            .FindingIf(worthWaiting,
                "Ждать выгоднее, чем запускать сейчас: неопределённость достаточно велика, " +
                "чтобы информация будущего года стоила отсрочки.")
            .FindingIf(!worthWaiting && !negativeNpv,
                "Премия за гибкость мала: проект стоит запускать сейчас, ожидание почти " +
                "ничего не добавляет.")
            .WarningIf(!double.IsNaN(methodGap) && methodGap > 0.05,
                $"Биномиальная оценка расходится с формулой Блэка — Шоулза на {Fmt.Pct(methodGap)}. " +
                "Расхождение создаёт утечка стоимости: она делает досрочный запуск осмысленным.")
            .Warning("Волатильность стоимости непубличного проекта не наблюдаема и берётся " +
                     "из аналогов. Результат к ней очень чувствителен, а проверить допущение нечем.")
            .Warning("Модель предполагает непрерывное изменение стоимости. Для НИОКР с явными " +
                     "вехами «испытание пройдено или нет» ближе дерево решений с дискретными исходами.")
            .Recommendation("Постройте зависимость стоимости опциона от волатильности: " +
                            "если решение меняется в диапазоне правдоподобных значений, " +
                            "уточняйте именно это допущение.")
            .Build();
    }
}

