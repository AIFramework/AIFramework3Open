using AI.DataStructs.Algebraic;
using AI.ML.Regression;
using AI.Statistics;

namespace AI.Solvers.Chem.Metrology;

/// <summary>
/// Способ взвешивания точек калибровки. В аналитике разброс сигнала обычно растёт
/// с концентрацией, поэтому равные веса занижают точность в нижней части диапазона.
/// </summary>
public enum WeightingScheme
{
    /// <summary>Равные веса (обычный МНК)</summary>
    None,

    /// <summary>Вес 1/x - компенсирует рост дисперсии пропорционально концентрации</summary>
    InverseX,

    /// <summary>Вес 1/x² - для сильной гетероскедастичности (ICP-MS, ЖХ-МС)</summary>
    InverseX2,

    /// <summary>Вес 1/y</summary>
    InverseY,

    /// <summary>Вес 1/y²</summary>
    InverseY2
}

/// <summary>
/// Линейная зависимость y = a + b·x, найденная методом наименьших квадратов,
/// вместе со статистикой, необходимой для метрологии: стандартные ошибки
/// коэффициентов, остаточное СКО (Sy/x), доверительные интервалы.
/// </summary>
/// <remarks>
/// Коэффициенты для невзвешенного случая берутся из регрессии фреймворка
/// (<see cref="LinearRegression"/>); взвешенный случай считается здесь, поскольку
/// весов регрессия фреймворка не поддерживает.
/// </remarks>
public sealed class LinearFit
{
    /// <summary>Наклон b</summary>
    public double Slope { get; private init; }

    /// <summary>Свободный член a</summary>
    public double Intercept { get; private init; }

    /// <summary>Стандартная ошибка наклона</summary>
    public double SlopeStdError { get; private init; }

    /// <summary>Стандартная ошибка свободного члена</summary>
    public double InterceptStdError { get; private init; }

    /// <summary>Остаточное стандартное отклонение Sy/x</summary>
    public double ResidualStd { get; private init; }

    /// <summary>Коэффициент детерминации R²</summary>
    public double R2 { get; private init; }

    /// <summary>Число точек</summary>
    public int PointCount { get; private init; }

    /// <summary>Число степеней свободы (n - 2)</summary>
    public int DegreesOfFreedom => PointCount - 2;

    /// <summary>Значения x</summary>
    public double[] X { get; private init; }

    /// <summary>Значения y</summary>
    public double[] Y { get; private init; }

    /// <summary>Веса точек</summary>
    public double[] Weights { get; private init; }

    /// <summary>Остатки y - ŷ</summary>
    public double[] Residuals { get; private init; }

    /// <summary>Взвешенное среднее x</summary>
    public double MeanX { get; private init; }

    /// <summary>Взвешенная сумма квадратов отклонений x</summary>
    public double Sxx { get; private init; }

    /// <summary>Сумма весов (для равных весов равна числу точек)</summary>
    public double WeightSum { get; private init; }

    /// <summary>Применённое взвешивание</summary>
    public WeightingScheme Weighting { get; private init; }

    /// <summary>Значение модели в точке</summary>
    public double Predict(double x) => Intercept + (Slope * x);

    /// <summary>Доверительный интервал наклона</summary>
    /// <param name="confidence">Доверительная вероятность, например 0.95</param>
    public (double Lower, double Upper) SlopeInterval(double confidence = 0.95)
    {
        double delta = TValue(confidence) * SlopeStdError;
        return (Slope - delta, Slope + delta);
    }

    /// <summary>Доверительный интервал свободного члена</summary>
    /// <param name="confidence">Доверительная вероятность</param>
    public (double Lower, double Upper) InterceptInterval(double confidence = 0.95)
    {
        double delta = TValue(confidence) * InterceptStdError;
        return (Intercept - delta, Intercept + delta);
    }

    /// <summary>
    /// Значимо ли отличается свободный член от нуля: если нет, калибровку
    /// можно вести через начало координат
    /// </summary>
    /// <param name="confidence">Доверительная вероятность</param>
    public bool InterceptIsSignificant(double confidence = 0.95)
    {
        var (lower, upper) = InterceptInterval(confidence);
        return lower > 0 || upper < 0;
    }

    /// <summary>Квантиль Стьюдента для двустороннего интервала</summary>
    public double TValue(double confidence = 0.95)
        => StatInference.TQuantile(1 - ((1 - confidence) / 2), Math.Max(1, DegreesOfFreedom));

    /// <summary>
    /// Строит линейную зависимость по точкам
    /// </summary>
    /// <param name="x">Независимая переменная (концентрации)</param>
    /// <param name="y">Отклик (сигнал)</param>
    /// <param name="weighting">Схема взвешивания</param>
    public static LinearFit Fit(double[] x, double[] y, WeightingScheme weighting = WeightingScheme.None)
        => Fit(x, y, BuildWeights(x, y, weighting), weighting);

    /// <summary>
    /// Строит линейную зависимость с явно заданными весами
    /// </summary>
    /// <param name="x">Независимая переменная</param>
    /// <param name="y">Отклик</param>
    /// <param name="weights">Веса точек; null - равные</param>
    public static LinearFit Fit(double[] x, double[] y, double[] weights)
        => Fit(x, y, weights, WeightingScheme.None);

    private static LinearFit Fit(double[] x, double[] y, double[] weights, WeightingScheme weighting)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Length != y.Length)
            throw new ArgumentException("X and Y must have the same length");

        if (x.Length < 3)
            throw new ArgumentException("At least three points are required for a calibration fit");

        int n = x.Length;
        weights ??= Enumerable.Repeat(1.0, n).ToArray();

        if (weights.Length != n)
            throw new ArgumentException("Weights must have the same length as the data");

        double weightSum = weights.Sum();
        double meanX = 0, meanY = 0;

        for (int i = 0; i < n; i++)
        {
            meanX += weights[i] * x[i];
            meanY += weights[i] * y[i];
        }

        meanX /= weightSum;
        meanY /= weightSum;

        double sxx = 0, sxy = 0, syy = 0;

        for (int i = 0; i < n; i++)
        {
            double dx = x[i] - meanX;
            double dy = y[i] - meanY;

            sxx += weights[i] * dx * dx;
            sxy += weights[i] * dx * dy;
            syy += weights[i] * dy * dy;
        }

        if (sxx <= 0)
            throw new ArgumentException("All X values are identical: the slope is undefined");

        double slope, intercept;

        if (weighting == WeightingScheme.None && weights.All(w => Math.Abs(w - 1.0) < 1e-12))
        {
            // Равные веса - обычный МНК фреймворка
            var model = new LinearRegression(new Vector(x), new Vector(y));
            slope = model.Lrm.Slope;
            intercept = model.Lrm.Intercept;
        }
        else
        {
            slope = sxy / sxx;
            intercept = meanY - (slope * meanX);
        }

        var residuals = new double[n];
        double weightedRss = 0;

        for (int i = 0; i < n; i++)
        {
            residuals[i] = y[i] - (intercept + (slope * x[i]));
            weightedRss += weights[i] * residuals[i] * residuals[i];
        }

        double residualStd = Math.Sqrt(weightedRss / (n - 2));

        return new LinearFit
        {
            Slope = slope,
            Intercept = intercept,
            SlopeStdError = residualStd / Math.Sqrt(sxx),
            InterceptStdError = residualStd * Math.Sqrt((1.0 / weightSum) + (meanX * meanX / sxx)),
            ResidualStd = residualStd,
            R2 = syy <= 0 ? 1.0 : 1.0 - (weightedRss / syy),
            PointCount = n,
            X = (double[])x.Clone(),
            Y = (double[])y.Clone(),
            Weights = weights,
            Residuals = residuals,
            MeanX = meanX,
            Sxx = sxx,
            WeightSum = weightSum,
            Weighting = weighting
        };
    }

    private static double[] BuildWeights(double[] x, double[] y, WeightingScheme scheme)
    {
        if (scheme == WeightingScheme.None)
            return null;

        var weights = new double[x.Length];

        for (int i = 0; i < x.Length; i++)
        {
            double basis = scheme switch
            {
                WeightingScheme.InverseX => x[i],
                WeightingScheme.InverseX2 => x[i] * x[i],
                WeightingScheme.InverseY => y[i],
                WeightingScheme.InverseY2 => y[i] * y[i],
                _ => 1.0
            };

            if (basis <= 0)
                throw new ArgumentException($"Weighting {scheme} requires positive values (point {i + 1} is {basis:G})");

            weights[i] = 1.0 / basis;
        }

        return weights;
    }
}
