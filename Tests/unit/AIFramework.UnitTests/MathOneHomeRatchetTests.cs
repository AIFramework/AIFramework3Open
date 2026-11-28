using System.Text.RegularExpressions;
using AIFramework.UnitTests.Dsp;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Храповик математического ядра: у каждого понятия один дом, копии не появляются заново.
/// </summary>
/// <remarks>
/// <para>
/// Каждая проверка считает по тексту <c>src</c> места, где понятие определено, и сравнивает с
/// разрешённым списком: дом понятия и поимённо перечисленные остатки — адаптеры без своей логики
/// под старыми публичными именами и случаи с записанной причиной. Новый файл или лишнее
/// определение в старом роняет тест. Число только уменьшается: когда адаптер удаляют, его строка
/// уходит из списка — тест напомнит об этом сам.
/// </para>
/// <para>
/// Проверяется текст, а не поведение: вторая реализация логарифма гамма-функции собирается и
/// проходит свои тесты, просто однажды начинает отвечать иначе, чем первая.
/// </para>
/// </remarks>
public sealed partial class MathOneHomeRatchetTests
{
    private static readonly Lazy<IReadOnlyList<(string Path, string Code)>> s_sources = new(LoadSources);

    // --- спецфункции ---

    /// <summary>
    /// Определения гамма-функции, её логарифма, неполных гамма- и бета-функций, функции ошибок и факториала.
    /// </summary>
    private static readonly Dictionary<string, int> s_specialFunctions = new(StringComparer.Ordinal)
    {
        // Дом: Gamma, LogGamma, LogBeta, RegularizedGammaP/Q, RegularizedBeta, Erf, Erfc, Factorial, LogFactorial
        // и их закрытые части (GammaPrefactor, GammaSeries, GammaFraction, BetaDirect, BetaFraction, LogGammaNearOne)
        ["src/AI/HighLevelFunctions/SpecialFunctions.cs"] = 16,
        // [Obsolete]-адаптеры Gamma, LogGamma, Erf, Erfc, Factorial и поэлементные Erf(Vector), Factorial(Vector)
        ["src/AI/HighLevelFunctions/FunctionsForEachElements.cs"] = 7,
        // [Obsolete]-адаптеры Erf, Erfc, LogGamma
        ["src/AI/Statistics/StatInference.cs"] = 3,
        // [Obsolete]-адаптер Factorial во вставленном NWaves
        ["src/AI.DSP/BackEnds/DSP/NWaves/Utils/MathUtils.cs"] = 1,
        // Привязка math.factorial к канону: имя функции скрипта, а не реализация
        ["src/AI.Script.Std/MathModule.cs"] = 1,
        // Узлы символьного выражения erf и erfc: конструкторы типов, значение считает канон
        ["src/AI.Solvers.Math/Core/Functions/Erf.cs"] = 1,
        ["src/AI.Solvers.Math/Core/Functions/Erfc.cs"] = 1,
        // Γ(k) в алгоритме Ремеза — множитель полинома Лагранжа, с гамма-функцией совпадает только именем
        ["src/AI.DSP/BackEnds/DSP/NWaves/Filters/Fda/Remez.cs"] = 1,
    };

    [Fact]
    public void SpecialFunctions_AreDefinedInOneHome() =>
        AssertRatchet("спецфункции — в AI.HighLevelFunctions.SpecialFunctions", SpecialFunctionDefinition(), s_specialFunctions);

    /// <summary>
    /// Приметы самостоятельной реализации: коэффициенты Ланцоша и формулы Абрамовица — Стиган для erf.
    /// </summary>
    [Fact]
    public void SpecialFunctions_HaveNoHandRolledApproximations() =>
        AssertRatchet("приближения гамма-функции и erf вне SpecialFunctions", HandRolledApproximation(), new Dictionary<string, int>(StringComparer.Ordinal));

    // --- физические и геоконстанты ---

    /// <summary>
    /// Числа фундаментальных констант и параметров Земли вне таблиц AI.Units.
    /// </summary>
    private static readonly Dictionary<string, int> s_constantLiterals = new(StringComparer.Ordinal)
    {
        // Таблицы: PhysicalConstants (Values и неопределённости), EarthConstants, определения единиц в Si
        ["src/AI/Units/PhysicalConstants.cs"] = 13,
        ["src/AI/Units/EarthConstants.cs"] = 5,
        ["src/AI/Units/Si.cs"] = 2,
        // Стандартная атмосфера 1976 года: R* = 8.31432 — число стандарта, а не CODATA;
        // с другим значением давления расходятся с таблицами стандарта
        ["src/AI.Physics/Fluids/StandardAtmosphere.cs"] = 1,
        // Рекомендация МСЭ-R P.1812: радиус Земли 6371 км входит в её формулы
        ["src/AI.Microwave/Propagation/ItuP1812.cs"] = 1,
        // Примеры вызова в справке AI-скрипта: числа пишет человек в своём скрипте
        ["src/AI.Script.Science/PhysModule.cs"] = 1,
        ["src/AI.Script.Science/PhysModule.Matter.cs"] = 1,
    };

    [Fact]
    public void PhysicalConstants_AreWrittenOnce() =>
        AssertRatchet("числа физических и геоконстант — в AI.Units.PhysicalConstants и EarthConstants", ConstantLiteral(), s_constantLiterals);

    // --- описательная статистика, ГСЧ, расстояния ---

    private const string Definition = @"\b(?:public|private|internal|protected)\s+(?:static\s+)?(?:override\s+)?[\w<>\[\],.? ]*?\b(?:";

    /// <summary>Дисперсия и среднеквадратичное отклонение.</summary>
    private static readonly Dictionary<string, int> s_variance = new(StringComparer.Ordinal)
    {
        // Дом: Statistic.CalcVariance / CalcStd поверх StatUtils.MeanAndVariance (два прохода с поправкой;
        // он возвращает кортеж, и шаблон определения его не считает)
        ["src/AI/Statistics/Statistic.Descriptive.cs"] = 6,
        // Обёртки над каноном под привычными именами типов и расширений массивов
        ["src/AI/DataStructs/Algebraic/Vector.Statistics.cs"] = 2,
        ["src/AI/DataStructs/Algebraic/Vector.Factory.cs"] = 1,
        ["src/AI/DataStructs/Algebraic/Matrix.Methods.cs"] = 4,
        ["src/AI/Extensions/StatisticsExtensions.cs"] = 14,
        // Tensor: две обёртки и две функции «вокруг заданного среднего» — центр подаёт вызывающий
        ["src/AI/DataStructs/Algebraic/Tensor.Methods.cs"] = 4,
        // Взвешенная дисперсия — другая формула (веса), а не копия
        ["src/AI/Statistics/WeightedStatistics.cs"] = 1,
        // Привязки функций скрипта stat.std, stat.var, mat.std и расчёт в exp.compare — вызывают канон
        ["src/AI.Script.Std/StatModule.cs"] = 2,
        ["src/AI.Script.Std/MatModule.cs"] = 1,
        ["src/AI.Script.Std/ExpModule.Compare.cs"] = 1,
        // Канон плюс правило края своей задачи (нижняя граница масштаба), одной строкой
        ["src/AI.Econometrics/StateSpace.cs"] = 1,
        ["src/AI.Economics/Corporate/Comparables.cs"] = 1,
        // Совпадение имени: ts.var — векторная авторегрессия, Var — имя переменной в символьной
        // библиотеке, Variance(time) — дисперсия процесса Орнштейна — Уленбека по формуле,
        // Spread — разброс задержек лучей и разброс частот волновода
        ["src/AI.Script.Std/TsModule.cs"] = 1,
        ["src/AI.Solvers.Math/Libs/SymbolicLib.cs"] = 1,
        ["src/AI.Simulation/Stochastic/OrnsteinUhlenbeckProcess.cs"] = 1,
        ["src/AI.Microwave/Propagation/MultipathChannel.cs"] = 1,
        ["src/AI.Microwave/Models/RectangularWaveguide.cs"] = 1,
    };

    [Fact]
    public void Variance_IsDefinedInOneHome() => AssertRatchet(
        "дисперсия и СКО — Statistic.CalcVariance / StatUtils.MeanAndVariance",
        new Regex(Definition + "Variance|Var|Std|StdDev|StandardDeviation|Dispersion|Disp|SampleStd|SampleVariance|CalcVariance|CalcStd|MeanAndVariance|Welford|Spread)\\s*\\("),
        s_variance);

    /// <summary>Выборочный квантиль и медиана.</summary>
    private static readonly Dictionary<string, int> s_quantile = new(StringComparer.Ordinal)
    {
        // Дом: Quantile.Of, OfSorted, Median; GetQuantile экземпляра и [Obsolete] FastQuantile — обёртки
        ["src/AI/Statistics/Quantile.cs"] = 5,
        // Взвешенный квантиль — другая формула (обращение взвешенной функции распределения)
        ["src/AI/Statistics/WeightedStatistics.cs"] = 1,
        ["src/AI.Microwave/Coverage/CoverageResults.cs"] = 1,
        // Квантиль модели (смеси гауссиан), а не выборки
        ["src/AI/Statistics/MixtureModeling/GaussianMixture.cs"] = 1,
        // Привязки stat.quantile, stat.median, расчёт exp.compare, медиана с правилом пустого входа — вызывают канон
        ["src/AI.Script.Std/StatModule.cs"] = 2,
        ["src/AI.Script.Std/ExpModule.Compare.cs"] = 1,
        ["src/AI.Economics/Marketing/MarketingMixModel.cs"] = 1,
        // Совпадение имени: квантильная регрессия и медианный фильтр изображения (окно 3×3 и скользящее)
        ["src/AI.Script.Std/RegressModule.cs"] = 1,
        ["src/AI.Script.Vision/CvModule.cs"] = 1,
        ["src/AI.ComputerVision/ComputerVision/Filters.cs"] = 1,
        ["src/AI.ImageEditor/Filters/Convolution.cs"] = 1,
    };

    [Fact]
    public void Quantile_IsDefinedInOneHome() => AssertRatchet(
        "квантиль и медиана — AI.Statistics.Quantile",
        new Regex(Definition + "Quantile|Median|Percentile|FastQuantile|GetQuantile|OfSorted|MedianInPlace|Median9)\\s*\\("),
        s_quantile);

    /// <summary>Ковариация, корреляция Пирсона, асимметрия, эксцесс, ранги, сумма квадратов остатков.</summary>
    private static readonly Dictionary<string, int> s_moments = new(StringComparer.Ordinal)
    {
        // Дом: Cov ×3, Pearson, CorrelationCoefficient ×2, Skewness, ExcessKurtosis, [Obsolete] Asymmetry и Excess, AverageRanks
        ["src/AI/Statistics/Statistic.Descriptive.cs"] = 11,
        // Ковариационная матрица столбцов — элементы считает Statistic.Cov; сжатие к диагонали поверх неё
        ["src/AI.Econometrics/Numerics/LinearAlgebra.cs"] = 1,
        ["src/AI.Economics/Portfolio/MeanVariance.cs"] = 1,
        // Привязка stat.corr
        ["src/AI.Script.Std/StatModule.cs"] = 1,
        // Совпадение имени: корреляционная функция случайного поля и корреляция фаз лучей — формулы моделей
        ["src/AI/Statistics/SpatialRandomField.cs"] = 1,
        ["src/AI.Microwave/Propagation/MultipathChannel.cs"] = 1,
        // Сумма квадратов остатков — одна на эконометрику
        ["src/AI.Econometrics/Diagnostics.cs"] = 1,
    };

    [Fact]
    public void CovarianceCorrelationMomentsRanks_AreDefinedInOneHome() => AssertRatchet(
        "ковариация, Пирсон, асимметрия, эксцесс, ранги, RSS — AI.Statistics.Statistic",
        new Regex(Definition + "Cov|Covariance|Pearson|CorrelationCoefficient|Correlation|Skewness|Kurtosis|ExcessKurtosis|Asymmetry|Excess|Ranks|AverageRanks|RankData|Rss)\\s*\\("),
        s_moments);

    /// <summary>Z-нормировка и min-max.</summary>
    private static readonly Dictionary<string, int> s_scaling = new(StringComparer.Ordinal)
    {
        // Дом: Statistic.ZScores и MinMaxScale
        ["src/AI/Statistics/Statistic.Descriptive.cs"] = 2,
        // Обёртки над каноном: Vector.ZNormalise, Vector.Minimax, Vector.ScaleData, Matrix.Minimax
        ["src/AI/DataStructs/Algebraic/Vector.Statistics.cs"] = 2,
        ["src/AI/DataStructs/Algebraic/Vector.Factory.cs"] = 1,
        ["src/AI/DataStructs/Algebraic/Matrix.Methods.cs"] = 1,
        // Привязки stat.zscore/minmax, mat.zscore/minmax; prep.zscore/minmax запоминают среднее и отклонение из канона
        ["src/AI.Script.Std/StatModule.cs"] = 2,
        ["src/AI.Script.Std/MatModule.cs"] = 2,
        ["src/AI.Script.Std/PrepModule.cs"] = 2,
        // Сохранённые масштабы моделей: отклонение из канона, применяются к новым данным
        ["src/AI.Economics/Statements/BankruptcyPredictor.cs"] = 1,
        ["src/AI.ML/DataHandling/DataSets/VectorDataset.cs"] = 1,
        // Перевод сырого балла в z по заданным норме и отклонению — формула шкалы, не нормировка выборки
        ["src/AI.Psychology/Psychometrics/StandardScores.cs"] = 1,
    };

    [Fact]
    public void Scaling_IsDefinedInOneHome() => AssertRatchet(
        "z-нормировка и min-max — Statistic.ZScores и Statistic.MinMaxScale",
        new Regex(Definition + "ZScore|ZScores|ZNormalise|ZNormalize|Standardize|MinMax|MinMaxScale|Minimax|ScaleData)\\s*\\("),
        s_scaling);

    /// <summary>Формула Бокса — Мюллера: корень из −2·ln.</summary>
    private static readonly Dictionary<string, int> s_gaussian = new(StringComparer.Ordinal)
    {
        // Дом: NextGaussian (полярный метод) и рэлеевская величина рядом
        ["src/AI/Statistics/RandomEngine.cs"] = 2,
        // Хвост приближения Акклама для нормального квантиля — та же запись, другая функция
        ["src/AI/Statistics/StatInference.cs"] = 1,
    };

    [Fact]
    public void GaussianGenerator_IsOne() => AssertRatchet(
        "нормальный генератор — RandomEngine.NextGaussian", new Regex(@"Math\.Sqrt\(-2(?:\.0)? \* Math\.Log\("), s_gaussian);

    /// <summary>Евклидово расстояние и косинусная мера.</summary>
    private static readonly Dictionary<string, int> s_distance = new(StringComparer.Ordinal)
    {
        // Дом: EuclideanDistance ×2, EuclideanDistanceC, SquaredEuclidean ×2, SquareEucl ×2, CosineSimilarity ×2, NegativeCosine, [Obsolete] CosDist
        ["src/AI/Distances/BaseDist.cs"] = 11,
        // [Obsolete]-адаптер DistanceFromAToB
        ["src/AI/HighLevelFunctions/AnalyticGeometryFunctions.cs"] = 1,
        // Канон плюс своё правило несравнимых векторов (поиск по вложениям: близость 0)
        ["src/AI.LLM/Agents/Memory/VectorMemory.cs"] = 1,
        ["src/AI.Script.Llm/Embeddings.cs"] = 1,
        // Тензорная функция потерь с автоградом — другая вычислительная цель
        ["src/AI.NeuralNetworks/V2/Losses/EmbeddingLosses.cs"] = 1,
    };

    [Fact]
    public void Distances_AreDefinedInOneHome() => AssertRatchet(
        "евклидово расстояние и косинус — AI.Distances.BaseDist",
        new Regex(Definition + "EuclideanDistance|EuclideanDistanceC|SquareEucl|SquaredEuclidean|SqDist|SquaredDistance|CosineSimilarity|Cosine|CosDist|NegativeCosine|DistanceFromAToB)\\s*\\("),
        s_distance);

    /// <summary>Гистограмма.</summary>
    private static readonly Dictionary<string, int> s_histogram = new(StringComparer.Ordinal)
    {
        // Дом: Statistic.HistogramCounts (возвращает кортеж, шаблон его не считает); здесь — Histogramm
        // экземпляра, который нормирует его счётчики
        ["src/AI/Statistics/Statistic.Descriptive.cs"] = 1,
        // Тип результата Histogramm (конструкторы)
        ["src/AI/Statistics/Hist.cs"] = 2,
        // Привязки stat.hist и plot.hist — вызывают канон; cv.histogram — гистограмма яркости изображения по 256 уровням
        ["src/AI.Script.Std/StatModule.cs"] = 1,
        ["src/AI.Script.Charts/PlotModule.cs"] = 1,
        ["src/AI.Script.Vision/CvModule.cs"] = 1,
    };

    [Fact]
    public void Histogram_IsDefinedInOneHome() => AssertRatchet(
        "гистограмма — Statistic.HistogramCounts",
        new Regex(Definition + "Histogram|Histogramm|HistogramCounts)\\s*\\("),
        s_histogram);

    // --- метрики качества ---

    /// <summary>R², MAE, MSE, RMSE, MAPE, MASE, accuracy, псевдо-R² Макфаддена.</summary>
    private static readonly Dictionary<string, int> s_metrics = new(StringComparer.Ordinal)
    {
        // Дом: MAE, MAPE, MSE, RMSE, RSquared, PearsonSquared (по две перегрузки), MASE, McFaddenRSquared,
        // усреднение по набору (MAE, MAPE, MSE, RMSE) и [Obsolete] R2 ×2
        ["src/AI.ClassicMath/AlgorithmAnalysis/MetricsForRegression.cs"] = 20,
        ["src/AI.ClassicMath/AlgorithmAnalysis/MetricsForClassification.cs"] = 1,
        // Привязки stat.rmse, stat.mae, stat.r2, stat.accuracy, stat.mse, stat.mape — вызывают канон
        ["src/AI.Script.Std/StatModule.cs"] = 6,
        // Фасад прогнозных метрик: проверка рядов и вызов канона
        ["src/AI.Economics/Forecasting/ForecastResult.cs"] = 3,
        // Строит подогнанную кривую Басса и отдаёт её в канон
        ["src/AI.Economics/Market/BassDiffusion.cs"] = 1,
        // Тензорная функция потерь с автоградом — другая вычислительная цель
        ["src/AI.NeuralNetworks/V2/Losses/RegressionLosses.cs"] = 1,
    };

    [Fact]
    public void Metrics_AreDefinedInOneHome() => AssertRatchet(
        "метрики качества — AI.ClassicMath.AlgorithmAnalysis.MetricsForRegression / MetricsForClassification",
        new Regex(Definition + "R2|RSquared|PearsonSquared|Rmse|RMSE|Mae|MAE|Mse|MSE|Mape|MAPE|Mase|MASE|Accuracy|McFaddenRSquared|McFaddenR2|ComputeRSquared|TotalSquares)\\s*\\("),
        s_metrics);

    /// <summary>
    /// Деление отрезка пополам: середина, присвоенная переменной. Поиск корня и порога — только
    /// <c>AI.HighLevelFunctions.RootFinder</c>; остальные строки списка — не поиск корня.
    /// </summary>
    private static readonly Dictionary<string, int> s_bisection = new(StringComparer.Ordinal)
    {
        ["src/AI/HighLevelFunctions/RootFinder.cs"] = 1,
        // деление отрезка интегрирования
        ["src/AI.Solvers.Math/Core/Numerics/Quadrature.cs"] = 2,
        // минимум по Бренту шаг в шаг как scipy.optimize.fminbound — другое понятие
        ["src/AI.DSP/IIR/Brent.cs"] = 2,
        // двоичный поиск по упорядоченному массиву
        ["src/AI.Economics/Cohorts/RetentionFitter.cs"] = 1,
        ["src/AI.Script/Syntax/SourceText.cs"] = 1,
        // середина между двумя проекциями классов, не итерация
        ["src/AI.ML/Classification/LinearModelTools/NonIterative.cs"] = 1,
        ["src/AI.ClassicMath/Calculator/Libs/Algebra/EquationLib.cs"] = 1,
    };

    [Fact]
    public void RootSearch_IsDefinedInOneHome() => AssertRatchet(
        "деление пополам для корня или порога — AI.HighLevelFunctions.RootFinder (Brent, Boundary)",
        new Regex(@"(?:mid|middle|center|centre|m)\s*=\s*(?:0\.5\s*\*\s*\(\s*\w+\s*\+\s*\w+\s*\)|\(\s*\w+\s*\+\s*\w+\s*\)\s*(?:/\s*2(?:\.0)?|\*\s*0\.5)|\w+\s*\+\s*\(?\s*\(\s*\w+\s*-\s*\w+\s*\)\s*(?:/\s*2(?:\.0)?|\*\s*0\.5)\s*\)?)\s*;"),
        s_bisection);

    /// <summary>Шаг Рунге — Кутты 4-го порядка: четвёртый коэффициент встречается только в формуле шага.</summary>
    private static readonly Dictionary<string, int> s_rungeKutta = new(StringComparer.Ordinal)
    {
        // скалярный и векторный шаг
        ["src/AI.ClassicMath/MathUtils/ODE/RungeKutta.cs"] = 2,
        // метод Дормана — Принса: другой метод, свои коэффициенты
        ["src/AI.ClassicMath/MathUtils/ODE/DormandPrince.cs"] = 1,
        // переменная с тем же именем в расчёте банка фильтров
        ["src/AI.DSP/BackEnds/DSP/NWaves/Filters/Fda/FilterBanks.Specific.cs"] = 1,
    };

    [Fact]
    public void RungeKuttaStep_IsWrittenOnce() => AssertRatchet(
        "шаг РК4 — AI.MathUtils.ODE.RungeKutta.Step", new Regex(@"\bk4\s*="), s_rungeKutta);

    /// <summary>Разложение Холецкого узнаётся по внутреннему произведению строк множителя.</summary>
    private static readonly Dictionary<string, int> s_cholesky = new(StringComparer.Ordinal)
    {
        ["src/AI/HighLevelFunctions/CholeskyDecomposition.cs"] = 1,
        // проверка L·Lᵀ против исходной матрицы, не разложение
        ["src/AI.Microwave/Propagation/Tr38901Scenario.cs"] = 1,
        // сборка матрицы из собственных векторов
        ["src/AI.ClassicMath/MatrixUtils/Eigen.cs"] = 1,
    };

    [Fact]
    public void Cholesky_IsDefinedInOneHome() => AssertRatchet(
        "разложение Холецкого — AI.HighLevelFunctions.CholeskyDecomposition",
        new Regex(@"(\w+)\[i, k\]\s*\*\s*\1\[j, k\]"), s_cholesky);

    /// <summary>Центральная разность, записанная руками: деление на удвоенный шаг.</summary>
    private static readonly Dictionary<string, int> s_finiteDifference = new(StringComparer.Ordinal)
    {
        // производные поля по узлам сетки — разностная схема, а не производная функции
        ["src/AI.Solvers.Pde/FiniteDifference/LidDrivenCavity.cs"] = 6,
        // проверка автоградиента нейросетей — вне этого шага
        ["src/AI.NeuralNetworks/V2/Autograd/GradCheck.cs"] = 1,
    };

    [Fact]
    public void FiniteDifferences_AreDefinedInOneHome() => AssertRatchet(
        "численная производная — AI.HighLevelFunctions.NumericalDifferentiation",
        new Regex(@"/\s*\(\s*2(?:\.0)?\s*\*\s*(?:h|step|delta|eps|epsilon|dx|width)\s*\)"), s_finiteDifference);

    /// <summary>Численный якобиан и гессиан: определения с такими именами.</summary>
    private static readonly Dictionary<string, int> s_jacobian = new(StringComparer.Ordinal)
    {
        ["src/AI/HighLevelFunctions/NumericalDifferentiation.cs"] = 2,
        // ранг якобиана в результате, не дифференцирование
        ["src/AI.ClassicMath/MathUtils/Nonlinear/LeastSquaresResult.cs"] = 1,
        // сборка якобиана эскиза из блоков ограничений; численный блок зовёт канон
        ["src/AI.Geometry/Constraints/GeometricSketch.Solve.cs"] = 3,
        // ковариация из гессиана: гессиан считает канон
        ["src/AI.Econometrics/LimitedDependent.cs"] = 1,
        // [Obsolete]-адаптер NumericalDerivative
        ["src/AI.Solvers.Math/Core/Solvers/NumericalEquationSolver.cs"] = 1,
        // численные значения символьных выражений (EvaluateNumerical…), не производные
        ["src/AI.Solvers.Math/Core/ExpressionEvaluator.cs"] = 7,
    };

    [Fact]
    public void JacobianAndHessian_AreDefinedInOneHome() => AssertRatchet(
        "численный якобиан и гессиан — AI.HighLevelFunctions.NumericalDifferentiation",
        new Regex(Definition + @"Numerical\w*|\w*Jacobian\w*|\w*Hessian\w*)\s*\("), s_jacobian);

    /// <summary>Левенберг — Марквардт, прогонка и матричная экспонента: по одному дому.</summary>
    private static readonly Dictionary<string, int> s_solvers = new(StringComparer.Ordinal)
    {
        // класс и шаг с демпфированием
        ["src/AI.ClassicMath/MathUtils/Nonlinear/LevenbergMarquardt.cs"] = 2,
        ["src/AI.ClassicMath/MatrixUtils/Tridiagonal.cs"] = 1,
        // [Obsolete]-адаптер SolveTridiagonal
        ["src/AI.Solvers.Pde/Numerics/IterativeSolvers.cs"] = 1,
        ["src/AI.ClassicMath/MatrixUtils/MatrixExponential.cs"] = 1,
    };

    [Fact]
    public void LevenbergMarquardtTridiagonalMatrixExponential_AreDefinedInOneHome() => AssertRatchet(
        "Левенберг — Марквардт, прогонка, матричная экспонента — AI.ClassicMath",
        new Regex(Definition + @"TrySolveStep|DampedStep|LevenbergMarquardt\w*|\w*Tridiagonal\w*|\w*Thomas\w*|MatrixExp\w*|Expm\w*)\s*\("
            + @"|class (?:LevenbergMarquardt|Tridiagonal|MatrixExponential)\b"),
        s_solvers);

    // --- механика храповика ---

    private static void AssertRatchet(string concept, Regex pattern, IReadOnlyDictionary<string, int> allowed)
    {
        var found = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach ((string path, string code) in s_sources.Value)
        {
            int count = pattern.Matches(code).Count;

            if (count > 0) found[path] = count;
        }

        string[] extra = [.. found
            .Where(pair => pair.Value > allowed.GetValueOrDefault(pair.Key))
            .Select(pair => $"{pair.Key}: {pair.Value} (разрешено {allowed.GetValueOrDefault(pair.Key)})")
            .Order(StringComparer.Ordinal)];

        Assert.True(extra.Length == 0, $"Новая копия: {concept}.\n  " + string.Join("\n  ", extra));

        string[] slack = [.. allowed
            .Where(pair => found.GetValueOrDefault(pair.Key) < pair.Value)
            .Select(pair => $"{pair.Key}: стало {found.GetValueOrDefault(pair.Key)}, в списке {pair.Value}")
            .Order(StringComparer.Ordinal)];

        Assert.True(slack.Length == 0, $"Копий стало меньше — уменьшите число в храповике ({concept}).\n  " + string.Join("\n  ", slack));
    }

    /// <summary>Исходники <c>src</c> без комментариев: упоминание в описании — не определение.</summary>
    private static IReadOnlyList<(string Path, string Code)> LoadSources()
    {
        string root = DspGolden.RepositoryRoot;
        var sources = new List<(string, string)>();

        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            string path = Path.GetRelativePath(root, file).Replace('\\', '/');

            if (path.Split('/').Any(part => part is "bin" or "obj" or "bin-light" or "obj-light")) continue;

            sources.Add((path, Comments().Replace(File.ReadAllText(file), string.Empty)));
        }

        return sources;
    }

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"\b(?:public|private|internal|protected)\s+(?:static\s+)?[\w<>\[\],. ]*?\b(?:Gamma|LogGamma|LnGamma|GammaLn|LogBeta|LnBeta|Erf|Erfc|ErfInv|Factorial|LogFactorial|RegularizedGamma[PQ]|IncompleteGamma\w*|GammaPrefactor|GammaSeries|GammaFraction|RegularizedBeta|RegularizedIncompleteBeta|IncompleteBeta\w*|BetaCF|BetaFraction|BetaDirect|LogGammaNearOne)\s*\(")]
    private static partial Regex SpecialFunctionDefinition();

    [GeneratedRegex(@"676\.5203681218851|0\.254829592|1\.061405429|0\.3275911|LanczosCoefficients")]
    private static partial Regex HandRolledApproximation();

    // c, h, e, k, N_A, R, F, σ, g, атмосфера, G, ε₀, μ₀; радиусы и масса Земли, сжатие WGS 84
    [GeneratedRegex(@"(?<![\w.])(?:299_?792_?458|6\.626\d*e-34|1\.602\d*e-19|1\.380\d*e-23|6\.022\d*e\+?23|8\.314\d*|96_?485(?:\.\d+)?|5\.670\d*e-0?8|9\.80665|101_?325(?:\.0)?|6\.67\d*e-11|8\.854\d*e-12|1\.2566\d*e-0?6|6_?371(?:_?\d{3})?(?:\.\d+)?(?:e\d)?|6\.371e6|6_?378_?137(?:\.0)?|6_?378_?245(?:\.0)?|298\.257\d*|5\.972\d*e24|0\.0821\d*|0\.08206\d*|376\.73\d*|4e-7 \* Math\.PI)(?![\w.]*\d)")]
    private static partial Regex ConstantLiteral();
}
