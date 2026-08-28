namespace AI.Microwave.Safety;

/// <summary>Категория облучаемых лиц.</summary>
public enum ExposureCategory
{
    /// <summary>Население, постоянное пребывание.</summary>
    General,

    /// <summary>Персонал, профессиональное воздействие.</summary>
    Occupational,
}

/// <summary>Нормативный документ, по которому проверяется соответствие.</summary>
public enum ExposureStandard
{
    /// <summary>СанПиН 1.2.3685-21 (Российская Федерация).</summary>
    Sanpin,

    /// <summary>ICNIRP 2020 (международные рекомендации).</summary>
    Icnirp2020,

    /// <summary>FCC OET Bulletin 65 (США).</summary>
    FccOet65,
}

/// <summary>Предельно допустимый уровень в одной полосе частот.</summary>
/// <param name="Standard">Документ.</param>
/// <param name="Category">Категория лиц.</param>
/// <param name="FrequencyMinHz">Нижняя граница полосы.</param>
/// <param name="FrequencyMaxHz">Верхняя граница полосы.</param>
/// <param name="PowerDensityWPerM2">ПДУ по ППЭ; null, если нормируется поле.</param>
/// <param name="ElectricFieldVPerM">ПДУ по напряжённости поля; null, если нормируется ППЭ.</param>
/// <param name="AveragingMinutes">Время усреднения, мин.</param>
/// <param name="FrequencyScaledMHz">
/// Если задано, предел по ППЭ зависит от частоты как f[МГц] / это_число, Вт/м^2.
/// </param>
public readonly record struct ExposureLimit(
    ExposureStandard Standard,
    ExposureCategory Category,
    double FrequencyMinHz,
    double FrequencyMaxHz,
    double? PowerDensityWPerM2,
    double? ElectricFieldVPerM,
    double AveragingMinutes,
    double? FrequencyScaledMHz = null);

/// <summary>
/// Нормативные пределы облучения по частоте и категории лиц.
/// </summary>
/// <remarks>
/// ВАЖНО: таблицы ниже - рабочая заготовка для инженерных прикидок, а не
/// выписка из официального текста. Перед применением в документах, имеющих
/// юридическую силу, значения обязаны быть сверены с действующей редакцией
/// документа: пределы пересматриваются, а для отдельных случаев (импульсный
/// режим, локальное облучение, конкретные типы установок) действуют свои
/// нормы. Таблицу можно целиком заменить через <see cref="Custom"/>.
/// </remarks>
public static class ExposureLimits
{
    private const double Eta0 = Physics.MicrowavePhysics.FreeSpaceImpedance;

    /// <summary>Пользовательская таблица; если непуста, используется вместо встроенных.</summary>
    public static List<ExposureLimit> Custom { get; } = [];

    /// <summary>
    /// СанПиН 1.2.3685-21: до 300 МГц нормируется напряжённость поля,
    /// выше - плотность потока энергии (для населения 10 мкВт/см^2 = 0.1 Вт/м^2).
    /// </summary>
    public static IReadOnlyList<ExposureLimit> Sanpin { get; } =
    [
        new(ExposureStandard.Sanpin, ExposureCategory.General, 30e3, 300e3, null, 25, 24 * 60),
        new(ExposureStandard.Sanpin, ExposureCategory.General, 300e3, 3e6, null, 15, 24 * 60),
        new(ExposureStandard.Sanpin, ExposureCategory.General, 3e6, 30e6, null, 10, 24 * 60),
        new(ExposureStandard.Sanpin, ExposureCategory.General, 30e6, 300e6, null, 3, 24 * 60),
        new(ExposureStandard.Sanpin, ExposureCategory.General, 300e6, 300e9, 0.1, null, 24 * 60),

        new(ExposureStandard.Sanpin, ExposureCategory.Occupational, 30e3, 300e3, null, 500, 8 * 60),
        new(ExposureStandard.Sanpin, ExposureCategory.Occupational, 300e3, 3e6, null, 300, 8 * 60),
        new(ExposureStandard.Sanpin, ExposureCategory.Occupational, 3e6, 30e6, null, 80, 8 * 60),
        new(ExposureStandard.Sanpin, ExposureCategory.Occupational, 30e6, 300e6, null, 27, 8 * 60),
        new(ExposureStandard.Sanpin, ExposureCategory.Occupational, 300e6, 300e9, 0.25, null, 8 * 60),
    ];

    /// <summary>
    /// ICNIRP 2020, усреднение по всему телу за 30 минут. В полосе
    /// 400 МГц...2 ГГц предел растёт линейно с частотой.
    /// </summary>
    public static IReadOnlyList<ExposureLimit> Icnirp2020 { get; } =
    [
        new(ExposureStandard.Icnirp2020, ExposureCategory.General, 2e6, 400e6, 2.0, null, 30),
        new(ExposureStandard.Icnirp2020, ExposureCategory.General, 400e6, 2e9, null, null, 30, 200),
        new(ExposureStandard.Icnirp2020, ExposureCategory.General, 2e9, 300e9, 10.0, null, 30),

        new(ExposureStandard.Icnirp2020, ExposureCategory.Occupational, 2e6, 400e6, 10.0, null, 30),
        new(ExposureStandard.Icnirp2020, ExposureCategory.Occupational, 400e6, 2e9, null, null, 30, 40),
        new(ExposureStandard.Icnirp2020, ExposureCategory.Occupational, 2e9, 300e9, 50.0, null, 30),
    ];

    /// <summary>
    /// FCC OET-65: неконтролируемая среда усредняется за 30 минут,
    /// контролируемая - за 6.
    /// </summary>
    public static IReadOnlyList<ExposureLimit> FccOet65 { get; } =
    [
        new(ExposureStandard.FccOet65, ExposureCategory.General, 30e6, 300e6, 2.0, null, 30),
        new(ExposureStandard.FccOet65, ExposureCategory.General, 300e6, 1.5e9, null, null, 30, 150),
        new(ExposureStandard.FccOet65, ExposureCategory.General, 1.5e9, 100e9, 10.0, null, 30),

        new(ExposureStandard.FccOet65, ExposureCategory.Occupational, 30e6, 300e6, 10.0, null, 6),
        new(ExposureStandard.FccOet65, ExposureCategory.Occupational, 300e6, 1.5e9, null, null, 6, 30),
        new(ExposureStandard.FccOet65, ExposureCategory.Occupational, 1.5e9, 100e9, 50.0, null, 6),
    ];

    /// <summary>Таблица выбранного документа, либо пользовательская, если она задана.</summary>
    public static IReadOnlyList<ExposureLimit> Table(ExposureStandard standard)
    {
        if (Custom.Count > 0) return Custom;

        return standard switch
        {
            ExposureStandard.Icnirp2020 => Icnirp2020,
            ExposureStandard.FccOet65 => FccOet65,
            _ => Sanpin,
        };
    }

    /// <summary>Применимая строка таблицы; null, если частота вне области действия.</summary>
    public static ExposureLimit? Find(ExposureStandard standard, double frequencyHz,
        ExposureCategory category)
    {
        foreach (var limit in Table(standard))
        {
            if (limit.Category != category) continue;
            if (frequencyHz >= limit.FrequencyMinHz && frequencyHz <= limit.FrequencyMaxHz)
                return limit;
        }

        return null;
    }

    /// <summary>
    /// ПДУ по плотности потока энергии, Вт/м^2.
    /// </summary>
    /// <remarks>
    /// Если документ нормирует напряжённость поля, значение пересчитывается
    /// как E^2 / eta0. Это верно только для плоской волны в дальней зоне;
    /// вблизи излучателя связь E и ППЭ другая.
    /// </remarks>
    /// <returns>Предел в Вт/м^2; NaN, если частота вне области действия документа.</returns>
    public static double PowerDensityLimit(ExposureStandard standard, double frequencyHz,
        ExposureCategory category = ExposureCategory.General)
    {
        var limit = Find(standard, frequencyHz, category);
        if (limit is null) return double.NaN;

        var value = limit.Value;
        if (value.FrequencyScaledMHz is { } divisor) return frequencyHz / 1e6 / divisor;
        if (value.PowerDensityWPerM2 is { } s) return s;
        if (value.ElectricFieldVPerM is { } e) return e * e / Eta0;

        return double.NaN;
    }

    /// <summary>ПДУ по напряжённости поля, В/м (пересчёт из ППЭ, если нормируется она).</summary>
    public static double ElectricFieldLimit(ExposureStandard standard, double frequencyHz,
        ExposureCategory category = ExposureCategory.General)
    {
        var limit = Find(standard, frequencyHz, category);
        if (limit?.ElectricFieldVPerM is { } e) return e;

        double s = PowerDensityLimit(standard, frequencyHz, category);
        return double.IsNaN(s) ? double.NaN : Math.Sqrt(s * Eta0);
    }

    /// <summary>Время усреднения, мин; NaN вне области действия документа.</summary>
    public static double AveragingMinutes(ExposureStandard standard, double frequencyHz,
        ExposureCategory category = ExposureCategory.General)
        => Find(standard, frequencyHz, category)?.AveragingMinutes ?? double.NaN;
}
