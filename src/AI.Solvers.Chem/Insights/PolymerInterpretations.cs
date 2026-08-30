using AI.Insights;

namespace AI.Solvers.Chem.Polymers;

/// <summary>Разбор молекулярно-массового распределения полимера.</summary>
public sealed partial class MolarMassDistribution : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double dispersity = Dispersity;
        bool narrow = dispersity < 1.2;
        bool typicalRadical = dispersity >= 1.5 && dispersity <= 2.5;
        bool broad = dispersity > 3.0;
        double skew = NumberAverage > 0 ? ZAverage / NumberAverage : double.NaN;

        return new InterpretationBuilder("Молекулярно-массовое распределение")
            .Summary($"Mn = {Fmt.Int(NumberAverage)} г/моль, Mw = {Fmt.Int(WeightAverage)} г/моль, "
                + $"дисперсность Đ = {Fmt.Num(dispersity, 3)}. Максимум распределения приходится на "
                + $"{Fmt.Int(PeakMass)} г/моль, срезов в распределении {Masses.Count}.")
            .Metric("Mn", Fmt.Int(NumberAverage), "г/моль",
                "среднечисленная масса: определяет число концевых групп и коллигативные свойства")
            .Metric("Mw", Fmt.Int(WeightAverage), "г/моль",
                "средневесовая масса: определяет вязкость расплава и прочность")
            .Metric("Mz", Fmt.Int(ZAverage), "г/моль",
                "z-средняя масса: чувствительна к высокомолекулярному хвосту")
            .Metric("Mp", Fmt.Int(PeakMass), "г/моль", "масса в максимуме распределения")
            .Metric("Đ = Mw/Mn", Fmt.Num(dispersity, 3), null,
                "ширина распределения; единица отвечает монодисперсному образцу",
                double.IsNaN(dispersity) ? MetricQuality.Unknown
                    : narrow ? MetricQuality.Good
                    : broad ? MetricQuality.Warning
                    : MetricQuality.Neutral)
            .Metric("Mz/Mn", Fmt.Num(skew, 3), null, "асимметрия: рост показателя выдаёт длинный хвост")
            .FindingIf(narrow,
                $"Распределение узкое (Đ = {Fmt.Num(dispersity, 3)}). Такая ширина характерна для живой "
                + "или контролируемой полимеризации, где цепи растут одновременно и обрыв подавлен.")
            .FindingIf(typicalRadical,
                $"Đ = {Fmt.Num(dispersity, 2)} — обычная величина для свободнорадикальной полимеризации "
                + "и поликонденсации: при полном превращении статистика Флори даёт ровно 2.")
            .FindingIf(broad,
                $"Распределение широкое (Đ = {Fmt.Num(dispersity, 2)}). Это признак нескольких типов "
                + "активных центров, передачи цепи, разветвления либо смеси двух фракций.")
            .FindingIf(!double.IsNaN(skew) && skew > 3.0 * dispersity,
                "Mz заметно оторвана от остальных средних: в образце есть высокомолекулярный хвост, "
                + "который сильно влияет на реологию, оставаясь малым по массовой доле.")
            .Warning("Средние вычислены по заданным срезам. Обрезание распределения по краям смещает "
                + "Mn вниз, а Mz вверх: хвосты весят в этих моментах сильнее всего.")
            .Warning("Если срезы получены гель-проникающей хроматографией, массы относительны — они "
                + "выражены через калибровку по стандартам, и для полимера иной природы отличаются "
                + "от абсолютных в разы.")
            .WarningIf(Masses.Count < 20,
                $"Срезов всего {Masses.Count}: моменты распределения на такой сетке считаются грубо.")
            .Recommendation("Сопоставлять Đ с механизмом синтеза: расхождение с ожидаемым по механизму "
                + "значением указывает на побочные процессы раньше, чем это заметно по выходу.")
            .RecommendationIf(broad,
                "Проверить распределение на бимодальность: широкий пик и два слитых пика дают близкую Đ, "
                + "но означают разные вещи.")
            .Build();
    }
}
