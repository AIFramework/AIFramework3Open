using System.Numerics;
using AI.Microwave.Safety;
using Vector3 = AI.Geometry.Primitives.Vector3;

namespace AI.Microwave.Propagation;

/// <summary>
/// Плоский отражатель: прямоугольник (параллелограмм) либо бесконечная плоскость — стена, пол, земля.
/// </summary>
public sealed class PlanarReflector
{
    private const double Epsilon = 1e-9;
    private readonly Vector3 _u, _v;

    /// <summary>Отражатель-параллелограмм: вершина и два ребра из неё</summary>
    /// <param name="corner">Вершина</param>
    /// <param name="edgeU">Первое ребро</param>
    /// <param name="edgeV">Второе ребро</param>
    /// <param name="material">Материал</param>
    /// <param name="name">Название</param>
    public PlanarReflector(Vector3 corner, Vector3 edgeU, Vector3 edgeV, RadioMaterial material, string name = "")
    {
        ArgumentNullException.ThrowIfNull(material);

        Vector3 normal = edgeU.Cross(edgeV);

        if (normal.Length < 1e-12)
            throw new ArgumentException("Рёбра отражателя не должны быть нулевыми или параллельными", nameof(edgeV));

        Origin = corner;
        _u = edgeU;
        _v = edgeV;
        Normal = normal.Normalized;
        Material = material;
        Name = name;
    }

    private PlanarReflector(Vector3 point, Vector3 normal, RadioMaterial material, string name)
    {
        Origin = point;
        Normal = normal.Normalized;
        Material = material;
        Name = name;
        IsInfinite = true;
    }

    /// <summary>Бесконечная плоскость — например, земля</summary>
    /// <param name="point">Точка плоскости</param>
    /// <param name="normal">Нормаль</param>
    /// <param name="material">Материал</param>
    /// <param name="name">Название</param>
    public static PlanarReflector Infinite(Vector3 point, Vector3 normal, RadioMaterial material, string name = "")
    {
        ArgumentNullException.ThrowIfNull(material);

        return normal.Length < 1e-12
            ? throw new ArgumentException("Нормаль не должна быть нулевой", nameof(normal))
            : new PlanarReflector(point, normal, material, name);
    }

    /// <summary>Точка плоскости; у параллелограмма — его вершина</summary>
    public Vector3 Origin { get; }

    /// <summary>Единичная нормаль</summary>
    public Vector3 Normal { get; }

    /// <summary>Материал</summary>
    public RadioMaterial Material { get; }

    /// <summary>Название</summary>
    public string Name { get; }

    /// <summary>Бесконечна ли плоскость</summary>
    public bool IsInfinite { get; }

    /// <summary>Зеркальное изображение точки в плоскости отражателя</summary>
    /// <param name="point">Точка</param>
    public Vector3 Mirror(Vector3 point) => point - (2 * (point - Origin).Dot(Normal) * Normal);

    /// <summary>Пересекает ли отрезок отражатель строго между концами</summary>
    internal bool TryIntersect(Vector3 from, Vector3 to, out Vector3 point)
    {
        point = default;
        Vector3 direction = to - from;
        double denominator = Normal.Dot(direction);

        if (Math.Abs(denominator) < 1e-15)
            return false;

        double t = Normal.Dot(Origin - from) / denominator;

        if (t <= Epsilon || t >= 1 - Epsilon)
            return false;

        point = from + (t * direction);

        return Contains(point);
    }

    private bool Contains(Vector3 point)
    {
        if (IsInfinite)
            return true;

        Vector3 w = point - Origin;
        double uu = _u.Dot(_u), uv = _u.Dot(_v), vv = _v.Dot(_v);
        double wu = w.Dot(_u), wv = w.Dot(_v);
        double determinant = (uu * vv) - (uv * uv);
        double alpha = ((wu * vv) - (wv * uv)) / determinant;
        double beta = ((wv * uu) - (wu * uv)) / determinant;

        return alpha >= -Epsilon && alpha <= 1 + Epsilon && beta >= -Epsilon && beta <= 1 + Epsilon;
    }

    /// <summary>Название и материал</summary>
    public override string ToString() => $"{(string.IsNullOrEmpty(Name) ? "отражатель" : Name)} ({Material.Name})";
}

/// <summary>Антенна на конце радиолинии: положение, скорость, поляризация, диаграмма направленности</summary>
/// <param name="Position">Положение, м</param>
public sealed record RadioEndpoint(Vector3 Position)
{
    /// <summary>Скорость, м/с — даёт доплеровский сдвиг путей</summary>
    public Vector3 Velocity { get; init; } = Vector3.Zero;

    /// <summary>Направление поляризации; по умолчанию вертикальное</summary>
    public Vector3 Polarization { get; init; } = new(0, 0, 1);

    /// <summary>Диаграмма направленности; по умолчанию изотропная</summary>
    public AntennaPattern Pattern { get; init; } = new IsotropicPattern();

    /// <summary>Усиление в максимуме, дБи</summary>
    public double GainDbi { get; init; }

    /// <summary>Азимут главного направления, градусы, против часовой стрелки от оси X</summary>
    public double BoresightAzimuthDeg { get; init; }

    /// <summary>Угол места главного направления, градусы</summary>
    public double BoresightElevationDeg { get; init; }

    /// <summary>Усиление по амплитуде в направлении, заданном единичным вектором</summary>
    internal double AmplitudeGain(Vector3 direction)
    {
        double azimuth = Math.Atan2(direction.Y, direction.X) * 180 / Math.PI;
        double elevation = Math.Asin(Math.Clamp(direction.Z, -1, 1)) * 180 / Math.PI;

        // Диаграмма отсчитывает азимут от главного направления по часовой стрелке
        double attenuation = Pattern.AttenuationDb(BoresightAzimuthDeg - azimuth, elevation - BoresightElevationDeg);

        return Math.Pow(10, (GainDbi - attenuation) / 20);
    }
}

/// <summary>
/// Трассировка лучей методом изображений: прямой луч и зеркальные отражения от плоских поверхностей.
/// </summary>
/// <remarks>
/// <para>
/// Для последовательности отражателей строятся изображения передатчика — отражение в первом, изображение
/// в втором и так далее, — а точки отражения находятся обратным ходом от приёмника. Путь существует, если
/// каждая точка лежит внутри своего отражателя, и не заслонён, если ни один отрезок не пересекает другие
/// отражатели (стены непрозрачны). Длина пути равна расстоянию от приёмника до последнего изображения.
/// </para>
/// <para>
/// Поле несёт вектор поляризации. При отражении он раскладывается на составляющую, перпендикулярную
/// плоскости падения, и составляющую в плоскости, которые умножаются на коэффициенты Френеля
/// <see cref="FresnelReflection"/>; на приёме берётся проекция на поляризацию приёмной антенны. Поэтому
/// горизонтальная и вертикальная антенны у стены видят разное.
/// </para>
/// <para>
/// Учитываются только зеркальные отражения. Дифракция на рёбрах, прохождение сквозь стены и диффузное
/// рассеяние не моделируются; число путей растёт как (число отражателей)^(порядок).
/// </para>
/// </remarks>
public sealed class RayTracingScene
{
    private readonly List<PlanarReflector> _reflectors = [];

    /// <summary>Создаёт сцену</summary>
    /// <param name="frequencyHz">Несущая, Гц</param>
    public RayTracingScene(double frequencyHz)
    {
        Wave.RequirePositive(frequencyHz, nameof(frequencyHz));

        FrequencyHz = frequencyHz;
    }

    /// <summary>Несущая, Гц</summary>
    public double FrequencyHz { get; }

    /// <summary>Длина волны, м</summary>
    public double WavelengthM => Wave.SpeedOfLight / FrequencyHz;

    /// <summary>Отражатели</summary>
    public IReadOnlyList<PlanarReflector> Reflectors => _reflectors;

    /// <summary>Учитывать ли заслонение отрезков пути отражателями</summary>
    public bool Blocking { get; init; } = true;

    /// <summary>Добавляет отражатель</summary>
    /// <param name="reflector">Отражатель</param>
    public RayTracingScene Add(PlanarReflector reflector)
    {
        ArgumentNullException.ThrowIfNull(reflector);
        _reflectors.Add(reflector);

        return this;
    }

    /// <summary>Находит пути от передатчика к приёмнику</summary>
    /// <param name="transmitter">Передатчик</param>
    /// <param name="receiver">Приёмник</param>
    /// <param name="maxReflections">Наибольший порядок отражений</param>
    public MultipathChannel Trace(RadioEndpoint transmitter, RadioEndpoint receiver, int maxReflections = 2)
    {
        ArgumentNullException.ThrowIfNull(transmitter);
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentOutOfRangeException.ThrowIfNegative(maxReflections);

        var paths = new List<PropagationPath>();
        var sequence = new List<int>();

        void Explore(int depth)
        {
            if (TryBuild(transmitter, receiver, sequence, out PropagationPath? path))
                paths.Add(path!);

            if (depth == maxReflections)
                return;

            for (int r = 0; r < _reflectors.Count; r++)
            {
                if (sequence.Count > 0 && sequence[^1] == r)
                    continue;

                sequence.Add(r);
                Explore(depth + 1);
                sequence.RemoveAt(sequence.Count - 1);
            }
        }

        Explore(0);

        return new MultipathChannel(paths, FrequencyHz);
    }

    private bool TryBuild(RadioEndpoint transmitter, RadioEndpoint receiver, List<int> sequence, out PropagationPath? path)
    {
        path = null;
        int order = sequence.Count;
        var images = new Vector3[order + 1];
        images[0] = transmitter.Position;

        for (int i = 0; i < order; i++)
            images[i + 1] = _reflectors[sequence[i]].Mirror(images[i]);

        var vertices = new Vector3[order + 2];
        vertices[0] = transmitter.Position;
        vertices[order + 1] = receiver.Position;
        Vector3 target = receiver.Position;

        for (int i = order; i >= 1; i--)
        {
            if (!_reflectors[sequence[i - 1]].TryIntersect(target, images[i], out Vector3 point))
                return false;

            vertices[i] = point;
            target = point;
        }

        if (Blocking && IsBlocked(vertices, sequence))
            return false;

        double length = 0;

        for (int i = 0; i + 1 < vertices.Length; i++)
            length += vertices[i].DistanceTo(vertices[i + 1]);

        if (!(length > 0))
            return false;

        Vector3 departure = (vertices[1] - vertices[0]).Normalized;
        Vector3 arrival = (vertices[^1] - vertices[^2]).Normalized;

        // Поляризация передатчика, поперечная к лучу, и её путь через отражения
        FieldVector field = FieldVector.From(Transverse(transmitter.Polarization, departure));
        Vector3 k = departure;

        for (int i = 0; i < order; i++)
        {
            PlanarReflector reflector = _reflectors[sequence[i]];
            Vector3 n = reflector.Normal;
            double grazing = Math.Asin(Math.Clamp(Math.Abs(k.Dot(n)), 0, 1));
            Vector3 s = k.Cross(n);

            // При нормальном падении плоскости падения нет, и базис выбирается произвольно
            s = s.Length < 1e-12 ? Transverse(new Vector3(1, 0, 0), k) : s.Normalized;
            Vector3 incoming = s.Cross(k);
            Vector3 outgoing = (vertices[i + 2] - vertices[i + 1]).Normalized;
            Vector3 reflected = s.Cross(outgoing);

            ReflectionCoefficients gamma = FresnelReflection.Coefficients(reflector.Material, FrequencyHz, grazing);
            Complex perpendicular = field.Dot(s) * gamma.Perpendicular;
            Complex parallel = field.Dot(incoming) * gamma.Parallel;

            field = FieldVector.From(s, perpendicular) + FieldVector.From(reflected, parallel);
            k = outgoing;
        }

        Complex polarizationMatch = field.Dot(Transverse(receiver.Polarization, arrival));
        double antennas = transmitter.AmplitudeGain(departure) * receiver.AmplitudeGain(-arrival);
        Complex gain = Wave.FreeSpaceGain(length, WavelengthM) * polarizationMatch * antennas;
        double doppler = (transmitter.Velocity.Dot(departure) - receiver.Velocity.Dot(arrival)) / WavelengthM;
        Vector3 from = -arrival;

        path = new PropagationPath(length / Wave.SpeedOfLight, gain, doppler, order == 0 ? PathKind.LineOfSight : PathKind.Reflection)
        {
            Order = order,
            LengthMetres = length,
            AzimuthOfDepartureDeg = Math.Atan2(departure.Y, departure.X) * 180 / Math.PI,
            ElevationOfDepartureDeg = Math.Asin(Math.Clamp(departure.Z, -1, 1)) * 180 / Math.PI,
            AzimuthOfArrivalDeg = Math.Atan2(from.Y, from.X) * 180 / Math.PI,
            ElevationOfArrivalDeg = Math.Asin(Math.Clamp(from.Z, -1, 1)) * 180 / Math.PI
        };

        return true;
    }

    private bool IsBlocked(Vector3[] vertices, List<int> sequence)
    {
        for (int i = 0; i + 1 < vertices.Length; i++)
        {
            for (int r = 0; r < _reflectors.Count; r++)
            {
                // Отражатели на концах отрезка его не заслоняют: луч от них и отражается
                bool atStart = i >= 1 && sequence[i - 1] == r;
                bool atEnd = i < sequence.Count && sequence[i] == r;

                if (atStart || atEnd)
                    continue;

                if (_reflectors[r].TryIntersect(vertices[i], vertices[i + 1], out _))
                    return true;
            }
        }

        return false;
    }

    // Составляющая вектора, поперечная направлению луча, единичной длины
    private static Vector3 Transverse(Vector3 vector, Vector3 direction)
    {
        Vector3 transverse = vector - (vector.Dot(direction) * direction);

        if (transverse.Length > 1e-9)
            return transverse.Normalized;

        // Поляризация вдоль луча: берётся любое поперечное направление
        Vector3 helper = Math.Abs(direction.X) < 0.9 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);

        return (helper - (helper.Dot(direction) * direction)).Normalized;
    }

    private readonly record struct FieldVector(Complex X, Complex Y, Complex Z)
    {
        public static FieldVector From(Vector3 direction) => From(direction, Complex.One);

        public static FieldVector From(Vector3 direction, Complex amplitude)
            => new(amplitude * direction.X, amplitude * direction.Y, amplitude * direction.Z);

        public Complex Dot(Vector3 other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

        public static FieldVector operator +(FieldVector a, FieldVector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }
}
