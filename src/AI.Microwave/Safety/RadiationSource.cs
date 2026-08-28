using AI.Microwave.Physics;

namespace AI.Microwave.Safety;

/// <summary>Точка на площадке: X - восток, Y - север, Z - высота, м.</summary>
/// <param name="X">Восточная координата, м.</param>
/// <param name="Y">Северная координата, м.</param>
/// <param name="Z">Высота над уровнем земли, м.</param>
public readonly record struct SitePoint(double X, double Y, double Z);

/// <summary>Модель спадания плотности потока с расстоянием.</summary>
public enum FieldDecayModel
{
    /// <summary>
    /// Обратный квадрат по всему пространству. Просто и всегда завышает
    /// вблизи излучателя, поэтому годится как консервативная оценка.
    /// </summary>
    InverseSquare,

    /// <summary>
    /// Трёхзонная апертурная модель FCC OET-65: полка в ближней зоне,
    /// спад 1/R в переходной, 1/R^2 в дальней.
    /// </summary>
    ApertureThreeRegion,
}

/// <summary>
/// Излучающий источник на площадке: антенна с известной ДН, мощностью и
/// ориентацией.
/// </summary>
public class RadiationSource
{
    /// <summary>Обозначение источника в отчёте.</summary>
    public string Name { get; set; } = "источник";

    /// <summary>Положение фазового центра антенны.</summary>
    public SitePoint Position { get; set; } = new(0, 0, 30);

    /// <summary>Азимут главного луча от севера по часовой стрелке, град.</summary>
    public double AzimuthDeg { get; set; }

    /// <summary>Суммарный наклон вниз (механический плюс электрический), град.</summary>
    public double DowntiltDeg { get; set; }

    /// <summary>Рабочая частота, Гц.</summary>
    public double FrequencyHz { get; set; } = 1800e6;

    /// <summary>Мощность на выходе передатчика, Вт.</summary>
    public double TransmitPowerW { get; set; } = 40;

    /// <summary>Потери в фидерном тракте, дБ.</summary>
    public double FeederLossDb { get; set; } = 2;

    /// <summary>Максимальное усиление антенны, дБи.</summary>
    public double GainDbi { get; set; } = 18;

    /// <summary>Диаграмма направленности.</summary>
    public AntennaPattern Pattern { get; set; } = new GaussianPattern();

    /// <summary>
    /// Доля времени излучения (для TDD - отношение нисходящих слотов).
    /// </summary>
    public double DutyCycle { get; set; } = 1.0;

    /// <summary>
    /// Коэффициент снижения мощности для антенн с формированием луча.
    /// </summary>
    /// <remarks>
    /// Массивная MIMO-антенна не светит максимальной ЭИИМ во все стороны
    /// одновременно: луч обслуживает абонентов по очереди. Методики
    /// (IEC 62232) вводят статистический множитель, который и задаётся здесь.
    /// Значение 1.0 означает расчёт по худшему случаю - непрерывный луч.
    /// </remarks>
    public double PowerReductionFactor { get; set; } = 1.0;

    /// <summary>Высота раскрыва антенны, м (нужна для ближней зоны).</summary>
    public double ApertureHeightM { get; set; } = 1.3;

    /// <summary>Ширина раскрыва антенны, м (нужна для ближней зоны).</summary>
    public double ApertureWidthM { get; set; } = 0.26;

    /// <summary>КИП раскрыва.</summary>
    public double ApertureEfficiency { get; set; } = 0.6;

    /// <summary>Длина волны, м.</summary>
    public double WavelengthM => MicrowavePhysics.Wavelength(FrequencyHz);

    /// <summary>Мощность, подведённая к антенне с учётом фидера и режима работы, Вт.</summary>
    public double RadiatedPowerW
        => TransmitPowerW * MicrowavePhysics.FromDb(-FeederLossDb)
           * Math.Clamp(DutyCycle, 0, 1) * Math.Clamp(PowerReductionFactor, 0, 1);

    /// <summary>Эквивалентная изотропно излучаемая мощность, Вт.</summary>
    public double EirpW => RadiatedPowerW * MicrowavePhysics.FromDb(GainDbi);

    /// <summary>ЭИИМ в дБм.</summary>
    public double EirpDbm => 10.0 * Math.Log10(EirpW * 1000.0);

    /// <summary>Эквивалентный диаметр раскрыва равной площади, м.</summary>
    public double EquivalentApertureDiameterM
        => Math.Sqrt(4.0 * ApertureHeightM * ApertureWidthM / Math.PI);

    /// <summary>Граница ближней зоны, м: D^2 / (4 lambda).</summary>
    public double NearFieldBoundaryM
    {
        get
        {
            double d = EquivalentApertureDiameterM;
            return d * d / (4.0 * WavelengthM);
        }
    }

    /// <summary>Начало дальней зоны, м: 0.6 D^2 / lambda.</summary>
    public double FarFieldBoundaryM
    {
        get
        {
            double d = EquivalentApertureDiameterM;
            return 0.6 * d * d / WavelengthM;
        }
    }

    /// <summary>
    /// Плотность потока на полке ближней зоны, Вт/м^2: 16 eta P / (pi D^2).
    /// Ближе этой границы поле уже не растёт - апертура не успевает
    /// сфокусироваться.
    /// </summary>
    public double NearFieldPowerDensityWPerM2
    {
        get
        {
            double d = EquivalentApertureDiameterM;
            return 16.0 * ApertureEfficiency * RadiatedPowerW / (Math.PI * d * d);
        }
    }

    /// <summary>
    /// Направление на точку в системе координат антенны.
    /// </summary>
    /// <returns>
    /// Азимут и угол места относительно главного луча, град, и наклонная
    /// дальность, м.
    /// </returns>
    public (double AzimuthDeg, double ElevationDeg, double RangeM) DirectionTo(SitePoint target)
    {
        double dx = target.X - Position.X;
        double dy = target.Y - Position.Y;
        double dz = target.Z - Position.Z;

        double ground = Math.Sqrt(dx * dx + dy * dy);
        double range = Math.Sqrt(ground * ground + dz * dz);

        double bearing = Math.Atan2(dx, dy) * 180.0 / Math.PI;
        double elevation = ground > 0 || dz != 0
            ? Math.Atan2(dz, ground) * 180.0 / Math.PI
            : 0.0;

        // Наклон вниз опускает луч, поэтому цель ниже горизонта оказывается
        // на оси: elevation = -downtilt даёт нулевой относительный угол.
        return (bearing - AzimuthDeg, elevation + DowntiltDeg, range);
    }

    /// <summary>
    /// Плотность потока энергии в точке, Вт/м^2.
    /// </summary>
    /// <remarks>
    /// В ближней зоне ДН не применяется: на таком расстоянии диаграмма ещё
    /// не сформирована, и правильнее считать по худшему случаю - полке.
    /// </remarks>
    public double PowerDensityAt(SitePoint target,
        FieldDecayModel model = FieldDecayModel.ApertureThreeRegion)
    {
        var (az, el, range) = DirectionTo(target);
        if (range <= 0) return NearFieldPowerDensityWPerM2;

        double relative = Pattern.RelativeGain(az, el);
        double farField = EirpW * relative / (4.0 * Math.PI * range * range);

        if (model == FieldDecayModel.InverseSquare) return farField;

        double nearBoundary = NearFieldBoundaryM;
        double farBoundary = FarFieldBoundaryM;

        if (range <= nearBoundary) return NearFieldPowerDensityWPerM2;
        if (range >= farBoundary) return farField;

        // Переходная зона: спад обратно пропорционально расстоянию.
        return NearFieldPowerDensityWPerM2 * nearBoundary / range * relative;
    }

    /// <summary>Напряжённость электрического поля в точке, В/м (плоская волна).</summary>
    public double ElectricFieldAt(SitePoint target,
        FieldDecayModel model = FieldDecayModel.ApertureThreeRegion)
        => Math.Sqrt(PowerDensityAt(target, model) * MicrowavePhysics.FreeSpaceImpedance);
}
