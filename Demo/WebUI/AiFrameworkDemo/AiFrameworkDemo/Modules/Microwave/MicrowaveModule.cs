using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Microwave;

/// <summary>
/// AI.Microwave: синтез СВЧ-антенн под техническое задание и апертурная
/// теория, на которой этот синтез стоит.
/// </summary>
public sealed class MicrowaveModule : LibraryModuleBase
{
    public override string Id => "microwave";

    public override string Name => "AI.Microwave";

    public override string Description =>
        "Синтез антенн под ТЗ, волноводный тракт, санитарные зоны и ЭМП, диэлектрический нагрев";

    public override string Color => "amber";

    public override string TutorialFolder => "Microwave";

    public override string IconSvg => """
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
          <path d="M3 12h3l3-7 3 14 3-9 2 2h4"/>
          <circle cx="19" cy="12" r="1.6"/>
        </svg>
        """;

    private static readonly AlgoChoice[] Metals =
    [
        new(0, "Медь"), new(1, "Алюминий"), new(2, "Латунь"),
        new(3, "Посеребр."), new(4, "Позолоч."), new(5, "Нержавейка"),
    ];

    private static readonly AlgoChoice[] Dielectrics =
    [
        new(0, "PTFE"), new(1, "Rexolite"), new(2, "HDPE"),
        new(3, "PP"), new(4, "PS"), new(5, "Кварц"),
    ];

    private static AlgoParam Freq() =>
        new("f", "Частота", 500, 12000, 2450, 50, "МГц", "Рабочая частота; от неё зависит вся геометрия");

    private static AlgoParam Power() =>
        new("p", "Мощность", 10, 20000, 900, 10, "Вт", "Подводимая мощность CW; определяет запас по пробою");

    private static AlgoParam Beam() =>
        new("bw", "Ширина луча", 1, 40, 5, 0.5, "град", "Требуемая ШДН по уровню -3 дБ");

    private static AlgoParam Sidelobe() =>
        new("sll", "Требуемый УБЛ", -35, -10, -20, 1, "дБ", "Допустимый уровень боковых лепестков");

    private static AlgoParam Metal(double def = 0) =>
        new("mat", "Материал", 0, 5, def, 1, "", "Металл конструкции: проводимость, плотность, цена")
        { Choices = Metals };

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        new("synthesis", "Синтез антенн",
            "HornAntennaCalculator, ParabolicAntennaCalculator, HornWithLensCalculator",
            [
                new("horn_design", "Пирамидальный рупор",
                    "Оптимальный рупор под заданную ШДН", "HornAntennaCalculator", "horn.md",
                    [Freq(), Power(), Beam(), Sidelobe(), Metal()]),

                new("parabolic_design", "Параболическая антенна",
                    "Зеркало с рупорным облучателем в фокусе", "ParabolicAntennaCalculator", "parabolic.md",
                    [
                        Freq(), Power(), Beam(), Metal(1),
                        new("fd", "f/D", 0.25, 0.80, 0.40, 0.05, "",
                            "Относительный фокус: меньше - глубже зеркало и шире луч облучателя"),
                        new("taper", "Спад к краю", 6, 20, 10, 1, "дБ",
                            "Ослабление поля облучателя на кромке зеркала: больше - ниже УБЛ, но хуже КИП"),
                        new("tol", "Допуск профиля", 0.1, 8, 0.5, 0.1, "мм",
                            "СКО отклонения поверхности; входит в потери Рузе"),
                    ]),

                new("lens_design", "Рупор с диэлектрической линзой",
                    "Гиперболическая линза выпрямляет фронт", "HornWithLensCalculator", "lens.md",
                    [
                        Freq(), Power(), Beam(),
                        new("fd", "f/D линзы", 0.6, 1.8, 1.0, 0.1, "",
                            "Больше фокус - тоньше и легче линза, но длиннее вся конструкция"),
                        new("diel", "Диэлектрик", 0, 5, 0, 1, "",
                            "Материал линзы: eps_r, потери, плотность, цена") { Choices = Dielectrics },
                    ]),

                new("compare_all", "Сравнение трёх схем",
                    "Одно ТЗ - три конструкции", "IAntennaCalculator", "comparison.md",
                    [Freq(), Power(), Beam(), Sidelobe(), Metal(1)]),
            ]),

        new("waveguide", "Волноводный тракт",
            "RectangularWaveguide: дисперсия, затухание, пропускная способность",
            [
                new("waveguide_te10", "Дисперсия и затухание TE10",
                    "Волновое сопротивление и потери по частоте", "RectangularWaveguide", "waveguide.md",
                    [
                        new("wg", "Волновод", 0, 14, 3, 1, "",
                            "Стандарт EIA: определяет критическую частоту и полосу")
                        {
                            Choices =
                            [
                                new(0, "WR-975"), new(1, "WR-650"), new(2, "WR-430"), new(3, "WR-340"),
                                new(4, "WR-284"), new(5, "WR-229"), new(6, "WR-187"), new(7, "WR-159"),
                                new(8, "WR-137"), new(9, "WR-112"), new(10, "WR-90"), new(11, "WR-75"),
                                new(12, "WR-62"), new(13, "WR-42"), new(14, "WR-28"),
                            ],
                        },
                        Metal(),
                    ]),

                new("power_handling", "Пропускная способность по пробою",
                    "Предельная мощность и условия среды", "EnvironmentalConditions", "breakdown.md",
                    [
                        new("alt", "Высота", 0, 12000, 0, 100, "м",
                            "Высота над уровнем моря: давление падает, порог пробоя вместе с ним"),
                        new("temp", "Температура", -60, 80, 20, 5, "C",
                            "Порог пробоя пропорционален плотности воздуха, то есть p/T"),
                        new("hum", "Влажность", 0, 100, 50, 5, "%",
                            "Водяной пар электроотрицателен и слегка повышает порог"),
                        new("margin", "Требуемый запас", 1.5, 6, 3, 0.5, "раз",
                            "Во сколько раз порог пробоя должен превышать рабочее поле"),
                    ]),
            ]),

        new("aperture", "Апертурная теория",
            "ApertureIllumination, DielectricLens: от чего зависят КИП и УБЛ",
            [
                new("edge_taper", "Спад к краю: КИП, перехват, УБЛ",
                    "Один параметр задаёт всю зеркальную антенну", "ApertureIllumination", "aperture.md",
                    [
                        new("taper", "Рабочая точка", 0, 25, 10, 0.5, "дБ",
                            "Спад поля облучателя на краю апертуры"),
                    ]),

                new("lens_profile", "Профиль линзы и зонирование",
                    "Гипербола, толщина, зоны Френеля", "DielectricLens", "lens.md",
                    [
                        Freq(),
                        new("d", "Диаметр линзы", 0.2, 3.0, 1.7, 0.1, "м", "Апертура линзы"),
                        new("fd", "f/D линзы", 0.6, 1.8, 1.0, 0.1, "",
                            "Относительный фокус: определяет кривизну профиля"),
                        new("diel", "Диэлектрик", 0, 5, 0, 1, "", "Материал линзы")
                            { Choices = Dielectrics },
                    ]),
            ]),

        new("safety", "Радиочастотная безопасность",
            "ExposureLimits, RadiationSource, ExposureScene: ППЭ, санзоны, соответствие нормам",
            [
                new("exposure_limits", "Пределы облучения по документам",
                    "СанПиН, ICNIRP, FCC на одной оси", "ExposureLimits", "exposure.md",
                    [
                        new("cat", "Категория", 0, 1, 0, 1, "", "Население или персонал")
                        {
                            Choices = [new(0, "Население"), new(1, "Персонал")],
                        },
                    ]),

                new("exposure_profile", "Профиль ППЭ вдоль луча",
                    "Ближняя зона, переходная, дальняя", "RadiationSource", "exposure.md",
                    [
                        new("f", "Частота", 300, 12000, 1800, 50, "МГц", "Рабочая частота источника"),
                        new("p", "Мощность передатчика", 1, 500, 40, 1, "Вт", "На выходе передатчика"),
                        new("g", "Усиление антенны", 5, 45, 18, 1, "дБи", "Максимальное усиление"),
                        new("h", "Высота подвеса", 3, 100, 30, 1, "м", "Высота фазового центра"),
                        new("tilt", "Наклон вниз", 0, 15, 4, 0.5, "град",
                            "Суммарный наклон луча: механический плюс электрический"),
                        new("obs", "Высота наблюдателя", 0.5, 50, 2, 0.5, "м",
                            "Высота точек расчёта над землёй"),
                        new("std", "Норматив", 0, 2, 0, 1, "", "Документ, по которому проверяется")
                        {
                            Choices = [new(0, "СанПиН"), new(1, "ICNIRP"), new(2, "FCC")],
                        },
                    ]),

                new("sanitary_zone", "Контур санзоны площадки",
                    "Три сектора, суммирование по долям ПДУ", "ExposureScene", "sanitary_zone.md",
                    [
                        new("f", "Частота", 300, 12000, 1800, 50, "МГц", "Рабочая частота секторов"),
                        new("p", "Мощность на сектор", 1, 500, 40, 1, "Вт", "На выходе передатчика"),
                        new("g", "Усиление антенны", 5, 45, 18, 1, "дБи", "Максимальное усиление"),
                        new("h", "Высота подвеса", 3, 100, 30, 1, "м", "Высота фазового центра"),
                        new("tilt", "Наклон вниз", 0, 15, 4, 0.5, "град", "Суммарный наклон луча"),
                        new("obs", "Высота расчёта", 0.5, 60, 2, 0.5, "м",
                            "Уровень тела человека либо отметка соседней кровли"),
                        new("duty", "Доля времени излучения", 0.1, 1, 1, 0.05, "",
                            "Для TDD - доля нисходящих слотов; снижает усреднённую ЭИИМ"),
                    ]),
            ]),

        new("heating", "СВЧ-нагрев",
            "DielectricMaterial, MultimodeCavity, TravelingWaveApplicator",
            [
                new("penetration_uniformity", "Глубина проникновения и равномерность",
                    "Почему толстый продукт не прогревается", "DielectricHeating", "dielectric_heating.md",
                    [
                        new("f", "Частота", 400, 6000, 2450, 5, "МГц",
                            "915 и 2450 МГц - разрешённые ISM-частоты нагрева"),
                        new("mat", "Материал", 0, 7, 0, 1, "", "Загрузка")
                        {
                            Choices =
                            [
                                new(0, "Вода"), new(1, "Лёд"), new(2, "Тесто"), new(3, "Мясо"),
                                new(4, "Древесина"), new(5, "Резина"), new(6, "Масло"), new(7, "Керамика"),
                            ],
                        },
                        new("t", "Толщина слоя", 5, 200, 50, 5, "мм", "Толщина обрабатываемого продукта"),
                        new("power", "Мощность", 100, 50000, 900, 100, "Вт", "Подведённая мощность"),
                        new("mass", "Масса загрузки", 0.1, 100, 1, 0.1, "кг", "Масса в рабочей зоне"),
                    ]),

                new("cavity_modes", "Моды рабочей камеры",
                    "Магнетрон против твердотельного источника", "MultimodeCavity", "cavity.md",
                    [
                        new("a", "Ширина камеры", 0.15, 2.0, 0.33, 0.01, "м", "Внутренний размер"),
                        new("b", "Высота камеры", 0.15, 2.0, 0.23, 0.01, "м", "Внутренний размер"),
                        new("d", "Глубина камеры", 0.15, 2.0, 0.35, 0.01, "м", "Внутренний размер"),
                        new("load", "Объём загрузки", 0, 50, 1, 0.5, "л",
                            "Загрузка резко снижает добротность и размывает моды"),
                        new("bw", "Полоса источника", 1, 300, 15, 1, "МГц",
                            "Магнетрон около 15 МГц, твердотельный качает сотнями"),
                    ]),

                new("applicator_balance", "Аппликатор бегущей волны",
                    "Баланс мощности и производительность линии", "TravelingWaveApplicator", "applicator.md",
                    [
                        new("f", "Частота", 400, 6000, 2450, 5, "МГц", "Рабочая частота"),
                        new("mat", "Материал", 0, 7, 4, 1, "", "Обрабатываемый продукт")
                        {
                            Choices =
                            [
                                new(0, "Вода"), new(1, "Лёд"), new(2, "Тесто"), new(3, "Мясо"),
                                new(4, "Древесина"), new(5, "Резина"), new(6, "Масло"), new(7, "Керамика"),
                            ],
                        },
                        new("t", "Толщина слоя", 2, 200, 30, 1, "мм", "Толщина слоя на конвейере"),
                        new("power", "Мощность", 500, 100000, 6000, 500, "Вт", "Мощность генератора"),
                        new("dt", "Нагрев", 5, 150, 60, 5, "К", "Требуемый подъём температуры"),
                        new("sides", "Облучение", 0, 1, 0, 1, "", "С одной или с двух сторон")
                        {
                            Choices = [new(0, "С одной стороны"), new(1, "С двух сторон")],
                        },
                    ]),
            ]),
    ];

    protected override DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string> textParams,
        DemoSettings settings)
        => MicrowaveDemoRunner.Run(algoKey, numericParams, textParams, settings);
}
