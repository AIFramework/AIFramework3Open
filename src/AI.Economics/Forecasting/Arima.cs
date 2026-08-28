using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Numerics;

namespace AI.Economics.Forecasting;

/// <summary>Порядок модели ARIMA с сезонной частью и внешними регрессорами.</summary>
public sealed record ArimaOrder
{
    /// <summary>Порядок авторегрессии.</summary>
    public int P { get; init; }

    /// <summary>Порядок обычного дифференцирования.</summary>
    public int D { get; init; }

    /// <summary>Порядок скользящего среднего.</summary>
    public int Q { get; init; }

    /// <summary>Порядок сезонной авторегрессии.</summary>
    public int SeasonalP { get; init; }

    /// <summary>Порядок сезонного дифференцирования.</summary>
    public int SeasonalD { get; init; }

    /// <summary>Порядок сезонного скользящего среднего.</summary>
    public int SeasonalQ { get; init; }

    /// <summary>Длина сезонного цикла; 1 отключает сезонную часть.</summary>
    public int Season { get; init; } = 1;

    /// <summary>Число оцениваемых параметров без учёта свободного члена.</summary>
    public int ParameterCount => P + Q + SeasonalP + SeasonalQ;

    /// <inheritdoc />
    public override string ToString() => Season > 1
        ? $"SARIMAX({P},{D},{Q})({SeasonalP},{SeasonalD},{SeasonalQ})[{Season}]"
        : $"ARIMA({P},{D},{Q})";
}

/// <summary>
/// Модель ARIMA/SARIMAX: авторегрессия и скользящее среднее с сезонной
/// частью и внешними регрессорами.
/// </summary>
/// <remarks>
/// <para>
/// Оценка ведётся условным методом наименьших квадратов: параметры
/// подбираются минимизацией суммы квадратов однодневных ошибок. Метод
/// уступает полному правдоподобию на коротких рядах, но не требует
/// фильтра Калмана и устойчив на данных, с которыми обычно работают
/// в планировании спроса.
/// </para>
/// <para>
/// Внешние регрессоры (цена, промо, праздники) входят как регрессия
/// с ARIMA-ошибками: сначала оценивается линейная связь, затем остатки
/// описываются ARIMA. Это не то же самое, что включить регрессоры внутрь
/// разностного уравнения, но зато коэффициенты сохраняют прямую
/// интерпретацию.
/// </para>
/// <para>
/// Стационарность и обратимость обеспечиваются параметризацией через
/// частные автокорреляции: любые значения оптимизируемых переменных дают
/// корни характеристического многочлена вне единичного круга.
/// </para>
/// </remarks>
public sealed class Arima
{
    private double[] _ar = [];
    private double[] _ma = [];
    private double[] _expandedAr = [];
    private double[] _expandedMa = [];
    private double[] _exogenousBeta = [];
    private double[] _series = [];
    private double[] _residuals = [];
    private double _sigma;
    private double _mean;

    /// <summary>Порядок модели.</summary>
    public ArimaOrder Order { get; private set; } = new();

    /// <summary>Коэффициенты авторегрессии.</summary>
    public Vector AutoRegressive => new(_ar);

    /// <summary>Коэффициенты скользящего среднего.</summary>
    public Vector MovingAverage => new(_ma);

    /// <summary>Коэффициенты внешних регрессоров.</summary>
    public Vector ExogenousCoefficients => new(_exogenousBeta);

    /// <summary>Оценка стандартного отклонения ошибки.</summary>
    public double Sigma => _sigma;

    /// <summary>Обучает модель и строит прогноз.</summary>
    /// <param name="series">Исторический ряд.</param>
    /// <param name="order">Порядок модели.</param>
    /// <param name="horizon">Горизонт прогноза.</param>
    /// <param name="exogenous">
    /// Внешние регрессоры: строки — периоды истории и горизонта,
    /// столбцы — переменные. Длина должна быть равна длине ряда плюс горизонт.
    /// </param>
    /// <param name="confidenceLevel">Уровень доверия интервалов.</param>
    /// <returns>Прогноз с интервалами и диагностикой.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    /// <exception cref="ArgumentException">Ряд слишком короткий или регрессоры не согласованы.</exception>
    public ForecastResult Fit(
        Vector series, ArimaOrder order, int horizon,
        Matrix? exogenous = null, double confidenceLevel = 0.9)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(order);

        int n = series.Count;
        int minimum = (order.P + (order.SeasonalP * order.Season)) + order.D + (order.SeasonalD * order.Season) + 10;
        if (n < minimum)
            throw new ArgumentException($"Для этого порядка нужно минимум {minimum} наблюдений.", nameof(series));
        if (horizon < 1) throw new ArgumentException("Горизонт должен быть положительным.", nameof(horizon));

        Order = order;
        _series = [.. series];

        double[] working = _series;
        double[,]? design = null;

        if (exogenous is not null)
        {
            if (exogenous.Height < n + horizon)
                throw new ArgumentException(
                    "Регрессоры должны покрывать историю и горизонт прогноза.", nameof(exogenous));

            design = new double[n, exogenous.Width + 1];
            for (int i = 0; i < n; i++)
            {
                design[i, 0] = 1.0;
                for (int j = 0; j < exogenous.Width; j++) design[i, j + 1] = exogenous[i, j];
            }

            OlsFit fit = Ols.Fit(design, _series)
                ?? throw new ArgumentException("Матрица регрессоров вырождена.", nameof(exogenous));

            _exogenousBeta = fit.Beta;
            working = fit.Residuals;
        }

        // Без дифференцирования ряд имеет ненулевой уровень, и модель обязана
        // его учесть. Иначе авторегрессия вынуждена воспроизводить уровень
        // сама, и коэффициент уезжает к единице независимо от истинной
        // динамики. Центрирование эквивалентно свободному члену
        // (1 - sum phi) * mean в разностном уравнении
        _mean = order.D == 0 && order.SeasonalD == 0 ? working.Average() : 0;
        if (_mean != 0)
        {
            var centered = new double[working.Length];
            for (int t = 0; t < working.Length; t++) centered[t] = working[t] - _mean;
            working = centered;
        }

        double[] optimum = Estimate(working, order);
        (_ar, _ma) = Split(optimum, order);
        (_expandedAr, _expandedMa) = Expand(_ar, _ma, order);

        _residuals = Errors(working, _expandedAr, _expandedMa);
        int effective = _residuals.Length - Warmup();
        double rss = 0;
        for (int t = Warmup(); t < _residuals.Length; t++) rss += _residuals[t] * _residuals[t];
        _sigma = effective > 0 ? Math.Sqrt(rss / effective) : 0;

        double[] forecast = Forecast(working, horizon);
        double[] psi = PsiWeights(horizon);

        var point = new Vector(horizon);
        var lower = new Vector(horizon);
        var upper = new Vector(horizon);
        double z = EconMath.NormalInv(1 - ((1 - confidenceLevel) / 2));

        double cumulative = 0;
        for (int h = 0; h < horizon; h++)
        {
            cumulative += psi[h] * psi[h];
            double halfWidth = z * _sigma * Math.Sqrt(cumulative);

            double value = forecast[h] + _mean;
            if (exogenous is not null)
            {
                double regression = _exogenousBeta[0];
                for (int j = 0; j < exogenous.Width; j++)
                    regression += _exogenousBeta[j + 1] * exogenous[n + h, j];
                value += regression;
            }

            point[h] = value;
            lower[h] = value - halfWidth;
            upper[h] = value + halfWidth;
        }

        var fitted = new Vector(n);
        for (int t = 0; t < n; t++) fitted[t] = _series[t] - _residuals[t];

        var parameters = new Dictionary<string, double>();
        for (int i = 0; i < _ar.Length; i++) parameters[$"ar{i + 1}"] = _ar[i];
        for (int i = 0; i < _ma.Length; i++) parameters[$"ma{i + 1}"] = _ma[i];

        int k = order.ParameterCount + (exogenous?.Width ?? 0) + 1;
        double aic = effective > 0 ? (effective * Math.Log(rss / effective)) + (2 * k) : double.NaN;

        return new ForecastResult
        {
            Model = order.ToString(),
            PointForecast = point,
            Lower = lower,
            Upper = upper,
            ConfidenceLevel = confidenceLevel,
            Fitted = fitted,
            Residuals = new Vector(_residuals),
            Parameters = parameters,
            Sigma = _sigma,
            Aic = aic,
            SeasonalPeriod = order.Season,
            InSampleMase = InSampleMase(fitted, order.Season),
            ResidualAutocorrelation = Autocorrelation(_residuals, Warmup()),
        };
    }

    /// <summary>
    /// Подбирает порядок модели перебором по сетке с выбором по AIC.
    /// </summary>
    /// <param name="series">Исторический ряд.</param>
    /// <param name="horizon">Горизонт прогноза.</param>
    /// <param name="season">Длина сезонного цикла; 1 отключает сезонную часть.</param>
    /// <param name="maxOrder">Максимальный порядок обычных частей.</param>
    /// <param name="confidenceLevel">Уровень доверия интервалов.</param>
    /// <returns>Прогноз лучшей по AIC модели.</returns>
    /// <remarks>
    /// Порядок дифференцирования выбирается по признаку убывания дисперсии,
    /// а не тестом на единичный корень: на рядах длиной в пару лет тест
    /// почти всегда неинформативен, а сравнение дисперсий устойчиво.
    /// </remarks>
    public static ForecastResult AutoFit(
        Vector series, int horizon, int season = 1, int maxOrder = 2, double confidenceLevel = 0.9)
    {
        ArgumentNullException.ThrowIfNull(series);

        int d = SuggestDifference([.. series], 1, 2);
        int seasonalD = season > 1 ? SuggestDifference([.. series], season, 1) : 0;

        ForecastResult? best = null;
        double bestAic = double.PositiveInfinity;

        for (int p = 0; p <= maxOrder; p++)
        {
            for (int q = 0; q <= maxOrder; q++)
            {
                int seasonalMax = season > 1 ? 1 : 0;
                for (int sp = 0; sp <= seasonalMax; sp++)
                {
                    for (int sq = 0; sq <= seasonalMax; sq++)
                    {
                        if (p + q + sp + sq == 0) continue;

                        var order = new ArimaOrder
                        {
                            P = p, D = d, Q = q,
                            SeasonalP = sp, SeasonalD = seasonalD, SeasonalQ = sq,
                            Season = season,
                        };

                        try
                        {
                            ForecastResult candidate = new Arima().Fit(series, order, horizon, null, confidenceLevel);
                            if (!double.IsNaN(candidate.Aic) && candidate.Aic < bestAic)
                            {
                                bestAic = candidate.Aic;
                                best = candidate;
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Порядок несовместим с длиной ряда — пропускаем
                        }
                    }
                }
            }
        }

        return best ?? throw new ArgumentException("Не удалось подобрать модель для этого ряда.", nameof(series));
    }

    private int Warmup() => Math.Max(_expandedAr.Length, _expandedMa.Length);

    /// <summary>Условный метод наименьших квадратов по преобразованным параметрам.</summary>
    private static double[] Estimate(double[] series, ArimaOrder order)
    {
        int k = order.ParameterCount;
        if (k == 0) return [];

        double Objective(double[] u)
        {
            (double[] ar, double[] ma) = Split(u, order);
            (double[] expandedAr, double[] expandedMa) = Expand(ar, ma, order);
            double[] errors = Errors(series, expandedAr, expandedMa);

            int warmup = Math.Max(expandedAr.Length, expandedMa.Length);
            double sum = 0;
            for (int t = warmup; t < errors.Length; t++) sum += errors[t] * errors[t];
            return double.IsNaN(sum) ? double.PositiveInfinity : sum;
        }

        return NelderMead.Minimize(Objective, new double[k], 0.3, 4000);
    }

    /// <summary>
    /// Преобразование частных автокорреляций в коэффициенты стационарной
    /// авторегрессии. Любая точка пространства оптимизации даёт допустимую
    /// модель, поэтому оптимизатору не нужны ограничения.
    /// </summary>
    private static double[] ToStationary(double[] raw)
    {
        int p = raw.Length;
        if (p == 0) return [];

        var pacf = new double[p];
        for (int i = 0; i < p; i++) pacf[i] = Math.Tanh(raw[i]);

        var phi = new double[p];
        for (int k = 0; k < p; k++)
        {
            var previous = (double[])phi.Clone();
            phi[k] = pacf[k];
            for (int i = 0; i < k; i++) phi[i] = previous[i] - (pacf[k] * previous[k - 1 - i]);
        }

        return phi;
    }

    private static (double[] Ar, double[] Ma) Split(double[] parameters, ArimaOrder order)
    {
        int offset = 0;
        double[] ar = ToStationary(parameters.Skip(offset).Take(order.P).ToArray());
        offset += order.P;
        double[] ma = ToStationary(parameters.Skip(offset).Take(order.Q).ToArray());
        offset += order.Q;
        double[] seasonalAr = ToStationary(parameters.Skip(offset).Take(order.SeasonalP).ToArray());
        offset += order.SeasonalP;
        double[] seasonalMa = ToStationary(parameters.Skip(offset).Take(order.SeasonalQ).ToArray());

        return ([.. ar, .. seasonalAr], [.. ma, .. seasonalMa]);
    }

    /// <summary>
    /// Разворачивает мультипликативную сезонную модель и оператор
    /// дифференцирования в обычные многочлены.
    /// </summary>
    private static (double[] Ar, double[] Ma) Expand(double[] ar, double[] ma, ArimaOrder order)
    {
        double[] regularAr = ar.Take(order.P).ToArray();
        double[] seasonalAr = ar.Skip(order.P).Take(order.SeasonalP).ToArray();
        double[] regularMa = ma.Take(order.Q).ToArray();
        double[] seasonalMa = ma.Skip(order.Q).Take(order.SeasonalQ).ToArray();

        double[] arPolynomial = Multiply(
            ToPolynomial(regularAr, 1, negate: true),
            ToPolynomial(seasonalAr, order.Season, negate: true));

        for (int i = 0; i < order.D; i++)
            arPolynomial = Multiply(arPolynomial, [1, -1]);

        for (int i = 0; i < order.SeasonalD; i++)
        {
            var difference = new double[order.Season + 1];
            difference[0] = 1;
            difference[order.Season] = -1;
            arPolynomial = Multiply(arPolynomial, difference);
        }

        double[] maPolynomial = Multiply(
            ToPolynomial(regularMa, 1, negate: false),
            ToPolynomial(seasonalMa, order.Season, negate: false));

        var expandedAr = new double[arPolynomial.Length - 1];
        for (int i = 1; i < arPolynomial.Length; i++) expandedAr[i - 1] = -arPolynomial[i];

        var expandedMa = new double[maPolynomial.Length - 1];
        for (int i = 1; i < maPolynomial.Length; i++) expandedMa[i - 1] = maPolynomial[i];

        return (expandedAr, expandedMa);
    }

    private static double[] ToPolynomial(double[] coefficients, int lagStep, bool negate)
    {
        var polynomial = new double[(coefficients.Length * lagStep) + 1];
        polynomial[0] = 1;

        for (int i = 0; i < coefficients.Length; i++)
            polynomial[(i + 1) * lagStep] = negate ? -coefficients[i] : coefficients[i];

        return polynomial;
    }

    private static double[] Multiply(double[] a, double[] b)
    {
        var result = new double[a.Length + b.Length - 1];
        for (int i = 0; i < a.Length; i++)
            for (int j = 0; j < b.Length; j++) result[i + j] += a[i] * b[j];
        return result;
    }

    /// <summary>Однодневные ошибки по разностному уравнению модели.</summary>
    private static double[] Errors(double[] series, double[] ar, double[] ma)
    {
        int n = series.Length;
        var errors = new double[n];
        int warmup = Math.Max(ar.Length, ma.Length);

        for (int t = warmup; t < n; t++)
        {
            double prediction = 0;
            for (int i = 0; i < ar.Length; i++) prediction += ar[i] * series[t - 1 - i];
            for (int j = 0; j < ma.Length; j++) prediction += ma[j] * errors[t - 1 - j];
            errors[t] = series[t] - prediction;
        }

        return errors;
    }

    private double[] Forecast(double[] series, int horizon)
    {
        int n = series.Length;
        var extended = new double[n + horizon];
        Array.Copy(series, extended, n);

        var errors = new double[n + horizon];
        Array.Copy(_residuals, errors, n);

        for (int h = 0; h < horizon; h++)
        {
            int t = n + h;
            double prediction = 0;
            for (int i = 0; i < _expandedAr.Length; i++) prediction += _expandedAr[i] * extended[t - 1 - i];
            for (int j = 0; j < _expandedMa.Length; j++)
            {
                int index = t - 1 - j;
                prediction += _expandedMa[j] * (index < n ? errors[index] : 0);
            }
            extended[t] = prediction;
        }

        var forecast = new double[horizon];
        Array.Copy(extended, n, forecast, 0, horizon);
        return forecast;
    }

    /// <summary>Веса представления скользящего среднего — основа интервалов прогноза.</summary>
    private double[] PsiWeights(int horizon)
    {
        var psi = new double[horizon];
        psi[0] = 1;

        for (int j = 1; j < horizon; j++)
        {
            double value = j - 1 < _expandedMa.Length ? _expandedMa[j - 1] : 0;
            for (int i = 0; i < _expandedAr.Length && i < j; i++)
                value += _expandedAr[i] * psi[j - 1 - i];
            psi[j] = value;
        }

        return psi;
    }

    private double InSampleMase(Vector fitted, int season)
    {
        var actual = new Vector(_series);
        int lag = Math.Max(season, 1);
        if (_series.Length <= lag) return double.NaN;

        double scale = 0;
        for (int i = lag; i < _series.Length; i++) scale += Math.Abs(_series[i] - _series[i - lag]);
        scale /= _series.Length - lag;

        int warmup = Warmup();
        double mae = 0;
        int counted = 0;
        for (int t = warmup; t < _series.Length; t++)
        {
            mae += Math.Abs(actual[t] - fitted[t]);
            counted++;
        }

        return counted > 0 && scale > 1e-12 ? mae / counted / scale : double.NaN;
    }

    private static double Autocorrelation(double[] values, int from)
    {
        int n = values.Length;
        if (n - from < 3) return double.NaN;

        double mean = 0;
        for (int t = from; t < n; t++) mean += values[t];
        mean /= n - from;

        double numerator = 0, denominator = 0;
        for (int t = from; t < n; t++)
        {
            double d = values[t] - mean;
            denominator += d * d;
            if (t > from) numerator += d * (values[t - 1] - mean);
        }

        return denominator > 1e-12 ? numerator / denominator : double.NaN;
    }

    /// <summary>
    /// Подбирает порядок дифференцирования: разность берётся, пока она
    /// уменьшает дисперсию ряда.
    /// </summary>
    private static int SuggestDifference(double[] series, int lag, int maxOrder)
    {
        double[] current = series;
        int order = 0;

        for (int i = 0; i < maxOrder; i++)
        {
            if (current.Length <= lag + 2) break;

            var differenced = new double[current.Length - lag];
            for (int t = lag; t < current.Length; t++) differenced[t - lag] = current[t] - current[t - lag];

            if (Variance(differenced) >= Variance(current) * 0.95) break;

            current = differenced;
            order++;
        }

        return order;
    }

    private static double Variance(double[] values)
    {
        if (values.Length < 2) return 0;
        double mean = values.Average();
        double sum = 0;
        foreach (double v in values) sum += (v - mean) * (v - mean);
        return sum / (values.Length - 1);
    }
}
