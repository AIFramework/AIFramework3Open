using System.Buffers.Binary;
using System.Globalization;
using AI.Earth.Geodesy;
using AI.Earth.Projections;

namespace AI.Microwave.Propagation;

/// <summary>
/// Рельеф и помехи на местности в локальных координатах: X на восток, Y на север, метры.
/// </summary>
public interface ITerrainModel
{
    /// <summary>Высота поверхности земли над уровнем моря, м</summary>
    double GroundHeightAt(double x, double y);

    /// <summary>Представительная высота помех над землёй — застройки или леса, м (0 на открытой местности)</summary>
    double ClutterHeightAt(double x, double y);

    /// <summary>Лежит ли точка на море: там у земли другие электрические свойства</summary>
    bool IsSeaAt(double x, double y);

    /// <summary>Класс помех в точке — для поправки на окружение антенны по ITU-R P.2108</summary>
    ClutterCategory ClutterAt(double x, double y);
}

/// <summary>Ровная суша или море на постоянной высоте</summary>
public sealed class FlatTerrain : ITerrainModel
{
    /// <summary>Высота над уровнем моря, м</summary>
    public double HeightM { get; init; }

    /// <summary>Высота помех, м</summary>
    public double ClutterHeightM { get; init; }

    /// <summary>Море</summary>
    public bool IsSea { get; init; }

    /// <summary>Класс помех у антенн</summary>
    public ClutterCategory Clutter { get; init; } = ClutterCategory.OpenRural;

    /// <inheritdoc />
    public double GroundHeightAt(double x, double y) => HeightM;

    /// <inheritdoc />
    public double ClutterHeightAt(double x, double y) => ClutterHeightM;

    /// <inheritdoc />
    public bool IsSeaAt(double x, double y) => IsSea;

    /// <inheritdoc />
    public ClutterCategory ClutterAt(double x, double y) => IsSea ? ClutterCategory.WaterSea : Clutter;
}

/// <summary>
/// Рельеф на регулярной сетке с билинейной интерполяцией: узел [i, j] — точка (MinX + j·шаг, MinY + i·шаг)
/// </summary>
/// <remarks>За краем сетки высота продолжается значением на краю.</remarks>
public sealed class GridTerrain : ITerrainModel
{
    private readonly double[,] _heights;
    private readonly double[,]? _clutter;
    private readonly bool[,]? _sea;
    private readonly ClutterCategory[,]? _categories;

    /// <summary>
    /// Создаёт рельеф с картой классов помех: высоты помех в профиле — по <see cref="ItuP2108.ProfileClutterHeightM"/>,
    /// море — класс «вода»
    /// </summary>
    /// <param name="minX">Координата X первого столбца, м</param>
    /// <param name="minY">Координата Y первой строки, м</param>
    /// <param name="spacingM">Шаг сетки, м</param>
    /// <param name="heights">Высоты земли над уровнем моря, м</param>
    /// <param name="clutter">Классы помех той же формы</param>
    public GridTerrain(double minX, double minY, double spacingM, double[,] heights, ClutterCategory[,] clutter)
        : this(minX, minY, spacingM, heights, Map(clutter, ItuP2108.ProfileClutterHeightM), Map(clutter, c => c == ClutterCategory.WaterSea))
    {
        _categories = clutter;
    }

    /// <summary>Создаёт рельеф</summary>
    /// <param name="minX">Координата X первого столбца, м</param>
    /// <param name="minY">Координата Y первой строки, м</param>
    /// <param name="spacingM">Шаг сетки, м</param>
    /// <param name="heights">Высоты земли над уровнем моря [строка — по северу, столбец — по востоку], м</param>
    /// <param name="clutterHeights">Высоты помех той же формы, м; null — открытая местность</param>
    /// <param name="sea">Море в узлах той же формы; null — всюду суша</param>
    public GridTerrain(double minX, double minY, double spacingM, double[,] heights, double[,]? clutterHeights = null, bool[,]? sea = null)
    {
        ArgumentNullException.ThrowIfNull(heights);
        Guard.RequirePositive(spacingM, nameof(spacingM));

        if (heights.GetLength(0) < 2 || heights.GetLength(1) < 2)
            throw new ArgumentException("Нужно хотя бы два узла по каждой оси", nameof(heights));

        if (clutterHeights is not null && (clutterHeights.GetLength(0) != heights.GetLength(0) || clutterHeights.GetLength(1) != heights.GetLength(1)))
            throw new ArgumentException("Сетка помех должна совпадать с сеткой высот", nameof(clutterHeights));

        if (sea is not null && (sea.GetLength(0) != heights.GetLength(0) || sea.GetLength(1) != heights.GetLength(1)))
            throw new ArgumentException("Сетка моря должна совпадать с сеткой высот", nameof(sea));

        MinX = minX;
        MinY = minY;
        SpacingM = spacingM;
        _heights = heights;
        _clutter = clutterHeights;
        _sea = sea;
    }

    /// <summary>Координата X первого столбца, м</summary>
    public double MinX { get; }

    /// <summary>Координата Y первой строки, м</summary>
    public double MinY { get; }

    /// <summary>Шаг сетки, м</summary>
    public double SpacingM { get; }

    /// <summary>Число строк</summary>
    public int Rows => _heights.GetLength(0);

    /// <summary>Число столбцов</summary>
    public int Columns => _heights.GetLength(1);

    /// <inheritdoc />
    public double GroundHeightAt(double x, double y) => Bilinear(_heights, x, y);

    /// <inheritdoc />
    public double ClutterHeightAt(double x, double y) => _clutter is null ? 0 : Bilinear(_clutter, x, y);

    /// <inheritdoc />
    public bool IsSeaAt(double x, double y)
    {
        if (_sea is null)
            return false;

        (int row, int column) = Nearest(x, y);

        return _sea[row, column];
    }

    /// <inheritdoc />
    public ClutterCategory ClutterAt(double x, double y)
    {
        if (_categories is not null)
        {
            (int row, int column) = Nearest(x, y);
            return _categories[row, column];
        }

        return IsSeaAt(x, y) ? ClutterCategory.WaterSea : ClutterCategory.OpenRural;
    }

    private (int Row, int Column) Nearest(double x, double y)
        => ((int)Math.Round(Math.Clamp((y - MinY) / SpacingM, 0, Rows - 1)), (int)Math.Round(Math.Clamp((x - MinX) / SpacingM, 0, Columns - 1)));

    private static T[,] Map<T>(ClutterCategory[,] clutter, Func<ClutterCategory, T> selector)
    {
        ArgumentNullException.ThrowIfNull(clutter);

        var result = new T[clutter.GetLength(0), clutter.GetLength(1)];

        for (int i = 0; i < clutter.GetLength(0); i++)
        {
            for (int j = 0; j < clutter.GetLength(1); j++)
                result[i, j] = selector(clutter[i, j]);
        }

        return result;
    }

    private double Bilinear(double[,] grid, double x, double y)
    {
        double u = Math.Clamp((x - MinX) / SpacingM, 0, Columns - 1);
        double v = Math.Clamp((y - MinY) / SpacingM, 0, Rows - 1);
        int j = Math.Min((int)u, Columns - 2), i = Math.Min((int)v, Rows - 2);
        double s = u - j, t = v - i;

        return ((1 - t) * (((1 - s) * grid[i, j]) + (s * grid[i, j + 1])))
            + (t * (((1 - s) * grid[i + 1, j]) + (s * grid[i + 1, j + 1])));
    }
}

/// <summary>
/// Плитка цифровой модели рельефа SRTM (формат HGT): квадрат в один градус, высоты в метрах как 16-битные
/// целые со старшим байтом вперёд, строки с севера на юг.
/// </summary>
/// <remarks>
/// <para>
/// Имя файла задаёт юго-западный угол: N55E037.hgt — 55° с. ш., 37° в. д. Плитка SRTM3 — 1201 × 1201 узлов
/// (шаг 3″, около 90 м), SRTM1 — 3601 × 3601 (1″, около 30 м); крайние строки и столбцы соседних плиток совпадают.
/// Пропуски записаны как −32768.
/// </para>
/// <para>
/// Для расчётов покрытия плитки переводятся в локальную метрическую сетку (<see cref="ToLocalGrid"/>) — через
/// проекцию UTM из <c>AI.Earth</c>: X и Y — смещения по восточной и северной координатам зоны от точки отсчёта.
/// Сеточный север отличается от истинного на угол сближения меридианов, в пределах десятков километров это доли
/// градуса.
/// </para>
/// </remarks>
public sealed class SrtmTile
{
    /// <summary>Отметка пропуска</summary>
    public const short Void = -32768;

    private readonly short[] _heights;

    /// <summary>Создаёт плитку из высот</summary>
    /// <param name="latitude">Широта южного края, градусы</param>
    /// <param name="longitude">Долгота западного края, градусы</param>
    /// <param name="heights">Высоты по строкам с севера на юг, в строке — с запада на восток; n × n значений</param>
    public SrtmTile(int latitude, int longitude, short[] heights)
    {
        ArgumentNullException.ThrowIfNull(heights);

        int samples = (int)Math.Round(Math.Sqrt(heights.Length));

        if (samples < 2 || samples * samples != heights.Length)
            throw new ArgumentException("Плитка должна быть квадратной, не меньше 2 × 2 узлов", nameof(heights));

        if (latitude is < -90 or > 89)
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Широта южного края — от −90 до 89");

        Latitude = latitude;
        Longitude = longitude;
        Samples = samples;
        _heights = heights;
    }

    /// <summary>Широта южного края, градусы</summary>
    public int Latitude { get; }

    /// <summary>Долгота западного края, градусы</summary>
    public int Longitude { get; }

    /// <summary>Узлов по стороне</summary>
    public int Samples { get; }

    /// <summary>Шаг сетки, градусы</summary>
    public double StepDeg => 1.0 / (Samples - 1);

    /// <summary>Имя файла плитки с юго-западным углом в данной точке, например N55E037</summary>
    public static string TileName(int latitude, int longitude)
        => string.Create(CultureInfo.InvariantCulture, $"{(latitude >= 0 ? 'N' : 'S')}{Math.Abs(latitude):00}{(longitude >= 0 ? 'E' : 'W')}{Math.Abs(longitude):000}");

    /// <summary>Читает файл HGT; угол берётся из имени файла</summary>
    /// <param name="path">Путь к файлу вида N55E037.hgt</param>
    public static SrtmTile Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        string name = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();

        if (name.Length < 7 || (name[0] != 'N' && name[0] != 'S') || (name[3] != 'E' && name[3] != 'W')
            || !int.TryParse(name.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int latitude)
            || !int.TryParse(name.AsSpan(4, 3), NumberStyles.None, CultureInfo.InvariantCulture, out int longitude))
            throw new FormatException($"Имя «{name}» не похоже на плитку SRTM вида N55E037");

        using FileStream stream = File.OpenRead(path);

        return Read(stream, name[0] == 'N' ? latitude : -latitude, name[3] == 'E' ? longitude : -longitude);
    }

    /// <summary>Читает высоты HGT из потока</summary>
    /// <param name="stream">Поток с 2·n² байтами</param>
    /// <param name="latitude">Широта южного края, градусы</param>
    /// <param name="longitude">Долгота западного края, градусы</param>
    public static SrtmTile Read(Stream stream, int latitude, int longitude)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();

        if (bytes.Length % 2 != 0)
            throw new FormatException("В файле HGT нечётное число байт");

        var heights = new short[bytes.Length / 2];

        for (int i = 0; i < heights.Length; i++)
            heights[i] = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(2 * i, 2));

        return new SrtmTile(latitude, longitude, heights);
    }

    /// <summary>Лежит ли точка на плитке</summary>
    public bool Contains(double latitude, double longitude)
        => latitude >= Latitude && latitude <= Latitude + 1 && longitude >= Longitude && longitude <= Longitude + 1;

    /// <summary>
    /// Высота в точке билинейной интерполяцией, м; пропуски пропускаются, NaN — если пропущены все четыре узла
    /// </summary>
    /// <param name="latitude">Широта, градусы</param>
    /// <param name="longitude">Долгота, градусы</param>
    public double HeightAt(double latitude, double longitude)
    {
        if (!Contains(latitude, longitude))
            throw new ArgumentOutOfRangeException(nameof(latitude), $"Точка ({latitude}; {longitude}) вне плитки {TileName(Latitude, Longitude)}");

        double row = (Latitude + 1 - latitude) * (Samples - 1);
        double column = (longitude - Longitude) * (Samples - 1);
        int i = Math.Min((int)row, Samples - 2), j = Math.Min((int)column, Samples - 2);
        double t = row - i, s = column - j;

        double sum = 0, weight = 0;

        void Add(int r, int c, double w)
        {
            short h = _heights[(r * Samples) + c];

            if (h != Void && w > 0)
            {
                sum += w * h;
                weight += w;
            }
        }

        Add(i, j, (1 - t) * (1 - s));
        Add(i, j + 1, (1 - t) * s);
        Add(i + 1, j, t * (1 - s));
        Add(i + 1, j + 1, t * s);

        return weight > 0 ? sum / weight : double.NaN;
    }

    /// <summary>
    /// Локальная метрическая сетка рельефа вокруг точки: X, Y — смещения по UTM от точки отсчёта, пропуски
    /// заполнены средним соседей
    /// </summary>
    /// <param name="tiles">Плитки, покрывающие область</param>
    /// <param name="origin">Точка отсчёта — начало локальных координат</param>
    /// <param name="halfSizeM">Половина стороны квадрата, м</param>
    /// <param name="spacingM">Шаг сетки, м</param>
    public static GridTerrain ToLocalGrid(IReadOnlyList<SrtmTile> tiles, GeoPoint origin, double halfSizeM, double spacingM)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        Guard.RequirePositive(halfSizeM, nameof(halfSizeM));
        Guard.RequirePositive(spacingM, nameof(spacingM));

        UtmPoint center = Utm.Project(origin);
        int nodes = (int)Math.Ceiling(2 * halfSizeM / spacingM) + 1;
        double start = -(nodes - 1) * spacingM / 2;
        var heights = new double[nodes, nodes];

        for (int i = 0; i < nodes; i++)
        {
            for (int j = 0; j < nodes; j++)
            {
                GeoPoint point = Utm.Unproject(center with
                {
                    Easting = center.Easting + start + (j * spacingM),
                    Northing = center.Northing + start + (i * spacingM),
                });

                SrtmTile? tile = tiles.FirstOrDefault(t => t.Contains(point.Latitude, point.Longitude))
                    ?? throw new InvalidOperationException(
                        $"Нет плитки {TileName((int)Math.Floor(point.Latitude), (int)Math.Floor(point.Longitude))} для точки ({point.Latitude:F4}; {point.Longitude:F4})");

                heights[i, j] = tile.HeightAt(point.Latitude, point.Longitude);
            }
        }

        FillVoids(heights);

        return new GridTerrain(start, start, spacingM, heights);
    }

    // Пропуски заполняются средним известных соседей, волной от краёв пропуска внутрь
    private static void FillVoids(double[,] heights)
    {
        int rows = heights.GetLength(0), columns = heights.GetLength(1);
        bool any = true;

        for (int pass = 0; any && pass < rows + columns; pass++)
        {
            any = false;
            var filled = (double[,])heights.Clone();

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    if (!double.IsNaN(heights[i, j]))
                        continue;

                    double sum = 0;
                    int count = 0;

                    for (int di = -1; di <= 1; di++)
                    {
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            int r = i + di, c = j + dj;

                            if (r >= 0 && r < rows && c >= 0 && c < columns && !double.IsNaN(heights[r, c]))
                            {
                                sum += heights[r, c];
                                count++;
                            }
                        }
                    }

                    if (count > 0)
                        filled[i, j] = sum / count;
                    else
                        any = true;
                }
            }

            Array.Copy(filled, heights, filled.Length);
        }

        if (any)
            throw new InvalidOperationException("В области нет ни одной известной высоты");
    }
}

/// <summary>
/// Профиль трассы: расстояния от передатчика, высоты земли, высоты помех и признак моря в точках.
/// </summary>
public sealed class TerrainProfile
{
    private readonly double[] _distances;
    private readonly double[] _ground;
    private readonly double[] _clutter;
    private readonly bool[] _sea;

    /// <summary>Создаёт профиль</summary>
    /// <param name="distancesM">Расстояния от передатчика по возрастанию, первое — 0, м</param>
    /// <param name="groundHeightsM">Высоты земли над уровнем моря, м</param>
    /// <param name="clutterHeightsM">Высоты помех над землёй, м; null — нули</param>
    /// <param name="sea">Море в точках; null — всюду суша</param>
    public TerrainProfile(IReadOnlyList<double> distancesM, IReadOnlyList<double> groundHeightsM, IReadOnlyList<double>? clutterHeightsM = null, IReadOnlyList<bool>? sea = null)
    {
        ArgumentNullException.ThrowIfNull(distancesM);
        ArgumentNullException.ThrowIfNull(groundHeightsM);

        int n = distancesM.Count;

        if (n < 3)
            throw new ArgumentException("В профиле нужно хотя бы три точки: концы и одна промежуточная", nameof(distancesM));

        if (groundHeightsM.Count != n || (clutterHeightsM is not null && clutterHeightsM.Count != n) || (sea is not null && sea.Count != n))
            throw new ArgumentException("Все массивы профиля должны быть одной длины");

        if (distancesM[0] != 0)
            throw new ArgumentException("Профиль начинается у передатчика: первое расстояние — 0", nameof(distancesM));

        for (int i = 1; i < n; i++)
        {
            if (!(distancesM[i] > distancesM[i - 1]))
                throw new ArgumentException("Расстояния профиля должны строго возрастать", nameof(distancesM));
        }

        _distances = [.. distancesM];
        _ground = [.. groundHeightsM];
        _clutter = clutterHeightsM is null ? new double[n] : [.. clutterHeightsM];
        _sea = sea is null ? new bool[n] : [.. sea];
    }

    /// <summary>Расстояния от передатчика, м</summary>
    public IReadOnlyList<double> DistancesM => _distances;

    /// <summary>Высоты земли над уровнем моря, м</summary>
    public IReadOnlyList<double> GroundHeightsM => _ground;

    /// <summary>Высоты помех над землёй, м</summary>
    public IReadOnlyList<double> ClutterHeightsM => _clutter;

    /// <summary>Море в точках</summary>
    public IReadOnlyList<bool> Sea => _sea;

    /// <summary>Число точек</summary>
    public int Count => _distances.Length;

    /// <summary>Длина трассы, м</summary>
    public double LengthM => _distances[^1];

    /// <summary>Доля длины трассы над морем ω: отрезок с морем на обоих концах — целиком, на одном — наполовину</summary>
    public double SeaFraction
    {
        get
        {
            double sea = 0;

            for (int i = 1; i < _distances.Length; i++)
                sea += (_distances[i] - _distances[i - 1]) * (((_sea[i] ? 1 : 0) + (_sea[i - 1] ? 1 : 0)) / 2.0);

            return sea / LengthM;
        }
    }

    /// <summary>Профиль по рельефу между двумя точками с шагом не больше заданного; не короче метра и трёх точек</summary>
    /// <param name="terrain">Рельеф</param>
    /// <param name="x1">X передатчика, м</param>
    /// <param name="y1">Y передатчика, м</param>
    /// <param name="x2">X приёмника, м</param>
    /// <param name="y2">Y приёмника, м</param>
    /// <param name="maxStepM">Наибольший шаг, м</param>
    public static TerrainProfile Between(ITerrainModel terrain, double x1, double y1, double x2, double y2, double maxStepM)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        Guard.RequirePositive(maxStepM, nameof(maxStepM));

        double dx = x2 - x1, dy = y2 - y1;
        double length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length < PropagationGeometry.MinimumDistanceM)
        {
            // Точка у самой мачты: трасса в метр в произвольную сторону
            dx = PropagationGeometry.MinimumDistanceM;
            dy = 0;
            length = PropagationGeometry.MinimumDistanceM;
        }

        int segments = Math.Max(2, (int)Math.Ceiling(length / maxStepM));
        var distances = new double[segments + 1];
        var ground = new double[segments + 1];
        var clutter = new double[segments + 1];
        var sea = new bool[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            double t = (double)i / segments;
            double x = x1 + (t * dx), y = y1 + (t * dy);

            distances[i] = t * length;
            ground[i] = terrain.GroundHeightAt(x, y);
            clutter[i] = terrain.ClutterHeightAt(x, y);
            sea[i] = terrain.IsSeaAt(x, y);
        }

        return new TerrainProfile(distances, ground, clutter, sea);
    }
}
