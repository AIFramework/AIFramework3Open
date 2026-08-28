namespace AI.Microwave.Safety;

/// <summary>
/// Диаграмма направленности в виде ослабления относительно максимума.
/// </summary>
/// <remarks>
/// Углы: азимут отсчитывается от главного направления по часовой стрелке,
/// 0...360 град; угол места - от горизонта, положительный вверх, -90...+90.
/// Возвращаемое ослабление неотрицательно: 0 дБ в максимуме.
/// </remarks>
public abstract class AntennaPattern
{
    /// <summary>Ослабление относительно максимума, дБ (0 в главном направлении).</summary>
    public abstract double AttenuationDb(double azimuthDeg, double elevationDeg);

    /// <summary>Относительное усиление по мощности, разы (1 в максимуме).</summary>
    public double RelativeGain(double azimuthDeg, double elevationDeg)
        => Math.Pow(10.0, -AttenuationDb(azimuthDeg, elevationDeg) / 10.0);

    /// <summary>Приводит угол к диапазону -180...180 град.</summary>
    protected static double Wrap180(double angleDeg)
    {
        double a = (angleDeg + 180.0) % 360.0;
        if (a < 0) a += 360.0;
        return a - 180.0;
    }

    /// <summary>Приводит угол к диапазону 0...360 град.</summary>
    protected static double Wrap360(double angleDeg)
    {
        double a = angleDeg % 360.0;
        return a < 0 ? a + 360.0 : a;
    }
}

/// <summary>Изотропный излучатель: одинаково во все стороны.</summary>
public sealed class IsotropicPattern : AntennaPattern
{
    /// <inheritdoc/>
    public override double AttenuationDb(double azimuthDeg, double elevationDeg) => 0.0;
}

/// <summary>
/// Гауссова аппроксимация по двум ширинам луча с полкой на уровне
/// заднего излучения. Достаточна, когда паспортной ДН нет, а порядок
/// величины нужен.
/// </summary>
/// <remarks>
/// Та же аппроксимация, что в <see cref="Physics.ApertureIllumination"/>:
/// спад 12*(theta/theta_0.5)^2 дБ. Складывается по двум плоскостям.
/// </remarks>
public sealed class GaussianPattern : AntennaPattern
{
    /// <summary>Ширина луча в горизонтальной плоскости по уровню -3 дБ, град.</summary>
    public double AzimuthBeamwidthDeg { get; init; } = 65;

    /// <summary>Ширина луча в вертикальной плоскости по уровню -3 дБ, град.</summary>
    public double ElevationBeamwidthDeg { get; init; } = 7;

    /// <summary>Отношение вперёд/назад, дБ: ниже этого уровня ДН не опускается.</summary>
    public double FrontToBackDb { get; init; } = 25;

    /// <inheritdoc/>
    public override double AttenuationDb(double azimuthDeg, double elevationDeg)
    {
        double az = Wrap180(azimuthDeg);
        double el = Wrap180(elevationDeg);

        double a = AzimuthBeamwidthDeg > 0 ? 12.0 * Math.Pow(az / AzimuthBeamwidthDeg, 2) : 0.0;
        double e = ElevationBeamwidthDeg > 0 ? 12.0 * Math.Pow(el / ElevationBeamwidthDeg, 2) : 0.0;

        return Math.Min(a + e, FrontToBackDb);
    }
}

/// <summary>
/// Табличная ДН по двум сечениям - горизонтальному и вертикальному,
/// как её публикуют производители секторных антенн.
/// </summary>
/// <remarks>
/// Полная ДН собирается сложением ослаблений двух сечений в децибелах.
/// Это стандартный для расчёта площадок приём: он точен в плоскостях
/// сечений и консервативен между ними.
/// </remarks>
public sealed class TabulatedPattern : AntennaPattern
{
    private readonly double[] _horizontal = new double[360];
    private readonly double[] _vertical = new double[360];

    /// <summary>Название антенны из паспорта.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Частота, для которой снята ДН, МГц.</summary>
    public double FrequencyMHz { get; init; }

    /// <summary>Паспортное усиление, дБи.</summary>
    public double GainDbi { get; init; }

    /// <summary>Ослабление в горизонтальном сечении по градусам, дБ.</summary>
    public IReadOnlyList<double> Horizontal => _horizontal;

    /// <summary>Ослабление в вертикальном сечении по градусам, дБ.</summary>
    public IReadOnlyList<double> Vertical => _vertical;

    /// <summary>Задаёт горизонтальное сечение: пары «угол, ослабление в дБ».</summary>
    public void SetHorizontal(IEnumerable<(double AngleDeg, double AttenuationDb)> samples)
        => Fill(_horizontal, samples);

    /// <summary>Задаёт вертикальное сечение: пары «угол, ослабление в дБ».</summary>
    public void SetVertical(IEnumerable<(double AngleDeg, double AttenuationDb)> samples)
        => Fill(_vertical, samples);

    /// <inheritdoc/>
    public override double AttenuationDb(double azimuthDeg, double elevationDeg)
        => Sample(_horizontal, Wrap360(azimuthDeg)) + Sample(_vertical, Wrap360(-elevationDeg));

    /// <summary>
    /// Разбор файла MSI Planet - формата, в котором ДН выдают почти все
    /// производители антенн базовых станций.
    /// </summary>
    /// <remarks>
    /// Усиление в паспорте бывает указано в дБд; такое значение приводится
    /// к дБи прибавлением 2.15.
    /// </remarks>
    public static TabulatedPattern ParseMsi(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string name = string.Empty;
        double frequency = 0, gain = 0;
        var horizontal = new List<(double, double)>();
        var vertical = new List<(double, double)>();
        List<(double, double)>? current = null;

        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            string[] parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            string key = parts[0].ToUpperInvariant();

            switch (key)
            {
                case "NAME":
                    name = string.Join(' ', parts.Skip(1));
                    continue;
                case "FREQUENCY":
                    frequency = ParseNumber(parts, 1);
                    continue;
                case "GAIN":
                    gain = ParseNumber(parts, 1);
                    if (parts.Length > 2 && parts[2].StartsWith("dBd", StringComparison.OrdinalIgnoreCase))
                        gain += 2.15;
                    continue;
                case "HORIZONTAL":
                    current = horizontal;
                    continue;
                case "VERTICAL":
                    current = vertical;
                    continue;
            }

            if (current is null || parts.Length < 2) continue;
            if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double angle))
                continue;

            current.Add((angle, ParseNumber(parts, 1)));
        }

        var pattern = new TabulatedPattern
        {
            Name = name,
            FrequencyMHz = frequency,
            GainDbi = gain,
        };

        pattern.SetHorizontal(horizontal.Select(p => (p.Item1, p.Item2)));
        pattern.SetVertical(vertical.Select(p => (p.Item1, p.Item2)));
        return pattern;
    }

    private static double ParseNumber(string[] parts, int index)
        => index < parts.Length
           && double.TryParse(parts[index], System.Globalization.NumberStyles.Float,
               System.Globalization.CultureInfo.InvariantCulture, out double v)
            ? v
            : 0.0;

    /// <summary>Раскладывает выборки по целым градусам, недостающие интерполируя.</summary>
    private static void Fill(double[] target, IEnumerable<(double AngleDeg, double AttenuationDb)> samples)
    {
        var known = new SortedDictionary<int, double>();
        foreach (var (angle, attenuation) in samples)
        {
            int index = ((int)Math.Round(angle) % 360 + 360) % 360;
            known[index] = attenuation;
        }

        if (known.Count == 0)
        {
            Array.Clear(target);
            return;
        }

        int[] angles = [.. known.Keys];
        for (int deg = 0; deg < 360; deg++)
        {
            if (known.TryGetValue(deg, out double exact))
            {
                target[deg] = exact;
                continue;
            }

            // Ближайшие известные углы слева и справа по кольцу.
            int lower = angles[^1], upper = angles[0];
            foreach (int a in angles)
            {
                if (a < deg) lower = a;
                if (a > deg) { upper = a; break; }
            }

            double span = ((upper - lower) % 360 + 360) % 360;
            double offset = ((deg - lower) % 360 + 360) % 360;
            double t = span > 0 ? offset / span : 0.0;
            target[deg] = known[lower] + t * (known[upper] - known[lower]);
        }
    }

    private static double Sample(double[] table, double angleDeg)
    {
        double x = angleDeg;
        int i0 = (int)Math.Floor(x) % 360;
        int i1 = (i0 + 1) % 360;
        double t = x - Math.Floor(x);
        return table[i0] + t * (table[i1] - table[i0]);
    }
}
