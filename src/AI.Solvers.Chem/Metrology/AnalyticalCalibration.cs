using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Metrology;

/// <summary>
/// Оценка концентрации по калибровке вместе с неопределённостью
/// </summary>
/// <param name="Value">Найденная концентрация</param>
/// <param name="StandardUncertainty">Стандартная неопределённость (s_x0)</param>
/// <param name="Lower">Нижняя граница доверительного интервала</param>
/// <param name="Upper">Верхняя граница доверительного интервала</param>
/// <param name="Confidence">Доверительная вероятность</param>
/// <param name="WithinRange">Лежит ли отклик внутри диапазона калибровки</param>
public readonly record struct ConcentrationEstimate(
    double Value,
    double StandardUncertainty,
    double Lower,
    double Upper,
    double Confidence,
    bool WithinRange)
{
    /// <summary>Относительная неопределённость, %</summary>
    public double RelativeUncertaintyPercent => Value == 0 ? double.NaN : 100.0 * StandardUncertainty / Math.Abs(Value);

    /// <summary>Текстовое представление результата</summary>
    public override string ToString()
        => $"{Value:G6} ± {(Upper - Lower) / 2:G4} (P = {Confidence:P0})";
}

/// <summary>
/// Градуировочная характеристика методики: обратный расчёт концентрации с
/// доверительным интервалом, пределы обнаружения и определения, проверка линейности.
/// </summary>
/// <remarks>
/// Пределы считаются по остаточному разбросу градуировки (подход ICH Q2):
/// LOD = 3.3·σ/S, LOQ = 10·σ/S. Если есть измерения холостой пробы,
/// используйте <see cref="DetectionLimits"/>.
/// </remarks>
public sealed class AnalyticalCalibration
{
    /// <summary>Линейная модель отклика</summary>
    public LinearFit Fit { get; }

    /// <summary>Чувствительность методики (наклон)</summary>
    public double Sensitivity => Fit.Slope;

    /// <summary>Нижняя граница диапазона калибровки</summary>
    public double RangeMin { get; }

    /// <summary>Верхняя граница диапазона калибровки</summary>
    public double RangeMax { get; }

    /// <summary>Предел обнаружения (LOD), 3.3·σ/S</summary>
    public double DetectionLimit => 3.3 * Fit.ResidualStd / Math.Abs(Fit.Slope);

    /// <summary>Предел количественного определения (LOQ), 10·σ/S</summary>
    public double QuantitationLimit => 10.0 * Fit.ResidualStd / Math.Abs(Fit.Slope);

    /// <summary>Строит градуировочную характеристику</summary>
    /// <param name="concentrations">Концентрации стандартов</param>
    /// <param name="signals">Отклики прибора</param>
    /// <param name="weighting">Схема взвешивания</param>
    public AnalyticalCalibration(double[] concentrations, double[] signals,
        WeightingScheme weighting = WeightingScheme.None)
        : this(LinearFit.Fit(concentrations, signals, weighting))
    {
    }

    /// <summary>Строит градуировочную характеристику по готовой модели</summary>
    public AnalyticalCalibration(LinearFit fit)
    {
        Fit = fit ?? throw new ArgumentNullException(nameof(fit));
        RangeMin = fit.X.Min();
        RangeMax = fit.X.Max();
    }

    /// <summary>Отклик, ожидаемый для заданной концентрации</summary>
    public double Signal(double concentration) => Fit.Predict(concentration);

    /// <summary>
    /// Обратный расчёт: концентрация пробы по её отклику.
    /// Неопределённость считается по классической формуле s_x0 для обратного предсказания.
    /// </summary>
    /// <param name="signal">Отклик пробы (среднее по повторам)</param>
    /// <param name="replicates">Число повторных измерений пробы</param>
    /// <param name="confidence">Доверительная вероятность</param>
    public ConcentrationEstimate Concentration(double signal, int replicates = 1, double confidence = 0.95)
    {
        if (replicates < 1)
            throw new ArgumentOutOfRangeException(nameof(replicates), "Number of replicates must be positive");

        if (Math.Abs(Fit.Slope) < 1e-15)
            throw new InvalidOperationException("Zero slope: concentration cannot be derived from the signal");

        double value = (signal - Fit.Intercept) / Fit.Slope;

        // Вес точки пробы: при взвешенной градуировке он входит в первый член
        double sampleWeight = SampleWeight(value, signal);
        double meanSignal = Fit.Y.Zip(Fit.Weights, (y, w) => y * w).Sum() / Fit.WeightSum;
        double deviation = signal - meanSignal;

        double variance = (1.0 / (replicates * sampleWeight))
            + (1.0 / Fit.WeightSum)
            + (deviation * deviation / (Fit.Slope * Fit.Slope * Fit.Sxx));

        double standardUncertainty = Fit.ResidualStd / Math.Abs(Fit.Slope) * Math.Sqrt(variance);
        double delta = Fit.TValue(confidence) * standardUncertainty;

        return new ConcentrationEstimate(
            value,
            standardUncertainty,
            value - delta,
            value + delta,
            confidence,
            value >= RangeMin && value <= RangeMax);
    }

    /// <summary>
    /// Проверка пригодности градуировки: линейность, значимость свободного члена,
    /// максимальное относительное отклонение точек
    /// </summary>
    /// <param name="minR2">Минимально допустимый R²</param>
    public CalibrationCheck Check(double minR2 = 0.99)
    {
        double maxDeviation = 0;

        for (int i = 0; i < Fit.PointCount; i++)
        {
            if (Math.Abs(Fit.Y[i]) < 1e-15)
                continue;

            maxDeviation = Math.Max(maxDeviation, Math.Abs(Fit.Residuals[i] / Fit.Y[i]) * 100.0);
        }

        return new CalibrationCheck(
            Fit.R2 >= minR2,
            Fit.R2,
            Fit.InterceptIsSignificant(),
            maxDeviation,
            DetectionLimit,
            QuantitationLimit);
    }

    /// <summary>Отчёт по градуировке в человекочитаемом виде</summary>
    public string Report(double confidence = 0.95)
    {
        var text = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;
        var (slopeLow, slopeHigh) = Fit.SlopeInterval(confidence);
        var (interceptLow, interceptHigh) = Fit.InterceptInterval(confidence);

        text.AppendLine("Градуировочная характеристика");
        text.AppendLine($"  Модель: y = {Fit.Intercept.ToString("G6", culture)} + {Fit.Slope.ToString("G6", culture)}·x");
        text.AppendLine($"  Точек: {Fit.PointCount}, диапазон: {RangeMin.ToString("G4", culture)} … {RangeMax.ToString("G4", culture)}");
        text.AppendLine($"  Взвешивание: {Fit.Weighting}");
        text.AppendLine($"  R² = {Fit.R2.ToString("F5", culture)}, Sy/x = {Fit.ResidualStd.ToString("G4", culture)}");
        text.AppendLine($"  Наклон: {Fit.Slope.ToString("G6", culture)} ± {(Fit.TValue(confidence) * Fit.SlopeStdError).ToString("G3", culture)}"
            + $"  [{slopeLow.ToString("G6", culture)}; {slopeHigh.ToString("G6", culture)}]");
        text.AppendLine($"  Свободный член: {Fit.Intercept.ToString("G6", culture)}"
            + $"  [{interceptLow.ToString("G6", culture)}; {interceptHigh.ToString("G6", culture)}]"
            + $" - {(Fit.InterceptIsSignificant(confidence) ? "значим" : "незначим")}");
        text.AppendLine($"  LOD = {DetectionLimit.ToString("G4", culture)}, LOQ = {QuantitationLimit.ToString("G4", culture)}");

        return text.ToString();
    }

    // Вес точки пробы в той же схеме, что и градуировка
    private double SampleWeight(double concentration, double signal) => Fit.Weighting switch
    {
        WeightingScheme.InverseX => concentration > 0 ? 1.0 / concentration : 1.0,
        WeightingScheme.InverseX2 => concentration > 0 ? 1.0 / (concentration * concentration) : 1.0,
        WeightingScheme.InverseY => signal > 0 ? 1.0 / signal : 1.0,
        WeightingScheme.InverseY2 => signal > 0 ? 1.0 / (signal * signal) : 1.0,
        _ => 1.0
    };
}

/// <summary>
/// Итог проверки градуировки
/// </summary>
/// <param name="Linear">Соответствует ли R² требованию</param>
/// <param name="R2">Коэффициент детерминации</param>
/// <param name="InterceptSignificant">Значим ли свободный член</param>
/// <param name="MaxDeviationPercent">Максимальное относительное отклонение точки, %</param>
/// <param name="DetectionLimit">Предел обнаружения</param>
/// <param name="QuantitationLimit">Предел определения</param>
public readonly record struct CalibrationCheck(
    bool Linear,
    double R2,
    bool InterceptSignificant,
    double MaxDeviationPercent,
    double DetectionLimit,
    double QuantitationLimit);
