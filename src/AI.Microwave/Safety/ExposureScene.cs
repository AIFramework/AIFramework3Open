using AI.Microwave.Physics;
using AI.Solvers.Math.Core.Solvers;

namespace AI.Microwave.Safety;

/// <summary>Оценка облучения в одной точке.</summary>
/// <param name="Point">Точка расчёта.</param>
/// <param name="PowerDensityWPerM2">Суммарная плотность потока энергии.</param>
/// <param name="ElectricFieldVPerM">Эквивалентная напряжённость поля.</param>
/// <param name="Ratio">
/// Сумма долей от ПДУ по всем источникам. Соответствие достигнуто при
/// значении не больше единицы.
/// </param>
public readonly record struct ExposureAssessment(
    SitePoint Point,
    double PowerDensityWPerM2,
    double ElectricFieldVPerM,
    double Ratio)
{
    /// <summary>Плотность потока в мкВт/см^2 - единицах санитарных норм.</summary>
    public double PowerDensityMicroWattPerCm2 => PowerDensityWPerM2 * 100.0;

    /// <summary>Запас до предела, дБ (положительный - норма выполняется).</summary>
    public double MarginDb => Ratio <= 0 ? 200.0 : -10.0 * Math.Log10(Ratio);

    /// <summary>Выполняется ли норматив.</summary>
    public bool IsCompliant => Ratio <= 1.0;
}

/// <summary>
/// Площадка с одним или несколькими излучателями: суммарное облучение,
/// проверка соответствия и поиск границ зон.
/// </summary>
/// <remarks>
/// Источники суммируются не по мощности, а по долям от предела: у каждой
/// частоты свой ПДУ, поэтому складывать ватты между диапазонами нельзя.
/// Норма выполнена, когда сумма долей не превышает единицы.
/// </remarks>
public class ExposureScene
{
    /// <summary>Излучатели площадки.</summary>
    public List<RadiationSource> Sources { get; } = [];

    /// <summary>Документ, по которому проверяется соответствие.</summary>
    public ExposureStandard Standard { get; set; } = ExposureStandard.Sanpin;

    /// <summary>Категория облучаемых лиц.</summary>
    public ExposureCategory Category { get; set; } = ExposureCategory.General;

    /// <summary>Модель спадания поля.</summary>
    public FieldDecayModel DecayModel { get; set; } = FieldDecayModel.ApertureThreeRegion;

    /// <summary>Добавляет источник и возвращает его для дальнейшей настройки.</summary>
    public RadiationSource Add(RadiationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Sources.Add(source);
        return source;
    }

    /// <summary>Суммарная плотность потока энергии в точке, Вт/м^2.</summary>
    public double PowerDensityAt(SitePoint point)
    {
        double total = 0;
        foreach (var source in Sources)
            total += source.PowerDensityAt(point, DecayModel);
        return total;
    }

    /// <summary>
    /// Суммарная доля от предельно допустимого уровня: сумма S_i / ПДУ(f_i).
    /// </summary>
    public double ExposureRatioAt(SitePoint point)
    {
        double ratio = 0;
        foreach (var source in Sources)
        {
            double limit = ExposureLimits.PowerDensityLimit(Standard, source.FrequencyHz, Category);
            if (double.IsNaN(limit) || limit <= 0) continue;
            ratio += source.PowerDensityAt(point, DecayModel) / limit;
        }

        return ratio;
    }

    /// <summary>Полная оценка облучения в точке.</summary>
    public ExposureAssessment AssessAt(SitePoint point)
    {
        double s = PowerDensityAt(point);
        return new ExposureAssessment(
            point,
            s,
            Math.Sqrt(s * MicrowavePhysics.FreeSpaceImpedance),
            ExposureRatioAt(point));
    }

    /// <summary>
    /// Дальность границы зоны вдоль луча заданного азимута на заданной высоте.
    /// </summary>
    /// <param name="azimuthDeg">Азимут луча от севера по часовой стрелке.</param>
    /// <param name="heightM">Высота точек расчёта, м (для населения - уровень тела).</param>
    /// <param name="originX">Начало отсчёта по востоку, м.</param>
    /// <param name="originY">Начало отсчёта по северу, м.</param>
    /// <param name="maxRangeM">Дальность, дальше которой не ищем.</param>
    /// <returns>
    /// Расстояние, за которым норматив выполняется. Ноль означает, что норма
    /// соблюдена уже в начале луча; NaN - что превышение сохраняется до
    /// <paramref name="maxRangeM"/>.
    /// </returns>
    /// <remarks>
    /// Корень ищется бисекцией из AI.Solvers.Math: доля от ПДУ монотонно
    /// убывает с расстоянием, поэтому граница единственна.
    /// </remarks>
    public double BoundaryDistance(double azimuthDeg, double heightM,
        double originX = 0, double originY = 0, double maxRangeM = 2000)
    {
        double sin = Math.Sin(azimuthDeg * Math.PI / 180.0);
        double cos = Math.Cos(azimuthDeg * Math.PI / 180.0);

        double Excess(double r)
            => ExposureRatioAt(new SitePoint(originX + r * sin, originY + r * cos, heightM)) - 1.0;

        const double minRange = 0.1;
        if (Excess(minRange) <= 0) return 0.0;
        if (Excess(maxRangeM) > 0) return double.NaN;

        var (success, root, _) = NumericalEquationSolver.Bisection(
            Excess, minRange, maxRangeM, 1e-9, 200);

        return success ? root : double.NaN;
    }

    /// <summary>
    /// Контур границы зоны по азимутам: массив пар «азимут, дальность».
    /// </summary>
    /// <param name="heightM">Высота точек расчёта, м.</param>
    /// <param name="stepDeg">Шаг по азимуту, град.</param>
    /// <param name="maxRangeM">Предел поиска, м.</param>
    public IReadOnlyList<(double AzimuthDeg, double DistanceM)> BoundaryContour(
        double heightM, double stepDeg = 5, double maxRangeM = 2000)
    {
        var contour = new List<(double, double)>();
        for (double azimuth = 0; azimuth < 360; azimuth += stepDeg)
            contour.Add((azimuth, BoundaryDistance(azimuth, heightM, maxRangeM: maxRangeM)));

        return contour;
    }

    /// <summary>
    /// Профиль облучения вдоль луча: расстояние, ППЭ и доля от ПДУ.
    /// </summary>
    public IReadOnlyList<(double DistanceM, double PowerDensityWPerM2, double Ratio)> Profile(
        double azimuthDeg, double heightM, double fromM, double toM, int points = 200)
    {
        double sin = Math.Sin(azimuthDeg * Math.PI / 180.0);
        double cos = Math.Cos(azimuthDeg * Math.PI / 180.0);

        var profile = new List<(double, double, double)>(points);
        for (int i = 0; i < points; i++)
        {
            double r = fromM + (toM - fromM) * i / Math.Max(points - 1, 1);
            var p = new SitePoint(r * sin, r * cos, heightM);
            profile.Add((r, PowerDensityAt(p), ExposureRatioAt(p)));
        }

        return profile;
    }

    /// <summary>
    /// Наибольшая по всем азимутам дальность границы зоны, м: именно это
    /// число попадает в паспорт площадки.
    /// </summary>
    public double MaxBoundaryDistance(double heightM, double stepDeg = 5, double maxRangeM = 2000)
    {
        double worst = 0;
        foreach (var (_, distance) in BoundaryContour(heightM, stepDeg, maxRangeM))
        {
            if (double.IsNaN(distance)) return double.NaN;
            worst = Math.Max(worst, distance);
        }

        return worst;
    }
}
