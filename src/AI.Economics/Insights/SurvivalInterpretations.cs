using System;
using System.Collections.Generic;
using System.Linq;
using AI.Insights;

namespace AI.Economics.Survival;

/// <summary>Разбор кривой Каплана — Мейера.</summary>
public sealed partial class KaplanMeier : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var times = Times;
        var curve = SurvivalCurve;
        int last = curve.Count - 1;

        double finalSurvival = last >= 0 ? curve[last] : double.NaN;
        double width = last >= 0 ? Upper[last] - Lower[last] : double.NaN;
        double horizon = last >= 0 ? times[last] : 0;
        double rmst = last >= 0 ? RestrictedMeanSurvival(horizon) : double.NaN;
        bool medianReached = !double.IsNaN(MedianSurvivalTime);

        return new InterpretationBuilder("Кривая дожития Каплана — Мейера")
            .Summary((medianReached
                         ? $"Половина клиентов уходит к {Fmt.Num(MedianSurvivalTime, 1)} периоду. "
                         : "Кривая не опустилась до половины: медианный срок жизни выходит за горизонт наблюдений. ") +
                     $"К концу наблюдений доживает {Fmt.Pct(finalSurvival)}, ограниченное среднее " +
                     $"время жизни — {Fmt.Num(rmst, 1)} периодов.")
            .Metric("Медиана", medianReached ? Fmt.Num(MedianSurvivalTime, 1) : "за горизонтом",
                medianReached ? "периодов" : null, "момент, когда доживает половина",
                medianReached ? MetricQuality.Neutral : MetricQuality.Warning)
            .Metric("Ограниченное среднее", rmst, "периодов",
                "площадь под кривой; определено всегда, в отличие от медианы")
            .Metric("Доживание на горизонте", Fmt.Pct(finalSurvival), null,
                $"интервал {Fmt.Pct(ConfidenceLevel, 0)}: {Fmt.Pct(Lower.Count > 0 ? Lower[last] : double.NaN)}" +
                $"–{Fmt.Pct(Upper.Count > 0 ? Upper[last] : double.NaN)}")
            .Metric("Ширина интервала на конце", Fmt.Pct(width), null,
                "растёт по мере уменьшения числа под риском",
                width > 0.3 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Под риском в конце", Fmt.Int(AtRisk.Count > 0 ? AtRisk[last] : 0), "клиентов",
                "правый конец кривой опирается на них",
                AtRisk.Count > 0 && AtRisk[last] < 20 ? MetricQuality.Warning : MetricQuality.Good)
            .Finding("Цензурированные наблюдения не создают ступенек, но уменьшают число под риском. " +
                     "Именно поэтому оценка не смещается ни при выбрасывании таких клиентов, " +
                     "ни при зачислении их в выжившие.")
            .FindingIf(!medianReached,
                "Медиана не достигнута — для сравнения групп используйте ограниченное среднее " +
                "время жизни: оно определено всегда.")
            .WarningIf(AtRisk.Count > 0 && AtRisk[last] < 20,
                $"На правом конце под риском остаётся {Fmt.Int(AtRisk[last])} клиентов. " +
                "Отдельные уходы двигают кривую на проценты — не делайте выводов по хвосту.")
            .Warning("Оценка непараметрическая и за горизонт наблюдений не экстраполируется: " +
                     "правее последнего события кривая просто продолжается горизонтально.")
            .Warning("Предполагается неинформативное цензурирование: причина обрыва наблюдения " +
                     "не должна быть связана с риском ухода.")
            .Recommendation("Для прогноза за горизонт данных подгоните параметрическую кривую " +
                            "удержания — она даст экстраполяцию с интервалом.")
            .Build();
    }
}

/// <summary>Разбор регрессии Кокса.</summary>
public sealed partial class CoxProportionalHazards : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var significant = Coefficients.Where(c => c.PValue < 0.05).ToList();
        CoxCoefficient? strongest = Coefficients
            .OrderByDescending(c => Math.Abs(Math.Log(Math.Max(c.HazardRatio, 1e-9))))
            .FirstOrDefault();

        bool separation = Coefficients.Any(c => Math.Abs(c.Beta) > 8 || double.IsNaN(c.StandardError));

        var builder = new InterpretationBuilder("Регрессия пропорциональных рисков Кокса")
            .Summary($"Значимых признаков: {significant.Count} из {Coefficients.Count}. " +
                     (strongest is null
                         ? string.Empty
                         : $"Сильнее всего на отток влияет «{strongest.Name}»: отношение рисков " +
                           $"{Fmt.Num(strongest.HazardRatio)}, то есть рост признака на единицу " +
                           (strongest.HazardRatio > 1 ? "увеличивает" : "снижает") +
                           $" мгновенный риск ухода в {Fmt.Num(Math.Max(strongest.HazardRatio, 1 / Math.Max(strongest.HazardRatio, 1e-9)))} раза. ") +
                     $"Индекс конкордации {Fmt.Num(ConcordanceIndex, 3)}.")
            .Metric("Индекс конкордации", ConcordanceIndex, null,
                "доля верно упорядоченных пар; 0,5 — не лучше монетки",
                ConcordanceIndex > 0.7 ? MetricQuality.Good
                    : ConcordanceIndex > 0.6 ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("Значимых признаков", significant.Count, null,
                $"из {Coefficients.Count}", MetricQuality.Unknown, 0)
            .Metric("lnL частичное", LogPartialLikelihood, null,
                "для сравнения вложенных моделей", MetricQuality.Unknown, 1)
            .Metric("Итераций Ньютона", Iterations, null, "сходимость оценки",
                MetricQuality.Unknown, 0);

        foreach (CoxCoefficient c in Coefficients)
        {
            builder.Metric($"HR: {c.Name}", c.HazardRatio, "раз",
                $"интервал {Fmt.Num(c.HazardRatioLower)}–{Fmt.Num(c.HazardRatioUpper)}, " +
                $"p = {Fmt.Num(c.PValue, 4)}",
                c.PValue < 0.05 ? MetricQuality.Good : MetricQuality.Neutral);
        }

        builder
            .Finding("Базовый риск остаётся непараметрическим: форму кривой оттока угадывать " +
                     "не нужно, оцениваются только относительные эффекты признаков.")
            .FindingIf(significant.Count > 0,
                $"Признаки с доказанным эффектом: {string.Join(", ", significant.Select(c => c.Name))}. " +
                "Их можно использовать для ранжирования действующих клиентов по риску.")
            .FindingIf(ConcordanceIndex > 0.7,
                "Ранжирование работает: модель пригодна для приоритизации работы отдела удержания.")
            .WarningIf(ConcordanceIndex < 0.6,
                "Ранжирование почти не отличается от случайного. Имеющихся признаков " +
                "недостаточно, чтобы предсказать, кто уйдёт.")
            .WarningIf(separation,
                "Один из коэффициентов чрезмерно велик — признак полного разделения данных. " +
                "Шаг Ньютона демпфирован, но интерпретировать величину такого коэффициента нельзя.")
            .WarningIf(Coefficients.Count(c => c.PValue >= 0.05) > 0,
                $"{Coefficients.Count(c => c.PValue >= 0.05)} признаков незначимы: их эффект " +
                "неотличим от нуля.")
            .Warning("Название модели содержит её главное допущение: отношение рисков между " +
                     "двумя клиентами постоянно во времени. Если признак работает только " +
                     "в первые месяцы, оценка усредняет эффект и занижает его.")
            .Warning("Коэффициенты отражают связь, а не причину. Рост обращений в поддержку " +
                     "перед уходом — симптом, и запрет обращений отток не снизит.")
            .Recommendation("Проверяйте допущение пропорциональности, разбив выборку по времени " +
                            "и сравнив коэффициенты.");

        return builder.Build();
    }
}

/// <summary>
/// Разбор результата анализа конкурирующих рисков.
/// </summary>
/// <remarks>
/// Реализован методом расширения, потому что осмысленное заключение делается
/// не по одной причине, а по их набору: главное здесь — сумма долей и величина
/// завышения у наивной оценки.
/// </remarks>
public static class CompetingRisksInsights
{
    /// <summary>Разбирает набор функций инцидентности по всем причинам.</summary>
    /// <param name="causes">Результат анализа конкурирующих рисков.</param>
    /// <returns>Итог, метрики и предупреждения.</returns>
    /// <exception cref="ArgumentNullException">Результат не задан.</exception>
    public static Interpretation Interpret(this IReadOnlyList<CumulativeIncidence> causes)
    {
        ArgumentNullException.ThrowIfNull(causes);

        double sum = causes.Sum(c => c.FinalIncidence);
        double naiveSum = causes.Sum(c => c.FinalNaiveIncidence);
        CumulativeIncidence? dominant = causes.OrderByDescending(c => c.FinalIncidence).FirstOrDefault();

        var builder = new InterpretationBuilder("Конкурирующие риски ухода")
            .Summary($"Всего за период наблюдения ушло {Fmt.Pct(sum)} клиентов. Главная причина — " +
                     $"«{dominant?.Name}» с долей {Fmt.Pct(dominant?.FinalIncidence ?? 0)}. " +
                     $"Наивный расчёт по каждой причине отдельно дал бы в сумме {Fmt.Pct(naiveSum)} — " +
                     $"на {Fmt.Pct(naiveSum - sum)} больше, чем ушло на самом деле.")
            .Metric("Всего ушло", Fmt.Pct(sum), null,
                "сумма долей по причинам; не может превышать единицу", MetricQuality.Good)
            .Metric("Сумма наивных оценок", Fmt.Pct(naiveSum), null,
                "оценка 1 − KM по каждой причине",
                naiveSum > 1 ? MetricQuality.Critical : MetricQuality.Warning)
            .Metric("Завышение", Fmt.Pct(naiveSum - sum), null,
                "цена игнорирования конкуренции причин",
                naiveSum - sum > 0.1 ? MetricQuality.Critical : MetricQuality.Warning)
            .Metric("Причин", causes.Count, null, null, MetricQuality.Unknown, 0);

        foreach (CumulativeIncidence cause in causes.OrderByDescending(c => c.FinalIncidence))
        {
            builder.Metric(cause.Name, Fmt.Pct(cause.FinalIncidence), null,
                $"наивная оценка дала бы {Fmt.Pct(cause.FinalNaiveIncidence)}");
        }

        return builder
            .Finding("Причины конкурируют: наступившая первой лишает возможности наступить " +
                     "остальные. Клиент, чья компания закрылась, уже не уйдёт из-за цены.")
            .Finding("Наивная оценка считает уход по другой причине цензурированием, то есть " +
                     "предполагает, что такой клиент мог бы уйти по нашей причине позже. " +
                     "Отсюда систематическое завышение каждой доли.")
            .FindingIf(dominant is not null && dominant.FinalIncidence > sum * 0.5,
                $"Более половины оттока приходится на одну причину — «{dominant?.Name}». " +
                "Работа над ней даст больший эффект, чем над остальными вместе.")
            .WarningIf(naiveSum > 1,
                "Сумма наивных оценок превышает единицу. Такой отчёт внутренне противоречив " +
                "и не может использоваться для планирования.")
            .Warning("Причина ухода должна быть единственной и определяться однозначным правилом, " +
                     "зафиксированным до анализа.")
            .Recommendation("Разделяйте управляемые причины и внешние: доля внешнего оттока " +
                            "задаёт потолок того, что вообще можно улучшить.")
            .Build();
    }
}
