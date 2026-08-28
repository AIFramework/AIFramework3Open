using AI.Solvers.Chem.Metrology;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Kinetics;

/// <summary>
/// Параметры уравнения Аррениуса с доверительными интервалами
/// </summary>
public sealed class ArrheniusResult
{
    /// <summary>Энергия активации, кДж/моль</summary>
    public double ActivationEnergy { get; init; }

    /// <summary>Стандартная ошибка энергии активации, кДж/моль</summary>
    public double ActivationEnergyError { get; init; }

    /// <summary>Доверительный интервал энергии активации, кДж/моль</summary>
    public (double Lower, double Upper) ActivationEnergyInterval { get; init; }

    /// <summary>Предэкспоненциальный множитель</summary>
    public double PreExponentialFactor { get; init; }

    /// <summary>Доверительный интервал предэкспоненциального множителя</summary>
    public (double Lower, double Upper) PreExponentialInterval { get; init; }

    /// <summary>Коэффициент детерминации линеаризации</summary>
    public double R2 { get; init; }

    /// <summary>Число точек</summary>
    public int PointCount { get; init; }

    /// <summary>Доверительная вероятность</summary>
    public double Confidence { get; init; }

    /// <summary>Константа скорости при заданной температуре</summary>
    /// <param name="temperature">Температура, K</param>
    public double RateConstantAt(double temperature)
        => PreExponentialFactor * Math.Exp(-ActivationEnergy * 1000 / (ArrheniusAnalysis.GasConstant * temperature));

    /// <summary>
    /// Во сколько раз ускорится реакция при нагреве на заданный интервал
    /// (правило Вант-Гоффа в пересчёте на найденную Ea)
    /// </summary>
    /// <param name="temperature">Исходная температура, K</param>
    /// <param name="increase">Прирост температуры, K</param>
    public double AccelerationFactor(double temperature, double increase)
        => RateConstantAt(temperature + increase) / RateConstantAt(temperature);

    /// <summary>Отчёт по анализу</summary>
    public string Report()
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine("Уравнение Аррениуса: k = A · exp(-Ea/RT)");
        text.AppendLine(string.Format(culture, "  Точек: {0}, R2 линеаризации = {1:F4}", PointCount, R2));
        text.AppendLine(string.Format(culture, "  Ea = {0:F1} +- {1:F1} кДж/моль, интервал [{2:F1}; {3:F1}]",
            ActivationEnergy, ActivationEnergyError, ActivationEnergyInterval.Lower, ActivationEnergyInterval.Upper));
        text.AppendLine(string.Format(culture, "  A = {0:E3}, интервал [{1:E2}; {2:E2}]",
            PreExponentialFactor, PreExponentialInterval.Lower, PreExponentialInterval.Upper));
        text.AppendLine(string.Format(culture, "  Ускорение при нагреве на 10 K от 298 K: в {0:F1} раза",
            AccelerationFactor(298.15, 10)));

        return text.ToString();
    }
}

/// <summary>
/// Температурная зависимость константы скорости
/// </summary>
/// <remarks>
/// Линеаризация ln k = ln A - Ea/(R·T) сводит задачу к прямой, поэтому
/// доверительные интервалы Ea и A берутся из интервалов наклона и свободного члена
/// градуировочной регрессии (<see cref="LinearFit"/>), а не оцениваются на глаз.
/// </remarks>
public static class ArrheniusAnalysis
{
    /// <summary>Универсальная газовая постоянная, Дж/(моль·K)</summary>
    public const double GasConstant = 8.314462618;

    /// <summary>
    /// Определяет Ea и A по набору измерений k(T)
    /// </summary>
    /// <param name="temperatures">Температуры, K</param>
    /// <param name="rateConstants">Константы скорости</param>
    /// <param name="confidence">Доверительная вероятность</param>
    public static ArrheniusResult Fit(
        IReadOnlyList<double> temperatures,
        IReadOnlyList<double> rateConstants,
        double confidence = 0.95)
    {
        ArgumentNullException.ThrowIfNull(temperatures);
        ArgumentNullException.ThrowIfNull(rateConstants);

        if (temperatures.Count != rateConstants.Count)
            throw new ArgumentException("Temperature and rate constant series must have the same length");

        if (temperatures.Count < 3)
            throw new ArgumentException("At least three temperatures are required for confidence intervals");

        var inverseTemperature = new double[temperatures.Count];
        var logRate = new double[temperatures.Count];

        for (int i = 0; i < temperatures.Count; i++)
        {
            if (temperatures[i] <= 0)
                throw new ArgumentException("Temperature must be positive (kelvin)");

            if (rateConstants[i] <= 0)
                throw new ArgumentException("Rate constant must be positive");

            inverseTemperature[i] = 1.0 / temperatures[i];
            logRate[i] = Math.Log(rateConstants[i]);
        }

        LinearFit fit = LinearFit.Fit(inverseTemperature, logRate);

        // Наклон равен -Ea/R, свободный член - ln A
        double activationEnergy = -fit.Slope * GasConstant / 1000.0;
        double activationError = fit.SlopeStdError * GasConstant / 1000.0;
        var (slopeLow, slopeHigh) = fit.SlopeInterval(confidence);
        var (interceptLow, interceptHigh) = fit.InterceptInterval(confidence);

        return new ArrheniusResult
        {
            ActivationEnergy = activationEnergy,
            ActivationEnergyError = activationError,
            ActivationEnergyInterval = (-slopeHigh * GasConstant / 1000.0, -slopeLow * GasConstant / 1000.0),
            PreExponentialFactor = Math.Exp(fit.Intercept),
            PreExponentialInterval = (Math.Exp(interceptLow), Math.Exp(interceptHigh)),
            R2 = fit.R2,
            PointCount = temperatures.Count,
            Confidence = confidence
        };
    }

    /// <summary>
    /// Энергия активации по двум точкам (без оценки погрешности)
    /// </summary>
    /// <param name="temperature1">Первая температура, K</param>
    /// <param name="rateConstant1">Константа при первой температуре</param>
    /// <param name="temperature2">Вторая температура, K</param>
    /// <param name="rateConstant2">Константа при второй температуре</param>
    public static double ActivationEnergyFromTwoPoints(
        double temperature1, double rateConstant1, double temperature2, double rateConstant2)
    {
        if (temperature1 <= 0 || temperature2 <= 0)
            throw new ArgumentException("Temperature must be positive (kelvin)");

        if (rateConstant1 <= 0 || rateConstant2 <= 0)
            throw new ArgumentException("Rate constant must be positive");

        double slope = Math.Log(rateConstant2 / rateConstant1) / ((1 / temperature2) - (1 / temperature1));

        return -slope * GasConstant / 1000.0;
    }
}
