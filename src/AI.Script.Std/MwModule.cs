using AI.Microwave.Calculators;
using AI.Microwave.Heating;
using AI.Microwave.Models;
using AI.Microwave.Physics;
using AI.Microwave.Safety;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>mw</c>: СВЧ-техника — волноводы, антенны, диэлектрический нагрев, ПДУ.
/// </summary>
/// <remarks>
/// Материал задаётся числами, а не объектом: скрипт либо берёт готовый набор через
/// <c>mw.material</c>, либо подставляет свои измеренные значения. Дескриптор материала здесь
/// был бы лишней сущностью — у него нет ни состояния, ни жизненного цикла, только семь чисел.
/// <para>
/// Частоты всюду в герцах, размеры в метрах, мощности в ваттах. Единицы не выбираются
/// аргументом: смешение мегагерц с герцами в одном скрипте — самая дорогая из возможных
/// здесь ошибок, и единственная защита от неё — не давать выбора.
/// </para>
/// </remarks>
[ScriptModule("mw", "СВЧ в единицах СИ: волноводы, антенны, нагрев, предельные уровни", Version = "0.1")]
public static class MwModule
{
    // --- физика ---

    [ScriptFn("wavelength", "Длина волны в свободном пространстве, м",
        Example = "mw.wavelength(2.45e9)")]
    public static double Wavelength(
        [ScriptParam("частота, Гц")] double frequency)
    {
        Require(frequency > 0, "mw.wavelength: частота должна быть положительной");

        return MicrowavePhysics.Wavelength(frequency);
    }

    [ScriptFn("skin_depth", "Глубина скин-слоя в металле, м",
        Example = "mw.skin_depth(2.45e9, conductivity: 5.8e7)")]
    public static double SkinDepth(
        [ScriptParam("частота, Гц")] double frequency,
        [ScriptParam("удельная проводимость, См/м")] double conductivity)
    {
        Require(frequency > 0 && conductivity > 0, "mw.skin_depth: частота и проводимость должны быть положительны");

        return MicrowavePhysics.SkinDepth(frequency, conductivity);
    }

    [ScriptFn("reflection", "Модуль коэффициента отражения по КСВ", Example = "mw.reflection(vswr: 1.5)")]
    public static double Reflection(
        [ScriptParam("коэффициент стоячей волны")] double vswr)
    {
        Require(vswr >= 1, "mw.reflection: КСВ не бывает меньше единицы");

        return MicrowavePhysics.ReflectionCoefficient(vswr);
    }

    [ScriptFn("vswr", "КСВ по модулю коэффициента отражения", Example = "mw.vswr(gamma: 0.2)")]
    public static double Vswr(
        [ScriptParam("модуль коэффициента отражения от 0 до 1")] double gamma)
    {
        Require(gamma is >= 0 and < 1, "mw.vswr: модуль коэффициента отражения лежит в [0, 1)");

        return MicrowavePhysics.VswrFromReflection(gamma);
    }

    [ScriptFn("return_loss", "Обратные потери, дБ", Example = "mw.return_loss(vswr: 1.5)")]
    public static double ReturnLoss(
        [ScriptParam("коэффициент стоячей волны")] double vswr)
    {
        Require(vswr >= 1, "mw.return_loss: КСВ не бывает меньше единицы");

        return MicrowavePhysics.ReturnLossDb(vswr);
    }

    /// <summary>
    /// Доля мощности, прошедшая в нагрузку при рассогласовании.
    /// </summary>
    /// <remarks>
    /// Отвечает на вопрос, который обычно и задают про КСВ: сколько мощности потеряно.
    /// Сам по себе КСВ этого не говорит — им меряют рассогласование, а не потери.
    /// </remarks>
    [ScriptFn("mismatch_efficiency", "Доля мощности, прошедшей в нагрузку при рассогласовании",
        Example = "mw.mismatch_efficiency(vswr: 2)")]
    public static double MismatchEfficiency(
        [ScriptParam("коэффициент стоячей волны")] double vswr)
    {
        Require(vswr >= 1, "mw.mismatch_efficiency: КСВ не бывает меньше единицы");

        return MicrowavePhysics.MismatchEfficiency(vswr);
    }

    [ScriptFn("aperture_gain", "Коэффициент усиления апертуры, дБи",
        Example = "mw.aperture_gain(area: 0.5, frequency: 2.45e9, efficiency: 0.6)")]
    public static double ApertureGain(
        [ScriptParam("площадь апертуры, м²")] double area,
        [ScriptParam("частота, Гц")] double frequency,
        [ScriptParam("апертурный КПД от 0 до 1")] double efficiency = 0.6)
    {
        Require(area > 0 && frequency > 0, "mw.aperture_gain: площадь и частота должны быть положительны");

        double gain = MicrowavePhysics.ApertureGain(area, efficiency, MicrowavePhysics.Wavelength(frequency));

        return MicrowavePhysics.ToDb(gain);
    }

    /// <summary>
    /// Граница дальней зоны.
    /// </summary>
    /// <remarks>
    /// Нужна раньше всех прочих расчётов антенны: коэффициент усиления и диаграмма
    /// направленности определены только за этой границей, и измерение ближе неё меряет не то.
    /// </remarks>
    [ScriptFn("far_field", "Расстояние до дальней зоны антенны, м",
        Example = "mw.far_field(aperture: 0.6, frequency: 2.45e9)")]
    public static double FarField(
        [ScriptParam("наибольший размер апертуры, м")] double aperture,
        [ScriptParam("частота, Гц")] double frequency)
    {
        Require(aperture > 0 && frequency > 0, "mw.far_field: размер и частота должны быть положительны");

        return MicrowavePhysics.FarFieldDistance(aperture, MicrowavePhysics.Wavelength(frequency));
    }

    // --- волноводы ---

    /// <summary>
    /// Прямоугольный волновод на заданной частоте.
    /// </summary>
    /// <remarks>
    /// Стандарт можно назвать, а можно доверить подбор по частоте. Подбор — не удобство:
    /// работа вне полосы одномодового режима означает, что по волноводу идёт не то, что
    /// рассчитывали, и признак <c>одномодовый</c> в результате об этом говорит прямо.
    /// </remarks>
    [ScriptFn("waveguide", "Прямоугольный волновод: критические частоты, длина волны, затухание",
        Example = "mw.waveguide(2.45e9)")]
    public static ScriptRecord Waveguide(
        [ScriptParam("частота, Гц")] double frequency,
        [ScriptParam("стандарт, например \"WR-340\"; пусто — подобрать по частоте")] string standard = "",
        [ScriptParam("удельная проводимость стенок, См/м")] double conductivity = 5.8e7)
    {
        Require(frequency > 0, "mw.waveguide: частота должна быть положительной");

        RectangularWaveguide guide;

        if (string.IsNullOrWhiteSpace(standard))
        {
            guide = RectangularWaveguide.SelectForFrequency(frequency);
        }
        else
        {
            guide = RectangularWaveguide.Find(standard) ?? throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"mw.waveguide: неизвестный стандарт '{standard}'",
                $"известны: {string.Join(", ", RectangularWaveguide.GetStandards().Select(w => w.Standard))}");
        }

        bool propagating = guide.IsPropagating(frequency);

        return Record(
            ("standard", ScriptValue.Str(guide.Standard)),
            ("width_mm", ScriptValue.Num(guide.WidthMm)),
            ("height_mm", ScriptValue.Num(guide.HeightMm)),
            ("cutoff", ScriptValue.Num(guide.CutoffTE10Hz)),
            ("band_low", ScriptValue.Num(guide.BandLowHz)),
            ("band_high", ScriptValue.Num(guide.BandHighHz)),
            ("propagating", ScriptValue.Bool(propagating)),
            ("single_mode", ScriptValue.Bool(guide.IsSingleMode(frequency))),
            ("guide_wavelength", ScriptValue.Num(propagating ? guide.GuideWavelength(frequency) : double.NaN)),
            ("impedance", ScriptValue.Num(propagating ? guide.WaveImpedanceTE10(frequency) : double.NaN)),
            ("attenuation_db_m", ScriptValue.Num(
                propagating ? guide.AttenuationDbPerM(frequency, conductivity) : double.NaN)));
    }

    [ScriptFn("waveguide_field", "Пиковая напряжённость поля в волноводе при заданной мощности, В/м",
        Example = "mw.waveguide_field(power: 3000, frequency: 2.45e9)")]
    public static double WaveguideField(
        [ScriptParam("подводимая мощность, Вт")] double power,
        [ScriptParam("частота, Гц")] double frequency,
        [ScriptParam("стандарт волновода; пусто — подобрать по частоте")] string standard = "")
    {
        Require(power > 0 && frequency > 0, "mw.waveguide_field: мощность и частота должны быть положительны");

        RectangularWaveguide guide = string.IsNullOrWhiteSpace(standard)
            ? RectangularWaveguide.SelectForFrequency(frequency)
            : RectangularWaveguide.Find(standard) ?? throw new ScriptError(
                DiagnosticCodes.BadOperand, $"mw.waveguide_field: неизвестный стандарт '{standard}'");

        return guide.PeakElectricField(power, frequency);
    }

    // --- материалы и нагрев ---

    /// <summary>
    /// Справочные свойства типового материала.
    /// </summary>
    /// <remarks>
    /// Числами, а не дескриптором: их сразу видно в выводе, их можно поправить под свои
    /// измерения, и они переживают запись в файл. Справочник — отправная точка, а не истина:
    /// диэлектрические свойства пищевых продуктов разнятся в разы от партии к партии.
    /// </remarks>
    [ScriptFn("material", "Свойства типового материала для СВЧ-нагрева", Example = "let water = mw.material(\"вода\")")]
    public static ScriptRecord Material(
        [ScriptParam("часть названия: \"вода\", \"тесто\", \"мясо\", \"древесина\", \"керамика\"")] string name)
    {
        List<DielectricMaterial> loads = DielectricMaterial.GetStandardLoads();

        DielectricMaterial? found = loads.Find(
            m => m.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

        if (found == null)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"mw.material: материал '{name}' не найден",
                $"известны: {string.Join(", ", loads.Select(m => m.Name))}");
        }

        return Record(
            ("name", ScriptValue.Str(found.Name)),
            ("permittivity", ScriptValue.Num(found.RelativePermittivity)),
            ("loss_factor", ScriptValue.Num(found.LossFactor)),
            ("loss_tangent", ScriptValue.Num(found.LossTangent)),
            ("density", ScriptValue.Num(found.DensityKgPerM3)),
            ("heat_capacity", ScriptValue.Num(found.SpecificHeatJPerKgK)),
            ("thermal_conductivity", ScriptValue.Num(found.ThermalConductivity)),
            ("max_temperature", ScriptValue.Num(found.MaxTemperatureC)));
    }

    [ScriptFn("heating_power", "Удельная мощность тепловыделения, Вт/м³",
        Example = "mw.heating_power(frequency: 2.45e9, loss_factor: 12, field: 5000)")]
    public static double HeatingPower(
        [ScriptParam("частота, Гц")] double frequency,
        [ScriptParam("фактор потерь материала")] double loss_factor,
        [ScriptParam("действующая напряжённость поля, В/м")] double field)
    {
        Require(frequency > 0, "mw.heating_power: частота должна быть положительной");

        return DielectricHeating.VolumetricPowerWPerM3(frequency, loss_factor, field);
    }

    [ScriptFn("heating_time", "Время нагрева массы до нужного перепада температур, с",
        Example = "mw.heating_time(mass: 2, delta: 60, power: 3000, heat_capacity: 4186)")]
    public static double HeatingTime(
        [ScriptParam("масса, кг")] double mass,
        [ScriptParam("требуемый перепад температуры, К")] double delta,
        [ScriptParam("подводимая мощность, Вт")] double power,
        [ScriptParam("удельная теплоёмкость, Дж/(кг·К)")] double heat_capacity,
        [ScriptParam("доля мощности, попавшая в материал")] double coupling = 1,
        [ScriptParam("удельная теплота фазового перехода, Дж/кг")] double latent_heat = 0)
    {
        Require(mass > 0 && power > 0, "mw.heating_time: масса и мощность должны быть положительны");
        Require(heat_capacity > 0, "mw.heating_time: теплоёмкость должна быть положительной");

        return DielectricHeating.HeatingTimeS(
            mass, Load(heat_capacity), delta, power, coupling, latent_heat);
    }

    [ScriptFn("throughput", "Производительность непрерывной линии, кг/ч",
        Example = "mw.throughput(power: 30000, delta: 40, heat_capacity: 2800)")]
    public static double Throughput(
        [ScriptParam("мощность установки, Вт")] double power,
        [ScriptParam("требуемый перепад температуры, К")] double delta,
        [ScriptParam("удельная теплоёмкость, Дж/(кг·К)")] double heat_capacity,
        [ScriptParam("доля мощности, попавшая в материал")] double coupling = 0.7,
        [ScriptParam("удельная теплота фазового перехода, Дж/кг")] double latent_heat = 0)
    {
        Require(power > 0, "mw.throughput: мощность должна быть положительной");
        Require(heat_capacity > 0, "mw.throughput: теплоёмкость должна быть положительной");

        return DielectricHeating.ThroughputKgPerHour(power, Load(heat_capacity), delta, coupling, latent_heat);
    }

    [ScriptFn("penetration", "Глубина проникновения поля в материал, м",
        Example = "mw.penetration(frequency: 2.45e9, permittivity: 78, loss_factor: 12)")]
    public static double Penetration(
        [ScriptParam("частота, Гц")] double frequency,
        [ScriptParam("относительная диэлектрическая проницаемость")] double permittivity,
        [ScriptParam("фактор потерь")] double loss_factor)
    {
        Require(frequency > 0, "mw.penetration: частота должна быть положительной");
        Require(permittivity > 0, "mw.penetration: проницаемость должна быть положительной");

        var material = new DielectricMaterial
        {
            Name = "материал",
            RelativePermittivity = permittivity,
            LossFactor = loss_factor,
        };

        return material.PenetrationDepthM(MicrowavePhysics.Wavelength(frequency));
    }

    /// <summary>
    /// Неравномерность прогрева по толщине.
    /// </summary>
    /// <remarks>
    /// Именно эта величина, а не КПД, ограничивает толщину обрабатываемого продукта: единица —
    /// идеально равномерно, десять означает, что поверхность получает на порядок больше центра.
    /// </remarks>
    [ScriptFn("uniformity", "Отношение тепловыделения на поверхности к центру слоя",
        Example = "mw.uniformity(thickness: 0.04, penetration: 0.015)")]
    public static double Uniformity(
        [ScriptParam("толщина слоя, м")] double thickness,
        [ScriptParam("глубина проникновения, м")] double penetration,
        [ScriptParam("облучение с двух сторон")] bool two_sided = false)
    {
        Require(thickness > 0 && penetration > 0, "mw.uniformity: толщина и глубина должны быть положительны");

        return two_sided
            ? DielectricHeating.SurfaceToCenterRatioTwoSided(thickness, penetration)
            : DielectricHeating.SurfaceToCenterRatio(thickness, penetration);
    }

    [ScriptFn("max_thickness", "Предельная толщина слоя при допустимой неравномерности, м",
        Example = "mw.max_thickness(penetration: 0.015, allowed: 2)")]
    public static double MaxThickness(
        [ScriptParam("глубина проникновения, м")] double penetration,
        [ScriptParam("допустимое отношение поверхность/центр")] double allowed = 2)
    {
        Require(penetration > 0, "mw.max_thickness: глубина должна быть положительной");
        Require(allowed > 1, "mw.max_thickness: допустимое отношение должно быть больше единицы");

        return DielectricHeating.MaxThicknessForUniformity(penetration, allowed);
    }

    // --- безопасность ---

    /// <summary>
    /// Предельно допустимый уровень плотности потока энергии.
    /// </summary>
    /// <remarks>
    /// Норматив указывается явно и не имеет значения по умолчанию, которое «обычно подходит»:
    /// пределы СанПиН, ICNIRP и FCC расходятся на порядок, и молчаливый выбор одного из них
    /// превратил бы расчёт защиты в лотерею.
    /// </remarks>
    [ScriptFn("exposure_limit", "ПДУ плотности потока энергии, Вт/м²",
        Example = "mw.exposure_limit(2.45e9, standard: \"sanpin\", category: \"general\")")]
    public static double ExposureLimit(
        [ScriptParam("частота, Гц")] double frequency,
        [ScriptParam("норматив: \"sanpin\", \"icnirp\" либо \"fcc\"")] string standard,
        [ScriptParam("категория: \"general\" — население, \"occupational\" — персонал")] string category = "general")
    {
        Require(frequency > 0, "mw.exposure_limit: частота должна быть положительной");

        return ExposureLimits.PowerDensityLimit(StandardOf(standard), frequency, CategoryOf(category));
    }

    [ScriptFn("field_limit", "ПДУ напряжённости электрического поля, В/м",
        Example = "mw.field_limit(900e6, standard: \"sanpin\")")]
    public static double FieldLimit(
        [ScriptParam("частота, Гц")] double frequency,
        [ScriptParam("норматив: \"sanpin\", \"icnirp\" либо \"fcc\"")] string standard,
        [ScriptParam("категория: \"general\" либо \"occupational\"")] string category = "general")
    {
        Require(frequency > 0, "mw.field_limit: частота должна быть положительной");

        return ExposureLimits.ElectricFieldLimit(StandardOf(standard), frequency, CategoryOf(category));
    }

    // --- антенны ---

    /// <summary>
    /// Расчёт антенны по требуемой ширине луча.
    /// </summary>
    /// <remarks>
    /// Возвращаются и электрические, и габаритные величины сразу: антенна, дающая нужный луч,
    /// но не помещающаяся в цех, — не решение, и узнавать об этом лучше в той же строке.
    /// </remarks>
    [ScriptFn("antenna", "Расчёт антенны: усиление, луч, габариты, запас по пробою", Example = "mw.antenna(\"horn\", frequency: 2.45e9, power: 900, beamwidth: 12)")]
    public static ScriptRecord Antenna(
        [ScriptParam("вид: \"horn\" — рупор, \"lens\" — рупор с линзой, \"parabolic\" — зеркало")] string kind,
        [ScriptParam("частота, Гц")] double frequency,
        [ScriptParam("подводимая мощность, Вт")] double power = 900,
        [ScriptParam("требуемая ширина луча, градусов")] double beamwidth = 5)
    {
        Require(frequency > 0, "mw.antenna: частота должна быть положительной");
        Require(beamwidth > 0, "mw.antenna: ширина луча должна быть положительной");

        AntennaCalculatorBase calculator = kind switch
        {
            "horn" => new HornAntennaCalculator(),
            "lens" => new HornWithLensCalculator(),
            "parabolic" => new ParabolicAntennaCalculator(),
            _ => throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"mw.antenna: неизвестный вид антенны '{kind}'",
                "известны: \"horn\" — пирамидальный рупор, \"lens\" — рупор с линзой, " +
                "\"parabolic\" — параболическое зеркало"),
        };

        AntennaDesignResult result = calculator.Calculate(new AntennaParameters
        {
            FrequencyMHz = frequency / 1e6,
            PowerWatts = power,
            RequiredBeamwidthDegrees = beamwidth,
        });

        return Record(
            ("kind", ScriptValue.Str(calculator.AntennaType)),
            ("gain_dbi", ScriptValue.Num(result.GainDbi)),
            ("efficiency", ScriptValue.Num(result.Efficiency)),
            ("beamwidth_e", ScriptValue.Num(result.BeamwidthEPlane)),
            ("beamwidth_h", ScriptValue.Num(result.BeamwidthHPlane)),
            ("sidelobe_db", ScriptValue.Num(result.SideLobeLevel)),
            ("vswr", ScriptValue.Num(result.VSWR)),
            ("aperture_width", ScriptValue.Num(result.ApertureWidthM)),
            ("aperture_height", ScriptValue.Num(result.ApertureHeightM)),
            ("length", ScriptValue.Num(result.TotalLengthM)),
            ("peak_field", ScriptValue.Num(result.MaxElectricField)),
            ("breakdown_margin", ScriptValue.Num(result.SafetyMargin)));
    }

    // --- внутреннее ---

    /// <summary>
    /// Материал, у которого важна только теплоёмкость.
    /// </summary>
    /// <remarks>
    /// Функции нагрева по массе от диэлектрических свойств не зависят — им нужна теплота, а
    /// не поле. Требовать от скрипта проницаемость там, где она не участвует в расчёте, значит
    /// заставлять придумывать число.
    /// </remarks>
    private static DielectricMaterial Load(double heatCapacity) => new()
    {
        Name = "материал",
        SpecificHeatJPerKgK = heatCapacity,
    };

    private static ExposureStandard StandardOf(string name) => name switch
    {
        "sanpin" => ExposureStandard.Sanpin,
        "icnirp" => ExposureStandard.Icnirp2020,
        "fcc" => ExposureStandard.FccOet65,
        _ => throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"неизвестный норматив '{name}'",
            "известны: \"sanpin\" — СанПиН 1.2.3685-21, \"icnirp\" — ICNIRP 2020, \"fcc\" — FCC OET 65"),
    };

    private static ExposureCategory CategoryOf(string name) => name switch
    {
        "general" => ExposureCategory.General,
        "occupational" => ExposureCategory.Occupational,
        _ => throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"неизвестная категория '{name}'",
            "известны: \"general\" — население, \"occupational\" — персонал"),
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ScriptError(DiagnosticCodes.BadOperand, message);
    }

    private static ScriptRecord Record(params (string Name, ScriptValue Value)[] fields)
    {
        var built = new List<KeyValuePair<string, ScriptValue>>(fields.Length);

        foreach ((string name, ScriptValue value) in fields)
            built.Add(new KeyValuePair<string, ScriptValue>(name, value));

        return ScriptRecord.From(built);
    }
}
