using AiFrameworkDemo.Core;

/*
 +-----------------------------------------------------------------------------+
 |  ШАБЛОН МОДУЛЯ — скопируйте эти два файла для нового раздела демо           |
 |                                                                             |
 |  1. Переименуйте класс, namespace и все поля.                               |
 |  2. Добавьте категории и алгоритмы в Categories.                            |
 |  3. Реализуйте логику в DemoRunner-файле через switch(algoKey).             |
 |  4. Зарегистрируйте модуль в LibraryRegistry.cs.                            |
 +-----------------------------------------------------------------------------+

 Доступные цвета (UIKit): sky | indigo | violet | emerald | amber | pink
*/

namespace AiFrameworkDemo.Modules.Template;

/// <summary>
/// Модуль-шаблон. Замените "Template" на название вашей библиотеки.
/// </summary>
public sealed class TemplateModule : LibraryModuleBase
{
    // -- Метаданные модуля -----------------------------------------------------

    public override string Id            => "template";
    public override string Name          => "AI.Template";
    public override string Description   => "Краткое описание того, что делает библиотека";
    public override string Color         => "indigo";         // sky | violet | emerald | amber | pink
    public override string TutorialFolder => "Template";      // папка в Docs/Tutorials/

    public override string IconSvg => """
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="1.8" stroke-linecap="round">
          <rect x="3" y="3" width="18" height="18" rx="2"/>
          <line x1="9" y1="9" x2="15" y2="15"/>
          <line x1="15" y1="9" x2="9" y2="15"/>
        </svg>
        """;

    // -- Категории и алгоритмы -------------------------------------------------

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        new("category_1", "Первая категория",
            "Описание первой группы алгоритмов",
            [
                new AlgoDef(
                    Key:         "algo_a",
                    Title:       "Алгоритм A",
                    Subtitle:    "Краткое описание алгоритма A",
                    ApiClass:    "AI.Template.AlgorithmA",
                    TheoryFile:  "algo_a.md",
                    Params:
                    [
                        new AlgoParam("n",     "Число точек",  10, 500, 100, 10, "шт.", "Объём выборки"),
                        new AlgoParam("alpha", "Коэффициент α",  0,   1, 0.5, 0.01, "", "Регуляризация"),
                    ]),

                new AlgoDef(
                    Key:         "algo_b",
                    Title:       "Алгоритм B",
                    Subtitle:    "Краткое описание алгоритма B",
                    ApiClass:    "AI.Template.AlgorithmB",
                    TheoryFile:  "algo_b.md",
                    Params: []),
            ]),

        new("category_2", "Вторая категория",
            "Описание второй группы алгоритмов",
            [
                new AlgoDef(
                    Key:         "algo_c",
                    Title:       "Алгоритм C",
                    Subtitle:    "Краткое описание алгоритма C",
                    ApiClass:    "AI.Template.AlgorithmC",
                    TheoryFile:  "algo_c.md",
                    Params: []),
            ]),
    ];

    // -- Запуск демо -----------------------------------------------------------

    protected override DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string>  textParams,
        DemoSettings settings)
        => TemplateDemoRunner.Run(algoKey, numericParams, textParams, settings);
}
