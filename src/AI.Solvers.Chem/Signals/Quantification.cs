using AI.Solvers.Chem.Metrology;

namespace AI.Solvers.Chem.Signals;

/// <summary>
/// Количественный расчёт по площадям пиков: внешний стандарт, внутренний стандарт,
/// метод добавок и нормировка
/// </summary>
public static class Quantification
{
    /// <summary>
    /// Внешний стандарт: концентрация по градуировке с доверительным интервалом
    /// </summary>
    /// <param name="area">Площадь пика пробы</param>
    /// <param name="calibration">Градуировочная характеристика</param>
    /// <param name="replicates">Число повторных вводов пробы</param>
    /// <param name="confidence">Доверительная вероятность</param>
    public static ConcentrationEstimate ExternalStandard(double area, AnalyticalCalibration calibration,
        int replicates = 1, double confidence = 0.95)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        return calibration.Concentration(area, replicates, confidence);
    }

    /// <summary>
    /// Относительный коэффициент чувствительности по калибровочной смеси:
    /// RF = (S_анализируемого/c_анализируемого) / (S_стандарта/c_стандарта)
    /// </summary>
    /// <param name="analyteArea">Площадь пика определяемого вещества</param>
    /// <param name="analyteConcentration">Его концентрация в смеси</param>
    /// <param name="standardArea">Площадь пика внутреннего стандарта</param>
    /// <param name="standardConcentration">Концентрация внутреннего стандарта</param>
    public static double ResponseFactor(double analyteArea, double analyteConcentration,
        double standardArea, double standardConcentration)
    {
        if (analyteConcentration <= 0 || standardConcentration <= 0)
            throw new ArgumentException("Concentrations must be positive");

        if (standardArea <= 0)
            throw new ArgumentException("Internal standard area must be positive");

        return (analyteArea / analyteConcentration) / (standardArea / standardConcentration);
    }

    /// <summary>
    /// Внутренний стандарт: c = (S_пробы / S_стандарта) · c_стандарта / RF
    /// </summary>
    /// <param name="analyteArea">Площадь пика определяемого вещества</param>
    /// <param name="standardArea">Площадь пика внутреннего стандарта</param>
    /// <param name="standardConcentration">Концентрация внутреннего стандарта в пробе</param>
    /// <param name="responseFactor">Относительный коэффициент чувствительности</param>
    public static double InternalStandard(double analyteArea, double standardArea,
        double standardConcentration, double responseFactor = 1.0)
    {
        if (standardArea <= 0)
            throw new ArgumentException("Internal standard area must be positive");

        if (responseFactor <= 0)
            throw new ArgumentException("Response factor must be positive");

        return analyteArea / standardArea * standardConcentration / responseFactor;
    }

    /// <summary>
    /// Метод добавок: концентрация в исходной пробе равна модулю точки пересечения
    /// градуировки с осью абсцисс, c = |a/b|
    /// </summary>
    /// <param name="addedConcentrations">Концентрации добавок (первая обычно 0)</param>
    /// <param name="signals">Отклики (площади) после каждой добавки</param>
    /// <param name="confidence">Доверительная вероятность</param>
    public static ConcentrationEstimate StandardAddition(double[] addedConcentrations, double[] signals,
        double confidence = 0.95)
    {
        var fit = LinearFit.Fit(addedConcentrations, signals);

        if (Math.Abs(fit.Slope) < 1e-15)
            throw new InvalidOperationException("Zero slope: the standard addition line is undefined");

        double value = Math.Abs(fit.Intercept / fit.Slope);
        double meanY = fit.Y.Average();

        // Неопределённость точки пересечения с осью x (экстраполяция градуировки)
        double variance = (1.0 / fit.PointCount) + (meanY * meanY / (fit.Slope * fit.Slope * fit.Sxx));
        double uncertainty = fit.ResidualStd / Math.Abs(fit.Slope) * Math.Sqrt(variance);
        double delta = fit.TValue(confidence) * uncertainty;

        return new ConcentrationEstimate(value, uncertainty, value - delta, value + delta, confidence, true);
    }

    /// <summary>
    /// Нормировка по площадям: доля каждого компонента в сумме,
    /// при необходимости с поправкой на коэффициенты чувствительности
    /// </summary>
    /// <param name="peaks">Пики</param>
    /// <param name="responseFactors">Коэффициенты чувствительности по именам пиков</param>
    public static IReadOnlyList<(Peak Peak, double Percent)> AreaNormalization(
        IReadOnlyList<Peak> peaks, IReadOnlyDictionary<string, double> responseFactors = null)
    {
        ArgumentNullException.ThrowIfNull(peaks);

        var corrected = new double[peaks.Count];

        for (int i = 0; i < peaks.Count; i++)
        {
            double factor = 1.0;

            if (responseFactors != null && !string.IsNullOrEmpty(peaks[i].Name)
                && responseFactors.TryGetValue(peaks[i].Name, out double value) && value > 0)
            {
                factor = value;
            }

            corrected[i] = peaks[i].Area / factor;
        }

        double total = corrected.Sum();
        var result = new List<(Peak, double)>(peaks.Count);

        for (int i = 0; i < peaks.Count; i++)
            result.Add((peaks[i], total > 0 ? 100.0 * corrected[i] / total : 0));

        return result;
    }

    /// <summary>
    /// Массовая доля компонента в пробе, %: из найденной концентрации,
    /// объёма пробы и навески
    /// </summary>
    /// <param name="concentration">Концентрация в анализируемом растворе, мг/л</param>
    /// <param name="volumeMl">Объём раствора, мл</param>
    /// <param name="sampleMassMg">Навеска пробы, мг</param>
    /// <param name="dilutionFactor">Кратность разбавления</param>
    public static double MassFractionPercent(double concentration, double volumeMl, double sampleMassMg,
        double dilutionFactor = 1.0)
    {
        if (sampleMassMg <= 0)
            throw new ArgumentException("Sample mass must be positive");

        double analyteMassMg = concentration * (volumeMl / 1000.0) * dilutionFactor;

        return 100.0 * analyteMassMg / sampleMassMg;
    }
}
