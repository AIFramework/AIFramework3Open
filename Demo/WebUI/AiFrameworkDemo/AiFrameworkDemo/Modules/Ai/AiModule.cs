using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Ai;

public sealed class AiModule : LibraryModuleBase
{
    public override string Id => "core";
    public override string Name => "AI (ядро)";
    public override string Description => "Ядро фреймворка: описательная статистика, распределения, корреляция, расстояния, свёртка, моменты и ряды";
    public override string Color => "amber";
    public override string TutorialFolder => "AiCore";

    public override string IconSvg => """
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
          <path d="M3 20h18"/>
          <path d="M5 20V11"/>
          <path d="M10 20V7"/>
          <path d="M15 20V14"/>
          <path d="M20 20V4"/>
          <circle cx="5"  cy="9"  r="0.8" fill="currentColor"/>
          <circle cx="10" cy="5"  r="0.8" fill="currentColor"/>
          <circle cx="15" cy="12" r="0.8" fill="currentColor"/>
          <circle cx="20" cy="2"  r="0.8" fill="currentColor"/>
        </svg>
        """;

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        new CategoryDef("core_descriptive", "Описательная статистика",
            "Оценки центра, разброса и формы распределения выборки",
            [
                new AlgoDef(
                    "descriptive", "Сводные статистики",
                    "Среднее, дисперсия, СКО, мин/макс, RMS, квантили, асимметрия, эксцесс. Визуализация — ящик с усами",
                    "AI.Statistics.Statistic",
                    "descriptive.md",
                    [
                        new AlgoParam("n",    "Объём выборки",    50, 2000, 400, 10,  "шт.", "Число наблюдений в выборке"),
                        new AlgoParam("dist", "Распределение", 0, 3, 0, 1, "", "Тип генерируемого распределения")
                            { Choices = [new AlgoChoice(0, "N(μ,σ)"), new AlgoChoice(1, "U(0,1)"), new AlgoChoice(2, "Exp"), new AlgoChoice(3, "Смесь")] },
                        new AlgoParam("mu",   "Среднее μ",       -5, 5,   0, 0.5,  "",    "Среднее для нормального/смеси"),
                        new AlgoParam("sigma","СКО σ",           0.1, 5,   1, 0.1,  "",    "Стандартное отклонение"),
                        new AlgoParam("ci",   "Дов. интервалы",  0, 1, 0, 1, "", "Показать CI и значимость")
                            { Choices = [new AlgoChoice(0, "Выкл."), new AlgoChoice(1, "Вкл.")] },
                        new AlgoParam("seed", "Seed",             0, 200, 42, 1,    "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "histogram_pdf", "Гистограмма + PDF",
                    "Эмпирическая плотность (нормированная гистограмма) с наложением теоретической N(μ,σ)",
                    "AI.Statistics.Statistic.Histogramm",
                    "histogram_pdf.md",
                    [
                        new AlgoParam("n",    "Объём выборки",  50, 5000, 800, 50,  "шт.", "Число наблюдений"),
                        new AlgoParam("bins", "Число бинов",     8, 80,   30, 1,    "",    "Разбиение оси X"),
                        new AlgoParam("mu",   "Среднее μ",      -5, 5,    0, 0.5,  "",    "Среднее истинного распределения"),
                        new AlgoParam("sigma","СКО σ",         0.2, 5,    1, 0.1,  "",    "СКО истинного распределения"),
                        new AlgoParam("ci",   "Дов. интервалы",  0, 1, 0, 1, "", "Показать CI и значимость")
                            { Choices = [new AlgoChoice(0, "Выкл."), new AlgoChoice(1, "Вкл.")] },
                        new AlgoParam("seed", "Seed",            0, 200, 42, 1,    "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "quantiles", "Квантили и CDF",
                    "Эмпирическая функция распределения (ECDF) с отмеченными квантилями Q1, Q2 (медиана) и Q3",
                    "AI.Statistics.Quantile",
                    "quantiles.md",
                    [
                        new AlgoParam("n",    "Объём выборки", 50, 3000, 400, 10,  "шт.", "Число наблюдений"),
                        new AlgoParam("dist", "Распределение", 0, 2, 0, 1, "", "Тип распределения")
                            { Choices = [new AlgoChoice(0, "N(μ,σ)"), new AlgoChoice(1, "U(0,1)"), new AlgoChoice(2, "Exp")] },
                        new AlgoParam("seed", "Seed",           0, 200, 42, 1,    "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "moments_scan", "Сканирование моментов",
                    "Зависимость выборочных моментов (среднее, дисперсия) от объёма выборки — сходимость",
                    "AI.Statistics.Statistic",
                    "moments_scan.md",
                    [
                        new AlgoParam("nMax", "Макс. объём",  100, 5000, 1500, 100, "шт.", "До какого N строить сходимость"),
                        new AlgoParam("mu",   "Истинное μ",   -3,  3,    0,   0.5, "",    "Истинное среднее"),
                        new AlgoParam("sigma","Истинное σ",   0.1, 3,    1,   0.1, "",    "Истинное СКО"),
                        new AlgoParam("seed", "Seed",          0,  200,  42,  1,   "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "confidence_interval", "Доверительные интервалы",
                    "z- и t-интервалы для среднего: сравнение ширины, покрытие, влияние n и уровня доверия",
                    "AI.Statistics.StatInference",
                    "confidence_interval.md",
                    [
                        new AlgoParam("n",    "Объём выборки", 10, 1000, 100, 10,   "шт.", "Число наблюдений"),
                        new AlgoParam("mu",   "Истинное μ",   -5,  5,    2,  0.5,  "",    "Истинное среднее"),
                        new AlgoParam("sigma","σ",           0.1, 5,    1,  0.1,  "",    "Стандартное отклонение"),
                        new AlgoParam("conf", "Уровень доверия", 0.8, 0.99, 0.95, 0.01, "", "1−α: вероятность покрытия"),
                        new AlgoParam("seed", "Seed",           0,  200,  42,  1,   "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "hypothesis_test", "Тесты гипотез",
                    "z- и t-тесты для среднего (двусторонние): визуализация критической области и p-value",
                    "AI.Statistics.StatInference",
                    "hypothesis_test.md",
                    [
                        new AlgoParam("n",      "Объём выборки", 10, 500, 50, 5,    "шт.", "Число наблюдений"),
                        new AlgoParam("mu0",    "H₀: μ₀",      -3,  3,   0,  0.5,  "",    "Гипотетическое среднее"),
                        new AlgoParam("muTrue", "Истинное μ",   -3,  3,   0.5, 0.1, "",    "Реальное среднее генерации"),
                        new AlgoParam("sigma",  "σ",           0.1, 5,   1,  0.1,  "",    "Стандартное отклонение"),
                        new AlgoParam("alpha",  "α",          0.01, 0.2, 0.05, 0.01, "",   "Уровень значимости"),
                        new AlgoParam("seed",   "Seed",         0,  200, 42,  1,    "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "normality_test", "Тесты на нормальность",
                    "Жарк-Бера и Андерсон-Дарлинг: Q-Q plot и проверка гипотезы нормальности",
                    "AI.Statistics.StatInference",
                    "normality_test.md",
                    [
                        new AlgoParam("n",     "Объём выборки", 20, 1000, 200, 10,  "шт.", "Число наблюдений"),
                        new AlgoParam("dist",  "Распределение", 0, 3, 0, 1, "", "Тип генерируемого распределения")
                            { Choices = [new AlgoChoice(0, "N(μ,σ)"), new AlgoChoice(1, "U(0,1)"), new AlgoChoice(2, "Exp"), new AlgoChoice(3, "Смесь")] },
                        new AlgoParam("mu",    "μ",           -3, 3,  0, 0.5,  "",    "Среднее"),
                        new AlgoParam("sigma", "σ",          0.1, 5,  1, 0.1,  "",    "СКО"),
                        new AlgoParam("seed",  "Seed",         0, 200, 42, 1,   "",    "Инициализация генератора"),
                    ]),
            ]),

        new CategoryDef("core_distributions", "Распределения",
            "Генерация и свойства вероятностных распределений",
            [
                new AlgoDef(
                    "uniform_normal", "U(0,1) и N(0,1)",
                    "Сравнение выборок равномерного и нормального распределений — гистограммы и параметры формы",
                    "AI.Statistics.Statistic.UniformDistribution",
                    "uniform_normal.md",
                    [
                        new AlgoParam("n",    "Объём выборки", 100, 5000, 1200, 50, "шт.", "Число точек на распределение"),
                        new AlgoParam("bins", "Число бинов",     8,  60,  28,   1,  "",    "Разбиение оси X"),
                        new AlgoParam("seed", "Seed",            0, 200,  42,   1,  "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "clt", "Центральная предельная теорема",
                    "Распределение среднего из k равномерных величин сходится к нормали при росте k",
                    "AI.Statistics.Statistic.RandNormP",
                    "clt.md",
                    [
                        new AlgoParam("k",    "Сколько U усредняем", 1, 50, 12, 1,  "",    "Число складываемых равномерных величин"),
                        new AlgoParam("n",    "Объём выборки",     200, 5000, 2000, 100, "шт.", "Размер выборки средних"),
                        new AlgoParam("bins", "Число бинов",         8,  60,  30,   1,  "",    "Разбиение оси X"),
                        new AlgoParam("seed", "Seed",                0, 200,  42,   1,  "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "mle", "ML-оценка параметров N(μ,σ)",
                    "Оценка параметров по методу максимального правдоподобия с визуализацией истинной и восстановленной PDF",
                    "AI.Statistics.Distributions.NonCorrelatedGaussian",
                    "mle.md",
                    [
                        new AlgoParam("n",     "Объём выборки", 30, 2000, 400, 10, "шт.", "Размер выборки"),
                        new AlgoParam("muT",   "Истинное μ",   -5, 5,    1.5, 0.5, "",    "Истинное среднее"),
                        new AlgoParam("sigT",  "Истинное σ",  0.2, 5,    1.0, 0.1, "",    "Истинное СКО"),
                        new AlgoParam("seed",  "Seed",          0, 200,  42,  1,   "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "exponential", "Экспоненциальное распределение",
                    "Выборка Exp(λ) с наложением теоретической плотности λ·e⁻ᵏᵡ",
                    "AI.Statistics.RandomEngine.NextExponential",
                    "exponential.md",
                    [
                        new AlgoParam("n",    "Объём выборки", 100, 5000, 1500, 50,   "шт.", "Число точек"),
                        new AlgoParam("rate", "Интенсивность λ", 0.1, 5,   1.0, 0.1,  "",    "Параметр λ экспоненциального распределения"),
                        new AlgoParam("bins", "Число бинов",      8, 80,   40,  1,    "",    "Разбиение оси X"),
                        new AlgoParam("seed", "Seed",             0, 200,  42,  1,    "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "gamma_beta", "Гамма и Бета распределения",
                    "Gamma(shape,scale) и Beta(α,β) — выборка и теоретические моменты",
                    "AI.Statistics.RandomEngine.NextGamma",
                    "gamma_beta.md",
                    [
                        new AlgoParam("n",     "Объём выборки", 100, 5000, 2000, 50, "шт.", "Число точек на каждое распределение"),
                        new AlgoParam("shape", "shape (Gamma)", 0.2, 10,  2.0, 0.2, "",    "Параметр формы гамма-распределения"),
                        new AlgoParam("scale", "scale (Gamma)", 0.1, 5,   1.0, 0.1, "",    "Параметр масштаба гамма-распределения"),
                        new AlgoParam("alpha", "α (Beta)",      0.1, 10,  2.0, 0.2, "",    "Параметр α бета-распределения"),
                        new AlgoParam("beta",  "β (Beta)",      0.1, 10,  5.0, 0.2, "",    "Параметр β бета-распределения"),
                        new AlgoParam("bins",  "Число бинов",     8, 80,  40,  1,   "",    "Разбиение оси X"),
                        new AlgoParam("seed",  "Seed",            0, 200, 42,  1,   "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "cauchy_laplace", "Коши и Лаплас",
                    "Cauchy(loc,γ) и Laplace(μ,b) — тяжёлые хвосты vs экспоненциальные хвосты",
                    "AI.Statistics.RandomEngine.NextCauchy",
                    "cauchy_laplace.md",
                    [
                        new AlgoParam("n",           "Объём выборки",  100, 5000, 2000, 50,  "шт.", "Число точек"),
                        new AlgoParam("loc",         "Положение",     -5, 5,     0,   0.5,  "",    "Параметр loc (центр)"),
                        new AlgoParam("scaleCauchy", "γ (Cauchy)",    0.1, 5,    1.0, 0.1,  "",    "Масштаб Коши"),
                        new AlgoParam("bLaplace",    "b (Laplace)",   0.1, 5,    1.0, 0.1,  "",    "Масштаб Лапласа"),
                        new AlgoParam("bins",        "Число бинов",     8, 80,   50,  1,    "",    "Разбиение оси X"),
                        new AlgoParam("seed",        "Seed",            0, 200,  42,  1,    "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "weibull_poisson", "Вейбулл и Пуассон",
                    "Weibull(k,λ) непрерывное + Poisson(λ) дискретное — выборки и моменты",
                    "AI.Statistics.RandomEngine.NextWeibull",
                    "weibull_poisson.md",
                    [
                        new AlgoParam("n",      "Объём выборки",  100, 5000, 2000, 50, "шт.", "Число точек"),
                        new AlgoParam("wShape", "k (Weibull)",   0.2, 5,    1.5, 0.1, "",    "Параметр формы Вейбулла"),
                        new AlgoParam("wScale", "λ (Weibull)",   0.1, 10,   2.0, 0.1, "",    "Параметр масштаба Вейбулла"),
                        new AlgoParam("lambda", "λ (Poisson)",   0.5, 30,   5.0, 0.5, "",    "Интенсивность Пуассона"),
                        new AlgoParam("bins",   "Число бинов",     4, 60,   30,  1,   "",    "Разбиение оси X"),
                        new AlgoParam("seed",   "Seed",            0, 200,  42,  1,   "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "mixture_em", "Смесь гауссиан (EM)",
                    "Генерация данных из K гауссовых компонент и восстановление параметров EM-алгоритмом",
                    "AI.Statistics.MixtureModeling.EM.Fit",
                    "mixture_em.md",
                    [
                        new AlgoParam("n",    "Объём выборки",  100, 5000, 1000, 50, "шт.", "Число наблюдений"),
                        new AlgoParam("k",    "Число компонент",  2, 6,    3,   1,  "",    "K — число гауссовых компонент"),
                        new AlgoParam("bins", "Число бинов",     10, 80,   50,  1,  "",    "Разбиение оси X"),
                        new AlgoParam("seed", "Seed",             0, 200,  42,  1,  "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "monte_carlo", "Монте-Карло 1D",
                    "Сходимость оценки интеграла ∫sin(x)/x dx на [1;10] при росте числа точек N",
                    "AI.Statistics.MonteCarlo.Integration",
                    "monte_carlo.md",
                    [
                        new AlgoParam("nMax", "Макс. число точек",  200, 100000, 20000, 500, "шт.", "Верхняя граница размера выборки"),
                        new AlgoParam("a",    "Нижний предел",     -10, 10,    1,    0.5, "",    "Левая граница интегрирования"),
                        new AlgoParam("b",    "Верхний предел",    -10, 20,   10,    0.5, "",    "Правая граница интегрирования"),
                        new AlgoParam("seed", "Seed",                0, 200,  42,    1,   "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "monte_carlo_nd", "Монте-Карло ND",
                    "Многомерный интеграл ∫exp(−|x|²)dx на гиперкубе [−2,2]^d — сходимость по N",
                    "AI.Statistics.MonteCarlo.Integration.CalcIntegralND",
                    "monte_carlo_nd.md",
                    [
                        new AlgoParam("dim",  "Размерность",        2, 8,     3,    1,   "",    "Число измерений d"),
                        new AlgoParam("nMax", "Макс. число точек", 500, 200000, 50000, 1000, "шт.", "Верхняя граница выборки"),
                        new AlgoParam("seed", "Seed",               0, 200,   42,   1,   "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "gauss2d", "Двумерная гауссиана 3D",
                    "Поверхность плотности двумерного нормального распределения N₂(μ, Σ) с корреляцией ρ",
                    "AI.Statistics.Distributions.NonCorrelatedGaussian",
                    "gauss2d.md",
                    [
                        new AlgoParam("mu1",  "μ₁",           -3, 3,    0,   0.5, "",  "Среднее по x₁"),
                        new AlgoParam("mu2",  "μ₂",           -3, 3,    0,   0.5, "",  "Среднее по x₂"),
                        new AlgoParam("sig1", "σ₁",          0.2, 3,   1.0, 0.1, "",  "СКО по x₁"),
                        new AlgoParam("sig2", "σ₂",          0.2, 3,   0.6, 0.1, "",  "СКО по x₂"),
                        new AlgoParam("rho",  "Корреляция ρ", -0.95, 0.95, 0.5, 0.05, "", "Коэффициент корреляции"),
                        new AlgoParam("azimuth",   "Азимут камеры",   -180, 180, -30, 5, "°", "Горизонтальный угол"),
                        new AlgoParam("elevation", "Элевация камеры", -90,  90,  30, 5, "°", "Вертикальный угол"),
                    ]),

                new AlgoDef(
                    "mixture2d", "Смесь гауссиан 2D → 3D",
                    "3D-поверхность плотности смеси K двумерных гауссиан (диагональная ковариация)",
                    "AI.Statistics.MixtureModeling.GaussianMixture",
                    "mixture2d.md",
                    [
                        new AlgoParam("k",    "Число компонент",    2, 5,   3, 1, "", "K — число гауссовых компонент"),
                        new AlgoParam("seed", "Seed",               0, 200, 42, 1, "", "Инициализация генератора"),
                        new AlgoParam("azimuth",   "Азимут камеры",   -180, 180, -30, 5, "°", "Горизонтальный угол"),
                        new AlgoParam("elevation", "Элевация камеры", -90,  90,  30, 5, "°", "Вертикальный угол"),
                    ]),

                new AlgoDef(
                    "rayleigh_rice", "Релей и Райс",
                    "Распределения Релея (модуль шума) и Райса (модуль сигнал + шум): PDF и генерация",
                    "AI.Statistics.RandomEngine.NextRayleigh",
                    "rayleigh_rice.md",
                    [
                        new AlgoParam("n",     "Объём выборки",  200, 5000, 2000, 100, "шт.", "Число сэмплов"),
                        new AlgoParam("sigma", "σ",             0.1, 3,    1,    0.1, "",    "Параметр масштаба"),
                        new AlgoParam("nu",    "ν (Rice)",       0,  5,    2,    0.1, "",    "Нецентральность (0 = Rayleigh)"),
                        new AlgoParam("seed",  "Seed",           0, 200,  42,    1,   "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "heterogeneous_mixture", "Смесь разных распределений",
                    "Гетерогенная смесь + Classification EM (индикаторный метод) для подгонки параметров",
                    "AI.Statistics.MixtureModeling.ClassificationEM",
                    "heterogeneous_mixture.md",
                    [
                        new AlgoParam("n",    "Объём выборки", 500, 10000, 3000, 200, "шт.", "Число сэмплов из смеси"),
                        new AlgoParam("kind", "Рецепт смеси",  0, 2, 0, 1, "", "Набор компонент")
                            { Choices = [new AlgoChoice(0, "N + Exp"), new AlgoChoice(1, "N + Laplace + Rayleigh"), new AlgoChoice(2, "U + Exp + N")] },
                        new AlgoParam("fit",  "Classification EM", 0, 1, 0, 1, "", "Подогнать параметры индикаторным EM")
                            { Choices = [new AlgoChoice(0, "Выкл."), new AlgoChoice(1, "Вкл.")] },
                        new AlgoParam("seed", "Seed",          0, 200, 42, 1,   "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "heterogeneous_mixture_nd", "Многомерная гетерогенная смесь",
                    "ND-смесь разных распределений (диагональная / полная ковариация) + Classification EM",
                    "AI.Statistics.MixtureModeling.ClassificationEM.FitND",
                    "heterogeneous_mixture_nd.md",
                    [
                        new AlgoParam("n",    "Объём выборки", 300, 5000, 2000, 200, "шт.", "Число сэмплов из ND-смеси"),
                        new AlgoParam("kind", "Тип ковариации", 0, 1, 0, 1, "", "Структура компонент")
                            { Choices = [new AlgoChoice(0, "Диагональная"), new AlgoChoice(1, "Полная ковариация")] },
                        new AlgoParam("fit",  "Classification EM", 0, 1, 0, 1, "", "Подогнать ND-параметры")
                            { Choices = [new AlgoChoice(0, "Выкл."), new AlgoChoice(1, "Вкл.")] },
                        new AlgoParam("seed", "Seed",          0, 200, 42, 1, "", "Инициализация генератора"),
                    ]),
            ]),

        new CategoryDef("core_correlation", "Корреляция и зависимости",
            "Связи между случайными величинами и сигналами",
            [
                new AlgoDef(
                    "pearson", "Коэффициент Пирсона",
                    "Диаграмма рассеяния (x, y = αx + βξ) с расчётом r и ковариации при заданном уровне шума",
                    "AI.Statistics.Statistic.CorrelationCoefficient",
                    "pearson.md",
                    [
                        new AlgoParam("n",     "Объём выборки", 50, 2000, 300, 10, "шт.", "Число наблюдений"),
                        new AlgoParam("alpha", "Наклон α",     -2, 2,    1.0, 0.1, "",    "Коэффициент линейной связи"),
                        new AlgoParam("noise", "Шум β·σ",     0.0, 3,    0.6, 0.1, "",    "Амплитуда нормального шума"),
                        new AlgoParam("seed",  "Seed",          0, 200,  42,  1,   "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "corr_matrix", "Матрица корреляций",
                    "Тепловая карта попарных корреляций между 5 сгенерированными рядами",
                    "AI.DataStructs.Algebraic.Matrix.GetCorrelationMatrixNorm",
                    "corr_matrix.md",
                    [
                        new AlgoParam("n",    "Длина рядов",   50, 2000, 300, 10, "шт.", "Количество наблюдений в каждом ряду"),
                        new AlgoParam("k",    "Число рядов",    3, 8,    5,   1,  "",    "Сколько переменных включить в матрицу"),
                        new AlgoParam("mix",  "Зависимость",  0.0, 1,    0.5, 0.05, "",  "Доля общего фактора: 0 — независимы, 1 — идентичны"),
                        new AlgoParam("seed", "Seed",           0, 200,  42,  1,  "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "autocorr", "Автокорреляция",
                    "АКФ для синусоиды, белого шума и AR(1)-процесса",
                    "AI.Correlation.AutoCorrelation",
                    "autocorr.md",
                    [
                        new AlgoParam("n",    "Длина ряда",    64, 2000, 256, 16, "шт.", "Число точек сигнала"),
                        new AlgoParam("kind", "Сигнал", 0, 2, 0, 1, "", "Тип входного сигнала")
                            { Choices = [new AlgoChoice(0, "sin"), new AlgoChoice(1, "Шум"), new AlgoChoice(2, "AR(1)")] },
                        new AlgoParam("freq", "Частота",     0.02, 0.4, 0.08, 0.01, "", "Нормированная частота синуса"),
                        new AlgoParam("phi",  "Коэф. AR",   -0.95, 0.95, 0.7, 0.05, "", "Коэффициент авторегрессии AR(1)"),
                        new AlgoParam("seed", "Seed",         0, 200,  42,   1, "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "crosscorr", "Взаимная корреляция",
                    "ВКФ двух сигналов с заданной задержкой — определяется пик корреляции",
                    "AI.Correlation.CrossCorrelation",
                    "crosscorr.md",
                    [
                        new AlgoParam("n",     "Длина ряда",    64, 1024, 256, 16, "шт.", "Число точек"),
                        new AlgoParam("lag",   "Задержка",     -60, 60,   18,  1, "шт.", "Сдвиг второго сигнала"),
                        new AlgoParam("noise", "Шум",         0.0, 2,    0.3, 0.05, "",  "Уровень шума"),
                        new AlgoParam("seed",  "Seed",          0, 200,  42,  1,  "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "convolution", "Свёртка",
                    "Прямая линейная свёртка прямоугольного импульса с ядром Гаусса — эффект сглаживания",
                    "AI.Convolution.DirectConvolution",
                    "convolution.md",
                    [
                        new AlgoParam("n",     "Длина сигнала", 32, 512, 128, 8,   "шт.", "Число точек входного сигнала"),
                        new AlgoParam("kw",    "Ширина ядра",    3, 51,  15, 2,   "шт.", "Нечётная длина ядра"),
                        new AlgoParam("sigma", "σ ядра",      0.5, 10,  3.0, 0.5, "",    "σ гауссова ядра"),
                    ]),
            ]),

        new CategoryDef("core_distances", "Расстояния и геометрия",
            "Метрики близости между векторами и простые геометрические операции",
            [
                new AlgoDef(
                    "metric_balls", "Единичные сферы",
                    "Сравнение форм единичных сфер в L1, L2, L∞ и угловой метрике cos",
                    "AI.Distances.BaseDist",
                    "metric_balls.md",
                    [
                        new AlgoParam("p",   "Параметр p (Lp)",  1, 8, 2, 1, "", "Степень нормы Минковского"),
                        new AlgoParam("res", "Разрешение",     50, 400, 200, 10, "", "Число точек по каждой оси"),
                    ]),

                new AlgoDef(
                    "metric_balls_3d", "Единичные сферы 3D",
                    "3D-поверхность единичной сферы Lp-нормы: |x|ᵖ + |y|ᵖ + |z|ᵖ = 1",
                    "AI.Distances.BaseDist",
                    "metric_balls.md",
                    [
                        new AlgoParam("p",         "Параметр p (Lp)",     1, 8, 2, 1, "", "Степень нормы Минковского"),
                        new AlgoParam("azimuth",   "Азимут камеры",    -180, 180, -35, 5, "°", "Горизонтальный угол обзора"),
                        new AlgoParam("elevation", "Элевация камеры",  -90,  90,  25, 5, "°", "Вертикальный угол обзора"),
                    ]),

                new AlgoDef(
                    "kl_divergence", "KL-дивергенция",
                    "Два распределения N(μ₁,σ₁) и N(μ₂,σ₂), численное значение KL по плотностям",
                    "AI.Distances.ProbabilityDistances.DKL",
                    "kl_divergence.md",
                    [
                        new AlgoParam("mu1",   "μ₁", -4, 4,   0,   0.2, "", "Среднее распределения p"),
                        new AlgoParam("sig1",  "σ₁", 0.2, 3,  1.0, 0.1, "", "СКО распределения p"),
                        new AlgoParam("mu2",   "μ₂", -4, 4,   1.5, 0.2, "", "Среднее распределения q"),
                        new AlgoParam("sig2",  "σ₂", 0.2, 3,  1.2, 0.1, "", "СКО распределения q"),
                    ]),

                new AlgoDef(
                    "projection", "Проекция вектора",
                    "Геометрическая проекция A на направление B с вычислением угла и длины проекции",
                    "AI.HighLevelFunctions.AnalyticGeometryFunctions.ProjectionAtoB",
                    "projection.md",
                    [
                        new AlgoParam("ax", "A.x", -5, 5, 3.0, 0.1, "", "X-компонента вектора A"),
                        new AlgoParam("ay", "A.y", -5, 5, 2.0, 0.1, "", "Y-компонента вектора A"),
                        new AlgoParam("bx", "B.x", -5, 5, 4.0, 0.1, "", "X-компонента вектора B"),
                        new AlgoParam("by", "B.y", -5, 5, 1.0, 0.1, "", "Y-компонента вектора B"),
                    ]),
            ]),

        new CategoryDef("core_series", "Ряды и обработка",
            "Численное дифференцирование/интегрирование и скользящие статистики",
            [
                new AlgoDef(
                    "diff_integr", "Диф. и интеграл ряда",
                    "Численное дифференцирование sin(x) -> cos(x) и интегрирование cos(x) -> sin(x)",
                    "AI.Functions.Diff / Integral",
                    "diff_integr.md",
                    [
                        new AlgoParam("n",    "Число точек", 50, 1000, 200, 10,   "шт.", "Длина сетки"),
                        new AlgoParam("xMax", "Верхняя граница", 2, 20, 6.28, 0.1, "", "Правый край интервала"),
                    ]),

                new AlgoDef(
                    "moving_stats", "Скользящие статистики",
                    "Скользящие среднее и СКО по зашумлённому сигналу через WindowFuncDouble",
                    "AI.Functions.WindowFuncDouble",
                    "moving_stats.md",
                    [
                        new AlgoParam("n",      "Длина ряда",  64, 2000, 512, 16,  "шт.", "Число точек"),
                        new AlgoParam("window", "Окно",         4, 128,  21,  1,  "шт.", "Размер скользящего окна"),
                        new AlgoParam("noise",  "Шум σ",      0.0, 2,   0.25, 0.05, "",  "СКО шума"),
                        new AlgoParam("seed",   "Seed",         0, 200, 42,   1,  "",    "Инициализация генератора"),
                    ]),

                new AlgoDef(
                    "form_factors", "Форм-факторы сигнала",
                    "Пик-фактор, форм-фактор и импульс-фактор для разных сигналов (sin, треугольник, прямоугольник, импульс)",
                    "AI.Statistics.FormStatistics",
                    "form_factors.md",
                    [
                        new AlgoParam("n",    "Длина ряда", 64, 2000, 256, 16, "шт.", "Число точек"),
                        new AlgoParam("kind", "Сигнал", 0, 3, 0, 1, "", "Тип генерируемого сигнала")
                            { Choices = [
                                new AlgoChoice(0, "Синус"),
                                new AlgoChoice(1, "Треугольный"),
                                new AlgoChoice(2, "Прямоугольный"),
                                new AlgoChoice(3, "Импульс"),
                            ] },
                    ]),
            ]),
    ];

    protected override DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string>  textParams,
        DemoSettings settings)
        => AiDemoRunner.Run(algoKey, numericParams, settings);
}
