using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.SignalLabs;

public sealed class SignalLabsModule : LibraryModuleBase
{
    public override string Id => "signallabs";
    public override string Name => "AI.SignalLabs";
    public override string Description => "Обработка сигналов: АРУ (AGC), цифровые модуляции BPSK/QPSK/QAM, согласованный SRRC-фильтр";
    public override string Color => "cyan";
    public override string TutorialFolder => "SignalLabs";

    public override string IconSvg => """
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
          <polyline points="2 12 5 7 8 15 11 10 14 13 17 5 20 12 22 12"/>
        </svg>
        """;

    #region AlgoChoice наборы

    private static readonly AlgoChoice[] AgcTypeChoices =
    [
        new(0, "DirectAGC (Прямая)"),
        new(1, "LogAGC (Логарифмическая)"),
        new(2, "MinCombineAGC (Комбинированная)"),
    ];

    private static readonly AlgoChoice[] ModulationChoices =
    [
        new(0, "BPSK"),
        new(1, "QPSK"),
        new(2, "8-QAM"),
        new(3, "16-QAM"),
    ];

    #endregion

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        #region 1. АРУ (AGC)

        new CategoryDef("agc", "Автоматическая регулировка усиления (АРУ)",
            "DirectAGC, LogAGC, MinCombineAGC — нормализация амплитуды входного сигнала в реальном времени",
            [
                new AlgoDef("agc_demo", "Демонстрация АРУ",
                    "Синусоида с резкими скачками амплитуды пропускается через выбранный алгоритм АРУ. " +
                    "Показываются исходный и нормализованный сигналы.",
                    "AI.SignalLab.AGC",
                    "agc.md",
                    [
                        new AlgoParam("agcType", "Тип АРУ", 0, 2, 0, 1, "", "Алгоритм автоматической регулировки усиления")
                            { Choices = AgcTypeChoices },
                        new AlgoParam("tresholdAgc", "Порог АРУ", 1, 10, 4, 0.5, "", "Ограничение выходного сигнала"),
                        new AlgoParam("signalFreq", "Частота сигнала, Гц", 100, 5000, 1000, 100, "Гц", "Частота тестовой синусоиды"),
                        new AlgoParam("sampleRate", "Частота дискр., Гц", 8000, 48000, 44100, 1000, "Гц", "Частота дискретизации"),
                        new AlgoParam("duration", "Длительность, мс", 200, 5000, 2000, 100, "мс", "Длина тестового сигнала"),
                    ]),
            ]),

        #endregion

        #region 2. Цифровые модуляции

        new CategoryDef("modulation", "Цифровые модуляции",
            "BPSK, QPSK, 8-QAM, 16-QAM с SRRC-фильтрацией на передатчике и квадратурной демодуляцией на приёмнике",
            [
                new AlgoDef("modulation_demo", "Квадратурная модуляция/демодуляция",
                    "Текст кодируется в биты, маппируется на созвездие, формируется через SRRC, модулируется несущей. " +
                    "Приёмник восстанавливает созвездие и декодирует текст.",
                    "AI.SignalLab.Modulation",
                    "modulation.md",
                    [
                        new AlgoParam("modType", "Модуляция", 0, 3, 0, 1, "", "Тип цифровой модуляции")
                            { Choices = ModulationChoices },
                        new AlgoParam("carrierFreq", "Несущая, Гц", 500, 10000, 3000, 500, "Гц", "Частота несущего колебания"),
                        new AlgoParam("sampleRate", "Частота дискр., Гц", 44100, 144100, 144100, 1000, "Гц", "Частота дискретизации"),
                        new AlgoParam("bitDuration", "Длит. бита, мкс", 100, 5000, 900, 100, "мкс", "Длительность одного символа"),
                        new AlgoParam("rollOff", "Roll-off SRRC", 1, 9, 3, 1, "×0.1", "Коэффициент скатывания β (1=0.1 … 9=0.9)"),
                        new AlgoParam("_text", "Текст для передачи", 0, 0, 0, 0, "",
                            "Строка для кодирования и передачи",
                            TextDefault: "AI.SignalLabs"),
                    ]),
            ]),

        #endregion

        #region 3. SRRC-фильтр

        new CategoryDef("srrc", "Формирующий SRRC-фильтр",
            "Root Raised Cosine Filter: импульсная характеристика и АЧХ. Пара SRRC (Tx + Rx) образует RC-фильтр, обеспечивающий нулевую МСИ.",
            [
                new AlgoDef("srrc_demo", "Импульсная характеристика и АЧХ",
                    "Коэффициенты ядра SRRC и его амплитудно-частотная характеристика. " +
                    "Произведение двух SRRC в частотной области эквивалентно фильтру Найквиста.",
                    "AI.SignalLab.Filters.RootRaisedCosineFilter",
                    "srrc.md",
                    [
                        new AlgoParam("rollOff", "Roll-off β", 1, 9, 3, 1, "×0.1", "Коэффициент скатывания β"),
                        new AlgoParam("span", "Длина, симв.", 2, 12, 6, 1, "симв.", "Длина фильтра в символах (с каждой стороны от 0)"),
                        new AlgoParam("symbolRate", "Скорость симв., Бд", 100, 10000, 2000, 100, "Бд", "Скорость символов (1/T)"),
                        new AlgoParam("sampleRate", "Частота дискр., Гц", 8000, 48000, 16000, 1000, "Гц", "Частота дискретизации"),
                    ]),
            ]),

        #endregion
    ];

    protected override DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string> textParams,
        DemoSettings settings) =>
        SignalLabsDemoRunner.Run(algoKey, numericParams, textParams, settings);
}
