using System;
using AI.Economics.Insights;

namespace AI.Economics.Market;

/// <summary>Разбор оценки объёма рынка.</summary>
public sealed partial record MarketSizingResult : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool consistent = TamDivergence <= 1.5;
        bool incompatible = TamDivergence > 10;
        bool implausibleShare = ImpliedMarketShare > 0.1;

        return new InterpretationBuilder("Объём рынка: TAM, SAM, SOM")
            .Summary($"Согласованный TAM — {Fmt.Money(ReconciledTam)}, SOM — {Fmt.Money(ReconciledSom)}, " +
                     $"это {Fmt.Pct(ImpliedMarketShare)} рынка. Оценки сверху вниз и снизу вверх " +
                     $"расходятся в {Fmt.Num(TamDivergence)} раза. {Verdict}")
            .Metric("Согласованный TAM", Fmt.Money(ReconciledTam), null,
                $"сверху {Fmt.Money(TamTopDown)}, снизу {Fmt.Money(TamBottomUp)}")
            .Metric("Согласованный SAM", Fmt.Money(ReconciledSam), null, "доступная часть рынка")
            .Metric("Согласованный SOM", Fmt.Money(ReconciledSom), null,
                "реалистично захватываемая часть", MetricQuality.Good)
            .Metric("Расхождение TAM", TamDivergence, "раз",
                "во сколько раз оценки отличаются друг от друга",
                consistent ? MetricQuality.Good : incompatible ? MetricQuality.Critical : MetricQuality.Warning)
            .Metric("Подразумеваемая доля", Fmt.Pct(ImpliedMarketShare), null,
                "SOM делить на TAM — проверка на здравый смысл",
                implausibleShare ? MetricQuality.Warning : MetricQuality.Good)
            .Finding("Ценность метода не в числе, а в расхождении. Оценка сверху вниз " +
                     "не проверяема, оценка снизу вверх систематически занижает; " +
                     "их совпадение означает непротиворечивую модель рынка.")
            .FindingIf(consistent,
                "Оценки согласованы: допущения о доле сегмента и среднем чеке не противоречат " +
                "друг другу.")
            .FindingIf(TamBottomUp > TamTopDown * 1.5,
                "Оценка снизу вверх заметно выше: либо переоценено число потенциальных клиентов, " +
                "либо целевой сегмент шире, чем предполагалось в оценке сверху.")
            .FindingIf(TamTopDown > TamBottomUp * 1.5,
                "Оценка сверху вниз заметно выше: обычно это значит, что отраслевой отчёт " +
                "считает рынком нечто более широкое, чем ваш продукт.")
            .WarningIf(incompatible,
                $"Расхождение в {Fmt.Num(TamDivergence)} раза делает усреднение бессмысленным. " +
                "Найдите ошибку в одной из оценок, прежде чем использовать результат.")
            .WarningIf(implausibleShare,
                $"Подразумеваемая доля рынка {Fmt.Pct(ImpliedMarketShare)} требует отдельного " +
                "обоснования: для нового игрока это очень много.")
            .Warning("Обе оценки статичны и описывают рынок сегодня. Рост рынка и скорость " +
                     "его захвата ими не учитываются.")
            .Recommendation("Постройте кривую проникновения диффузионной моделью: она покажет, " +
                            "за сколько лет достижим заявленный SOM.")
            .Build();
    }
}

/// <summary>Разбор подогнанной модели диффузии.</summary>
public sealed partial class BassDiffusion : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool typicalP = Innovation is > 0.005 and < 0.05;
        bool typicalQ = Imitation is > 0.2 and < 0.8;
        double wordOfMouth = Innovation > 0 ? Imitation / Innovation : double.NaN;

        return new InterpretationBuilder("Диффузия продукта на рынке")
            .Summary($"Потенциал рынка {Fmt.Int(MarketPotential)} клиентов, коэффициент инновации " +
                     $"{Fmt.Num(Innovation, 4)}, коэффициент имитации {Fmt.Num(Imitation, 4)}. " +
                     $"Пик новых клиентов приходится на {Fmt.Num(PeakTime, 1)} период " +
                     $"и составляет {Fmt.Int(PeakAdopters)} клиентов за период.")
            .Metric("Потенциал рынка", Fmt.Int(MarketPotential), "клиентов",
                "предельное число принявших продукт")
            .Metric("Коэффициент инновации p", Innovation, null,
                "принятие независимо от других; типичные значения 0,01-0,03",
                typicalP ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("Коэффициент имитации q", Imitation, null,
                "сила сарафанного радио; типичные значения 0,3-0,5",
                typicalQ ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("Отношение q к p", wordOfMouth, null,
                "во сколько раз сарафанное радио сильнее самостоятельного принятия")
            .Metric("Пик продаж", PeakTime, "период", "после него поток новых клиентов падает сам",
                MetricQuality.Neutral)
            .Metric("R2", RSquared, null, "качество подгонки по накопленным принявшим",
                RSquared > 0.99 ? MetricQuality.Good : RSquared > 0.9 ? MetricQuality.Warning : MetricQuality.Critical, 4)
            .Finding($"После {Fmt.Num(PeakTime, 1)} периода число новых клиентов начнёт падать " +
                     "само, без изменения маркетинга. Не зная этого, падение выручки легко " +
                     "принять за ухудшение работы команды.")
            .FindingIf(!double.IsNaN(wordOfMouth) && wordOfMouth > 20,
                "Сарафанное радио сильно доминирует над самостоятельным принятием: рост " +
                "будет медленно стартовать и резко ускоряться. Ранние продажи плохо " +
                "предсказывают будущие.")
            .FindingIf(!double.IsNaN(wordOfMouth) && wordOfMouth < 5,
                "Слабое сарафанное радио: рост почти линейно зависит от маркетингового давления, " +
                "самоподдерживающегося ускорения не будет.")
            .WarningIf(!typicalP,
                $"Коэффициент инновации {Fmt.Num(Innovation, 4)} выходит за типичный диапазон. " +
                "Проверьте, не подгонялась ли модель по слишком короткой истории.")
            .WarningIf(RSquared < 0.9,
                $"Качество подгонки {Fmt.Num(RSquared, 3)}: ряд плохо описывается S-образной " +
                "кривой. Возможно, на продажи влияют факторы вне модели диффузии.")
            .Warning("Подгонка по данным до пика неустойчива: одну и ту же начальную часть " +
                     "кривой одинаково хорошо описывают разные пары потенциала и имитации. " +
                     "Пока пик не пройден, оценка потолка рынка — ориентир, а не число.")
            .Warning("Модель описывает первую покупку и ничего не говорит о повторных " +
                     "и об оттоке. Для выручки её надо совмещать с моделью удержания.")
            .Recommendation("Сравните подогнанный потенциал с оценкой SAM: заметное расхождение " +
                            "означает, что гипотеза о рынке не подтверждается продажами.")
            .Build();
    }
}
