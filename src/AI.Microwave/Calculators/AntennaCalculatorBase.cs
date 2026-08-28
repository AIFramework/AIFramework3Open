using AI.Microwave.Models;
using AI.Microwave.Physics;

namespace AI.Microwave.Calculators;

/// <summary>
/// Общая часть всех калькуляторов: проверка задания, оценка электрической
/// прочности по худшей точке тракта, сверка с требованиями ТЗ, тепловой
/// режим и рекомендации по условиям эксплуатации.
/// </summary>
/// <remarks>
/// Вынесено из трёх реализаций, каждая из которых повторяла эти блоки
/// по-своему: возвратные потери считались одной и той же неверной формулой
/// трижды, проверка ШДН сравнивала вещественные числа без допуска,
/// а условия среды учитывались только в рупоре.
/// </remarks>
public abstract class AntennaCalculatorBase : IAntennaCalculator
{
    /// <summary>Относительный допуск при сверке с требованиями ТЗ.</summary>
    protected const double RequirementTolerance = 1e-6;

    /// <summary>
    /// Коэффициент теплоотдачи при естественной конвекции в спокойном
    /// воздухе, Вт/(м^2 К).
    /// </summary>
    protected const double NaturalConvectionCoefficient = 10.0;

    /// <summary>Наиболее нагруженная точка тракта.</summary>
    /// <param name="Location">Где находится.</param>
    /// <param name="FieldVPerM">Напряжённость поля, В/м.</param>
    /// <param name="BreakdownVPerM">Порог пробоя в этой точке, В/м.</param>
    protected readonly record struct BreakdownPoint(
        string Location, double FieldVPerM, double BreakdownVPerM)
    {
        /// <summary>Запас по пробою, раз.</summary>
        public double Margin
            => FieldVPerM <= 0 ? double.PositiveInfinity : BreakdownVPerM / FieldVPerM;
    }

    public abstract string AntennaType { get; }

    public abstract string GetDescription();

    public abstract string GetAdvantages();

    public abstract string GetDisadvantages();

    /// <inheritdoc/>
    public AntennaDesignResult Calculate(AntennaParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var result = new AntennaDesignResult();
        result.Warnings.AddRange(parameters.Validate());

        if (parameters.FrequencyMHz <= 0 || parameters.PowerWatts <= 0
            || parameters.RequiredBeamwidthDegrees <= 0)
        {
            result.Warnings.Add("Расчёт не выполнен: задание физически несостоятельно.");
            return result;
        }

        // За отсечкой волновода все волноводные величины обращаются в NaN,
        // и результат выглядел бы посчитанным, не будучи им.
        if (!parameters.Waveguide.IsPropagating(parameters.FrequencyHz))
        {
            result.Warnings.Add(
                "Расчёт не выполнен: основная мода в волноводе не распространяется.");
            return result;
        }

        result.SpecificParameters["Wavelength_mm"] = parameters.WavelengthM * 1000.0;
        CalculateCore(parameters, result);
        AddEnvironmentRecommendations(parameters, result);
        return result;
    }

    /// <summary>Расчёт конкретного типа антенны.</summary>
    protected abstract void CalculateCore(AntennaParameters param, AntennaDesignResult result);

    /// <summary>
    /// Выбирает худшую по запасу точку тракта и записывает её в результат.
    /// </summary>
    /// <remarks>
    /// Смысл в том, что опасное место не то, которое удобнее посчитать.
    /// У рупора поле максимально в горловине волновода, а не в раскрыве:
    /// сечение там минимально по всему тракту, и запас отличается на порядки.
    /// </remarks>
    protected static void ApplyBreakdown(AntennaDesignResult result, params BreakdownPoint[] points)
    {
        if (points.Length == 0) return;

        BreakdownPoint worst = points[0];
        foreach (var p in points)
        {
            result.SpecificParameters["Field_" + p.Location + "_kVperM"] = p.FieldVPerM / 1000.0;
            if (p.Margin < worst.Margin) worst = p;
        }

        result.HotSpot = worst.Location;
        result.MaxElectricField = worst.FieldVPerM;
        result.BreakdownThreshold = worst.BreakdownVPerM;
        result.SafetyMargin = worst.Margin;

        if (worst.Margin < 1.0)
        {
            result.Warnings.Add(
                $"Пробой: поле в наиболее нагруженной точке ({worst.Location}) превышает порог " +
                $"в {1.0 / worst.Margin:F2} раза. Снизьте мощность или увеличьте сечение.");
        }
        else if (worst.Margin < result.RequiredSafetyMargin)
        {
            result.Warnings.Add(
                $"Опасность пробоя: запас {worst.Margin:F2} в наиболее нагруженной точке " +
                $"({worst.Location}) меньше требуемого {result.RequiredSafetyMargin:F1}.");
        }
        else if (worst.Margin < 3.0)
        {
            result.Warnings.Add(
                $"Низкий запас по пробою ({worst.Margin:F2}, {worst.Location}). Рекомендуется больше 3.");
        }
    }

    /// <summary>Записывает КСВ и согласованные с ним возвратные потери.</summary>
    protected static void ApplyMatching(AntennaDesignResult result, double vswr, double impedanceOhm)
    {
        result.VSWR = vswr;
        result.ReturnLossDb = MicrowavePhysics.ReturnLossDb(vswr);
        result.Impedance = impedanceOhm;

        if (vswr > 1.5)
        {
            double reflected = 100.0 * (1.0 - MicrowavePhysics.MismatchEfficiency(vswr));
            result.Warnings.Add(
                $"Высокий КСВ ({vswr:F2}): в тракт возвращается {reflected:F1} % мощности.");
        }
    }

    /// <summary>Сверка полученной ДН с требованиями ТЗ.</summary>
    protected static void CheckRequirements(AntennaParameters param, AntennaDesignResult result)
    {
        double allowedBeam = param.RequiredBeamwidthDegrees * (1.0 + RequirementTolerance);
        result.MeetsBeamwidthRequirement =
            result.BeamwidthEPlane <= allowedBeam && result.BeamwidthHPlane <= allowedBeam;

        result.MeetsSidelobeRequirement =
            result.SideLobeLevel <= param.RequiredSidelobeLevelDb + RequirementTolerance;

        if (!result.MeetsBeamwidthRequirement)
        {
            result.Warnings.Add(
                $"Ширина луча ({result.BeamwidthEPlane:F2} на {result.BeamwidthHPlane:F2} град) " +
                $"шире требуемой ({param.RequiredBeamwidthDegrees:F2} град).");
        }

        if (!result.MeetsSidelobeRequirement)
        {
            result.Warnings.Add(
                $"УБЛ {result.SideLobeLevel:F1} дБ не удовлетворяет требованию " +
                $"{param.RequiredSidelobeLevelDb:F1} дБ.");
        }
    }

    /// <summary>
    /// Перегрев поверхности над окружающей средой, К: сумма перепада на
    /// естественной конвекции и градиента в стенке.
    /// </summary>
    /// <remarks>
    /// Прежние формулы содержали подгоночные множители (умножение на 1000
    /// у рупора, на 10 у линзы) и давали сотни градусов там, где реально
    /// выделяются единицы ватт на десятки квадратных метров.
    /// </remarks>
    protected static double TemperatureRise(double heatFluxWPerM2, double thicknessM,
        double thermalConductivity)
        => heatFluxWPerM2 * (1.0 / NaturalConvectionCoefficient
                             + (thermalConductivity > 0 ? thicknessM / thermalConductivity : 0.0));

    /// <summary>Рекомендации, зависящие от условий эксплуатации и материала.</summary>
    protected static void AddEnvironmentRecommendations(AntennaParameters param, AntennaDesignResult result)
    {
        var env = param.Environment;

        if (env.Humidity > 70)
        {
            result.Recommendations.Add(
                $"Влажность {env.Humidity:F0} %, точка росы {env.GetDewPoint():F1} C: " +
                "нужна герметизация с осушителем или наддув сухим воздухом, иначе конденсат " +
                "на диэлектрике снизит электрическую прочность поверхности.");
        }

        if (env.Altitude > 1500)
        {
            result.Recommendations.Add(
                $"На высоте {env.Altitude:F0} м абсолютное давление " +
                $"{env.GetAbsolutePressureAtm():F2} атм, порог пробоя пропорционально ниже: " +
                "рассмотрите наддув тракта.");
        }

        if (param.Material.MeltingPoint < 700)
        {
            result.Recommendations.Add(
                $"{param.Material.Name}: температура плавления {param.Material.MeltingPoint:F0} C, " +
                "избегайте высокотемпературной пайки.");
        }

        if (param.Material.Conductivity < 1e7)
        {
            result.Recommendations.Add(
                $"{param.Material.Name} имеет низкую проводимость: серебрение или меднение " +
                "внутренних поверхностей заметно снизит омические потери.");
        }
    }
}
