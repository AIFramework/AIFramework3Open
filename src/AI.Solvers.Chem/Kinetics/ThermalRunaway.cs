using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Kinetics;

/// <summary>
/// Исходные данные для оценки теплового разгона
/// </summary>
public sealed class RunawayParameters
{
    /// <summary>Удельная теплота реакции (разложения), Дж/кг; положительна для экзотермики</summary>
    public double ReactionHeat { get; init; }

    /// <summary>Удельная теплоёмкость смеси, Дж/(кг·K)</summary>
    public double HeatCapacity { get; init; } = 1800;

    /// <summary>Энергия активации, кДж/моль</summary>
    public double ActivationEnergy { get; init; }

    /// <summary>Предэкспоненциальный множитель, 1/с</summary>
    public double PreExponentialFactor { get; init; }

    /// <summary>Порядок реакции по степени превращения</summary>
    public double ReactionOrder { get; init; } = 1;

    /// <summary>Начальная температура, K</summary>
    public double InitialTemperature { get; init; } = 298.15;

    /// <summary>Начальная степень превращения</summary>
    public double InitialConversion { get; init; }

    /// <summary>Адиабатический подъём температуры, K</summary>
    public double AdiabaticTemperatureRise => HeatCapacity > 0 ? ReactionHeat / HeatCapacity : double.NaN;

    /// <summary>Константа скорости при температуре, 1/с</summary>
    /// <param name="temperature">Температура, K</param>
    public double RateConstantAt(double temperature)
        => PreExponentialFactor * Math.Exp(-ActivationEnergy * 1000 / (ArrheniusAnalysis.GasConstant * temperature));

    /// <summary>Удельная скорость тепловыделения при температуре, Вт/кг</summary>
    /// <param name="temperature">Температура, K</param>
    /// <param name="conversion">Степень превращения</param>
    public double HeatReleaseRate(double temperature, double conversion = 0)
        => ReactionHeat * RateConstantAt(temperature) * Math.Pow(Math.Max(0, 1 - conversion), ReactionOrder);
}

/// <summary>
/// Итог оценки теплового разгона
/// </summary>
public sealed class RunawayResult
{
    /// <summary>Адиабатический подъём температуры, K</summary>
    public double AdiabaticTemperatureRise { get; init; }

    /// <summary>Максимальная достигнутая температура, K</summary>
    public double MaximumTemperature { get; init; }

    /// <summary>Время достижения максимальной скорости разогрева, с</summary>
    public double TimeToMaximumRate { get; init; }

    /// <summary>То же в часах</summary>
    public double TimeToMaximumRateHours => TimeToMaximumRate / 3600.0;

    /// <summary>Температура в момент максимальной скорости, K</summary>
    public double TemperatureAtMaximumRate { get; init; }

    /// <summary>Максимальная скорость разогрева, K/с</summary>
    public double MaximumHeatingRate { get; init; }

    /// <summary>Сетка времени траектории, с</summary>
    public double[] Times { get; init; } = Array.Empty<double>();

    /// <summary>Температура вдоль траектории, K</summary>
    public double[] Temperatures { get; init; } = Array.Empty<double>();

    /// <summary>Степень превращения вдоль траектории</summary>
    public double[] Conversions { get; init; } = Array.Empty<double>();

    /// <summary>Успел ли разгон произойти внутри заданного окна времени</summary>
    public bool RunawayWithinWindow { get; init; }

    /// <summary>Отчёт по оценке</summary>
    public string Report()
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine("Адиабатический разгон");
        text.AppendLine(string.Format(culture, "  Адиабатический подъём: {0:F1} K", AdiabaticTemperatureRise));
        text.AppendLine(string.Format(culture, "  Максимальная температура: {0:F1} K ({1:F1} C)",
            MaximumTemperature, MaximumTemperature - 273.15));

        if (RunawayWithinWindow)
        {
            text.AppendLine(string.Format(culture, "  Время до максимальной скорости: {0:G4} с ({1:F2} ч)",
                TimeToMaximumRate, TimeToMaximumRateHours));
            text.AppendLine(string.Format(culture, "  Температура в этот момент: {0:F1} K, скорость {1:G3} K/с",
                TemperatureAtMaximumRate, MaximumHeatingRate));
        }
        else
        {
            text.AppendLine("  В заданном окне времени разгон не наступает");
        }

        return text.ToString();
    }
}

/// <summary>
/// Оценка теплового разгона в адиабатических условиях
/// </summary>
/// <remarks>
/// Модель: степень превращения растёт по закону n-го порядка, всё выделившееся тепло
/// идёт в нагрев (охлаждения нет). Это верхняя оценка опасности - именно её используют
/// при масштабировании, когда отношение поверхности к объёму падает и реактор
/// приближается к адиабатическому.
/// </remarks>
public static class ThermalRunaway
{
    // Ограничения на один шаг интегрирования
    private const double ConversionStep = 0.002;
    private const double TemperatureStep = 0.5;
    private const double MinimumRelativeStep = 1e-6;

    // Скорость превращения при текущем состоянии
    private static double Rate(RunawayParameters parameters, double temperature, double conversion)
    {
        double remaining = Math.Max(0, 1 - conversion);

        if (remaining <= 0)
            return 0;

        return parameters.RateConstantAt(temperature)
            * (Math.Abs(parameters.ReactionOrder - 1) < 1e-12 ? remaining : Math.Pow(remaining, parameters.ReactionOrder));
    }

    // Шаг Рунге-Кутты 4-го порядка по паре «превращение - температура»
    private static (double Conversion, double Temperature) Advance(
        RunawayParameters parameters, double rise, double conversion, double temperature, double step)
    {
        double k1 = Rate(parameters, temperature, conversion);
        double k2 = Rate(parameters, temperature + (rise * k1 * step / 2), conversion + (k1 * step / 2));
        double k3 = Rate(parameters, temperature + (rise * k2 * step / 2), conversion + (k2 * step / 2));
        double k4 = Rate(parameters, temperature + (rise * k3 * step), conversion + (k3 * step));

        double delta = step * (k1 + (2 * k2) + (2 * k3) + k4) / 6;
        double next = Math.Clamp(conversion + delta, 0, 1);

        return (next, parameters.InitialTemperature + (rise * (next - parameters.InitialConversion)));
    }

    /// <summary>
    /// Интегрирует разогрев и находит момент максимальной скорости (TMR)
    /// </summary>
    /// <param name="parameters">Параметры реакции и смеси</param>
    /// <param name="duration">Окно наблюдения, с</param>
    /// <param name="points">Число точек траектории</param>
    public static RunawayResult Simulate(RunawayParameters parameters, double duration, int points = 400)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (duration <= 0)
            throw new ArgumentException("Duration must be positive", nameof(duration));

        if (points < 10)
            throw new ArgumentException("At least ten points are required", nameof(points));

        double rise = parameters.AdiabaticTemperatureRise;
        var times = new double[points];

        for (int i = 0; i < points; i++)
            times[i] = duration * i / (points - 1.0);

        var temperatures = new double[points];
        var conversions = new double[points];

        double conversion = parameters.InitialConversion;
        double temperature = parameters.InitialTemperature;
        double time = 0;

        temperatures[0] = temperature;
        conversions[0] = conversion;

        for (int point = 1; point < points; point++)
        {
            double target = times[point];

            // Шаг подбирается под текущую скорость: в момент разгона характерное время
            // падает на порядки, и равномерная сетка либо промахивается мимо пика,
            // либо разносит решение
            while (time < target)
            {
                double remaining = target - time;
                double step = remaining;
                double rate = Rate(parameters, temperature, conversion);

                if (rate > 0)
                {
                    double byConversion = ConversionStep / rate;
                    double byTemperature = rise > 0 ? TemperatureStep / (rise * rate) : remaining;
                    step = Math.Min(remaining, Math.Min(byConversion, byTemperature));
                    step = Math.Max(step, remaining * MinimumRelativeStep);
                }

                (conversion, temperature) = Advance(parameters, rise, conversion, temperature, step);
                time += step;
            }

            time = target;
            conversions[point] = conversion;
            temperatures[point] = temperature;
        }

        int peak = 0;
        double maxRate = 0;

        for (int i = 1; i < points; i++)
        {
            double rate = (temperatures[i] - temperatures[i - 1]) / (times[i] - times[i - 1]);

            if (rate > maxRate)
            {
                maxRate = rate;
                peak = i;
            }
        }

        // Разгон считается состоявшимся, если превращение заметно продвинулось
        bool happened = conversions[^1] > 0.5 && peak > 0 && peak < points - 1;

        return new RunawayResult
        {
            AdiabaticTemperatureRise = rise,
            MaximumTemperature = temperatures[^1],
            TimeToMaximumRate = times[peak],
            TemperatureAtMaximumRate = temperatures[peak],
            MaximumHeatingRate = maxRate,
            Times = times,
            Temperatures = temperatures,
            Conversions = conversions,
            RunawayWithinWindow = happened
        };
    }

    /// <summary>
    /// Аналитическая оценка времени до максимальной скорости:
    /// TMR = cp·R·T² / (q(T)·Ea)
    /// </summary>
    /// <param name="parameters">Параметры реакции и смеси</param>
    /// <param name="temperature">Температура, K; по умолчанию начальная</param>
    public static double TimeToMaximumRateEstimate(RunawayParameters parameters, double temperature = 0)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        double t = temperature > 0 ? temperature : parameters.InitialTemperature;
        double heatRate = parameters.HeatReleaseRate(t, parameters.InitialConversion);

        if (heatRate <= 0)
            return double.PositiveInfinity;

        return parameters.HeatCapacity * ArrheniusAnalysis.GasConstant * t * t
            / (heatRate * parameters.ActivationEnergy * 1000);
    }

    /// <summary>
    /// Температура, при которой время до максимальной скорости равно заданному
    /// (показатель T_D24 при 24 часах - принятый в процессной безопасности порог)
    /// </summary>
    /// <param name="parameters">Параметры реакции и смеси</param>
    /// <param name="targetHours">Целевое время, ч</param>
    /// <param name="lowerTemperature">Нижняя граница поиска, K</param>
    /// <param name="upperTemperature">Верхняя граница поиска, K</param>
    public static double TemperatureForTimeToMaximumRate(
        RunawayParameters parameters,
        double targetHours = 24,
        double lowerTemperature = 250,
        double upperTemperature = 700)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        double target = targetHours * 3600;

        // TMR монотонно убывает с температурой, поэтому годится половинное деление
        double low = lowerTemperature, high = upperTemperature;

        if (TimeToMaximumRateEstimate(parameters, low) < target)
            return double.NaN;

        for (int i = 0; i < 200; i++)
        {
            double middle = 0.5 * (low + high);

            if (TimeToMaximumRateEstimate(parameters, middle) > target)
                low = middle;
            else
                high = middle;
        }

        return 0.5 * (low + high);
    }
}
