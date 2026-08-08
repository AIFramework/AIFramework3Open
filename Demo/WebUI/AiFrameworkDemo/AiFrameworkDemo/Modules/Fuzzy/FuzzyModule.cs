using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Fuzzy;

/// <summary>
/// AI.Fuzzy — нечёткая логика: фаззификация, четыре схемы вывода, дефаззификация.
///
/// Все четыре метода вывода работают на ОДНОЙ базе правил (термостат:
/// температура -> мощность нагревателя). Это сделано намеренно: различия
/// между Мамдани, Ларсеном, Сугено и Цукамото видны только тогда, когда
/// всё остальное одинаково.
/// </summary>
public sealed class FuzzyModule : LibraryModuleBase
{
    public override string Id             => "fuzzy";
    public override string Name           => "AI.Fuzzy";
    public override string Description    => "Нечёткая логика: функции принадлежности, вывод по Мамдани, Ларсену, Сугено и Цукамото, дефаззификация";
    public override string Color          => "amber";
    public override string TutorialFolder => "Fuzzy";

    public override string IconSvg => """
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
          <path d="M3 17c3 0 3-10 6-10s3 10 6 10 3-6 6-6"/>
          <line x1="3" y1="21" x2="21" y2="21"/>
          <line x1="3" y1="21" x2="3" y2="4"/>
        </svg>
        """;

    #region Общие наборы вариантов

    /// <summary>Форма функции принадлежности входных термов.</summary>
    private static readonly AlgoChoice[] ShapeChoices =
    [
        new(0, "Треугольная"),
        new(1, "Трапециевидная"),
    ];

    private static readonly AlgoChoice[] MethodChoices =
    [
        new(0, "Мамдани"),
        new(1, "Ларсен"),
        new(2, "Сугено"),
        new(3, "Цукамото"),
    ];

    /// <summary>Температура на входе — общий параметр всех демо вывода.</summary>
    private static AlgoParam TempParam() =>
        new("temp", "Температура", 0, 40, 12, 0.5, "°C",
            "Вход системы: текущая температура в помещении");

    private static AlgoParam ShapeParam() =>
        new("shape", "Форма термов", 0, 1, 0, 1, "",
            "Функция принадлежности входных термов «Холодно», «Норма», «Жарко»")
        { Choices = ShapeChoices };

    private static AlgoParam GridParam() =>
        new("grid", "Узлов сетки выхода", 21, 401, 201, 20, "шт.",
            "Дискретизация универсума выхода: от неё зависит точность центра тяжести");

    #endregion

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        #region 1. Фаззификация
        new CategoryDef("fuzzification", "Фаззификация",
            "FuzzyMembershipShapes: треугольные и трапециевидные функции принадлежности, разбиение универсума на термы",
            [
                new AlgoDef("fuzzy_membership", "Функции принадлежности",
                    "Треугольные и трапециевидные термы на универсуме; степень принадлежности конкретного значения каждому терму",
                    "AI.Fuzzy.Inference.FuzzyMembershipShapes",
                    "fuzzy_membership.md",
                    [
                        TempParam(),
                        ShapeParam(),
                        new AlgoParam("overlap", "Перекрытие термов", 0, 1, 0.5, 0.05, "",
                            "Насколько соседние термы заходят друг на друга: 0 — не пересекаются, 1 — максимальное перекрытие"),
                    ]),
            ]),
        #endregion

        #region 2. Нечёткий вывод
        new CategoryDef("inference", "Нечёткий вывод",
            "Четыре классические схемы на одной базе правил: Мамдани, Ларсен, Сугено, Цукамото",
            [
                new AlgoDef("fuzzy_mamdani", "Вывод по Мамдани",
                    "min-импликация, max-агрегирование, дефаззификация центром тяжести. Классическая схема 1974 года",
                    "AI.Fuzzy.Inference.FuzzyMamdaniInference",
                    "Mamdani.md",
                    [TempParam(), ShapeParam(), GridParam()]),

                new AlgoDef("fuzzy_larsen", "Вывод по Ларсену",
                    "То же, что Мамдани, но импликация — произведение: терм не срезается, а масштабируется",
                    "AI.Fuzzy.Inference.FuzzyLarsenInference",
                    "Larsen.md",
                    [TempParam(), ShapeParam(), GridParam()]),

                new AlgoDef("fuzzy_sugeno", "Вывод по Сугено",
                    "Следствия — чёткие значения (синглтоны) или линейные формы; агрегирование взвешенным средним",
                    "AI.Fuzzy.Inference.FuzzySugenoInference",
                    "Sugeno.md",
                    [
                        TempParam(),
                        ShapeParam(),
                        new AlgoParam("order", "Порядок Сугено", 0, 1, 0, 1, "",
                            "0 — следствия-константы, 1 — линейные функции входа")
                            { Choices = [new(0, "0-й (синглтоны)"), new(1, "1-й (линейный)")] },
                    ]),

                new AlgoDef("fuzzy_tsukamoto", "Вывод по Цукамото",
                    "Следствия — монотонные функции принадлежности; чёткое значение правила берётся обратной функцией μ⁻¹(α)",
                    "AI.Fuzzy.Inference.FuzzyTsukamotoInference",
                    "Tsukamoto.md",
                    [TempParam(), ShapeParam(), GridParam()]),
            ]),
        #endregion

        #region 3. Сравнение методов
        new CategoryDef("comparison", "Сравнение методов",
            "Характеристика управления «вход → выход» для всех четырёх схем на одной базе правил",
            [
                new AlgoDef("fuzzy_compare", "Характеристики четырёх методов",
                    "Прогон всего диапазона входа через Мамдани, Ларсена, Сугено и Цукамото: где схемы совпадают, а где расходятся",
                    "AI.Fuzzy.Inference",
                    "fuzzy_compare.md",
                    [
                        TempParam(),
                        ShapeParam(),
                        new AlgoParam("sweep", "Точек развёртки", 21, 201, 81, 10, "шт.",
                            "Сколько значений входа прогоняется через все четыре метода"),
                        new AlgoParam("highlight", "Подсветить метод", 0, 3, 0, 1, "",
                            "Метод, значение которого выводится в метриках")
                            { Choices = MethodChoices },
                    ]),
            ]),
        #endregion
    ];

    protected override DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string> textParams,
        DemoSettings settings) =>
        FuzzyDemoRunner.Run(algoKey, numericParams, textParams, settings);
}
