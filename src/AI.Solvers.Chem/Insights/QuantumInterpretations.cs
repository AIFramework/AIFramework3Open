using AI.Insights;

namespace AI.Solvers.Chem.Quantum;

/// <summary>Разбор решения по методу Хюккеля.</summary>
public sealed partial class HuckelSolution : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool hasGap = !double.IsNaN(Gap);
        bool openShell = Electrons % 2 != 0;
        double excitation = hasGap ? ExcitationEnergy() : double.NaN;
        double wavelength = hasGap ? AbsorptionWavelength() : double.NaN;
        double stabilization = DelocalizationEnergy;

        var builder = new InterpretationBuilder("Расчёт π-системы по методу Хюккеля")
            .Summary($"Система из {Count} центров с {Electrons} π-электронами. "
                + (hasGap
                    ? $"Щель ВЗМО — НСМО составляет {Fmt.Num(Gap, 3)}·|β|, что при β = −2.7 эВ отвечает "
                      + $"переходу {Fmt.Num(excitation, 2)} эВ и полосе около {Fmt.Num(wavelength, 0)} нм. "
                    : "Щель ВЗМО — НСМО не определена: нет либо занятых, либо свободных орбиталей. ")
                + $"Энергия делокализации {Fmt.Num(stabilization, 3)}·β относительно локализованных двойных связей.")
            .Metric("Центров", Count, null, "число атомов, включённых в π-систему", MetricQuality.Unknown, 0)
            .Metric("π-электронов", Electrons, null, "заселяют орбитали снизу вверх", MetricQuality.Unknown, 0)
            .Metric("E(ВЗМО)", Fmt.Num(Homo, 4), "x в E = α + xβ", "верхняя занятая орбиталь")
            .Metric("E(НСМО)", Fmt.Num(Lumo, 4), "x в E = α + xβ", "нижняя свободная орбиталь")
            .Metric("Щель", Fmt.Num(Gap, 4), "|β|",
                "чем меньше, тем ближе уровни и тем реакционноспособнее система",
                !hasGap ? MetricQuality.Unknown
                    : Gap < 0.5 ? MetricQuality.Warning
                    : MetricQuality.Good)
            .Metric("Энергия делокализации", Fmt.Num(stabilization, 4), "β",
                "выигрыш относительно изолированных двойных связей",
                stabilization > 0.1 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Полная π-энергия", Fmt.Num(TotalEnergy, 4), "β", "сумма по занятым орбиталям");

        if (hasGap)
        {
            builder = builder
                .Metric("Энергия перехода", Fmt.Num(excitation, 3), "эВ", "оценка при β = −2.7 эВ")
                .Metric("Полоса поглощения", Fmt.Num(wavelength, 0), "нм", "грубая оценка по щели")
                .Metric("Жёсткость", Fmt.Num(Hardness(), 3), "эВ", "половина щели: устойчивость к переносу заряда");
        }

        return builder
            .FindingIf(stabilization > 0.5,
                $"Сопряжение даёт заметную стабилизацию — {Fmt.Num(stabilization, 2)}·β. "
                + "Для бензола этот показатель равен 2β, что и отвечает его ароматичности.")
            .FindingIf(stabilization <= 0.01 && stabilization >= -0.01,
                "Делокализация практически не даёт выигрыша: система ведёт себя как набор "
                + "изолированных кратных связей.")
            .FindingIf(hasGap && Gap < 0.5,
                "Малая щель означает близкие граничные орбитали: возможен бирадикальный характер, "
                + "низкая устойчивость и поглощение в длинноволновой области.")
            .FindingIf(ObeysHuckelRule && stabilization > 0.5,
                $"Число π-электронов ({Electrons}) отвечает правилу 4n+2. Вместе с заметной энергией "
                + "делокализации это признак ароматической системы — при условии, что цикл один и он плоский: "
                + "связность метод берёт из графа, а планарность не проверяет.")
            .FindingIf(openShell,
                "Нечётное число π-электронов: верхняя орбиталь заселена одним электроном, "
                + "система — радикал, и однодетерминантная картина Хюккеля для неё особенно груба.")
            .Warning("Метод учитывает только π-подсистему: σ-остов, электронная корреляция, "
                + "сольватация и геометрия в расчёт не входят.")
            .Warning("α и β — эмпирические параметры, а не вычисленные величины. Все энергии здесь "
                + "выражены в их долях; абсолютные значения получаются подстановкой α = −11.4 эВ и β = −2.7 эВ.")
            .Warning("Интеграл перекрывания принят нулевым для несоседних центров и единичным для "
                + "самоперекрывания — приближение, завышающее делокализацию в напряжённых системах.")
            .WarningIf(hasGap,
                "Полоса поглощения оценена по щели граничных орбиталей: это порядок величины, "
                + "а не спектр. Ошибка в сотню нанометров здесь обычна.")
            .Recommendation("Числа пригодны для сравнения родственных систем между собой; "
                + "для абсолютных величин нужен полуэмпирический или ab initio расчёт.")
            .RecommendationIf(hasGap && Gap < 0.5,
                "Проверить систему методом, учитывающим корреляцию: при малой щели однодетерминантное "
                + "описание ненадёжно.")
            .Build();
    }
}
