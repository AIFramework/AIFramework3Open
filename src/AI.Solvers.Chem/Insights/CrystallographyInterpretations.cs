using AI.Insights;
using System.Linq;

namespace AI.Solvers.Chem.Crystallography;

/// <summary>Разбор результата индицирования порошковой дифрактограммы.</summary>
public sealed partial class IndexingResult : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int lines = Lines?.Count ?? 0;
        double meanDeviation = lines > 0 ? Lines.Average(l => System.Math.Abs(l.Delta)) : double.NaN;
        bool few = lines < 5;
        bool reliable = FigureOfMerit >= 20 && !few && MaxDeviation < 0.1;

        return new InterpretationBuilder("Индицирование порошковой дифрактограммы")
            .Summary($"Дифрактограмма проиндицирована в кубической установке с параметром "
                + $"a = {Fmt.Num(Cell.A, 4)} Å, центрировка — {CenteringName(Centering)}. "
                + $"Проиндицировано линий: {lines}, наибольшее расхождение {Fmt.Num(MaxDeviation, 3)}°, "
                + $"критерий качества {Fmt.Num(FigureOfMerit, 1)}.")
            .Metric("Параметр a", Fmt.Num(Cell.A, 4), "Å", "ребро кубической ячейки")
            .Metric("Объём ячейки", Fmt.Num(Cell.Volume, 2), "Å³", "a³ для кубической сингонии")
            .Metric("Центрировка", CenteringName(Centering), null, "определена по погасаниям отражений")
            .Metric("Линий", lines, null, "число проиндицированных отражений",
                few ? MetricQuality.Warning : MetricQuality.Good, 0)
            .Metric("Максимальное расхождение", Fmt.Num(MaxDeviation, 4), "°",
                "наибольшее отличие расчётного угла от наблюдённого",
                MaxDeviation < 0.05 ? MetricQuality.Good
                    : MaxDeviation < 0.15 ? MetricQuality.Neutral
                    : MetricQuality.Warning)
            .Metric("Среднее расхождение", Fmt.Num(meanDeviation, 4), "°", "по всем линиям")
            .Metric("Критерий качества", Fmt.Num(FigureOfMerit, 1), null,
                "отношение числа линий к среднему расхождению; чем больше, тем увереннее решение",
                FigureOfMerit >= 20 ? MetricQuality.Good
                    : FigureOfMerit >= 10 ? MetricQuality.Neutral
                    : MetricQuality.Warning)
            .FindingIf(reliable,
                "Решение согласовано: расхождения малы, линий достаточно, критерий качества высок. "
                + "Кубическая метрика описывает наблюдённый набор отражений.")
            .FindingIf(few,
                $"Линий всего {lines}. На таком числе отражений кубическую метрику можно подобрать "
                + "случайно: чем меньше линий, тем больше решёток им удовлетворяет.")
            .FindingIf(MaxDeviation >= 0.15,
                $"Наибольшее расхождение {Fmt.Num(MaxDeviation, 3)}° велико для порошковой съёмки. "
                + "Причиной бывает смещение нуля гониометра, смещение образца или неверная сингония.")
            .FindingIf(Centering != LatticeCentering.Primitive,
                $"Погасания указывают на {CenteringName(Centering)} решётку — это сокращает набор "
                + "разрешённых отражений и должно согласовываться с пространственной группой.")
            .Warning("Индицирование выполнено в предположении кубической сингонии. Решётка более низкой "
                + "симметрии с близкими параметрами даёт похожий набор углов, поэтому согласие само по себе "
                + "не доказывает кубичность.")
            .Warning("Учтены только положения линий. Интенсивности в расчёт не входят, а значит "
                + "структура — расположение атомов в ячейке — этим результатом не определена.")
            .Warning("Посторонние линии от примесных фаз метод не отделяет: они портят критерий качества "
                + "либо, что хуже, подгоняются вместе с основными.")
            .Recommendation("Проверить решение уточнением ячейки по всем линиям и сравнить параметр "
                + "с базой известных фаз.")
            .RecommendationIf(!reliable,
                "Снять дифрактограмму в более широком угловом диапазоне: дальние линии сильнее всего "
                + "различают метрики решёток.")
            .RecommendationIf(MaxDeviation >= 0.15,
                "Проверить нуль гониометра и юстировку образца прежде, чем менять модель решётки.")
            .Build();
    }

    private static string CenteringName(LatticeCentering centering) => centering switch
    {
        LatticeCentering.Primitive => "примитивная",
        LatticeCentering.BodyCentred => "объёмноцентрированная",
        LatticeCentering.FaceCentred => "гранецентрированная",
        _ => centering.ToString(),
    };
}

/// <summary>Разбор доли фазы, найденной по корундовым числам.</summary>
public readonly partial record struct PhaseQuantity : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool observed = Intensity > 0;
        double reduced = ReferenceIntensityRatio > 0
            ? System.Math.Max(0, Intensity) / ReferenceIntensityRatio
            : double.NaN;
        bool minor = observed && MassFraction < 5;

        return new InterpretationBuilder($"Количественный фазовый анализ: {Phase}")
            .Summary(observed
                ? $"Массовая доля фазы «{Phase}» — {Fmt.Num(MassFraction, 1)} % по методу корундовых чисел: "
                  + $"интенсивность опорной линии {Fmt.Num(Intensity, 1)}, корундовое число {Fmt.Num(ReferenceIntensityRatio, 2)}."
                : $"Опорная линия фазы «{Phase}» не наблюдалась, и доля принята нулевой.")
            .Metric("Массовая доля", Fmt.Num(MassFraction, 2), "%", "доля среди найденных кристаллических фаз",
                minor ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Интенсивность опорной линии", Fmt.Num(Intensity, 1), null, "в тех же единицах, что у остальных фаз")
            .Metric("Корундовое число", Fmt.Num(ReferenceIntensityRatio, 2), null, "I/Ic — отношение к корунду в смеси 1:1")
            .Metric("Приведённая интенсивность", Fmt.Num(reduced, 2), null, "I / (I/Ic): доли пропорциональны именно ей")
            .FindingIf(!observed,
                "Нулевая доля означает, что линия не видна, а не что фазы нет: малое содержание теряется "
                + "в фоне раньше, чем обращается в нуль.")
            .FindingIf(minor,
                "Доля в единицы процентов опирается на слабую линию, сравнимую с фоном: её погрешность "
                + "растёт по мере уменьшения доли, и последняя цифра здесь не значима.")
            .Warning("Доли нормированы на сумму найденных кристаллических фаз и в сумме всегда дают 100 %. "
                + "Аморфная составляющая и неопознанные фазы в расчёт не входят — если они есть, все доли завышены.")
            .Warning("Интенсивность опорной линии искажают преимущественная ориентация кристаллитов и различие "
                + "в поглощении фаз. Корундовое число должно относиться к той же линии, по которой измерена интенсивность.")
            .Recommendation("Для абсолютных долей и оценки аморфной части добавить в образец известное количество "
                + "внутреннего стандарта и пересчитать доли относительно него.")
            .Build();
    }
}
