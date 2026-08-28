using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Economics;

/// <summary>
/// AI.Economics — экономика и финансовая аналитика: юнит-экономика и когорты,
/// CLV без контракта, отток как анализ выживаемости, SaaS-метрики, стохастический
/// runway, cap table и оценка компании, ценообразование и маркетинг-аналитика,
/// прогнозирование спроса, кредитный риск и скоринг, анализ отчётности и форензика.
/// </summary>
public sealed class EconomicsModule : LibraryModuleBase
{
    public override string Id => "econ";

    public override string Name => "AI.Economics";

    public override string Description =>
        "Юнит-экономика, когорты, CLV, отток, SaaS, runway, cap table, цены, прогнозы, кредитный риск, финанализ";

    public override string Color => "emerald";

    public override string TutorialFolder => "Economics";

    public override string IconSvg => """
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
          <line x1="3" y1="21" x2="21" y2="21"/>
          <line x1="3" y1="21" x2="3" y2="3"/>
          <rect x="6" y="13" width="3" height="8"/>
          <rect x="11" y="9" width="3" height="12"/>
          <rect x="16" y="5" width="3" height="16"/>
        </svg>
        """;

    #region Общие наборы вариантов

    private static readonly AlgoChoice[] RetentionModelChoices =
    [
        new(0, "Все четыре"),
        new(1, "Экспоненциальная"),
        new(2, "Степенная"),
        new(3, "Вейбулла"),
        new(4, "sBG"),
    ];

    private static readonly AlgoChoice[] PreferenceChoices =
    [
        new(0, "Неучаствующая"),
        new(1, "Участвующая"),
        new(2, "Участвующая с потолком"),
    ];

    private static readonly AlgoChoice[] ClvModelChoices =
    [
        new(0, "BG/NBD"),
        new(1, "Pareto/NBD"),
        new(2, "Сравнить обе"),
    ];

    private static readonly AlgoChoice[] ElasticityChoices =
    [
        new(0, "Наивный МНК"),
        new(1, "Панель"),
        new(2, "Инструмент"),
    ];

    private static readonly AlgoChoice[] WtpChoices =
    [
        new(0, "Ван Вестендорп"),
        new(1, "Габор — Грейнджер"),
    ];

    private static readonly AlgoChoice[] ConjointChoices =
    [
        new(0, "Агрегатный логит"),
        new(1, "Иерархический байес"),
    ];

    private static readonly AlgoChoice[] SequentialChoices =
    [
        new(0, "mSPRT"),
        new(1, "Байесовский"),
    ];

    private static readonly AlgoChoice[] BanditChoices =
    [
        new(0, "Равномерное деление"),
        new(1, "Эпсилон-жадная"),
        new(2, "UCB"),
        new(3, "Томпсон"),
    ];

    private static readonly AlgoChoice[] YesNoChoices =
    [
        new(0, "Нет"),
        new(1, "Да"),
    ];

    private static readonly AlgoChoice[] SeasonalityChoices =
    [
        new(0, "Нет"),
        new(1, "Аддитивная"),
        new(2, "Мультипликативная"),
        new(3, "Автоподбор"),
    ];

    private static readonly AlgoChoice[] IntermittentChoices =
    [
        new(0, "Кростон"),
        new(1, "Синтетос — Бойлан"),
        new(2, "TSB"),
    ];

    private static readonly AlgoChoice[] ReconciliationChoices =
    [
        new(0, "Снизу вверх"),
        new(1, "Сверху вниз"),
        new(2, "МНК"),
        new(3, "MinT"),
    ];

    private static readonly AlgoChoice[] ConformalModelChoices =
    [
        new(0, "Сглаживание"),
        new(1, "Theta"),
    ];

    private static readonly AlgoChoice[] BenfordScopeChoices =
    [
        new(0, "Первая цифра"),
        new(1, "Первые две цифры"),
    ];

    private static readonly AlgoChoice[] BenfordPatternChoices =
    [
        new(0, "Естественные платежи"),
        new(1, "Дробление под порог"),
        new(2, "Придуманные суммы"),
    ];

    private static readonly AlgoChoice[] BankruptcyModelChoices =
    [
        new(0, "Логистическая регрессия"),
        new(1, "Байесовский классификатор"),
        new(2, "Опорные векторы"),
        new(3, "Сравнить все"),
    ];

    private static readonly AlgoChoice[] ProjectOptionChoices =
    [
        new(0, "Отсрочка"),
        new(1, "Расширение"),
        new(2, "Отказ"),
    ];

    private static readonly AlgoChoice[] DepreciationChoices =
    [
        new(0, "Линейный"),
        new(1, "Уменьшаемого остатка"),
        new(2, "Сумма чисел лет"),
        new(3, "Нелинейный налоговый"),
    ];

    private static readonly AlgoChoice[] RepaymentChoices =
    [
        new(0, "Аннуитетный"),
        new(1, "Дифференцированный"),
        new(2, "Только проценты"),
    ];

    private static readonly AlgoChoice[] PrepaymentChoices =
    [
        new(0, "Срок"),
        new(1, "Платёж"),
    ];

    private static readonly AlgoChoice[] VarMethodChoices =
    [
        new(0, "Исторический"),
        new(1, "Параметрический"),
        new(2, "Корниш — Фишер"),
        new(3, "Монте-Карло"),
    ];

    private static readonly AlgoChoice[] CopulaChoices =
    [
        new(0, "Гауссова"),
        new(1, "Стьюдента"),
        new(2, "Клейтона"),
        new(3, "Гумбеля"),
    ];

    private static readonly AlgoChoice[] RiskParityChoices =
    [
        new(0, "Обратная волатильность"),
        new(1, "Равный вклад в риск"),
        new(2, "Иерархический"),
    ];

    private static readonly AlgoChoice[] RebalancingChoices =
    [
        new(0, "Без перебалансировки"),
        new(1, "По календарю"),
        new(2, "По порогу"),
        new(3, "Частичная"),
    ];

    private static readonly AlgoChoice[] RobustVarianceChoices =
    [
        new(0, "Классические"),
        new(1, "HC0"),
        new(2, "HC1"),
        new(3, "HC3"),
        new(4, "Ньюи — Уэст"),
        new(5, "Кластерные"),
        new(6, "HC2"),
    ];

    private static readonly AlgoChoice[] IvEstimatorChoices =
    [
        new(0, "Двухшаговый МНК"),
        new(1, "Метод моментов"),
    ];

    private static readonly AlgoChoice[] PanelEstimatorChoices =
    [
        new(0, "Объединённый МНК"),
        new(1, "Фиксированные эффекты"),
        new(2, "Двусторонние эффекты"),
        new(3, "Случайные эффекты"),
        new(4, "Первые разности"),
        new(5, "Межгрупповая"),
    ];

    private static readonly AlgoChoice[] LimitedDependentChoices =
    [
        new(0, "Логит"),
        new(1, "Пробит"),
        new(2, "Тобит"),
        new(3, "Пуассон"),
        new(4, "Отрицательная биномиальная"),
    ];

    private static readonly AlgoChoice[] DeterministicChoices =
    [
        new(0, "Без константы"),
        new(1, "Константа"),
        new(2, "Константа и тренд"),
    ];

    private static readonly AlgoChoice[] GarchChoices =
    [
        new(0, "GARCH"),
        new(1, "GJR-GARCH"),
        new(2, "EGARCH"),
    ];

    private static readonly AlgoChoice[] StateSpaceChoices =
    [
        new(0, "Локальный уровень"),
        new(1, "Локальный тренд"),
    ];

    #endregion

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        new("unit", "Юнит-экономика",
            "CAC, LTV, окупаемость привлечения и маржинальный вклад по каналам",
            [
                new AlgoDef(
                    Key: "unit_economics",
                    Title: "CAC, LTV и окупаемость",
                    Subtitle: "Дисконтированный LTV по марже и дробный срок окупаемости",
                    ApiClass: "AI.Economics.UnitEconomics.UnitEconomicsCalculator",
                    TheoryFile: "unit_economics.md",
                    Params:
                    [
                        new AlgoParam("spend", "Затраты на привлечение", 50_000, 5_000_000, 900_000, 50_000, "руб.",
                            "Маркетинг и продажи за период привлечения"),
                        new AlgoParam("customers", "Привлечено клиентов", 20, 3000, 300, 10, "шт.",
                            "Делитель для CAC"),
                        new AlgoParam("arpu", "ARPU в месяц", 200, 50_000, 6000, 100, "руб.",
                            "Средний доход с клиента за месяц"),
                        new AlgoParam("margin", "Валовая маржа", 0.1, 1.0, 0.8, 0.01, "доля",
                            "Ключевое отличие от расчёта по выручке: LTV считается по марже"),
                        new AlgoParam("churn", "Отток в месяц", 0.005, 0.2, 0.045, 0.005, "доля",
                            "Постоянный отток геометрической модели"),
                        new AlgoParam("discount", "Ставка дисконтирования", 0, 0.04, 0.01, 0.0025, "в месяц",
                            "Ноль — считать без дисконтирования, как в большинстве таблиц"),
                        new AlgoParam("horizon", "Горизонт", 6, 120, 36, 6, "мес.",
                            "На каком сроке признаётся ценность клиента"),
                    ]),

                new AlgoDef(
                    Key: "channel_mix",
                    Title: "Экономика каналов привлечения",
                    Subtitle: "Blended CAC против paid CAC и убыточные каналы под средним",
                    ApiClass: "AI.Economics.UnitEconomics.ChannelEconomics",
                    TheoryFile: "channel_mix.md",
                    Params:
                    [
                        new AlgoParam("budget", "Бюджет на платные каналы", 200_000, 20_000_000, 3_000_000, 100_000, "руб.",
                            "Распределяется между контекстом, таргетом и партнёрской сетью"),
                        new AlgoParam("share_ctx", "Доля контекста в бюджете", 0.1, 0.9, 0.5, 0.05, "доля",
                            "Остаток делится между таргетом и партнёркой"),
                        new AlgoParam("cpa", "CPA контекста", 1000, 60_000, 9000, 500, "руб.",
                            "У таргета CPA выше на 40 %, у партнёрки ниже на 20 %"),
                        new AlgoParam("organic", "Органика", 0, 800, 120, 10, "клиентов",
                            "Бесплатные клиенты, которые прячут дорогие каналы в blended CAC"),
                        new AlgoParam("arpu", "ARPU в месяц", 200, 50_000, 6000, 100, "руб.", "Средний доход с клиента"),
                        new AlgoParam("churn", "Базовый отток", 0.01, 0.2, 0.04, 0.005, "доля",
                            "Отток органики и контекста"),
                        new AlgoParam("quality_gap", "Разрыв в качестве трафика", 1.0, 4.0, 2.0, 0.1, "×",
                            "Во сколько раз выше отток у партнёрского трафика"),
                    ]),
            ]),

        new("cohorts", "Когорты и удержание",
            "Подгонка кривой удержания с доверительным интервалом вместо среднего оттока",
            [
                new AlgoDef(
                    Key: "retention_fit",
                    Title: "Подгонка кривой удержания",
                    Subtitle: "Power-law, Вейбулл, sBG: выбор по AIC и экстраполяция хвоста",
                    ApiClass: "AI.Economics.Cohorts.RetentionFitter",
                    TheoryFile: "retention_fit.md",
                    Params:
                    [
                        new AlgoParam("model", "Модель", 0, 4, 0, 1, "", "Какое семейство кривых подгонять")
                            { Choices = RetentionModelChoices },
                        new AlgoParam("cohort", "Размер когорты", 100, 50_000, 2000, 100, "клиентов",
                            "От него зависит ширина доверительного интервала"),
                        new AlgoParam("observed", "Наблюдаемых периодов", 3, 24, 6, 1, "мес.",
                            "Сколько месяцев данных есть на самом деле"),
                        new AlgoParam("horizon", "Горизонт экстраполяции", 12, 72, 36, 6, "мес.",
                            "Докуда достраивается хвост кривой"),
                        new AlgoParam("churn1", "Отток первого месяца", 0.1, 0.7, 0.4, 0.05, "доля",
                            "Задаёт S(1) генерирующей модели"),
                        new AlgoParam("spread", "Однородность клиентов", 0.2, 5.0, 0.8, 0.1, "",
                            "Меньше — сильнее различаются клиенты и быстрее растёт удержание"),
                        new AlgoParam("boot", "Повторов бутстрапа", 0, 300, 120, 20, "шт.",
                            "Ноль — не считать доверительный интервал"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 42, 1, "", "Воспроизводимость выборки"),
                    ]),

                new AlgoDef(
                    Key: "cohort_matrix",
                    Title: "Когортная матрица",
                    Subtitle: "Треугольник когорт и сводная кривая без смещения от неполных данных",
                    ApiClass: "AI.Economics.Cohorts.CohortMatrix",
                    TheoryFile: "cohort_matrix.md",
                    Params:
                    [
                        new AlgoParam("cohorts", "Число когорт", 4, 18, 9, 1, "шт.", "Строк в треугольнике"),
                        new AlgoParam("size", "Размер первой когорты", 100, 5000, 600, 50, "клиентов",
                            "Последующие растут на заданный темп"),
                        new AlgoParam("growth", "Рост когорт", -0.1, 0.3, 0.08, 0.01, "в месяц",
                            "Быстрый рост делает молодые когорты доминирующими"),
                        new AlgoParam("churn1", "Отток первого месяца", 0.1, 0.7, 0.4, 0.05, "доля",
                            "Задаёт форму кривой удержания"),
                        new AlgoParam("spread", "Однородность клиентов", 0.2, 5.0, 0.8, 0.1, "",
                            "Меньше — сильнее различаются клиенты"),
                        new AlgoParam("drift", "Дрейф качества когорт", -0.02, 0.02, 0.0, 0.002, "в месяц",
                            "Ухудшение или улучшение удержания у поздних когорт"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 7, 1, "", "Воспроизводимость выборки"),
                    ]),
            ]),

        new("clv", "CLV без контракта",
            "Кто из клиентов ещё жив и сколько принесёт: BG/NBD, Pareto/NBD, Gamma-Gamma",
            [
                new AlgoDef(
                    Key: "bg_nbd",
                    Title: "BG/NBD: число будущих покупок",
                    Subtitle: "Вероятность активности и прогноз покупок по истории RFM",
                    ApiClass: "AI.Economics.Clv.BgNbdModel",
                    TheoryFile: "bg_nbd.md",
                    Params:
                    [
                        new AlgoParam("model", "Модель", 0, 2, 0, 1, "", "Какую модель частоты обучать")
                            { Choices = ClvModelChoices },
                        new AlgoParam("customers", "Клиентов в выборке", 100, 3000, 800, 50, "шт.",
                            "Синтетический портфель покупателей"),
                        new AlgoParam("active", "Доля активных", 0.2, 0.95, 0.6, 0.05, "доля",
                            "Остальные ушли в неизвестный момент"),
                        new AlgoParam("rate", "Покупок в месяц у активного", 0.2, 4.0, 1.0, 0.1, "шт.",
                            "Интенсивность пуассоновского потока"),
                        new AlgoParam("window", "Окно наблюдения", 6, 36, 18, 1, "мес.",
                            "Длительность истории покупок"),
                        new AlgoParam("horizon", "Горизонт прогноза", 1, 24, 12, 1, "мес.",
                            "На сколько вперёд прогнозируются покупки"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 17, 1, "", "Воспроизводимость выборки"),
                    ]),

                new AlgoDef(
                    Key: "gamma_gamma",
                    Title: "Gamma-Gamma: средний чек",
                    Subtitle: "Регрессия к среднему: почему один дорогой заказ ничего не доказывает",
                    ApiClass: "AI.Economics.Clv.GammaGammaModel",
                    TheoryFile: "gamma_gamma.md",
                    Params:
                    [
                        new AlgoParam("customers", "Клиентов в выборке", 100, 3000, 800, 50, "шт.", "Размер портфеля"),
                        new AlgoParam("mean_value", "Средний чек популяции", 500, 50_000, 5000, 100, "руб.",
                            "Центр распределения чеков"),
                        new AlgoParam("dispersion", "Разброс чеков", 0.1, 2.0, 0.6, 0.05, "",
                            "Коэффициент вариации внутри клиента"),
                        new AlgoParam("rate", "Покупок в месяц", 0.2, 4.0, 1.0, 0.1, "шт.", "Частота покупок"),
                        new AlgoParam("window", "Окно наблюдения", 6, 36, 18, 1, "мес.", "Длительность истории"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 23, 1, "", "Воспроизводимость выборки"),
                    ]),

                new AlgoDef(
                    Key: "clv_portfolio",
                    Title: "CLV портфеля",
                    Subtitle: "Частота × чек × дисконт: концентрация ценности в верхнем дециле",
                    ApiClass: "AI.Economics.Clv.ClvCalculator",
                    TheoryFile: "clv_portfolio.md",
                    Params:
                    [
                        new AlgoParam("customers", "Клиентов в выборке", 100, 3000, 800, 50, "шт.", "Размер портфеля"),
                        new AlgoParam("active", "Доля активных", 0.2, 0.95, 0.6, 0.05, "доля", "Остальные ушли"),
                        new AlgoParam("mean_value", "Средний чек", 500, 50_000, 5000, 100, "руб.", "Центр распределения"),
                        new AlgoParam("margin", "Маржа в чеке", 0.1, 1.0, 0.4, 0.05, "доля", "CLV считается по марже"),
                        new AlgoParam("horizon", "Горизонт", 3, 36, 12, 1, "мес.", "Период прогноза"),
                        new AlgoParam("discount", "Ставка дисконтирования", 0, 0.04, 0.01, 0.0025, "в месяц",
                            "Покупка через год стоит сегодня меньше"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 31, 1, "", "Воспроизводимость выборки"),
                    ]),
            ]),

        new("survival", "Отток как анализ выживаемости",
            "Кто уйдёт и когда: Каплан — Мейер, регрессия Кокса, конкурирующие риски",
            [
                new AlgoDef(
                    Key: "kaplan_meier",
                    Title: "Кривая Каплана — Мейера",
                    Subtitle: "Цензурирование, доверительный коридор и лог-ранговый критерий",
                    ApiClass: "AI.Economics.Survival.KaplanMeier",
                    TheoryFile: "kaplan_meier.md",
                    Params:
                    [
                        new AlgoParam("n", "Клиентов в группе", 30, 1000, 150, 10, "шт.", "Размер каждой из двух групп"),
                        new AlgoParam("rate_a", "Интенсивность оттока группы A", 0.01, 0.3, 0.05, 0.005, "в месяц",
                            "Базовая группа"),
                        new AlgoParam("rate_b", "Интенсивность оттока группы B", 0.01, 0.3, 0.09, 0.005, "в месяц",
                            "Сравниваемая группа"),
                        new AlgoParam("censor", "Конец наблюдения", 6, 60, 24, 2, "мес.",
                            "Все, кто дожил, попадают в цензурированные"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 5, 1, "", "Воспроизводимость выборки"),
                    ]),

                new AlgoDef(
                    Key: "cox_ph",
                    Title: "Регрессия Кокса",
                    Subtitle: "Отношения рисков по признакам клиента и индекс конкордации",
                    ApiClass: "AI.Economics.Survival.CoxProportionalHazards",
                    TheoryFile: "cox_ph.md",
                    Params:
                    [
                        new AlgoParam("n", "Клиентов", 60, 2000, 400, 20, "шт.", "Размер выборки"),
                        new AlgoParam("beta_usage", "Эффект интенсивности использования", -2.0, 0.5, -1.2, 0.1, "",
                            "Отрицательный коэффициент снижает риск оттока"),
                        new AlgoParam("beta_support", "Эффект обращений в поддержку", -0.5, 2.0, 0.8, 0.1, "",
                            "Положительный коэффициент повышает риск"),
                        new AlgoParam("base_rate", "Базовая интенсивность", 0.01, 0.2, 0.05, 0.005, "в месяц",
                            "Риск клиента со средними признаками"),
                        new AlgoParam("censor", "Конец наблюдения", 6, 60, 24, 2, "мес.", "Горизонт наблюдения"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 11, 1, "", "Воспроизводимость выборки"),
                    ]),

                new AlgoDef(
                    Key: "competing_risks",
                    Title: "Конкурирующие риски",
                    Subtitle: "Аален — Йохансен против наивного 1 − KM по каждой причине ухода",
                    ApiClass: "AI.Economics.Survival.CompetingRisks",
                    TheoryFile: "competing_risks.md",
                    Params:
                    [
                        new AlgoParam("n", "Клиентов", 100, 3000, 600, 50, "шт.", "Размер выборки"),
                        new AlgoParam("rate_price", "Интенсивность ухода из-за цены", 0.005, 0.2, 0.04, 0.005, "в месяц",
                            "Первая причина"),
                        new AlgoParam("rate_product", "Интенсивность ухода из-за продукта", 0.005, 0.2, 0.03, 0.005, "в месяц",
                            "Вторая причина"),
                        new AlgoParam("rate_external", "Интенсивность внешнего ухода", 0.0, 0.2, 0.02, 0.005, "в месяц",
                            "Клиент закрылся — повлиять нельзя"),
                        new AlgoParam("censor", "Конец наблюдения", 6, 60, 24, 2, "мес.", "Горизонт наблюдения"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 13, 1, "", "Воспроизводимость выборки"),
                    ]),
            ]),

        new("saas", "SaaS-метрики",
            "MRR-мостик, NDR и GRR, Rule of 40, magic number, burn multiple",
            [
                new AlgoDef(
                    Key: "mrr_bridge",
                    Title: "MRR-мостик",
                    Subtitle: "New / expansion / contraction / churn, NDR, GRR и quick ratio",
                    ApiClass: "AI.Economics.Saas.MrrBridge",
                    TheoryFile: "mrr_bridge.md",
                    Params:
                    [
                        new AlgoParam("customers", "Клиентов на старте", 20, 2000, 200, 10, "шт.", "База первого месяца"),
                        new AlgoParam("months", "Месяцев", 3, 24, 12, 1, "шт.", "Длина ряда снимков"),
                        new AlgoParam("mrr", "Средний MRR клиента", 1000, 200_000, 25_000, 1000, "руб.",
                            "Стартовая выручка на клиента"),
                        new AlgoParam("new_rate", "Приток новых", 0, 0.3, 0.08, 0.01, "в месяц", "Доля от базы"),
                        new AlgoParam("churn_rate", "Отток клиентов", 0, 0.15, 0.03, 0.005, "в месяц", "Доля от базы"),
                        new AlgoParam("expansion", "Доля расширяющихся", 0, 0.4, 0.12, 0.01, "в месяц",
                            "Клиенты, увеличивающие тариф"),
                        new AlgoParam("contraction", "Доля сокращающихся", 0, 0.4, 0.06, 0.01, "в месяц",
                            "Клиенты, уменьшающие тариф"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 19, 1, "", "Воспроизводимость выборки"),
                    ]),

                new AlgoDef(
                    Key: "saas_health",
                    Title: "Здоровье SaaS-бизнеса",
                    Subtitle: "Rule of 40, magic number, burn multiple, CAC payback с оценками",
                    ApiClass: "AI.Economics.Saas.SaasMetrics",
                    TheoryFile: "saas_health.md",
                    Params:
                    [
                        new AlgoParam("arr_start", "ARR на начало года", 10_000_000, 2_000_000_000, 120_000_000, 10_000_000, "руб.",
                            "База для темпа роста"),
                        new AlgoParam("growth", "Рост ARR за год", -0.2, 3.0, 0.7, 0.05, "доля", "Годовой темп"),
                        new AlgoParam("sm_share", "Доля S&M в ARR", 0.05, 1.5, 0.4, 0.05, "доля",
                            "Затраты на продажи и маркетинг"),
                        new AlgoParam("burn_share", "Чистое сжигание к ARR", 0, 2.0, 0.5, 0.05, "доля", "Сколько денег уходит"),
                        new AlgoParam("fcf", "Маржа FCF", -1.5, 0.5, -0.35, 0.05, "доля", "Вторая половина Rule of 40"),
                        new AlgoParam("cac", "CAC", 10_000, 3_000_000, 300_000, 10_000, "руб.", "Стоимость привлечения"),
                        new AlgoParam("arpa", "ARPA в месяц", 5000, 1_000_000, 60_000, 5000, "руб.", "Выручка с клиента"),
                        new AlgoParam("margin", "Валовая маржа", 0.3, 1.0, 0.8, 0.05, "доля", "Для расчёта окупаемости"),
                        new AlgoParam("ndr", "NDR", 0.6, 1.6, 1.12, 0.02, "доля", "Удержание выручки с расширениями"),
                    ]),
            ]),

        new("runway", "Запас прочности",
            "Сколько компания живёт на своих деньгах, если будущее случайно",
            [
                new AlgoDef(
                    Key: "runway_mc",
                    Title: "Runway методом Монте-Карло",
                    Subtitle: "Распределение месяца исчерпания денег вместо деления кассы на burn",
                    ApiClass: "AI.Economics.Runway.RunwaySimulator",
                    TheoryFile: "runway_mc.md",
                    Params:
                    [
                        new AlgoParam("cash", "Денег на счету", 1_000_000, 500_000_000, 60_000_000, 1_000_000, "руб.",
                            "Стартовый остаток"),
                        new AlgoParam("revenue", "Выручка первого месяца", 0, 100_000_000, 6_000_000, 500_000, "руб.",
                            "Стартовая точка случайного блуждания"),
                        new AlgoParam("growth", "Средний рост выручки", -0.05, 0.3, 0.07, 0.01, "в месяц",
                            "Медианный темп"),
                        new AlgoParam("vol", "Волатильность роста", 0, 0.5, 0.15, 0.01, "",
                            "Главный параметр: именно он превращает одно число в распределение"),
                        new AlgoParam("costs", "Затраты первого месяца", 500_000, 100_000_000, 12_000_000, 500_000, "руб.",
                            "Операционные расходы"),
                        new AlgoParam("cost_growth", "Рост затрат", -0.05, 0.2, 0.03, 0.005, "в месяц", "Медианный темп"),
                        new AlgoParam("margin", "Валовая маржа", 0.2, 1.0, 0.75, 0.05, "доля", "Доля выручки, доходящая до денег"),
                        new AlgoParam("horizon", "Горизонт", 6, 60, 30, 3, "мес.", "Длина симуляции"),
                        new AlgoParam("sims", "Траекторий", 200, 20_000, 4000, 200, "шт.", "Число прогонов Монте-Карло"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 42, 1, "", "Воспроизводимость"),
                    ]),
            ]),

        new("equity", "Cap table и выход",
            "Раунды, SAFE, опционный пул и распределение денег при продаже компании",
            [
                new AlgoDef(
                    Key: "funding_round",
                    Title: "Ценовой раунд и разводнение",
                    Subtitle: "Pool shuffle, конвертация SAFE и эффективная оценка для основателей",
                    ApiClass: "AI.Economics.Equity.FundingRound",
                    TheoryFile: "funding_round.md",
                    Params:
                    [
                        new AlgoParam("premoney", "Оценка до денег", 10_000_000, 5_000_000_000, 400_000_000, 10_000_000, "руб.",
                            "Заявленная в term sheet"),
                        new AlgoParam("investment", "Инвестиция", 1_000_000, 1_000_000_000, 100_000_000, 1_000_000, "руб.",
                            "Сумма раунда"),
                        new AlgoParam("pool", "Целевой опционный пул", 0, 0.25, 0.1, 0.01, "доля после раунда",
                            "Создаётся до денег и размывает только основателей"),
                        new AlgoParam("safe_amount", "Сумма SAFE", 0, 200_000_000, 20_000_000, 1_000_000, "руб.",
                            "Ноль — раунд без конвертируемых"),
                        new AlgoParam("safe_cap", "Потолок оценки SAFE", 10_000_000, 1_000_000_000, 120_000_000, 10_000_000, "руб.",
                            "Ниже оценки раунда — ангел входит дешевле"),
                        new AlgoParam("safe_discount", "Скидка SAFE", 0, 0.4, 0.2, 0.05, "доля",
                            "Применяется, если она выгоднее потолка"),
                        new AlgoParam("founder_split", "Доля первого основателя", 0.3, 0.9, 0.6, 0.05, "доля",
                            "Как поделены акции между двумя основателями"),
                    ]),

                new AlgoDef(
                    Key: "exit_waterfall",
                    Title: "Каскад выплат при выходе",
                    Subtitle: "Преференции, участие с потолком и точка перехода к конвертации",
                    ApiClass: "AI.Economics.Equity.ExitWaterfall",
                    TheoryFile: "exit_waterfall.md",
                    Params:
                    [
                        new AlgoParam("exit", "Цена продажи компании", 10_000_000, 10_000_000_000, 1_200_000_000, 10_000_000, "руб.",
                            "Точка, для которой считается разбивка"),
                        new AlgoParam("pref_type", "Тип преференции Series B", 0, 2, 0, 1, "",
                            "Неучаствующая, участвующая или участвующая с потолком")
                            { Choices = PreferenceChoices },
                        new AlgoParam("multiple", "Кратность преференции", 1.0, 3.0, 1.0, 0.25, "×",
                            "Сколько вложенного возвращается раньше остальных"),
                        new AlgoParam("cap", "Потолок участия", 1.5, 5.0, 2.0, 0.25, "×",
                            "Работает только для участвующей с потолком"),
                        new AlgoParam("a_investment", "Инвестиция Series A", 10_000_000, 500_000_000, 100_000_000, 10_000_000, "руб.",
                            "Первый раунд"),
                        new AlgoParam("a_premoney", "Оценка до денег Series A", 50_000_000, 3_000_000_000, 400_000_000, 10_000_000, "руб.",
                            "Определяет долю Series A"),
                        new AlgoParam("b_investment", "Инвестиция Series B", 10_000_000, 2_000_000_000, 300_000_000, 10_000_000, "руб.",
                            "Второй раунд, старшая преференция"),
                        new AlgoParam("b_premoney", "Оценка до денег Series B", 100_000_000, 10_000_000_000, 1_500_000_000, 50_000_000, "руб.",
                            "Определяет долю Series B"),
                    ]),
            ]),

        new("valuation", "Оценка стартапа",
            "Четыре классических метода на одних данных и реальные опционы для НИОКР",
            [
                new AlgoDef(
                    Key: "startup_valuation",
                    Title: "VC, Беркус, Scorecard, First Chicago",
                    Subtitle: "Расхождение методов показывает, какое допущение решает всё",
                    ApiClass: "AI.Economics.Valuation.StartupValuation",
                    TheoryFile: "startup_valuation.md",
                    Params:
                    [
                        new AlgoParam("investment", "Инвестиция", 1_000_000, 500_000_000, 50_000_000, 1_000_000, "руб.",
                            "Размер раунда для метода венчурного капитала"),
                        new AlgoParam("exit_revenue", "Выручка на выходе", 50_000_000, 20_000_000_000, 1_500_000_000, 50_000_000, "руб.",
                            "Прогноз через N лет"),
                        new AlgoParam("multiple", "Мультипликатор к выручке", 1, 15, 4, 0.5, "×", "Оценка при выходе"),
                        new AlgoParam("years", "Лет до выхода", 2, 10, 5, 1, "лет", "Горизонт инвестора"),
                        new AlgoParam("irr", "Требуемая доходность", 0.2, 1.2, 0.5, 0.05, "годовых",
                            "Ключевой параметр метода венчурного капитала"),
                        new AlgoParam("dilution", "Разводнение будущими раундами", 0, 0.7, 0.4, 0.05, "доля",
                            "Насколько уменьшится доля инвестора к выходу"),
                        new AlgoParam("market_avg", "Средняя оценка по рынку", 10_000_000, 1_000_000_000, 120_000_000, 10_000_000, "руб.",
                            "База метода Scorecard"),
                        new AlgoParam("team", "Оценка команды", 0.3, 2.0, 1.3, 0.1, "× к рынку",
                            "Самый весомый фактор Scorecard"),
                        new AlgoParam("p_success", "Вероятность прорыва", 0.01, 0.5, 0.1, 0.01, "доля",
                            "Лучший сценарий First Chicago"),
                    ]),

                new AlgoDef(
                    Key: "real_options",
                    Title: "Реальные опционы для НИОКР",
                    Subtitle: "Стоимость права подождать там, где обычный NPV отрицателен",
                    ApiClass: "AI.Economics.Valuation.RealOptionValuation",
                    TheoryFile: "real_options.md",
                    Params:
                    [
                        new AlgoParam("value", "Приведённая стоимость проекта", 10_000_000, 2_000_000_000, 160_000_000, 10_000_000, "руб.",
                            "Аналог цены базового актива"),
                        new AlgoParam("cost", "Стоимость запуска", 10_000_000, 2_000_000_000, 200_000_000, 10_000_000, "руб.",
                            "Аналог цены исполнения"),
                        new AlgoParam("years", "Срок принятия решения", 0.5, 10, 3, 0.5, "лет", "Время до экспирации"),
                        new AlgoParam("vol", "Волатильность стоимости", 0.1, 1.5, 0.6, 0.05, "годовая",
                            "Чем выше неопределённость, тем дороже право подождать"),
                        new AlgoParam("rate", "Безрисковая ставка", 0, 0.25, 0.08, 0.01, "годовых", "Ставка дисконтирования"),
                        new AlgoParam("leak", "Утечка стоимости", 0, 0.3, 0.05, 0.01, "в год",
                            "Потери от того, что конкурент выйдет раньше"),
                    ]),
            ]),

        new("market", "Рынок и adoption",
            "TAM/SAM/SOM двумя способами и кривая проникновения продукта",
            [
                new AlgoDef(
                    Key: "market_sizing",
                    Title: "TAM / SAM / SOM с согласованием",
                    Subtitle: "Сверху вниз и снизу вверх: ценность в расхождении оценок",
                    ApiClass: "AI.Economics.Market.MarketSizing",
                    TheoryFile: "market_sizing.md",
                    Params:
                    [
                        new AlgoParam("total", "Объём мирового рынка", 1_000_000_000, 5_000_000_000_000, 800_000_000_000, 10_000_000_000, "руб.",
                            "Число из отраслевого отчёта"),
                        new AlgoParam("geo", "Доля целевой географии", 0.01, 1.0, 0.08, 0.01, "доля", "Первый фильтр"),
                        new AlgoParam("segment", "Доля целевого сегмента", 0.01, 1.0, 0.25, 0.01, "доля", "Второй фильтр"),
                        new AlgoParam("addressable", "Доля, доступная продукту", 0.05, 1.0, 0.5, 0.05, "доля", "Третий фильтр"),
                        new AlgoParam("achievable", "Захватываемая доля", 0.005, 0.3, 0.04, 0.005, "доля", "Переход от SAM к SOM"),
                        new AlgoParam("accounts", "Потенциальных клиентов", 100, 500_000, 12_000, 100, "шт.",
                            "База оценки снизу вверх"),
                        new AlgoParam("arpa", "Годовой чек", 50_000, 50_000_000, 1_200_000, 50_000, "руб.", "Средний контракт"),
                        new AlgoParam("qualified", "Доля подходящих", 0.05, 1.0, 0.6, 0.05, "доля", "Профиль клиента"),
                        new AlgoParam("reachable", "Доля достижимых", 0.05, 1.0, 0.5, 0.05, "доля", "Охват каналов"),
                        new AlgoParam("winrate", "Конверсия в оплату", 0.01, 0.5, 0.06, 0.01, "доля", "Итоговая воронка"),
                    ]),

                new AlgoDef(
                    Key: "bass_diffusion",
                    Title: "Диффузия Басса",
                    Subtitle: "Подгонка p, q, m и момент, после которого продажи падают сами",
                    ApiClass: "AI.Economics.Market.BassDiffusion",
                    TheoryFile: "bass_diffusion.md",
                    Params:
                    [
                        new AlgoParam("m", "Потенциал рынка", 10_000, 5_000_000, 200_000, 10_000, "клиентов",
                            "Предельное число принявших продукт"),
                        new AlgoParam("p", "Коэффициент инновации", 0.001, 0.1, 0.02, 0.001, "",
                            "Скорость принятия независимо от других"),
                        new AlgoParam("q", "Коэффициент имитации", 0.05, 1.0, 0.4, 0.01, "",
                            "Сила сарафанного радио"),
                        new AlgoParam("observed", "Наблюдаемых периодов", 5, 40, 14, 1, "мес.",
                            "Сколько месяцев истории доступно для подгонки"),
                        new AlgoParam("horizon", "Горизонт прогноза", 12, 96, 48, 6, "мес.", "Длина прогноза"),
                        new AlgoParam("noise", "Шум наблюдений", 0, 0.15, 0.03, 0.005, "доля",
                            "Проверка устойчивости подгонки к погрешности учёта"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 3, 1, "", "Воспроизводимость выборки"),
                    ]),
            ]),

        new("pricing", "Ценообразование",
            "Эластичность с поправкой на эндогенность, оптимизация цен линейки, готовность платить",
            [
                new AlgoDef(
                    Key: "elasticity",
                    Title: "Эластичность спроса",
                    Subtitle: "Наивный МНК даёт неверный знак: панель и инструмент против смещения",
                    ApiClass: "AI.Economics.Pricing.DemandElasticity",
                    TheoryFile: "elasticity.md",
                    Params:
                    [
                        new AlgoParam("method", "Способ оценки", 0, 2, 2, 1, "",
                            "Какую из трёх оценок показать подробно")
                            { Choices = ElasticityChoices },
                        new AlgoParam("elasticity", "Истинная эластичность", -4.0, -0.3, -1.8, 0.1, "",
                            "Значение, заложенное в генератор данных"),
                        new AlgoParam("endogeneity", "Сила эндогенности", 0, 2.0, 0.8, 0.1, "",
                            "Насколько цена реагирует на ненаблюдаемый спрос — источник смещения"),
                        new AlgoParam("n", "Наблюдений", 60, 2000, 400, 20, "шт.", "Размер выборки"),
                        new AlgoParam("noise", "Шум", 0.02, 0.5, 0.1, 0.02, "", "Прочая случайность"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 11, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "price_optimization",
                    Title: "Оптимизация цен линейки",
                    Subtitle: "Кросс-эластичности, каннибализация и ограничения по марже и объёму",
                    ApiClass: "AI.Economics.Pricing.PriceOptimizer",
                    TheoryFile: "price_optimization.md",
                    Params:
                    [
                        new AlgoParam("own", "Собственная эластичность", -5.0, -0.5, -2.2, 0.1, "",
                            "Одинаковая у всех позиций линейки"),
                        new AlgoParam("cross", "Перекрёстная эластичность", 0, 2.0, 0.6, 0.1, "",
                            "Положительная означает заменители: снижение цены забирает спрос у соседей"),
                        new AlgoParam("max_change", "Допустимое изменение цены", 0.05, 0.6, 0.25, 0.05, "доля",
                            "Коридор, за который выходить нельзя"),
                        new AlgoParam("min_margin", "Минимальная маржа", 0, 0.6, 0.25, 0.05, "доля",
                            "Задаётся границей цены, а не штрафом — выполняется точно"),
                        new AlgoParam("min_volume", "Минимальный объём", 0, 12_000, 0, 500, "шт.",
                            "Ограничение на долю рынка; 0 — не задано"),
                    ]),

                new AlgoDef(
                    Key: "wtp_survey",
                    Title: "Готовность платить",
                    Subtitle: "Ван Вестендорп очерчивает коридор, Габор — Грейнджер даёт кривую спроса",
                    ApiClass: "AI.Economics.Pricing.WillingnessToPay",
                    TheoryFile: "wtp_survey.md",
                    Params:
                    [
                        new AlgoParam("method", "Метод", 0, 1, 0, 1, "", "Какое исследование смоделировать")
                            { Choices = WtpChoices },
                        new AlgoParam("respondents", "Респондентов", 30, 1500, 300, 10, "чел.",
                            "От размера выборки зависит устойчивость пересечений"),
                        new AlgoParam("centre", "Центр готовности платить", 200, 20_000, 1000, 100, "руб.",
                            "Медианная оценка аудитории"),
                        new AlgoParam("spread", "Разброс аудитории", 0.05, 1.0, 0.25, 0.05, "",
                            "Больше — аудитория неоднороднее, коридор шире"),
                        new AlgoParam("cost", "Переменные издержки", 0, 10_000, 300, 50, "руб.",
                            "Нужны для оптимума по прибыли в методе Габора — Грейнджера"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 21, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "conjoint",
                    Title: "Conjoint-анализ",
                    Subtitle: "Частные полезности, важность атрибутов, готовность платить и симулятор долей",
                    ApiClass: "AI.Economics.Pricing.MultinomialLogit",
                    TheoryFile: "conjoint.md",
                    Params:
                    [
                        new AlgoParam("method", "Модель", 0, 1, 0, 1, "",
                            "Агрегатный логит или индивидуальные полезности")
                            { Choices = ConjointChoices },
                        new AlgoParam("respondents", "Респондентов", 40, 400, 150, 10, "чел.", "Размер выборки"),
                        new AlgoParam("tasks", "Заданий на респондента", 4, 20, 10, 1, "шт.",
                            "Сколько раз каждый делает выбор"),
                        new AlgoParam("heterogeneity", "Неоднородность аудитории", 0, 1.5, 0.6, 0.1, "",
                            "Разброс индивидуальных предпочтений вокруг среднего"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 42, 1, "", "Воспроизводимость"),
                    ]),
            ]),

        new("marketing", "Маркетинг-аналитика",
            "Маркетинг-микс с adstock и насыщением, распределение бюджета, uplift-моделирование",
            [
                new AlgoDef(
                    Key: "mmm",
                    Title: "Маркетинг-микс модель",
                    Subtitle: "Adstock, кривая Хилла и декомпозиция продаж по каналам",
                    ApiClass: "AI.Economics.Marketing.MarketingMixModel",
                    TheoryFile: "mmm.md",
                    Params:
                    [
                        new AlgoParam("weeks", "Недель истории", 60, 260, 156, 4, "нед.",
                            "Для устойчивой оценки нужно минимум два года"),
                        new AlgoParam("tv_decay", "Затухание ТВ", 0, 0.85, 0.6, 0.05, "",
                            "Истинный коэффициент adstock, заложенный в генератор"),
                        new AlgoParam("digital_decay", "Затухание digital", 0, 0.85, 0.2, 0.05, "",
                            "У digital эффект короче"),
                        new AlgoParam("saturation", "Точка насыщения", 0.3, 3.0, 1.0, 0.1, "×",
                            "Ниже — каналы насыщаются раньше"),
                        new AlgoParam("noise", "Шум продаж", 0.05, 1.5, 0.25, 0.05, "млн",
                            "Насколько трудно отделить рекламу от прочего"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 3, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "budget_allocation",
                    Title: "Распределение бюджета",
                    Subtitle: "Выравнивание предельной отдачи вместо распределения по среднему ROI",
                    ApiClass: "AI.Economics.Marketing.BudgetOptimizer",
                    TheoryFile: "budget_allocation.md",
                    Params:
                    [
                        new AlgoParam("budget_scale", "Бюджет к текущему", 0.3, 3.0, 1.0, 0.1, "×",
                            "Единица означает перераспределение того же бюджета"),
                        new AlgoParam("tv_decay", "Затухание ТВ", 0, 0.85, 0.6, 0.05, "", "Параметр генератора"),
                        new AlgoParam("saturation", "Точка насыщения", 0.3, 3.0, 1.0, 0.1, "×",
                            "Определяет, где начинается убывающая отдача"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 3, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "uplift",
                    Title: "Uplift-моделирование промо",
                    Subtitle: "Кому скидка меняет поведение, а кому просто уменьшает чек",
                    ApiClass: "AI.Economics.Marketing.UpliftModeling",
                    TheoryFile: "uplift.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 1000, 30_000, 8000, 500, "шт.",
                            "Uplift — разность двух зашумлённых величин, данных нужно много"),
                        new AlgoParam("effect", "Максимальный прирост", 0, 0.6, 0.30, 0.05, "доля",
                            "Насколько сильно промо действует на самых восприимчивых"),
                        new AlgoParam("sleeping", "Вред лояльным", 0, 0.3, 0.10, 0.02, "доля",
                            "Отрицательный эффект на тех, кто купил бы и без скидки"),
                        new AlgoParam("promo_cost", "Стоимость промо", 5, 500, 60, 5, "руб.",
                            "На одного охваченного клиента"),
                        new AlgoParam("margin", "Маржа с конверсии", 50, 3000, 300, 50, "руб.",
                            "Отношение стоимости к марже задаёт порог окупаемости"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 13, 1, "", "Воспроизводимость"),
                    ]),
            ]),

        new("experiments", "Эксперименты",
            "Размер выборки, CUPED, последовательные критерии и многорукие бандиты",
            [
                new AlgoDef(
                    Key: "experiment_design",
                    Title: "Размер выборки и MDE",
                    Subtitle: "Что эксперимент способен обнаружить на доступном трафике",
                    ApiClass: "AI.Economics.Experiments.ExperimentDesign",
                    TheoryFile: "experiment_design.md",
                    Params:
                    [
                        new AlgoParam("baseline", "Базовая конверсия", 0.002, 0.5, 0.05, 0.002, "доля",
                            "Конверсия контрольной группы"),
                        new AlgoParam("effect", "Обнаруживаемый эффект", 0.01, 0.5, 0.10, 0.01, "относительный",
                            "Прирост, который нужно уметь отличить от нуля"),
                        new AlgoParam("alpha", "Уровень значимости", 0.01, 0.2, 0.05, 0.01, "",
                            "Вероятность ложного срабатывания"),
                        new AlgoParam("power", "Мощность", 0.5, 0.99, 0.8, 0.05, "",
                            "Вероятность обнаружить эффект, если он есть"),
                        new AlgoParam("variants", "Вариантов", 2, 6, 2, 1, "шт.",
                            "Включая контроль; больше двух ужесточает порог значимости"),
                        new AlgoParam("traffic", "Трафик в сутки", 100, 100_000, 2000, 100, "шт.",
                            "Для расчёта длительности эксперимента"),
                    ]),

                new AlgoDef(
                    Key: "cuped",
                    Title: "CUPED: снижение дисперсии",
                    Subtitle: "Предэкспериментальные данные сокращают требуемую выборку вдвое",
                    ApiClass: "AI.Economics.Experiments.Cuped",
                    TheoryFile: "cuped.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений в группе", 200, 20_000, 2000, 100, "шт.",
                            "Размер каждой из двух групп"),
                        new AlgoParam("correlation", "Связь с прошлым периодом", 0.05, 0.95, 0.8, 0.05, "",
                            "Главный параметр: выигрыш равен квадрату корреляции"),
                        new AlgoParam("effect", "Истинный эффект", 0, 2.0, 0.5, 0.1, "",
                            "Прирост метрики в группе воздействия"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 31, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "sequential_testing",
                    Title: "Последовательные критерии",
                    Subtitle: "Остановка в любой момент без раздувания ошибки первого рода",
                    ApiClass: "AI.Economics.Experiments.SequentialTest",
                    TheoryFile: "sequential_testing.md",
                    Params:
                    [
                        new AlgoParam("method", "Подход", 0, 1, 0, 1, "",
                            "Всегда допустимое p-значение или байесовское сравнение")
                            { Choices = SequentialChoices },
                        new AlgoParam("n", "Наблюдений в группе", 500, 50_000, 4000, 500, "шт.",
                            "Полный горизонт эксперимента"),
                        new AlgoParam("baseline", "Базовая конверсия", 0.01, 0.5, 0.10, 0.01, "доля",
                            "Контрольная группа"),
                        new AlgoParam("lift", "Истинный прирост", 0, 1.0, 0.30, 0.05, "относительный",
                            "Ноль означает отсутствие эффекта — проверка ложных срабатываний"),
                        new AlgoParam("tau", "Масштаб априорного эффекта", 0.005, 0.3, 0.05, 0.005, "",
                            "Больше — критерий чувствительнее к крупным эффектам"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 77, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "bandits",
                    Title: "Многорукие бандиты",
                    Subtitle: "Потери от исследования: A/B-тест против адаптивных стратегий",
                    ApiClass: "AI.Economics.Experiments.Bandits",
                    TheoryFile: "bandits.md",
                    Params:
                    [
                        new AlgoParam("policy", "Стратегия для разбора", 0, 3, 3, 1, "",
                            "Какую стратегию показать подробно")
                            { Choices = BanditChoices },
                        new AlgoParam("arms", "Вариантов", 2, 8, 3, 1, "шт.", "Число сравниваемых вариантов"),
                        new AlgoParam("best_rate", "Конверсия лучшего", 0.02, 0.5, 0.12, 0.01, "доля",
                            "Истинная конверсия лучшего варианта"),
                        new AlgoParam("gap", "Разрыв между вариантами", 0.002, 0.1, 0.03, 0.002, "доля",
                            "Чем меньше, тем труднее найти лучший"),
                        new AlgoParam("rounds", "Показов", 1000, 100_000, 20_000, 1000, "шт.",
                            "Длина симуляции"),
                        new AlgoParam("epsilon", "Доля исследования", 0.01, 0.5, 0.1, 0.01, "",
                            "Только для эпсилон-жадной стратегии"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 9, 1, "", "Воспроизводимость"),
                    ]),
            ]),

        new("forecasting", "Прогнозирование спроса",
            "ARIMA, сглаживание, Theta, STL, прерывистый спрос, иерархия, бэктест и интервалы",
            [
                new AlgoDef(
                    Key: "arima",
                    Title: "ARIMA и SARIMA",
                    Subtitle: "Авторегрессия со скользящим средним и сезонной частью",
                    ApiClass: "AI.Economics.Forecasting.Arima",
                    TheoryFile: "arima.md",
                    Params:
                    [
                        new AlgoParam("auto", "Подбор порядка", 0, 1, 1, 1, "",
                            "Перебор по сетке с выбором по AIC")
                            { Choices = YesNoChoices },
                        new AlgoParam("n", "Наблюдений", 60, 400, 156, 4, "шт.", "Длина истории"),
                        new AlgoParam("period", "Сезонный период", 1, 52, 52, 1, "", "1 отключает сезонность"),
                        new AlgoParam("horizon", "Горизонт", 4, 104, 26, 2, "", "Длина прогноза"),
                        new AlgoParam("ar", "Порядок AR", 0, 3, 1, 1, "", "Используется при ручном режиме"),
                        new AlgoParam("diff", "Порядок разности", 0, 2, 1, 1, "", "Используется при ручном режиме"),
                        new AlgoParam("ma", "Порядок MA", 0, 3, 1, 1, "", "Используется при ручном режиме"),
                        new AlgoParam("amplitude", "Амплитуда сезонности", 0, 400, 150, 10, "", "Параметр генератора"),
                        new AlgoParam("slope", "Тренд", -5, 10, 2.0, 0.5, "за период", "Параметр генератора"),
                        new AlgoParam("noise", "Шум", 5, 200, 40, 5, "", "Параметр генератора"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 7, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "ets",
                    Title: "Экспоненциальное сглаживание",
                    Subtitle: "Хольт — Уинтерс с затухающим трендом: прогноз не уходит в бесконечность",
                    ApiClass: "AI.Economics.Forecasting.ExponentialSmoothing",
                    TheoryFile: "ets.md",
                    Params:
                    [
                        new AlgoParam("seasonality", "Сезонность", 0, 3, 1, 1, "",
                            "Вид сезонной составляющей либо автоподбор")
                            { Choices = SeasonalityChoices },
                        new AlgoParam("damped", "Затухание тренда", 0, 1, 1, 1, "",
                            "Без него прогноз линейно продолжает последний наклон")
                            { Choices = YesNoChoices },
                        new AlgoParam("n", "Наблюдений", 30, 300, 120, 6, "шт.", "Длина истории"),
                        new AlgoParam("period", "Сезонный период", 1, 52, 12, 1, "", "1 отключает сезонность"),
                        new AlgoParam("horizon", "Горизонт", 4, 60, 24, 2, "", "Длина прогноза"),
                        new AlgoParam("amplitude", "Амплитуда сезонности", 0, 400, 150, 10, "", "Параметр генератора"),
                        new AlgoParam("slope", "Тренд", -5, 15, 4.0, 0.5, "за период", "Параметр генератора"),
                        new AlgoParam("noise", "Шум", 5, 200, 30, 5, "", "Параметр генератора"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 3, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "theta",
                    Title: "Метод Theta",
                    Subtitle: "Победитель соревнования M3: сглаживание плюс половина тренда",
                    ApiClass: "AI.Economics.Forecasting.ThetaMethod",
                    TheoryFile: "theta.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 20, 300, 96, 4, "шт.", "Длина истории"),
                        new AlgoParam("period", "Сезонный период", 1, 52, 12, 1, "", "1 отключает сезонность"),
                        new AlgoParam("horizon", "Горизонт", 3, 48, 18, 1, "", "Длина прогноза"),
                        new AlgoParam("amplitude", "Амплитуда сезонности", 0, 400, 120, 10, "", "Параметр генератора"),
                        new AlgoParam("slope", "Тренд", -5, 15, 3.0, 0.5, "за период", "Параметр генератора"),
                        new AlgoParam("noise", "Шум", 5, 200, 40, 5, "", "Параметр генератора"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 23, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "stl",
                    Title: "STL-разложение",
                    Subtitle: "Тренд, сезонность и остаток; устойчиво к промо-выбросам",
                    ApiClass: "AI.Economics.Forecasting.StlDecomposition",
                    TheoryFile: "stl.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 60, 400, 156, 4, "шт.", "Длина ряда"),
                        new AlgoParam("period", "Сезонный период", 4, 52, 52, 1, "", "Длина сезонного цикла"),
                        new AlgoParam("robust", "Итераций устойчивости", 0, 3, 1, 1, "",
                            "Ноль отключает защиту от выбросов — сравните результат"),
                        new AlgoParam("outliers", "Доля промо-всплесков", 0, 0.15, 0.03, 0.01, "доля",
                            "Разовые выбросы, ломающие классическое разложение"),
                        new AlgoParam("amplitude", "Амплитуда сезонности", 0, 500, 200, 10, "", "Параметр генератора"),
                        new AlgoParam("slope", "Тренд", -5, 10, 2.0, 0.5, "за период", "Параметр генератора"),
                        new AlgoParam("noise", "Шум", 5, 200, 40, 5, "", "Параметр генератора"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 13, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "intermittent",
                    Title: "Прерывистый спрос",
                    Subtitle: "Кростон, Синтетос — Бойлан и TSB для запчастей и B2B",
                    ApiClass: "AI.Economics.Forecasting.IntermittentDemand",
                    TheoryFile: "intermittent.md",
                    Params:
                    [
                        new AlgoParam("method", "Метод для разбора", 0, 2, 1, 1, "",
                            "Какой из трёх методов показать подробно")
                            { Choices = IntermittentChoices },
                        new AlgoParam("n", "Наблюдений", 24, 300, 120, 6, "шт.", "Длина ряда"),
                        new AlgoParam("probability", "Вероятность спроса", 0.05, 0.9, 0.25, 0.05, "в период",
                            "Чем ниже, тем прерывистее ряд"),
                        new AlgoParam("size", "Средний размер заказа", 1, 200, 12, 1, "шт.",
                            "Когда спрос есть"),
                        new AlgoParam("alpha", "Параметр сглаживания", 0.01, 0.5, 0.1, 0.01, "",
                            "Скорость реакции на новые наблюдения"),
                        new AlgoParam("lead_time", "Срок поставки", 1, 24, 4, 1, "периодов",
                            "Определяет точку перезаказа"),
                        new AlgoParam("service", "Уровень сервиса", 0.5, 0.999, 0.95, 0.01, "",
                            "Задаёт страховой запас"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 3, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "hierarchical",
                    Title: "Иерархическое согласование",
                    Subtitle: "Сумма прогнозов по SKU обязана сходиться с прогнозом по компании",
                    ApiClass: "AI.Economics.Forecasting.HierarchicalReconciliation",
                    TheoryFile: "hierarchical.md",
                    Params:
                    [
                        new AlgoParam("method", "Метод согласования", 0, 3, 3, 1, "",
                            "Как распределять расхождение между уровнями")
                            { Choices = ReconciliationChoices },
                        new AlgoParam("groups", "Групп", 2, 8, 3, 1, "шт.", "Средний уровень иерархии"),
                        new AlgoParam("per_group", "Позиций в группе", 2, 8, 3, 1, "шт.", "Нижний уровень"),
                        new AlgoParam("disagreement", "Расхождение уровней", 0, 0.4, 0.12, 0.02, "доля",
                            "Насколько независимые прогнозы противоречат друг другу"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 5, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "backtest",
                    Title: "Бэктест со скользящим началом",
                    Subtitle: "Единственный честный способ сравнить модели — и наивный прогноз в их числе",
                    ApiClass: "AI.Economics.Forecasting.ForecastBacktest",
                    TheoryFile: "backtest.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 60, 400, 150, 6, "шт.", "Длина ряда"),
                        new AlgoParam("period", "Сезонный период", 1, 52, 12, 1, "", "1 отключает сезонность"),
                        new AlgoParam("horizon", "Горизонт среза", 1, 24, 6, 1, "", "На сколько прогнозируем"),
                        new AlgoParam("folds", "Срезов", 2, 20, 6, 1, "шт.", "Число последовательных проверок"),
                        new AlgoParam("amplitude", "Амплитуда сезонности", 0, 400, 150, 10, "", "Параметр генератора"),
                        new AlgoParam("slope", "Тренд", -5, 15, 3.0, 0.5, "за период", "Параметр генератора"),
                        new AlgoParam("noise", "Шум", 5, 200, 40, 5, "",
                            "Чем больше, тем труднее обыграть наивный прогноз"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 21, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "conformal",
                    Title: "Конформные интервалы",
                    Subtitle: "Гарантированное покрытие без предположений о распределении ошибки",
                    ApiClass: "AI.Economics.Forecasting.ConformalPrediction",
                    TheoryFile: "conformal.md",
                    Params:
                    [
                        new AlgoParam("model", "Базовая модель", 0, 1, 0, 1, "",
                            "Какую модель калибровать")
                            { Choices = ConformalModelChoices },
                        new AlgoParam("n", "Наблюдений", 80, 400, 180, 4, "шт.", "Длина ряда"),
                        new AlgoParam("period", "Сезонный период", 1, 52, 12, 1, "", "1 отключает сезонность"),
                        new AlgoParam("horizon", "Горизонт", 2, 24, 8, 1, "", "Длина прогноза"),
                        new AlgoParam("level", "Уровень покрытия", 0.5, 0.99, 0.9, 0.01, "",
                            "Доля фактов, которая должна попадать в интервал"),
                        new AlgoParam("calibration", "Срезов калибровки", 10, 80, 30, 5, "шт.",
                            "Чем больше, тем точнее покрытие"),
                        new AlgoParam("noise", "Шум", 5, 200, 45, 5, "", "Параметр генератора"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 41, 1, "", "Воспроизводимость"),
                    ]),
            ]),

        new("credit", "Кредитный риск и скоринг",
            "Скоркарты на WoE/IV, резерв по МСФО 9, миграции рейтингов, Мертон и лимиты контрагентов",
            [
                new AlgoDef(
                    Key: "scorecard",
                    Title: "Скоркарта на весах доказательства",
                    Subtitle: "Биннинг с WoE/IV, отбор признаков и перевод в объяснимую шкалу баллов",
                    ApiClass: "AI.Economics.Credit.Scorecard",
                    TheoryFile: "scorecard.md",
                    Params:
                    [
                        new AlgoParam("n", "Заявок в выборке", 300, 20_000, 3000, 100, "шт.",
                            "От объёма зависит устойчивость биннинга"),
                        new AlgoParam("signal", "Сила связи с дефолтом", 0.2, 2.5, 1.0, 0.1, "×",
                            "Множитель коэффициентов генерирующей модели"),
                        new AlgoParam("bad_rate", "Целевая доля дефолтов", 0.03, 0.4, 0.15, 0.01, "доля",
                            "Сдвигает свободный член генератора"),
                        new AlgoParam("max_bins", "Максимум интервалов", 3, 10, 6, 1, "шт.",
                            "Больше интервалов — выше IV и выше риск переобучения"),
                        new AlgoParam("min_share", "Минимальная доля интервала", 0.02, 0.2, 0.05, 0.01, "доля",
                            "Не даёт биннингу выделять редкие группы"),
                        new AlgoParam("pdo", "Баллов на удвоение шансов", 10, 60, 20, 5, "баллов",
                            "Масштаб шкалы: классическое значение 20"),
                        new AlgoParam("base_score", "Базовый балл", 300, 900, 600, 10, "баллов",
                            "Балл, которому соответствуют базовые шансы"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 7, 1, "", "Воспроизводимость выборки"),
                    ]),

                new AlgoDef(
                    Key: "score_monitoring",
                    Title: "Мониторинг модели: Джини, KS и PSI",
                    Subtitle: "Разделяющая способность, калибровка и индекс стабильности популяции",
                    ApiClass: "AI.Economics.Credit.ScoreMetrics",
                    TheoryFile: "score_monitoring.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений в выборке", 300, 20_000, 3000, 100, "шт.",
                            "Размер контрольной выборки"),
                        new AlgoParam("signal", "Сила модели", 0.2, 3.0, 1.2, 0.1, "×",
                            "Чем больше, тем выше коэффициент Джини"),
                        new AlgoParam("bad_rate", "Доля дефолтов", 0.02, 0.4, 0.12, 0.01, "доля",
                            "Базовая частота события"),
                        new AlgoParam("shift", "Сдвиг популяции", -1.5, 1.5, -0.6, 0.1, "σ",
                            "На сколько стандартных отклонений сместилась новая выборка"),
                        new AlgoParam("spread", "Изменение разброса", 0.5, 2.0, 1.0, 0.05, "×",
                            "Растяжение распределения баллов в новой выборке"),
                        new AlgoParam("bias", "Смещение калибровки", 0.5, 2.0, 1.0, 0.05, "×",
                            "Множитель прогнозной вероятности: проверка наклона калибровки"),
                        new AlgoParam("bins", "Интервалов PSI", 5, 20, 10, 1, "шт.",
                            "Десять — отраслевой стандарт"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 13, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "ifrs9",
                    Title: "Резерв по МСФО 9",
                    Subtitle: "12 месяцев против всего срока, стадии и взвешивание макросценариев",
                    ApiClass: "AI.Economics.Credit.Ifrs9",
                    TheoryFile: "ifrs9.md",
                    Params:
                    [
                        new AlgoParam("n", "Договоров в портфеле", 20, 2000, 300, 10, "шт.",
                            "Синтетический кредитный портфель"),
                        new AlgoParam("pd", "Средняя вероятность дефолта", 0.005, 0.25, 0.04, 0.005, "в год",
                            "Годовая вероятность на отчётную дату"),
                        new AlgoParam("lgd", "Потери при дефолте", 0.1, 0.9, 0.45, 0.05, "доля",
                            "Доля экспозиции, теряемая при дефолте"),
                        new AlgoParam("eir", "Эффективная ставка", 0, 0.4, 0.16, 0.01, "в год",
                            "Ставка дисконтирования будущих убытков"),
                        new AlgoParam("months", "Срок до погашения", 6, 120, 36, 6, "мес.",
                            "Чем длиннее срок, тем больше разрыв между 12 месяцами и всем сроком"),
                        new AlgoParam("sicr", "Доля со значимым ростом риска", 0, 0.6, 0.2, 0.05, "доля",
                            "Договоры, у которых вероятность дефолта выросла с момента выдачи"),
                        new AlgoParam("impaired", "Доля обесцененных", 0, 0.2, 0.05, 0.01, "доля",
                            "Договоры с просрочкой свыше 90 дней"),
                        new AlgoParam("stress_pd", "Множитель PD в стрессе", 1.0, 4.0, 1.8, 0.1, "×",
                            "Насколько хуже вероятность дефолта в стрессовом сценарии"),
                        new AlgoParam("stress_p", "Вероятность стресса", 0.05, 0.6, 0.25, 0.05, "доля",
                            "Вес стрессового сценария в взвешивании"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 5, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "migration_matrix",
                    Title: "Матрица миграции рейтингов",
                    Subtitle: "Оценка переходов, кумулятивная вероятность дефолта и стационарное распределение",
                    ApiClass: "AI.Economics.Credit.MigrationMatrix",
                    TheoryFile: "migration_matrix.md",
                    Params:
                    [
                        new AlgoParam("observations", "Наблюдений переходов", 500, 100_000, 8000, 500, "шт.",
                            "Чем меньше, тем шумнее оценка редких переходов"),
                        new AlgoParam("grades", "Рейтинговых классов", 3, 8, 5, 1, "шт.",
                            "Последний класс — дефолт"),
                        new AlgoParam("stability", "Устойчивость рейтинга", 0.5, 0.98, 0.85, 0.01, "доля",
                            "Вероятность сохранить рейтинг за период"),
                        new AlgoParam("downgrade", "Перевес понижений", 0.5, 5.0, 2.0, 0.1, "×",
                            "Во сколько раз понижения вероятнее повышений"),
                        new AlgoParam("horizon", "Горизонт кумулятивной PD", 2, 20, 10, 1, "периодов",
                            "На сколько периодов возводится матрица"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 11, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "roll_rate",
                    Title: "Перетекание просрочки",
                    Subtitle: "Ставки перехода между корзинами и ожидаемые потери прямо из оборотов",
                    ApiClass: "AI.Economics.Credit.RollRate",
                    TheoryFile: "roll_rate.md",
                    Params:
                    [
                        new AlgoParam("periods", "Периодов наблюдения", 3, 36, 12, 1, "мес.",
                            "Длина истории остатков"),
                        new AlgoParam("portfolio", "Текущий портфель", 10_000_000, 10_000_000_000, 1_000_000_000,
                            10_000_000, "руб.", "Остаток текущей задолженности"),
                        new AlgoParam("entry", "Вход в просрочку", 0.005, 0.15, 0.04, 0.005, "доля",
                            "Какая часть текущей задолженности уходит в первую корзину"),
                        new AlgoParam("roll", "Ставка перетекания", 0.2, 0.95, 0.55, 0.05, "доля",
                            "Средняя доля остатка, переходящая в следующую корзину"),
                        new AlgoParam("trend", "Ухудшение сбора", -0.03, 0.05, 0.01, 0.005, "за период",
                            "Дрейф ставок перетекания во времени"),
                        new AlgoParam("noise", "Шум остатков", 0, 0.2, 0.05, 0.01, "доля",
                            "Случайные колебания корзин"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 3, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "vintage",
                    Title: "Винтажный анализ портфеля",
                    Subtitle: "Кривая созревания, сравнение когорт на одном возрасте и тренд качества выдач",
                    ApiClass: "AI.Economics.Credit.VintageAnalysis",
                    TheoryFile: "vintage.md",
                    Params:
                    [
                        new AlgoParam("vintages", "Винтажей", 3, 24, 8, 1, "шт.", "Когорт выдач"),
                        new AlgoParam("max_age", "Возраст старшего винтажа", 6, 48, 24, 1, "мес.",
                            "Каждый следующий винтаж моложе на один месяц"),
                        new AlgoParam("terminal", "Итоговые потери", 0.01, 0.25, 0.06, 0.005, "доля",
                            "Уровень потерь зрелого винтажа"),
                        new AlgoParam("drift", "Дрейф качества выдач", -0.01, 0.02, 0.004, 0.001, "за винтаж",
                            "Положительный — политика выдач ослабевает"),
                        new AlgoParam("seasoning", "Скорость созревания", 3, 20, 8, 1, "мес.",
                            "Постоянная времени кривой созревания"),
                        new AlgoParam("noise", "Шум винтажей", 0, 0.3, 0.08, 0.01, "доля",
                            "Разброс кривых потерь между когортами"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 23, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "merton",
                    Title: "Мертон и KMV: PD из цены акции",
                    Subtitle: "Стоимость активов из капитализации, расстояние до дефолта и кредитный спред",
                    ApiClass: "AI.Economics.Credit.MertonModel",
                    TheoryFile: "merton.md",
                    Params:
                    [
                        new AlgoParam("equity", "Капитализация", 100_000_000, 500_000_000_000, 5_000_000_000,
                            100_000_000, "руб.", "Рыночная стоимость собственного капитала"),
                        new AlgoParam("vol", "Волатильность акции", 0.1, 1.2, 0.35, 0.05, "в год",
                            "Годовая волатильность доходности"),
                        new AlgoParam("short_debt", "Краткосрочный долг", 0, 200_000_000_000, 1_200_000_000,
                            100_000_000, "руб.", "Погашение в пределах года"),
                        new AlgoParam("long_debt", "Долгосрочный долг", 0, 400_000_000_000, 2_800_000_000,
                            100_000_000, "руб.", "В точку дефолта входит наполовину"),
                        new AlgoParam("rate", "Безрисковая ставка", 0, 0.3, 0.07, 0.01, "в год",
                            "Ставка дисконтирования долга"),
                        new AlgoParam("drift", "Ожидаемая доходность активов", 0, 0.4, 0.09, 0.01, "в год",
                            "Используется для реальной, а не риск-нейтральной вероятности"),
                        new AlgoParam("horizon", "Горизонт", 0.25, 5, 1, 0.25, "лет",
                            "Срок, на котором оценивается дефолт"),
                    ]),

                new AlgoDef(
                    Key: "counterparty",
                    Title: "Скоринг контрагента и лимит",
                    Subtitle: "Балл, класс, вероятность дефолта, лимит отгрузки и ставка факторинга",
                    ApiClass: "AI.Economics.Credit.CounterpartyScoring",
                    TheoryFile: "counterparty.md",
                    Params:
                    [
                        new AlgoParam("revenue", "Годовая выручка", 10_000_000, 100_000_000_000, 1_200_000_000,
                            10_000_000, "руб.", "Масштаб бизнеса контрагента"),
                        new AlgoParam("margin", "Рентабельность", -0.1, 0.4, 0.12, 0.01, "доля",
                            "Прибыль до амортизации к выручке"),
                        new AlgoParam("equity_share", "Доля собственного капитала", 0.02, 0.9, 0.6, 0.05, "доля",
                            "Капитал к сумме капитала и долга"),
                        new AlgoParam("current_ratio", "Текущая ликвидность", 0.4, 3.5, 2.0, 0.1, "",
                            "Оборотные активы к краткосрочным обязательствам"),
                        new AlgoParam("delay", "Средняя просрочка оплат", 0, 90, 5, 1, "дн.",
                            "Свыше 60 дней срабатывает стоп-фактор"),
                        new AlgoParam("years", "Срок работы", 0.5, 25, 8, 0.5, "лет", "Возраст компании"),
                        new AlgoParam("concentration", "Концентрация покупателей", 0.1, 0.95, 0.3, 0.05, "доля",
                            "Доля крупнейшего покупателя в выручке"),
                        new AlgoParam("limit", "Запрошенный лимит", 1_000_000, 2_000_000_000, 40_000_000,
                            1_000_000, "руб.", "Сумма отгрузки без предоплаты"),
                        new AlgoParam("tax", "Налоговая задолженность", 0, 1, 0, 1, "",
                            "Стоп-фактор кредитной политики")
                            { Choices = YesNoChoices },
                    ]),
            ]),

        new("statements", "Финанализ и форензика",
            "Коэффициенты и Дюпон, модели банкротства, M-score Бениша, закон Бенфорда и качество прибыли",
            [
                new AlgoDef(
                    Key: "financial_ratios",
                    Title: "Коэффициентный анализ отчётности",
                    Subtitle: "Ликвидность, рентабельность, оборачиваемость, долг и денежный поток с ориентирами",
                    ApiClass: "AI.Economics.Statements.FinancialRatios",
                    TheoryFile: "financial_ratios.md",
                    Params: StatementParams()),

                new AlgoDef(
                    Key: "dupont",
                    Title: "Разложение Дюпона",
                    Subtitle: "Три и пять факторов, вклад каждого множителя в изменение рентабельности капитала",
                    ApiClass: "AI.Economics.Statements.DuPontAnalysis",
                    TheoryFile: "dupont.md",
                    Params: StatementParams()),

                new AlgoDef(
                    Key: "distress_scores",
                    Title: "Модели банкротства",
                    Subtitle: "Альтман Z и Z'', Ольсон, Спрингейт, Таффлер и F-счёт Пиотроски",
                    ApiClass: "AI.Economics.Statements.DistressScores",
                    TheoryFile: "distress_scores.md",
                    Params: StatementParams()),

                new AlgoDef(
                    Key: "beneish",
                    Title: "M-score Бениша",
                    Subtitle: "Восемь индексов: кто похож на компанию, приукрашивающую отчётность",
                    ApiClass: "AI.Economics.Statements.BeneishModel",
                    TheoryFile: "beneish.md",
                    Params: StatementParams()),

                new AlgoDef(
                    Key: "working_capital",
                    Title: "Оборотный капитал и финансовый цикл",
                    Subtitle: "Драйверы цикла, цена одного дня и потенциал высвобождения денег",
                    ApiClass: "AI.Economics.Statements.WorkingCapitalAnalysis",
                    TheoryFile: "working_capital.md",
                    Params:
                    [
                        .. StatementParams(),
                        new AlgoParam("target_dso", "Целевой сбор дебиторки", 10, 90, 40, 5, "дн.",
                            "Ориентир для расчёта высвобождения"),
                        new AlgoParam("target_dio", "Целевой оборот запасов", 10, 120, 45, 5, "дн.",
                            "Ориентир для расчёта высвобождения"),
                        new AlgoParam("funding", "Стоимость финансирования", 0.05, 0.4, 0.18, 0.01, "в год",
                            "По ней считается экономия на процентах"),
                    ]),

                new AlgoDef(
                    Key: "earnings_quality",
                    Title: "Качество прибыли",
                    Subtitle: "Начисления по Слоуну, подтверждение прибыли деньгами и опережение оборотных статей",
                    ApiClass: "AI.Economics.Statements.EarningsQuality",
                    TheoryFile: "earnings_quality.md",
                    Params: StatementParams()),

                new AlgoDef(
                    Key: "benford",
                    Title: "Закон Бенфорда для транзакций",
                    Subtitle: "Первая цифра и первые две: где искать дробление платежей и придуманные суммы",
                    ApiClass: "AI.Economics.Statements.BenfordAnalysis",
                    TheoryFile: "benford.md",
                    Params:
                    [
                        new AlgoParam("scope", "Разрез", 0, 1, 0, 1, "", "Первая цифра или первые две")
                            { Choices = BenfordScopeChoices },
                        new AlgoParam("pattern", "Природа сумм", 0, 2, 0, 1, "",
                            "Что именно проверяем")
                            { Choices = BenfordPatternChoices },
                        new AlgoParam("n", "Число платежей", 200, 50_000, 4000, 100, "шт.",
                            "Меньше 300 — тест теряет мощность"),
                        new AlgoParam("spread", "Разброс сумм", 0.5, 4.0, 2.5, 0.1, "порядков",
                            "Закон выполняется, когда суммы охватывают несколько порядков"),
                        new AlgoParam("contamination", "Доля подобранных сумм", 0, 1, 0.25, 0.05, "доля",
                            "Какая часть платежей придумана вручную"),
                        new AlgoParam("threshold", "Порог согласования", 50_000, 5_000_000, 500_000, 50_000, "руб.",
                            "Возле него концентрируются дробящиеся платежи"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 31, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "bankruptcy_ml",
                    Title: "Предсказание банкротства на моделях фреймворка",
                    Subtitle: "Скользящий контроль, перестановочная важность и сравнение с баллом Альтмана",
                    ApiClass: "AI.Economics.Statements.BankruptcyPredictor",
                    TheoryFile: "bankruptcy_ml.md",
                    Params:
                    [
                        new AlgoParam("model", "Модель", 0, 3, 3, 1, "",
                            "Классификатор AI.ML или сравнение всех")
                            { Choices = BankruptcyModelChoices },
                        new AlgoParam("n", "Компаний в выборке", 60, 3000, 400, 20, "шт.",
                            "Банкротство — редкое событие, объём критичен"),
                        new AlgoParam("rate", "Доля банкротств", 0.05, 0.5, 0.25, 0.05, "доля",
                            "Смещает порог качества генерирующей модели"),
                        new AlgoParam("signal", "Разделимость классов", 2, 15, 6, 0.5, "×",
                            "Насколько отчётность банкротов отличается от здоровых"),
                        new AlgoParam("folds", "Блоков контроля", 2, 10, 5, 1, "шт.",
                            "Стратифицированный скользящий контроль"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 21, 1, "", "Воспроизводимость"),
                    ]),
            ]),

        new("corporate", "Оценка бизнеса и корпфинансы",
            "WACC и CAPM с премиями, дисконтированные потоки со сценариями, мультипликаторы, LBO, EVA и реальные опционы",
            [
                new AlgoDef(
                    Key: "wacc",
                    Title: "Стоимость капитала",
                    Subtitle: "CAPM с премиями за страну и размер, кривая WACC по доле долга",
                    ApiClass: "AI.Economics.Corporate.CostOfCapital",
                    TheoryFile: "wacc.md",
                    Params:
                    [
                        new AlgoParam("rf", "Безрисковая ставка", 0, 0.25, 0.08, 0.005, "в год",
                            "Доходность длинных государственных облигаций"),
                        new AlgoParam("erp", "Премия за рыночный риск", 0.02, 0.15, 0.06, 0.005, "",
                            "Историческая премия акций над облигациями"),
                        new AlgoParam("beta", "Отраслевая бета без долга", 0.2, 2.5, 1.0, 0.05, "",
                            "Чувствительность отрасли к рынку"),
                        new AlgoParam("crp", "Страновая премия", 0, 0.1, 0.02, 0.005, "",
                            "Экспертная величина, часто определяющая результат оценки"),
                        new AlgoParam("size", "Премия за размер", 0, 0.08, 0.015, 0.005, "",
                            "Надбавка для небольших компаний"),
                        new AlgoParam("debt_share", "Доля долга", 0, 0.85, 0.3, 0.05, "доля",
                            "Целевая структура капитала по рыночной стоимости"),
                        new AlgoParam("kd", "Стоимость долга", 0.03, 0.4, 0.13, 0.01, "в год",
                            "Ставка привлечения до налога"),
                        new AlgoParam("tax", "Ставка налога", 0, 0.5, 0.2, 0.01, "доля",
                            "Определяет величину налогового щита"),
                        new AlgoParam("distress", "Издержки затруднений", 0, 0.6, 0.2, 0.05, "",
                            "Безвозвратные потери при высокой нагрузке: без них оптимума нет"),
                    ]),

                new AlgoDef(
                    Key: "dcf",
                    Title: "Дисконтированные денежные потоки",
                    Subtitle: "FCFF, продлённая стоимость двумя способами, поправка на середину года и торнадо",
                    ApiClass: "AI.Economics.Corporate.DiscountedCashFlow",
                    TheoryFile: "dcf.md",
                    Params: DcfParams()),

                new AlgoDef(
                    Key: "dcf_monte_carlo",
                    Title: "Монте-Карло вокруг оценки",
                    Subtitle: "Распределение стоимости вместо точечной цифры",
                    ApiClass: "AI.Economics.Corporate.DiscountedCashFlow",
                    TheoryFile: "dcf_monte_carlo.md",
                    Params:
                    [
                        .. DcfParams(),
                        new AlgoParam("rev_vol", "Разброс выручки", 0.02, 0.4, 0.12, 0.02, "в год",
                            "Накопленное отклонение прогноза от факта"),
                        new AlgoParam("margin_vol", "Разброс рентабельности", 0.005, 0.1, 0.02, 0.005, "п.п.",
                            "Неопределённость операционной маржи"),
                        new AlgoParam("rate_vol", "Разброс ставки", 0.002, 0.05, 0.015, 0.002, "п.п.",
                            "Неопределённость стоимости капитала"),
                        new AlgoParam("sims", "Симуляций", 500, 20000, 4000, 500, "шт.",
                            "Больше — уже доверительный интервал оценки"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 42, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "comparables",
                    Title: "Оценка по мультипликаторам",
                    Subtitle: "Отбор аналогов по драйверам стоимости и регрессия мультипликатора",
                    ApiClass: "AI.Economics.Corporate.Comparables",
                    TheoryFile: "comparables.md",
                    Params:
                    [
                        new AlgoParam("peers", "Аналогов в пуле", 6, 60, 20, 1, "шт.",
                            "Из них отбираются ближайшие"),
                        new AlgoParam("selected", "Взять аналогов", 3, 30, 8, 1, "шт.",
                            "Размер итоговой группы сравнения"),
                        new AlgoParam("revenue", "Выручка компании", 100_000_000, 100_000_000_000,
                            1_000_000_000, 100_000_000, "руб.", "Масштаб оцениваемой компании"),
                        new AlgoParam("margin", "Рентабельность", 0.05, 0.5, 0.2, 0.01, "доля",
                            "Прибыль до амортизации к выручке"),
                        new AlgoParam("growth", "Темп роста", -0.1, 0.6, 0.15, 0.01, "в год",
                            "Ключевой драйвер мультипликатора"),
                        new AlgoParam("dispersion", "Разброс мультипликаторов", 0.05, 1.0, 0.35, 0.05, "",
                            "Насколько аналоги отличаются друг от друга"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 11, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "lbo",
                    Title: "Выкуп за счёт долга",
                    Subtitle: "График долга, ковенанты и разложение доходности на три источника",
                    ApiClass: "AI.Economics.Corporate.LeveragedBuyout",
                    TheoryFile: "lbo.md",
                    Params:
                    [
                        new AlgoParam("ebitda", "Прибыль на входе", 10_000_000, 10_000_000_000,
                            1_000_000_000, 10_000_000, "руб.", "Прибыль до амортизации в год сделки"),
                        new AlgoParam("entry", "Мультипликатор входа", 3, 15, 7, 0.5, "×",
                            "Цена покупки к прибыли"),
                        new AlgoParam("exit", "Мультипликатор выхода", 3, 15, 7, 0.5, "×",
                            "Равенство входу отделяет работу с активом от ставки на рынок"),
                        new AlgoParam("years", "Срок владения", 3, 10, 5, 1, "лет", "Горизонт сделки"),
                        new AlgoParam("growth", "Рост прибыли", -0.05, 0.3, 0.08, 0.01, "в год",
                            "Операционный рост за период владения"),
                        new AlgoParam("senior", "Старший долг", 0, 8, 3.0, 0.25, "× прибыли",
                            "Основной транш с амортизацией"),
                        new AlgoParam("mezz", "Мезонин", 0, 4, 1.0, 0.25, "× прибыли",
                            "Дорогой транш без амортизации"),
                        new AlgoParam("senior_rate", "Ставка старшего долга", 0.05, 0.3, 0.13, 0.01, "в год",
                            "Стоимость основного транша"),
                        new AlgoParam("drag", "Отвлечение прибыли", 0.05, 0.6, 0.25, 0.05, "доля",
                            "Капзатраты и оборотный капитал в долях прибыли"),
                        new AlgoParam("max_lev", "Ковенант по нагрузке", 2, 8, 5.0, 0.25, "×",
                            "Предельный долг к прибыли"),
                    ]),

                new AlgoDef(
                    Key: "eva",
                    Title: "Экономическая добавленная стоимость",
                    Subtitle: "ROIC против WACC по подразделениям и потенциал перераспределения капитала",
                    ApiClass: "AI.Economics.Corporate.EconomicValueAdded",
                    TheoryFile: "eva.md",
                    Params:
                    [
                        new AlgoParam("units", "Подразделений", 2, 10, 4, 1, "шт.",
                            "Направления бизнеса компании"),
                        new AlgoParam("capital", "Инвестированный капитал", 100_000_000, 100_000_000_000,
                            5_000_000_000, 100_000_000, "руб.", "Суммарно по компании"),
                        new AlgoParam("roic", "Средний ROIC", -0.05, 0.5, 0.16, 0.01, "в год",
                            "Рентабельность инвестированного капитала"),
                        new AlgoParam("spread_gap", "Разброс между подразделениями", 0, 0.3, 0.12, 0.01, "",
                            "Разница рентабельности лучшего и худшего направления"),
                        new AlgoParam("wacc", "Стоимость капитала", 0.05, 0.35, 0.15, 0.01, "в год",
                            "Порог, ниже которого стоимость разрушается"),
                        new AlgoParam("tax", "Ставка налога", 0, 0.5, 0.2, 0.01, "доля", "Налог на прибыль"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 5, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "real_options_lsm",
                    Title: "Реальные опционы: Лонгстафф — Шварц",
                    Subtitle: "Опцион на отсрочку, расширение или отказ с границей исполнения",
                    ApiClass: "AI.Economics.Corporate.LongstaffSchwartz",
                    TheoryFile: "real_options_lsm.md",
                    Params:
                    [
                        new AlgoParam("option", "Тип опциона", 0, 2, 0, 1, "",
                            "Что именно можно сделать позже")
                            { Choices = ProjectOptionChoices },
                        new AlgoParam("value", "Стоимость проекта", 10_000_000, 10_000_000_000,
                            1_000_000_000, 10_000_000, "руб.", "Приведённая стоимость денежных потоков"),
                        new AlgoParam("cost", "Инвестиции", 10_000_000, 10_000_000_000,
                            1_050_000_000, 10_000_000, "руб.", "Затраты на реализацию опциона"),
                        new AlgoParam("salvage", "Ликвидационная стоимость", 0, 5_000_000_000,
                            600_000_000, 10_000_000, "руб.", "Что можно выручить при отказе"),
                        new AlgoParam("expansion", "Коэффициент расширения", 0.1, 2.0, 0.5, 0.1, "×",
                            "Прирост масштаба при исполнении опциона на расширение"),
                        new AlgoParam("horizon", "Срок опциона", 0.5, 10, 3, 0.5, "лет",
                            "Сколько есть времени на решение"),
                        new AlgoParam("vol", "Волатильность проекта", 0.1, 1.2, 0.4, 0.05, "в год",
                            "Главный источник стоимости гибкости"),
                        new AlgoParam("rate", "Безрисковая ставка", 0, 0.3, 0.08, 0.01, "в год",
                            "Ставка дисконтирования"),
                        new AlgoParam("paths", "Траекторий", 1000, 30000, 8000, 1000, "шт.",
                            "Точность метода Монте-Карло"),
                        new AlgoParam("steps", "Моментов решения", 4, 40, 12, 1, "шт.",
                            "Когда можно принимать решение"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 7, 1, "", "Воспроизводимость"),
                    ]),
            ]),

        new("projects", "Проектный анализ и кредит",
            "NPV и IRR, амортизация и налоговый щит, графики погашения, лизинг против кредита, рычаги и структура капитала",
            [
                new AlgoDef(
                    Key: "investment_criteria",
                    Title: "Критерии оценки проекта",
                    Subtitle: "NPV, IRR, MIRR, индекс прибыльности и оба срока окупаемости",
                    ApiClass: "AI.Economics.Projects.InvestmentCriteria",
                    TheoryFile: "investment_criteria.md",
                    Params:
                    [
                        new AlgoParam("investment", "Первоначальные вложения", 1_000_000, 5_000_000_000,
                            100_000_000, 1_000_000, "руб.", "Отток в нулевом периоде"),
                        new AlgoParam("years", "Срок проекта", 2, 20, 6, 1, "лет", "Горизонт потока"),
                        new AlgoParam("inflow", "Поток первого года", 1_000_000, 2_000_000_000,
                            28_000_000, 1_000_000, "руб.", "Приток после первого года"),
                        new AlgoParam("growth", "Рост потока", -0.2, 0.4, 0.05, 0.01, "в год",
                            "Динамика поступлений"),
                        new AlgoParam("rate", "Ставка дисконтирования", 0.01, 0.4, 0.14, 0.01, "в год",
                            "Стоимость капитала проекта"),
                        new AlgoParam("reinvest", "Ставка реинвестирования", 0.0, 0.4, 0.08, 0.01, "в год",
                            "Куда вкладываются промежуточные поступления"),
                        new AlgoParam("overhaul", "Капремонт в середине срока", 0, 500_000_000, 0,
                            5_000_000, "руб.", "Отток посреди проекта: даёт вторую смену знака"),
                    ]),

                new AlgoDef(
                    Key: "depreciation",
                    Title: "Амортизация и налоговый щит",
                    Subtitle: "Линейный, уменьшаемого остатка, по сумме чисел лет и нелинейный налоговый",
                    ApiClass: "AI.Economics.Projects.Depreciation",
                    TheoryFile: "depreciation.md",
                    Params:
                    [
                        new AlgoParam("method", "Метод", 0, 3, 2, 1, "", "Способ начисления")
                            { Choices = DepreciationChoices },
                        new AlgoParam("cost", "Стоимость актива", 1_000_000, 5_000_000_000,
                            100_000_000, 1_000_000, "руб.", "Первоначальная стоимость"),
                        new AlgoParam("life", "Срок использования", 2, 20, 5, 1, "лет",
                            "Период списания"),
                        new AlgoParam("salvage", "Ликвидационная стоимость", 0, 0.5, 0.0, 0.05, "доля",
                            "Остаточная стоимость в конце срока"),
                        new AlgoParam("tax", "Ставка налога", 0, 0.5, 0.2, 0.01, "доля",
                            "Определяет величину щита"),
                        new AlgoParam("rate", "Ставка дисконтирования", 0.01, 0.4, 0.14, 0.01, "в год",
                            "По ней приводится налоговая экономия"),
                        new AlgoParam("factor", "Коэффициент ускорения", 1.0, 3.0, 2.0, 0.25, "×",
                            "Для метода уменьшаемого остатка"),
                    ]),

                new AlgoDef(
                    Key: "loan_schedule",
                    Title: "График погашения кредита",
                    Subtitle: "Аннуитет против дифференцированного, полная стоимость и досрочные погашения",
                    ApiClass: "AI.Economics.Projects.LoanSchedule",
                    TheoryFile: "loan_schedule.md",
                    Params:
                    [
                        new AlgoParam("type", "Тип графика", 0, 2, 0, 1, "", "Как гасится тело")
                            { Choices = RepaymentChoices },
                        new AlgoParam("principal", "Сумма кредита", 100_000, 500_000_000, 5_000_000,
                            100_000, "руб.", "Тело долга"),
                        new AlgoParam("rate", "Ставка", 0.03, 0.6, 0.18, 0.005, "в год",
                            "Номинальная годовая ставка"),
                        new AlgoParam("months", "Срок", 6, 360, 60, 6, "мес.", "Число платежей"),
                        new AlgoParam("fee", "Комиссия при выдаче", 0, 0.1, 0.01, 0.005, "доля",
                            "Входит в полную стоимость кредита"),
                        new AlgoParam("prepay_month", "Месяц досрочного погашения", 0, 120, 12, 1, "",
                            "Ноль отключает досрочное погашение"),
                        new AlgoParam("prepay_amount", "Сумма досрочного погашения", 0, 100_000_000,
                            500_000, 50_000, "руб.", "Разовый взнос сверх графика"),
                        new AlgoParam("prepay_mode", "Что уменьшать", 0, 1, 0, 1, "",
                            "Срок выгоднее платежа при той же сумме взноса")
                            { Choices = PrepaymentChoices },
                    ]),

                new AlgoDef(
                    Key: "lease_vs_buy",
                    Title: "Лизинг, кредит или покупка",
                    Subtitle: "Приведённые затраты после налога и порог удорожания лизинга",
                    ApiClass: "AI.Economics.Projects.LeaseVsBuy",
                    TheoryFile: "lease_vs_buy.md",
                    Params:
                    [
                        new AlgoParam("cost", "Стоимость актива", 1_000_000, 1_000_000_000, 10_000_000,
                            500_000, "руб.", "Цена приобретения"),
                        new AlgoParam("years", "Срок использования", 2, 15, 5, 1, "лет", "Горизонт сравнения"),
                        new AlgoParam("tax", "Ставка налога", 0, 0.5, 0.2, 0.01, "доля",
                            "Без прибыли налоговый щит не работает"),
                        new AlgoParam("rate", "Ставка дисконтирования", 0.02, 0.4, 0.12, 0.01, "в год",
                            "Стоимость денег для компании"),
                        new AlgoParam("credit_rate", "Ставка кредита", 0.05, 0.4, 0.16, 0.01, "в год",
                            "Стоимость заёмных средств"),
                        new AlgoParam("down", "Первоначальный взнос", 0, 0.6, 0.2, 0.05, "доля",
                            "Собственные средства в кредитной схеме"),
                        new AlgoParam("markup", "Удорожание лизинга", 0.02, 0.3, 0.09, 0.01, "в год",
                            "Так лизинговые компании и называют свою цену"),
                        new AlgoParam("advance", "Аванс по лизингу", 0, 0.5, 0.2, 0.05, "доля",
                            "Первый платёж"),
                        new AlgoParam("residual", "Остаточная стоимость", 0, 0.6, 0.15, 0.05, "доля",
                            "Что стоит актив в конце срока"),
                    ]),

                new AlgoDef(
                    Key: "break_even",
                    Title: "Безубыточность и рычаги",
                    Subtitle: "Точка безубыточности, запас прочности, операционный и финансовый рычаг",
                    ApiClass: "AI.Economics.Projects.BreakEven",
                    TheoryFile: "break_even.md",
                    Params:
                    [
                        new AlgoParam("price", "Цена единицы", 10, 1_000_000, 1000, 10, "руб.",
                            "Цена продажи"),
                        new AlgoParam("variable", "Переменные затраты", 1, 900_000, 600, 10, "руб.",
                            "На единицу продукции"),
                        new AlgoParam("fixed", "Постоянные затраты", 100_000, 5_000_000_000, 2_000_000,
                            100_000, "руб.", "За период"),
                        new AlgoParam("volume", "Текущий объём", 100, 1_000_000, 8000, 100, "ед.",
                            "Фактические продажи"),
                        new AlgoParam("interest", "Процентные расходы", 0, 1_000_000_000, 300_000,
                            10_000, "руб.", "Обслуживание долга за период"),
                        new AlgoParam("target", "Целевая прибыль", 0, 2_000_000_000, 1_000_000,
                            100_000, "руб.", "Для расчёта необходимого объёма"),
                        new AlgoParam("tax", "Ставка налога", 0, 0.5, 0.2, 0.01, "доля", "Налог на прибыль"),
                    ]),

                new AlgoDef(
                    Key: "capital_structure",
                    Title: "Оптимальная структура капитала",
                    Subtitle: "Минимум стоимости капитала между налоговым щитом и издержками затруднений",
                    ApiClass: "AI.Economics.Projects.BreakEven",
                    TheoryFile: "capital_structure.md",
                    Params:
                    [
                        new AlgoParam("ke", "Стоимость капитала без долга", 0.06, 0.4, 0.18, 0.01, "в год",
                            "Требуемая доходность при нулевом долге"),
                        new AlgoParam("kd", "Стоимость долга без нагрузки", 0.03, 0.3, 0.12, 0.01, "в год",
                            "Ставка при минимальном рычаге"),
                        new AlgoParam("tax", "Ставка налога", 0, 0.5, 0.2, 0.01, "доля",
                            "Источник выгоды от долга"),
                        new AlgoParam("ebit", "Операционная прибыль", 10_000_000, 10_000_000_000,
                            500_000_000, 10_000_000, "руб.", "Для расчёта стоимости компании"),
                        new AlgoParam("current", "Текущая доля долга", 0, 0.85, 0.25, 0.05, "доля",
                            "Отправная точка для сравнения"),
                        new AlgoParam("distress", "Издержки затруднений", 0.05, 1.0, 0.35, 0.05, "",
                            "Скорость роста стоимости долга с нагрузкой"),
                    ]),
            ]),

        new("risk", "Риск-менеджмент",
            "VaR и ожидаемые потери, хвосты по теории экстремальных значений, копулы, обратное тестирование и ликвидность",
            [
                new AlgoDef(
                    Key: "value_at_risk",
                    Title: "Стоимость под риском и ожидаемые потери",
                    Subtitle: "Исторический, параметрический, Корниш — Фишер и Монте-Карло",
                    ApiClass: "AI.Economics.Risk.ValueAtRisk",
                    TheoryFile: "value_at_risk.md",
                    Params:
                    [
                        new AlgoParam("method", "Метод", 0, 3, 0, 1, "", "Способ расчёта")
                            { Choices = VarMethodChoices },
                        new AlgoParam("n", "Наблюдений", 250, 5000, 1500, 50, "дн.",
                            "Длина истории доходностей"),
                        new AlgoParam("vol", "Волатильность", 0.002, 0.06, 0.012, 0.001, "в день",
                            "Разброс дневной доходности"),
                        new AlgoParam("jump_prob", "Вероятность скачка", 0, 0.2, 0.05, 0.01, "доля",
                            "Доля дней с аномальным движением"),
                        new AlgoParam("jump_size", "Размер скачка", 1, 10, 4, 0.5, "×",
                            "Во сколько раз шире распределение в такие дни"),
                        new AlgoParam("confidence", "Уровень доверия", 0.9, 0.999, 0.99, 0.005, "",
                            "Квантиль распределения убытков"),
                        new AlgoParam("horizon", "Горизонт", 1, 20, 1, 1, "дн.",
                            "Пересчёт корнем из времени верен не всегда"),
                        new AlgoParam("portfolio", "Стоимость портфеля", 1_000_000, 100_000_000_000,
                            1_000_000_000, 10_000_000, "руб.", "Для перевода в деньги"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 3, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "extreme_value",
                    Title: "Теория экстремальных значений",
                    Subtitle: "Обобщённое распределение Парето для хвоста и квантили за пределами выборки",
                    ApiClass: "AI.Economics.Risk.ExtremeValue",
                    TheoryFile: "extreme_value.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 500, 10000, 3000, 100, "дн.", "Длина истории"),
                        new AlgoParam("tail", "Тяжесть хвоста", 0.05, 0.6, 0.25, 0.05, "",
                            "Показатель степенного закона убытков"),
                        new AlgoParam("scale", "Масштаб убытков", 0.002, 0.1, 0.01, 0.002, "",
                            "Характерная величина потерь"),
                        new AlgoParam("threshold", "Квантиль порога", 0.8, 0.99, 0.95, 0.01, "",
                            "Компромисс между смещением и дисперсией оценки"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 13, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "copulas",
                    Title: "Копулы и хвостовая зависимость",
                    Subtitle: "Гаусс, Стьюдент, Клейтон и Гумбель: что происходит с активами в кризис",
                    ApiClass: "AI.Economics.Risk.Copulas",
                    TheoryFile: "copulas.md",
                    Params:
                    [
                        new AlgoParam("family", "Семейство генератора", 0, 3, 2, 1, "",
                            "Какая зависимость заложена в данные")
                            { Choices = CopulaChoices },
                        new AlgoParam("fit_family", "Подгоняемое семейство", 0, 3, 2, 1, "",
                            "Какое семейство оценивается")
                            { Choices = CopulaChoices },
                        new AlgoParam("n", "Наблюдений", 200, 5000, 1500, 100, "шт.", "Размер выборки"),
                        new AlgoParam("dependence", "Сила зависимости", 0.1, 8, 3, 0.1, "",
                            "Параметр в шкале выбранного семейства"),
                        new AlgoParam("df", "Степени свободы", 2.5, 30, 5, 0.5, "",
                            "Для копулы Стьюдента: меньше — тяжелее хвосты"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 4, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "var_backtest",
                    Title: "Обратное тестирование и стресс",
                    Subtitle: "Тесты Купца и Кристофферсена, светофор надзора, обратный стресс-тест",
                    ApiClass: "AI.Economics.Risk.VarBacktesting",
                    TheoryFile: "var_backtest.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 250, 5000, 1000, 50, "дн.", "Длина проверки"),
                        new AlgoParam("vol", "Волатильность", 0.005, 0.05, 0.02, 0.001, "в день",
                            "Истинный разброс доходности"),
                        new AlgoParam("bias", "Смещение модели", 0.4, 2.0, 1.0, 0.05, "×",
                            "Во сколько раз модель ошибается в оценке риска"),
                        new AlgoParam("clustering", "Кластеризация волатильности", 0, 0.95, 0.0, 0.05, "",
                            "Инерция дисперсии: причина группировки пробоев"),
                        new AlgoParam("confidence", "Уровень доверия", 0.9, 0.999, 0.99, 0.005, "",
                            "Заявленная надёжность модели"),
                        new AlgoParam("reverse", "Целевые потери", 0.05, 0.6, 0.2, 0.05, "доля",
                            "Для обратного стресс-теста"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 5, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "liquidity",
                    Title: "Ликвидность и кассовые разрывы",
                    Subtitle: "Платёжный календарь, вероятность разрыва, модели Баумоля и Миллера — Орра",
                    ApiClass: "AI.Economics.Risk.LiquidityRisk",
                    TheoryFile: "liquidity.md",
                    Params:
                    [
                        new AlgoParam("opening", "Начальный остаток", 0, 1_000_000_000, 50_000_000,
                            1_000_000, "руб.", "Деньги на старте горизонта"),
                        new AlgoParam("periods", "Периодов", 4, 24, 12, 1, "мес.", "Горизонт планирования"),
                        new AlgoParam("inflow", "Средние поступления", 1_000_000, 5_000_000_000,
                            100_000_000, 1_000_000, "руб.", "За период"),
                        new AlgoParam("outflow", "Средние выплаты", 1_000_000, 5_000_000_000,
                            105_000_000, 1_000_000, "руб.", "За период"),
                        new AlgoParam("seasonality", "Сезонность", 0, 0.6, 0.25, 0.05, "доля",
                            "Амплитуда колебаний потоков"),
                        new AlgoParam("volatility", "Разброс поступлений", 0.02, 0.6, 0.15, 0.01, "доля",
                            "Неопределённость оплат покупателей"),
                        new AlgoParam("cost", "Издержки конвертации", 1000, 1_000_000, 50_000, 1000, "руб.",
                            "Стоимость одной операции с деньгами"),
                        new AlgoParam("rate", "Ставка размещения", 0.001, 0.05, 0.01, 0.001, "за период",
                            "Упущенный доход от хранения денег"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 7, 1, "", "Воспроизводимость"),
                    ]),
            ]),

        new("portfolio", "Портфель и инвестиции",
            "Метрики риска и доходности, Марковиц, паритет риска, Блэк — Литтерман, CVaR, факторные модели и перебалансировка",
            [
                new AlgoDef(
                    Key: "portfolio_metrics",
                    Title: "Метрики портфеля",
                    Subtitle: "Шарп, Сортино, Кальмар, Омега, просадки, альфа и информационный коэффициент",
                    ApiClass: "AI.Economics.Portfolio.PortfolioMetrics",
                    TheoryFile: "portfolio_metrics.md",
                    Params:
                    [
                        .. MarketParams(),
                        new AlgoParam("w_bonds", "Доля облигаций", 0, 1, 0.4, 0.05, "доля",
                            "Остаток делится между акциями и сырьём"),
                        new AlgoParam("w_equity", "Доля акций", 0, 1, 0.4, 0.05, "доля",
                            "Вес рискового актива"),
                        new AlgoParam("rf", "Безрисковая ставка", 0, 0.02, 0.004, 0.001, "в месяц",
                            "Для расчёта избыточной доходности"),
                    ]),

                new AlgoDef(
                    Key: "mean_variance",
                    Title: "Марковиц и эффективная граница",
                    Subtitle: "Оптимизация по средней и дисперсии с ограничениями и вкладами в риск",
                    ApiClass: "AI.Economics.Portfolio.MeanVariance",
                    TheoryFile: "mean_variance.md",
                    Params:
                    [
                        .. MarketParams(),
                        new AlgoParam("max_weight", "Максимальный вес актива", 0.2, 1.0, 0.6, 0.05, "доля",
                            "Простейшая защита от концентрации"),
                        new AlgoParam("shrinkage", "Сжатие ковариаций", 0, 0.9, 0.1, 0.05, "доля",
                            "Стабилизирует матрицу на коротких историях"),
                        new AlgoParam("rf", "Безрисковая ставка", 0, 0.15, 0.05, 0.005, "в год",
                            "Для расчёта коэффициента Шарпа"),
                    ]),

                new AlgoDef(
                    Key: "risk_parity",
                    Title: "Паритет риска",
                    Subtitle: "Обратная волатильность, равный вклад в риск и иерархический паритет",
                    ApiClass: "AI.Economics.Portfolio.RiskParity",
                    TheoryFile: "risk_parity.md",
                    Params:
                    [
                        .. MarketParams(),
                        new AlgoParam("method", "Метод", 0, 2, 1, 1, "", "Способ распределения риска")
                            { Choices = RiskParityChoices },
                        new AlgoParam("shrinkage", "Сжатие ковариаций", 0, 0.9, 0.05, 0.05, "доля",
                            "Устойчивость оценки зависимостей"),
                    ]),

                new AlgoDef(
                    Key: "black_litterman",
                    Title: "Блэк — Литтерман",
                    Subtitle: "Равновесие рынка плюс взгляды инвестора вместо исторических доходностей",
                    ApiClass: "AI.Economics.Portfolio.BlackLitterman",
                    TheoryFile: "black_litterman.md",
                    Params:
                    [
                        .. MarketParams(),
                        new AlgoParam("view_excess", "Взгляд: превышение", -0.1, 0.2, 0.03, 0.005, "в год",
                            "На сколько акции обгонят облигации"),
                        new AlgoParam("confidence", "Уверенность во взгляде", 0.05, 0.95, 0.5, 0.05, "доля",
                            "Определяет, насколько портфель отойдёт от рыночного"),
                        new AlgoParam("risk_aversion", "Неприятие риска", 1, 6, 2.5, 0.25, "",
                            "Чем выше, тем ближе к рыночному портфелю"),
                        new AlgoParam("tau", "Вес априорного распределения", 0.01, 0.3, 0.05, 0.01, "",
                            "Неопределённость равновесных доходностей"),
                    ]),

                new AlgoDef(
                    Key: "cvar_portfolio",
                    Title: "Оптимизация по хвостовым потерям",
                    Subtitle: "Минимизация ожидаемых потерь в хвосте вместо дисперсии",
                    ApiClass: "AI.Economics.Portfolio.CvarOptimization",
                    TheoryFile: "cvar_portfolio.md",
                    Params:
                    [
                        .. MarketParams(),
                        new AlgoParam("crash_prob", "Вероятность обвала", 0, 0.15, 0.03, 0.005, "доля",
                            "Как часто у третьего актива случаются глубокие падения"),
                        new AlgoParam("crash_size", "Глубина обвала", 0.02, 0.5, 0.15, 0.01, "доля",
                            "Дисперсия такие события недооценивает"),
                        new AlgoParam("confidence", "Уровень доверия", 0.8, 0.99, 0.95, 0.01, "",
                            "Какая часть худших сценариев учитывается"),
                        new AlgoParam("max_weight", "Максимальный вес", 0.3, 1.0, 0.7, 0.05, "доля",
                            "Ограничение концентрации"),
                    ]),

                new AlgoDef(
                    Key: "factor_model",
                    Title: "Факторные модели и главные компоненты",
                    Subtitle: "Альфа и нагрузки против факторов, статистические факторы через PCA",
                    ApiClass: "AI.Economics.Portfolio.FactorModels",
                    TheoryFile: "factor_model.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 36, 480, 180, 12, "мес.", "Длина истории"),
                        new AlgoParam("market_beta", "Бета к рынку", 0, 2.5, 1.1, 0.05, "",
                            "Чувствительность к рыночному фактору"),
                        new AlgoParam("size_beta", "Нагрузка на размер", -1.0, 1.5, 0.4, 0.05, "",
                            "Смещение в малые компании"),
                        new AlgoParam("alpha", "Истинная альфа", -0.02, 0.02, 0.002, 0.001, "в месяц",
                            "Доходность сверх факторной"),
                        new AlgoParam("noise", "Специфический риск", 0.001, 0.05, 0.008, 0.001, "в месяц",
                            "Разброс, не объяснённый факторами"),
                        new AlgoParam("market_vol", "Волатильность рынка", 0.01, 0.12, 0.04, 0.005, "в месяц",
                            "Разброс рыночного фактора"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 11, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "attribution",
                    Title: "Атрибуция Бринсона",
                    Subtitle: "Разложение активной доходности на распределение и выбор инструментов",
                    ApiClass: "AI.Economics.Portfolio.FactorModels",
                    TheoryFile: "attribution.md",
                    Params:
                    [
                        new AlgoParam("w_equity", "Вес акций в портфеле", 0, 1, 0.5, 0.05, "доля",
                            "Против эталонного веса"),
                        new AlgoParam("b_equity", "Вес акций в эталоне", 0, 1, 0.4, 0.05, "доля",
                            "Базовое распределение"),
                        new AlgoParam("w_bonds", "Вес облигаций в портфеле", 0, 1, 0.3, 0.05, "доля",
                            "Остаток уходит в денежный сегмент"),
                        new AlgoParam("b_bonds", "Вес облигаций в эталоне", 0, 1, 0.4, 0.05, "доля",
                            "Базовое распределение"),
                        new AlgoParam("r_equity", "Доходность акций в портфеле", -0.3, 0.5, 0.1, 0.01, "",
                            "Результат выбора внутри сегмента"),
                        new AlgoParam("rb_equity", "Доходность акций в эталоне", -0.3, 0.5, 0.08, 0.01, "",
                            "Сегментный эталон"),
                        new AlgoParam("r_bonds", "Доходность облигаций в портфеле", -0.2, 0.3, 0.05, 0.01, "",
                            "Результат выбора внутри сегмента"),
                        new AlgoParam("rb_bonds", "Доходность облигаций в эталоне", -0.2, 0.3, 0.06, 0.01, "",
                            "Сегментный эталон"),
                    ]),

                new AlgoDef(
                    Key: "rebalancing",
                    Title: "Перебалансировка с издержками",
                    Subtitle: "Календарное, пороговое и частичное правило против дрейфа весов",
                    ApiClass: "AI.Economics.Portfolio.Rebalancing",
                    TheoryFile: "rebalancing.md",
                    Params:
                    [
                        .. MarketParams(),
                        new AlgoParam("rule", "Правило", 0, 3, 2, 1, "", "Когда совершать сделки")
                            { Choices = RebalancingChoices },
                        new AlgoParam("cost", "Издержки сделки", 0, 0.01, 0.001, 0.0005, "доля оборота",
                            "Комиссия и проскальзывание"),
                        new AlgoParam("tax", "Налог на прибыль", 0, 0.35, 0.13, 0.01, "доля",
                            "Часто важнее комиссии"),
                        new AlgoParam("threshold", "Порог отклонения", 0.01, 0.3, 0.05, 0.01, "доля",
                            "Когда веса считаются ушедшими"),
                        new AlgoParam("interval", "Периодичность", 1, 24, 12, 1, "мес.",
                            "Для календарного правила"),
                    ]),
            ]),

        new("econometrics", "Эконометрика",
            "Регрессии с устойчивыми ошибками, панели, ограниченные отклики, причинность, ряды, GARCH и фильтр Калмана",
            [
                new AlgoDef(
                    Key: "regression_robust",
                    Title: "Регрессия с устойчивыми ошибками",
                    Subtitle: "HC0-HC3, Ньюи — Уэст и кластерные ошибки: оценки те же, выводы разные",
                    ApiClass: "AI.Economics.Econometrics.LinearRegression",
                    TheoryFile: "regression_robust.md",
                    Params:
                    [
                        new AlgoParam("variance", "Способ оценки ошибок", 0, 6, 3, 1, "",
                            "Формула ковариационной матрицы")
                            { Choices = RobustVarianceChoices },
                        new AlgoParam("n", "Наблюдений", 50, 5000, 400, 50, "шт.", "Размер выборки"),
                        new AlgoParam("hetero", "Гетероскедастичность", 0, 3, 1.0, 0.1, "",
                            "Насколько дисперсия ошибки зависит от регрессора"),
                        new AlgoParam("autocorr", "Автокорреляция ошибок", 0, 0.95, 0.0, 0.05, "",
                            "Инерция ошибки во времени"),
                        new AlgoParam("clusters", "Число кластеров", 2, 100, 20, 1, "шт.",
                            "Группы с общим шоком"),
                        new AlgoParam("cluster_shock", "Групповой шок", 0, 3, 0.0, 0.1, "",
                            "Разброс общего для группы фактора"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 1, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "regression_diagnostics",
                    Title: "Диагностика регрессии",
                    Subtitle: "Бройш — Паган, Уайт, Дарбин — Уотсон, RESET, Чоу и факторы раздувания дисперсии",
                    ApiClass: "AI.Economics.Econometrics.Diagnostics",
                    TheoryFile: "regression_diagnostics.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 50, 3000, 400, 50, "шт.", "Размер выборки"),
                        new AlgoParam("hetero", "Гетероскедастичность", 0, 3, 0.8, 0.1, "",
                            "Зависимость дисперсии ошибки от регрессора"),
                        new AlgoParam("collinear", "Коллинеарность", 0, 0.99, 0.5, 0.05, "",
                            "Корреляция между регрессорами"),
                        new AlgoParam("nonlinear", "Нелинейность", 0, 2, 0.0, 0.1, "",
                            "Квадратичный член, пропущенный в модели"),
                        new AlgoParam("break_size", "Структурный сдвиг", 0, 4, 0.0, 0.1, "",
                            "Изменение коэффициента в середине выборки"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 4, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "iv_2sls",
                    Title: "Инструментальные переменные",
                    Subtitle: "Двухшаговый МНК и обобщённый метод моментов против эндогенности цены",
                    ApiClass: "AI.Economics.Econometrics.InstrumentalVariables",
                    TheoryFile: "iv_2sls.md",
                    Params:
                    [
                        new AlgoParam("estimator", "Оценщик", 0, 1, 0, 1, "", "Метод оценивания")
                            { Choices = IvEstimatorChoices },
                        new AlgoParam("n", "Наблюдений", 200, 10000, 2000, 100, "шт.", "Размер выборки"),
                        new AlgoParam("strength", "Сила инструмента", 0.05, 2.0, 0.8, 0.05, "",
                            "Ниже 0,2 инструмент становится слабым"),
                        new AlgoParam("endogeneity", "Сила эндогенности", 0, 3, 1.5, 0.1, "",
                            "Насколько ненаблюдаемый фактор искажает МНК"),
                        new AlgoParam("instruments", "Число инструментов", 1, 5, 2, 1, "шт.",
                            "Больше одного даёт проверку сверхидентификации"),
                        new AlgoParam("invalid", "Нарушение экзогенности", 0, 1.5, 0.0, 0.05, "",
                            "Прямое влияние инструмента на отклик"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 7, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "panel_data",
                    Title: "Панельные данные",
                    Subtitle: "Фиксированные и случайные эффекты, первые разности и тест Хаусмана",
                    ApiClass: "AI.Economics.Econometrics.PanelData",
                    TheoryFile: "panel_data.md",
                    Params:
                    [
                        new AlgoParam("estimator", "Оценщик", 0, 5, 1, 1, "", "Способ учёта эффектов")
                            { Choices = PanelEstimatorChoices },
                        new AlgoParam("units", "Объектов", 10, 500, 60, 5, "шт.", "Число компаний или регионов"),
                        new AlgoParam("periods", "Периодов", 3, 30, 8, 1, "шт.", "Длина панели"),
                        new AlgoParam("correlation", "Связь эффекта с регрессором", 0, 2, 0.8, 0.1, "",
                            "Ноль делает случайные эффекты состоятельными"),
                        new AlgoParam("effect_sd", "Разброс эффектов", 0, 5, 2.0, 0.1, "",
                            "Насколько объекты различаются между собой"),
                        new AlgoParam("noise", "Шум", 0.05, 3, 0.5, 0.05, "", "Идиосинкратическая ошибка"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 8, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "dynamic_panel",
                    Title: "Динамические панели",
                    Subtitle: "Ареллано — Бонд: оценка инерции между двумя смещёнными границами",
                    ApiClass: "AI.Economics.Econometrics.DynamicPanel",
                    TheoryFile: "dynamic_panel.md",
                    Params:
                    [
                        new AlgoParam("units", "Объектов", 20, 500, 100, 10, "шт.", "Число объектов панели"),
                        new AlgoParam("periods", "Периодов", 5, 20, 8, 1, "шт.",
                            "Минимум четыре для разностного метода"),
                        new AlgoParam("persistence", "Истинная инерция", 0, 0.95, 0.5, 0.05, "",
                            "Коэффициент при лаге отклика"),
                        new AlgoParam("effect_sd", "Разброс эффектов", 0, 2, 0.5, 0.05, "",
                            "Именно он и смещает обычные оценки"),
                        new AlgoParam("noise", "Шум", 0.05, 2, 0.3, 0.05, "", "Идиосинкратическая ошибка"),
                        new AlgoParam("max_lags", "Глубина инструментов", 1, 6, 3, 1, "шт.",
                            "Ограничение разрастания набора инструментов"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 10, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "limited_dependent",
                    Title: "Ограниченные зависимые переменные",
                    Subtitle: "Логит, пробит, тобит, Пуассон и отрицательная биномиальная",
                    ApiClass: "AI.Economics.Econometrics.LimitedDependent",
                    TheoryFile: "limited_dependent.md",
                    Params:
                    [
                        new AlgoParam("model", "Модель", 0, 4, 0, 1, "", "Тип отклика")
                            { Choices = LimitedDependentChoices },
                        new AlgoParam("n", "Наблюдений", 200, 10000, 2000, 100, "шт.", "Размер выборки"),
                        new AlgoParam("beta", "Истинный коэффициент", -3, 3, 1.2, 0.1, "",
                            "Сила связи регрессора с откликом"),
                        new AlgoParam("intercept", "Свободный член", -3, 3, 0.3, 0.1, "",
                            "Задаёт базовый уровень отклика"),
                        new AlgoParam("dispersion", "Сверхдисперсия", 0, 2, 0.5, 0.05, "",
                            "Для счётных моделей: превышение дисперсии над средним"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 11, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "quantile_regression",
                    Title: "Квантильная регрессия",
                    Subtitle: "Влияние фактора на разные части распределения, а не только на среднее",
                    ApiClass: "AI.Economics.Econometrics.QuantileRegression",
                    TheoryFile: "quantile_regression.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 100, 3000, 500, 50, "шт.", "Размер выборки"),
                        new AlgoParam("slope", "Средний наклон", -3, 5, 1.5, 0.1, "",
                            "Эффект в центре распределения"),
                        new AlgoParam("heteroskedasticity", "Рост разброса", 0, 3, 1.0, 0.1, "",
                            "Расхождение эффекта по квантилям создаётся им"),
                        new AlgoParam("outliers", "Доля выбросов", 0, 0.2, 0.05, 0.01, "доля",
                            "Медианная регрессия к ним устойчива"),
                        new AlgoParam("bootstrap", "Повторов бутстрапа", 20, 500, 80, 10, "шт.",
                            "Точность стандартных ошибок"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 14, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "causal_did",
                    Title: "Разность разностей",
                    Subtitle: "Динамика эффекта, проверка параллельных трендов и поправка на разновременное внедрение",
                    ApiClass: "AI.Economics.Econometrics.DifferenceInDifferences",
                    TheoryFile: "causal_did.md",
                    Params:
                    [
                        new AlgoParam("units", "Объектов", 20, 300, 60, 5, "шт.", "Регионы, магазины, клиенты"),
                        new AlgoParam("periods", "Периодов", 4, 24, 8, 1, "шт.", "Длина панели"),
                        new AlgoParam("effect", "Истинный эффект", -10, 20, 3, 0.5, "",
                            "Что должна найти модель"),
                        new AlgoParam("staggered", "Разновременное внедрение", 0, 1, 1, 1, "",
                            "Именно здесь ломаются двусторонние фиксированные эффекты")
                            { Choices = YesNoChoices },
                        new AlgoParam("pretrend", "Расхождение трендов", -1, 1, 0.0, 0.05, "за период",
                            "Нарушение ключевой предпосылки дизайна"),
                        new AlgoParam("noise", "Шум", 0.1, 5, 0.5, 0.1, "", "Разброс отклика"),
                        new AlgoParam("boot", "Повторов бутстрапа", 20, 300, 60, 10, "шт.",
                            "Кластерные стандартные ошибки"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 15, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "causal_rdd",
                    Title: "Разрывный дизайн",
                    Subtitle: "Скачок на пороге, проверка плотности и эффекты на ложных порогах",
                    ApiClass: "AI.Economics.Econometrics.RegressionDiscontinuity",
                    TheoryFile: "causal_rdd.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 200, 10000, 2000, 100, "шт.", "Размер выборки"),
                        new AlgoParam("jump", "Истинный скачок", -10, 10, 2, 0.25, "",
                            "Эффект программы на пороге"),
                        new AlgoParam("slope", "Наклон зависимости", -3, 3, 0.5, 0.1, "",
                            "Связь отклика с переменной назначения"),
                        new AlgoParam("curvature", "Кривизна", -2, 2, 0.0, 0.1, "",
                            "Нелинейность, которую можно принять за скачок"),
                        new AlgoParam("noise", "Шум", 0.05, 3, 0.3, 0.05, "", "Разброс отклика"),
                        new AlgoParam("manipulation", "Манипуляция порогом", 0, 0.5, 0.0, 0.05, "доля",
                            "Подтягивание объектов выше порога ломает дизайн"),
                        new AlgoParam("bandwidth", "Полоса пропускания", 0, 2, 0, 0.05, "",
                            "Ноль — подобрать эмпирическим правилом"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 16, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "causal_matching",
                    Title: "Сопоставление по склонности",
                    Subtitle: "Устранение смещения отбора и проверка баланса ковариат",
                    ApiClass: "AI.Economics.Econometrics.PropensityScoreMatching",
                    TheoryFile: "causal_matching.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 300, 10000, 2000, 100, "шт.", "Размер выборки"),
                        new AlgoParam("effect", "Истинный эффект", -5, 10, 1, 0.25, "",
                            "Что должно остаться после устранения отбора"),
                        new AlgoParam("selection", "Сила отбора", 0, 3, 1.0, 0.1, "",
                            "Насколько участие зависит от характеристик"),
                        new AlgoParam("confounding", "Влияние характеристик на отклик", 0, 3, 1.5, 0.1, "",
                            "Вместе с отбором создаёт смещение"),
                        new AlgoParam("caliper", "Радиус сопоставления", 0.05, 1.0, 0.2, 0.05, "σ",
                            "Уже радиус — лучше баланс, но меньше пар"),
                        new AlgoParam("neighbours", "Соседей на объект", 1, 10, 3, 1, "шт.",
                            "Усреднение по нескольким контрольным"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 17, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "synthetic_control",
                    Title: "Синтетический контроль",
                    Subtitle: "Взвешенная комбинация доноров вместо контрольной группы и плацебо-тест",
                    ApiClass: "AI.Economics.Econometrics.SyntheticControl",
                    TheoryFile: "synthetic_control.md",
                    Params:
                    [
                        new AlgoParam("periods", "Периодов", 15, 80, 30, 1, "шт.", "Длина ряда"),
                        new AlgoParam("treatment", "Период вмешательства", 5, 60, 20, 1, "шт.",
                            "До него подгоняется синтетический двойник"),
                        new AlgoParam("donors", "Доноров", 3, 30, 8, 1, "шт.",
                            "Пул для построения комбинации"),
                        new AlgoParam("effect", "Истинный эффект", -20, 30, 5, 0.5, "",
                            "Сдвиг после вмешательства"),
                        new AlgoParam("noise", "Шум", 0.05, 3, 0.3, 0.05, "",
                            "Определяет качество подгонки до вмешательства"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 18, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "causal_forest",
                    Title: "Причинный лес",
                    Subtitle: "Индивидуальные эффекты, важность признаков и выигрыш от таргетирования",
                    ApiClass: "AI.Economics.Econometrics.CausalForest",
                    TheoryFile: "causal_forest.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 300, 10000, 2000, 100, "шт.", "Размер выборки"),
                        new AlgoParam("effect", "Эффект у чувствительных", -5, 10, 2, 0.25, "",
                            "У остальных объектов эффекта нет"),
                        new AlgoParam("share", "Доля чувствительных", 0.1, 0.9, 0.5, 0.05, "доля",
                            "Кому воздействие вообще помогает"),
                        new AlgoParam("noise", "Шум", 0.05, 3, 0.5, 0.05, "", "Разброс отклика"),
                        new AlgoParam("trees", "Деревьев", 20, 300, 80, 10, "шт.", "Размер леса"),
                        new AlgoParam("min_leaf", "Минимум в листе", 5, 100, 25, 5, "шт.",
                            "Каждой группы: честное разбиение требует обеих"),
                        new AlgoParam("depth", "Глубина", 2, 6, 3, 1, "", "Максимальная глубина дерева"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 19, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "stationarity",
                    Title: "Стационарность ряда",
                    Subtitle: "Дики — Фуллер и KPSS вместе: две противоположные гипотезы",
                    ApiClass: "AI.Economics.Econometrics.StationarityTests",
                    TheoryFile: "stationarity.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 50, 2000, 300, 50, "шт.", "Длина ряда"),
                        new AlgoParam("persistence", "Инерция ряда", 0, 1.0, 0.5, 0.05, "",
                            "Единица означает случайное блуждание"),
                        new AlgoParam("trend", "Тренд", -0.5, 0.5, 0.0, 0.01, "за период",
                            "Детерминированная составляющая"),
                        new AlgoParam("terms", "Детерминированная часть", 0, 2, 1, 1, "",
                            "Что включать в тестовую регрессию")
                            { Choices = DeterministicChoices },
                        new AlgoParam("noise", "Шум", 0.1, 5, 1.0, 0.1, "", "Разброс инноваций"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 20, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "var_model",
                    Title: "Векторная авторегрессия",
                    Subtitle: "Причинность по Гренджеру, импульсные отклики и разложение дисперсии",
                    ApiClass: "AI.Economics.Econometrics.VectorAutoregression",
                    TheoryFile: "var_model.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 100, 2000, 500, 50, "шт.", "Длина рядов"),
                        new AlgoParam("order", "Порядок модели", 1, 6, 1, 1, "",
                            "Число лагов в каждом уравнении"),
                        new AlgoParam("own", "Собственная инерция", 0, 0.95, 0.6, 0.05, "",
                            "Зависимость переменной от своего прошлого"),
                        new AlgoParam("cross", "Переток", 0, 1.2, 0.7, 0.05, "",
                            "Влияние первой переменной на вторую"),
                        new AlgoParam("feedback", "Обратная связь", 0, 1.0, 0.0, 0.05, "",
                            "Влияние второй переменной на первую"),
                        new AlgoParam("horizon", "Горизонт отклика", 5, 40, 20, 1, "", "Длина импульсного отклика"),
                        new AlgoParam("noise", "Шум", 0.05, 3, 0.5, 0.05, "", "Разброс инноваций"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 21, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "cointegration",
                    Title: "Коинтеграция и коррекция ошибками",
                    Subtitle: "Тест Йохансена на ранг и скорость возврата к долгосрочному равновесию",
                    ApiClass: "AI.Economics.Econometrics.Cointegration",
                    TheoryFile: "cointegration.md",
                    Params:
                    [
                        new AlgoParam("n", "Наблюдений", 100, 2000, 400, 50, "шт.", "Длина рядов"),
                        new AlgoParam("beta", "Долгосрочное соотношение", 0.2, 5, 2, 0.1, "",
                            "Коэффициент связи между рядами"),
                        new AlgoParam("adjustment", "Скорость возврата", 0, 0.9, 0.3, 0.05, "за период",
                            "Ноль означает отсутствие коинтеграции"),
                        new AlgoParam("lags", "Лагов в разностях", 1, 5, 1, 1, "", "Порядок краткосрочной динамики"),
                        new AlgoParam("noise", "Шум", 0.05, 3, 0.4, 0.05, "", "Разброс отклонений от равновесия"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 22, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "garch",
                    Title: "Условная волатильность",
                    Subtitle: "GARCH, GJR и EGARCH: кластеризация волатильности и эффект рычага",
                    ApiClass: "AI.Economics.Econometrics.Garch",
                    TheoryFile: "garch.md",
                    Params:
                    [
                        new AlgoParam("model", "Спецификация", 0, 2, 0, 1, "", "Форма уравнения дисперсии")
                            { Choices = GarchChoices },
                        new AlgoParam("n", "Наблюдений", 250, 5000, 1500, 50, "дн.", "Длина ряда доходностей"),
                        new AlgoParam("alpha", "Реакция на шок", 0.01, 0.4, 0.1, 0.01, "",
                            "Вес квадрата вчерашнего шока"),
                        new AlgoParam("beta", "Память", 0.3, 0.98, 0.85, 0.01, "",
                            "Вес вчерашней дисперсии"),
                        new AlgoParam("leverage", "Эффект рычага", 0, 0.3, 0.0, 0.01, "",
                            "Насколько падения поднимают волатильность сильнее роста"),
                        new AlgoParam("vol", "Долгосрочная волатильность", 0.005, 0.06, 0.02, 0.001, "в день",
                            "Уровень, к которому возвращается дисперсия"),
                        new AlgoParam("horizon", "Горизонт прогноза", 5, 60, 20, 1, "дн.", "Длина прогноза"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 23, 1, "", "Воспроизводимость"),
                    ]),

                new AlgoDef(
                    Key: "state_space",
                    Title: "Фильтр Калмана",
                    Subtitle: "Разделение ряда на ненаблюдаемый уровень и шум измерения",
                    ApiClass: "AI.Economics.Econometrics.StateSpace",
                    TheoryFile: "state_space.md",
                    Params:
                    [
                        new AlgoParam("model", "Модель", 0, 1, 0, 1, "", "Состав состояния")
                            { Choices = StateSpaceChoices },
                        new AlgoParam("n", "Наблюдений", 30, 1000, 200, 10, "шт.", "Длина ряда"),
                        new AlgoParam("level_sd", "Подвижность уровня", 0.01, 3, 0.3, 0.01, "",
                            "Разброс шока состояния"),
                        new AlgoParam("slope_sd", "Подвижность наклона", 0, 1, 0.02, 0.01, "",
                            "Для модели с трендом"),
                        new AlgoParam("noise_sd", "Шум измерения", 0.1, 10, 2.0, 0.1, "",
                            "Разброс наблюдения вокруг уровня"),
                        new AlgoParam("horizon", "Горизонт прогноза", 3, 40, 12, 1, "шт.", "Длина прогноза"),
                        new AlgoParam("seed", "Зерно генератора", 1, 999, 24, 1, "", "Воспроизводимость"),
                    ]),
            ]),
    ];

    /// <summary>
    /// Общий набор параметров прогноза для оценки дисконтированных потоков.
    /// </summary>
    private static AlgoParam[] DcfParams() =>
    [
        new AlgoParam("revenue", "Выручка первого года", 10_000_000, 100_000_000_000,
            1_000_000_000, 10_000_000, "руб.", "База прогноза"),
        new AlgoParam("growth", "Рост выручки", -0.2, 0.6, 0.1, 0.01, "в год",
            "Темп прогнозного периода"),
        new AlgoParam("margin", "Операционная рентабельность", 0.02, 0.5, 0.2, 0.01, "доля",
            "Прибыль до процентов и налогов к выручке"),
        new AlgoParam("tax", "Ставка налога", 0, 0.5, 0.2, 0.01, "доля", "Налог на прибыль"),
        new AlgoParam("capex", "Капитальные затраты", 0, 0.3, 0.06, 0.01, "доля выручки",
            "Инвестиции в основные средства"),
        new AlgoParam("depreciation", "Амортизация", 0, 0.3, 0.05, 0.01, "доля выручки",
            "Возвращается в поток"),
        new AlgoParam("working_capital", "Прирост оборотного капитала", 0, 0.2, 0.02, 0.005, "доля выручки",
            "Деньги, замороженные ростом"),
        new AlgoParam("years", "Прогнозный период", 3, 15, 5, 1, "лет", "Горизонт детального прогноза"),
        new AlgoParam("wacc", "Ставка дисконтирования", 0.03, 0.5, 0.16, 0.01, "в год",
            "Средневзвешенная стоимость капитала"),
        new AlgoParam("terminal_growth", "Темп вечного роста", -0.02, 0.1, 0.03, 0.005, "в год",
            "Не может превышать долгосрочный рост экономики"),
        new AlgoParam("net_debt", "Чистый долг", 0, 50_000_000_000, 400_000_000, 10_000_000, "руб.",
            "Вычитается из стоимости бизнеса"),
        new AlgoParam("mid_year", "Поправка на середину года", 0, 1, 1, 1, "",
            "Отражает равномерное поступление денег")
            { Choices = YesNoChoices },
    ];

    /// <summary>
    /// Общий набор параметров рынка для портфельных алгоритмов.
    /// </summary>
    /// <remarks>
    /// Все портфельные демонстраторы работают с одним и тем же синтетическим
    /// рынком из трёх активов: так видно, что разные критерии оптимизации
    /// дают разные ответы на одних данных.
    /// </remarks>
    private static AlgoParam[] MarketParams() =>
    [
        new AlgoParam("months", "Наблюдений", 36, 480, 180, 12, "мес.", "Длина истории доходностей"),
        new AlgoParam("market_vol", "Волатильность рынка", 0.01, 0.15, 0.04, 0.005, "в месяц",
            "Общий фактор всех активов"),
        new AlgoParam("beta_bonds", "Бета облигаций", -0.5, 1.5, 0.3, 0.05, "",
            "Чувствительность к рынку"),
        new AlgoParam("beta_equity", "Бета акций", 0, 2.5, 1.1, 0.05, "", "Чувствительность к рынку"),
        new AlgoParam("beta_commodity", "Бета сырья", -1.0, 2.0, 0.6, 0.05, "",
            "Отрицательная бета даёт диверсификацию"),
        new AlgoParam("idio_vol", "Специфический риск", 0.002, 0.08, 0.015, 0.002, "в месяц",
            "Разброс, не связанный с рынком"),
        new AlgoParam("drift", "Премия рынка", -0.01, 0.03, 0.006, 0.001, "в месяц",
            "Средняя доходность рыночного фактора"),
        new AlgoParam("seed", "Зерно генератора", 1, 999, 9, 1, "", "Воспроизводимость"),
    ];

    /// <summary>
    /// Общий набор параметров синтетической отчётности.
    /// </summary>
    /// <remarks>
    /// Все алгоритмы блока анализа отчётности работают с одной и той же
    /// моделью компании: так видно, что коэффициенты, модели банкротства и
    /// качество прибыли реагируют на одни и те же управленческие решения.
    /// </remarks>
    private static AlgoParam[] StatementParams() =>
    [
        new AlgoParam("revenue", "Выручка", 50_000_000, 500_000_000_000, 1_000_000_000, 50_000_000, "руб.",
            "Масштаб компании"),
        new AlgoParam("gross_margin", "Валовая маржа", 0.05, 0.8, 0.4, 0.01, "доля",
            "Доля выручки после себестоимости"),
        new AlgoParam("opex", "Коммерческие и управленческие расходы", 0.05, 0.6, 0.22, 0.01, "доля",
            "К выручке"),
        new AlgoParam("leverage", "Долг к активам", 0, 0.9, 0.35, 0.05, "доля",
            "Главный рычаг всех моделей банкротства"),
        new AlgoParam("dso", "Сбор дебиторской задолженности", 5, 180, 65, 5, "дн.",
            "Сколько дней покупатели держат деньги компании"),
        new AlgoParam("dio", "Оборот запасов", 5, 240, 90, 5, "дн.", "Сколько дней товар лежит на складе"),
        new AlgoParam("dpo", "Оплата поставщикам", 5, 180, 90, 5, "дн.",
            "Сколько дней компания пользуется деньгами поставщиков"),
        new AlgoParam("accruals", "Разрыв прибыли и денег", -0.05, 0.15, 0.02, 0.005, "доля выручки",
            "Насколько прибыль опережает операционный поток"),
        new AlgoParam("growth", "Рост выручки за год", -0.3, 0.8, 0.15, 0.05, "доля",
            "Для сравнения с предыдущим периодом"),
    ];

    protected override DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string> textParams,
        DemoSettings settings)
        => EconomicsDemoRunner.Run(algoKey, numericParams, settings);
}
