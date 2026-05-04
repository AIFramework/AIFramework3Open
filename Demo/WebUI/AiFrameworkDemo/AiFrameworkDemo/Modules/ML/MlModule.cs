using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.ML;

public class MlModule : LibraryModuleBase
{
    public override string Id => "ml";
    public override string Name => "AI.ML";
    public override string Description => "Машинное обучение: кластеризация, классификация, регрессия, PCA, анализ рядов, генетические алгоритмы";
    public override string Color => "violet";
    public override string TutorialFolder => "ML";

    public override string IconSvg => """
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="3"/>
          <circle cx="4"  cy="6"  r="2"/>
          <circle cx="20" cy="6"  r="2"/>
          <circle cx="4"  cy="18" r="2"/>
          <circle cx="20" cy="18" r="2"/>
          <line x1="6" y1="6"  x2="10" y2="11"/>
          <line x1="18" y1="6"  x2="14" y2="11"/>
          <line x1="6"  y1="18" x2="10" y2="13"/>
          <line x1="18" y1="18" x2="14" y2="13"/>
        </svg>
        """;

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        new CategoryDef("clustering", "Кластеризация",
            "Алгоритмы разбиения данных на группы без учителя",
            [
                new AlgoDef(
                    "kmeans", "K-Means",
                    "Итерационная кластеризация методом k-средних с инициализацией k-means++",
                    "AI.ML.Clustering.KMeans",
                    "kmeans.md",
                    [
                        new AlgoParam("k",       "Число кластеров", 2, 6,   3, 1, "", "Количество кластеров K"),
                        new AlgoParam("n",       "Число точек",    30, 300, 120, 10, "шт.", "Объём обучающей выборки"),
                        new AlgoParam("dataset", "Датасет (0=блобы, 1=кольца, 2=спираль, 3=вытянутые)", 0, 3, 0, 1, "", "Тип распределения: blobs / rings / spiral / anisotropic"),
                        new AlgoParam("seed",    "Seed",            0, 200,  42,  1, "", "Начальное значение генератора"),
                    ]),

                new AlgoDef(
                    "fast_kmeans", "Fast K-Means",
                    "Ускоренный K-Means с BallTree для быстрого поиска ближайшего центроида",
                    "AI.ML.Clustering.FastKMeans",
                    "fast_kmeans.md",
                    [
                        new AlgoParam("k",       "Число кластеров", 2, 6,   3, 1, "", "Количество кластеров K"),
                        new AlgoParam("n",       "Число точек",    30, 400, 150, 10, "шт.", "Объём обучающей выборки"),
                        new AlgoParam("dataset", "Датасет (0=блобы, 1=кольца, 2=спираль, 3=вытянутые)", 0, 3, 0, 1, "", "Тип распределения"),
                        new AlgoParam("seed",    "Seed",            0, 200,  42,  1, "", "Начальное значение генератора"),
                    ]),

                new AlgoDef(
                    "forel", "FOREL",
                    "Автоматическое определение числа кластеров методом гиперсфер",
                    "AI.ML.Clustering.Forel",
                    "forel.md",
                    [
                        new AlgoParam("n",       "Число точек", 30, 300, 120, 10, "шт.", "Объём обучающей выборки"),
                        new AlgoParam("dataset", "Датасет (0=блобы, 1=кольца, 2=спираль, 3=вытянутые)", 0, 3, 0, 1, "", "Тип распределения"),
                        new AlgoParam("seed",    "Seed",         0, 200,  42,  1, "", "Начальное значение генератора"),
                    ]),

                new AlgoDef(
                    "kohonen", "Сеть Кохонена (SOM)",
                    "Самоорганизующаяся карта — нейронная сеть без учителя с конкурентным обучением",
                    "AI.ML.Clustering.KohonenNet",
                    "kohonen.md",
                    [
                        new AlgoParam("k",       "Число нейронов",      2,   8,  4,  1,    "", "Число нейронов карты"),
                        new AlgoParam("n",       "Число точек",        30, 300, 120, 10, "шт.", "Объём обучающей выборки"),
                        new AlgoParam("dataset", "Датасет (0=блобы, 1=кольца, 2=спираль, 3=вытянутые)", 0, 3, 0, 1, "", "Тип распределения"),
                        new AlgoParam("eta0",    "Нач. скорость обуч.", 0.01, 0.99, 0.3, 0.01, "", "Начальная скорость обучения η₀"),
                        new AlgoParam("steps",   "Шаги убывания η",    10, 300,  50, 10,    "", "За сколько шагов η убывает до минимума"),
                        new AlgoParam("seed",    "Seed",                0, 200,   42,  1,    "", "Начальное значение генератора"),
                    ]),

                new AlgoDef(
                    "kmeans_3d", "K-Means 3D",
                    "Кластеризация K-Means в трёхмерном пространстве — 3D scatter по кластерам",
                    "AI.ML.Clustering.KMeans",
                    "kmeans.md",
                    [
                        new AlgoParam("k",         "Число кластеров",  2, 6,   3, 1, "", "Количество кластеров K"),
                        new AlgoParam("n",         "Число точек",     30, 300, 120, 10, "шт.", "Объём обучающей выборки"),
                        new AlgoParam("seed",      "Seed",             0, 200,  42,  1, "", "Начальное значение генератора"),
                        new AlgoParam("azimuth",   "Азимут камеры",  -180, 180, -35, 5, "°", "Горизонтальный угол обзора"),
                        new AlgoParam("elevation", "Элевация камеры", -90,  90,  25, 5, "°", "Вертикальный угол обзора"),
                    ]),
            ]),

        new CategoryDef("classification", "Классификация",
            "Алгоритмы отнесения объекта к одному из заданных классов",
            [
                new AlgoDef(
                    "bayes_cls", "Байесовский классификатор",
                    "Вероятностный классификатор на основе теоремы Байеса с некоррелированными гауссианами",
                    "AI.ML.Classification.BayesianClassifier",
                    "bayes_cls.md",
                    [
                        new AlgoParam("n",       "Число точек", 40, 400, 120, 10, "шт.", "Суммарный объём выборки"),
                        new AlgoParam("dataset", "Датасет (0=линейный, 1=луны, 2=кольца, 3=шахматка)", 0, 3, 1, 1, "", "Тип распределения: linear / moons / circles / checkerboard"),
                        new AlgoParam("seed",    "Seed",         0, 200,  42,  1, "", "Начальное значение генератора"),
                    ]),

                new AlgoDef(
                    "nn_cls", "Ближайший эталон (NN)",
                    "Классификация по ближайшему прототипу класса с евклидовой метрикой",
                    "AI.ML.Classification.NN",
                    "nn_cls.md",
                    [
                        new AlgoParam("n",       "Число точек", 40, 400, 120, 10, "шт.", "Суммарный объём выборки"),
                        new AlgoParam("dataset", "Датасет (0=линейный, 1=луны, 2=кольца, 3=шахматка)", 0, 3, 1, 1, "", "Тип распределения"),
                        new AlgoParam("seed",    "Seed",         0, 200,   7,  1, "", "Начальное значение генератора"),
                    ]),

                new AlgoDef(
                    "linear_cls", "Линейный классификатор",
                    "Бинарный линейный классификатор с обучением на основе градиента отступа (margin)",
                    "AI.ML.Classification.LinearClassifierBinarry",
                    "linear_cls.md",
                    [
                        new AlgoParam("n",       "Число точек", 40, 400, 120, 10, "шт.", "Суммарный объём выборки"),
                        new AlgoParam("epochs",  "Эпохи",       5, 200,  40,  5, "", "Число проходов обучения"),
                        new AlgoParam("dataset", "Датасет (0=линейный, 1=луны, 2=кольца, 3=шахматка)", 0, 3, 0, 1, "", "Тип распределения"),
                        new AlgoParam("seed",    "Seed",         0, 200,  42,  1, "", "Начальное значение генератора"),
                    ]),

                new AlgoDef(
                    "svm_binary", "SVM бинарный",
                    "Машина опорных векторов с отбором опорных векторов и градиентным обучением",
                    "AI.ML.Classification.SVMBinary",
                    "svm_binary.md",
                    [
                        new AlgoParam("n",       "Число точек",        40, 400, 120, 10, "шт.", "Суммарный объём выборки"),
                        new AlgoParam("epochs",  "Эпохи",              5, 200,  40,  5, "", "Число проходов обучения"),
                        new AlgoParam("numSv",   "Опорных векторов",   2,  20,   6,  1, "", "Число опорных векторов на итерацию"),
                        new AlgoParam("dataset", "Датасет (0=линейный, 1=луны, 2=кольца, 3=шахматка)", 0, 3, 0, 1, "", "Тип распределения"),
                        new AlgoParam("seed",    "Seed",                0, 200,  42,  1, "", "Начальное значение генератора"),
                    ]),

                new AlgoDef(
                    "corr_cls", "Корреляционный классификатор",
                    "Классификация по максимуму коэффициента корреляции с эталонами классов",
                    "AI.ML.Classification.CorrelationClassifier",
                    "corr_cls.md",
                    [
                        new AlgoParam("n",    "Число точек", 40, 400, 100, 10, "шт.", "Суммарный объём выборки"),
                        new AlgoParam("seed", "Seed",         0, 200,  42,  1, "", "Начальное значение генератора"),
                    ]),
            ]),

        new CategoryDef("regression", "Регрессия",
            "Алгоритмы аппроксимации числовых зависимостей",
            [
                new AlgoDef(
                    "lin_reg", "Линейная регрессия",
                    "Однофакторная линейная модель y = k·x + b",
                    "AI.ML.Regression.LinearRegression",
                    "lin_reg.md",
                    [
                        new AlgoParam("k",     "Наклон k",     -5, 5, 2.0, 0.1, "", "Коэффициент при x"),
                        new AlgoParam("b",     "Смещение b",   -5, 5, 1.0, 0.1, "", "Свободный член"),
                        new AlgoParam("noise", "Шум σ",       0.0, 3, 0.8, 0.1, "", "Стандартное отклонение шума"),
                    ]),

                new AlgoDef(
                    "poly_reg", "Полиномиальная регрессия",
                    "Аппроксимация кривой полиномом произвольной степени",
                    "AI.ML.Regression.PolynomialRegression",
                    "poly_reg.md",
                    [
                        new AlgoParam("deg",   "Степень полинома", 1, 6, 3, 1, "", "Степень аппроксимирующего полинома"),
                        new AlgoParam("noise", "Шум σ",          0.0, 2, 0.4, 0.05, "", "Стандартное отклонение шума"),
                    ]),

                new AlgoDef(
                    "multiple_reg", "Множественная регрессия",
                    "Многофакторная линейная модель y = w₁x₁ + w₂x₂ + w₃x₃ + b",
                    "AI.ML.Regression.MultipleRegression",
                    "multiple_reg.md",
                    [
                        new AlgoParam("n",     "Число точек", 30, 300, 80, 10, "шт.", "Объём обучающей выборки"),
                        new AlgoParam("noise", "Шум σ",      0.0,  5, 1.5, 0.1, "", "Стандартное отклонение шума"),
                    ]),

                new AlgoDef(
                    "multiple_reg_3d", "Множественная регрессия 3D",
                    "3D-поверхность y = 3x₁ + 2x₂ при x₃ = 0 с обучающими точками",
                    "AI.ML.Regression.MultipleRegression",
                    "multiple_reg.md",
                    [
                        new AlgoParam("n",         "Число точек",      30, 300, 80, 10, "шт.", "Объём обучающей выборки"),
                        new AlgoParam("noise",     "Шум σ",           0.0,  5, 1.5, 0.1, "", "Стандартное отклонение шума"),
                        new AlgoParam("azimuth",   "Азимут камеры",  -180, 180, -35, 5, "°", "Горизонтальный угол обзора"),
                        new AlgoParam("elevation", "Элевация камеры", -90,  90,  25, 5, "°", "Вертикальный угол обзора"),
                    ]),
            ]),

        new CategoryDef("dim_reduction", "Снижение размерности",
            "Преобразование признакового пространства для визуализации и компрессии",
            [
                new AlgoDef(
                    "pca_2d", "PCA",
                    "Метод главных компонент — проекция на направления наибольшей дисперсии",
                    "AI.ML.DataHandling.FeaturesTransforms.PCA",
                    "pca.md",
                    [
                        new AlgoParam("n",     "Число точек", 30, 300, 120, 10, "шт.", "Объём выборки"),
                        new AlgoParam("angle", "Угол данных", 0, 90, 35, 5, "°", "Угол вытяжения облака точек"),
                    ]),

                new AlgoDef(
                    "pca_3d", "PCA 3D",
                    "Главные компоненты в трёхмерном пространстве — облако точек и направление PC1",
                    "AI.ML.DataHandling.FeaturesTransforms.PCA",
                    "pca.md",
                    [
                        new AlgoParam("n",         "Число точек",      30, 300, 120, 10, "шт.", "Объём выборки"),
                        new AlgoParam("azimuth",   "Азимут камеры",  -180, 180, -35, 5, "°", "Горизонтальный угол обзора"),
                        new AlgoParam("elevation", "Элевация камеры", -90,  90,  25, 5, "°", "Вертикальный угол обзора"),
                    ]),
            ]),

        new CategoryDef("sequence", "Анализ рядов",
            "Прогнозирование и моделирование временных рядов и последовательностей",
            [
                new AlgoDef(
                    "ar_predict", "AR-прогноз",
                    "Авторегрессионная модель для предсказания временного ряда",
                    "AI.ML.SequenceAnalysis.SeqPredict.AR",
                    "ar_predict.md",
                    [
                        new AlgoParam("trainLen", "Длина обучения",      20, 200, 80,  5, "шт.", "Количество точек для обучения"),
                        new AlgoParam("predLen",  "Горизонт прогноза",    5,  60, 30,  5, "шт.", "Сколько точек вперёд предсказать"),
                        new AlgoParam("window",   "Окно AR",              2,  15,  5,  1, "", "Порядок авторегрессионной модели"),
                        new AlgoParam("freq",     "Частота ряда",      0.05, 0.4, 0.15, 0.01, "Гц", "Базовая частота синусоиды"),
                    ]),
            ]),

        new CategoryDef("genetic", "Генетические алгоритмы",
            "Эволюционная оптимизация параметров на основе отбора, скрещивания и мутации",
            [
                new AlgoDef(
                    "genetic", "Кривая сходимости",
                    "Эволюция лучшего MSE по эпохам при поиске параметров квадратичной функции",
                    "AI.ML.Genetic.GeneticCore.Population",
                    "genetic.md",
                    [
                        new AlgoParam("popSize",  "Размер популяции", 10, 100, 30, 5, "", "Число особей в популяции"),
                        new AlgoParam("epochs",   "Эпохи",            10, 200, 60, 5, "", "Число поколений"),
                        new AlgoParam("mutProb",  "Вероятность мутации", 0.01, 0.9, 0.25, 0.05, "", "Вероятность мутации особи"),
                    ]),

                new AlgoDef(
                    "genetic_fit", "Подбор параметров",
                    "Аппроксимация целевой функции y = 2x² − x + 0.5 через эволюционный подбор коэффициентов",
                    "AI.ML.Genetic.GeneticCore.Population",
                    "genetic.md",
                    [
                        new AlgoParam("popSize", "Размер популяции", 10, 100, 30, 5, "", "Число особей в популяции"),
                        new AlgoParam("epochs",  "Эпохи",            10, 200, 80, 5, "", "Число поколений"),
                    ]),

                new AlgoDef(
                    "genetic_landscape", "3D ландшафт потерь",
                    "Поверхность log₁₀(MSE) по параметрам (a, b) при фиксированном c = 0.5",
                    "AI.ML.Genetic.GeneticCore.Population",
                    "genetic.md",
                    [
                        new AlgoParam("azimuth",   "Азимут камеры",  -180, 180, -35, 5, "°", "Горизонтальный угол обзора"),
                        new AlgoParam("elevation", "Элевация камеры", -90,  90,  25, 5, "°", "Вертикальный угол обзора"),
                    ]),
            ]),
    ];

    protected override DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string>  textParams,
        DemoSettings settings)
        => MlDemoRunner.Run(algoKey, numericParams, settings);
}
