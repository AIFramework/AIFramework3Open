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
