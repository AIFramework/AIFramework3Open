using System.Globalization;
using System.IO.Compression;
using System.Net;
using AI.Earth.Geodesy;

namespace AI.Microwave.Propagation;

/// <summary>
/// Загрузка плиток рельефа SRTM из открытого хранилища с кэшем на диске.
/// </summary>
/// <remarks>
/// <para>
/// По умолчанию плитки берутся из открытого набора AWS Terrain Tiles (Mapzen/Tilezen) в формате skadi: квадрат
/// в один градус, файл HGT, сжатый gzip, по адресу
/// <c>https://s3.amazonaws.com/elevation-tiles-prod/skadi/N55/N55E037.hgt.gz</c>. Набор собран из SRTM и других
/// источников, высоты — над геоидом EGM96. Адрес задаётся шаблоном <see cref="UrlTemplate"/>: {0} — полоса широт
/// вида N55, {1} — имя плитки N55E037; шаблон без .gz читается как несжатый HGT.
/// </para>
/// <para>
/// Скачанная плитка кладётся в <see cref="CacheDirectory"/> как N55E037.hgt и повторно не загружается. Над открытым
/// морем плиток нет: ответ 404 по умолчанию превращается в плитку на уровне моря.
/// </para>
/// </remarks>
public sealed class SrtmDownloader
{
    /// <summary>Шаблон адреса открытого набора AWS Terrain Tiles</summary>
    public const string TerrainTilesUrl = "https://s3.amazonaws.com/elevation-tiles-prod/skadi/{0}/{1}.hgt.gz";

    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromMinutes(10) };

    private readonly HttpClient _http;

    /// <summary>Создаёт загрузчик</summary>
    /// <param name="cacheDirectory">Каталог кэша плиток</param>
    /// <param name="httpClient">Клиент HTTP — например с прокси; null — общий клиент</param>
    public SrtmDownloader(string cacheDirectory, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        CacheDirectory = cacheDirectory;
        _http = httpClient ?? SharedClient;
    }

    /// <summary>Каталог кэша</summary>
    public string CacheDirectory { get; }

    /// <summary>Шаблон адреса плитки: {0} — полоса широт (N55), {1} — имя плитки (N55E037)</summary>
    public string UrlTemplate { get; init; } = TerrainTilesUrl;

    /// <summary>Считать отсутствующую плитку (404) морем на нулевой высоте, а не ошибкой</summary>
    public bool MissingTileIsSea { get; init; } = true;

    /// <summary>Адрес плитки с юго-западным углом в данной точке</summary>
    /// <param name="latitude">Широта южного края, градусы</param>
    /// <param name="longitude">Долгота западного края, градусы</param>
    public string TileUrl(int latitude, int longitude)
    {
        string name = SrtmTile.TileName(latitude, longitude);

        return string.Format(CultureInfo.InvariantCulture, UrlTemplate, name[..3], name);
    }

    /// <summary>Плитка из кэша или из сети</summary>
    /// <param name="latitude">Широта южного края, градусы</param>
    /// <param name="longitude">Долгота западного края, градусы</param>
    /// <param name="cancellationToken">Отмена</param>
    public async Task<SrtmTile> GetTileAsync(int latitude, int longitude, CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(CacheDirectory, SrtmTile.TileName(latitude, longitude) + ".hgt");

        if (!File.Exists(path))
            await DownloadAsync(latitude, longitude, path, cancellationToken).ConfigureAwait(false);

        return SrtmTile.Read(path);
    }

    /// <summary>Все плитки, покрывающие квадрат вокруг точки, с запасом на повороте сетки UTM</summary>
    /// <param name="origin">Центр квадрата</param>
    /// <param name="halfSizeM">Половина стороны квадрата, м</param>
    /// <param name="cancellationToken">Отмена</param>
    public async Task<IReadOnlyList<SrtmTile>> GetTilesAsync(GeoPoint origin, double halfSizeM, CancellationToken cancellationToken = default)
    {
        var tiles = new List<SrtmTile>();

        foreach ((int latitude, int longitude) in TilesAround(origin, halfSizeM))
            tiles.Add(await GetTileAsync(latitude, longitude, cancellationToken).ConfigureAwait(false));

        return tiles;
    }

    /// <summary>Рельеф квадрата вокруг точки в локальных метрах: загрузка плиток и <see cref="SrtmTile.ToLocalGrid"/></summary>
    /// <param name="origin">Центр квадрата — начало локальных координат</param>
    /// <param name="halfSizeM">Половина стороны, м</param>
    /// <param name="spacingM">Шаг сетки, м</param>
    /// <param name="cancellationToken">Отмена</param>
    public async Task<GridTerrain> LoadTerrainAsync(GeoPoint origin, double halfSizeM, double spacingM, CancellationToken cancellationToken = default)
        => SrtmTile.ToLocalGrid(await GetTilesAsync(origin, halfSizeM, cancellationToken).ConfigureAwait(false), origin, halfSizeM, spacingM);

    /// <summary>Юго-западные углы плиток, покрывающих квадрат вокруг точки</summary>
    /// <param name="origin">Центр</param>
    /// <param name="halfSizeM">Половина стороны, м</param>
    public static IReadOnlyList<(int Latitude, int Longitude)> TilesAround(GeoPoint origin, double halfSizeM)
    {
        Guard.RequirePositive(halfSizeM, nameof(halfSizeM));

        // Угол квадрата лежит на половине диагонали; запас — на поворот сетки UTM и сжатие градуса долготы
        double reach = halfSizeM * Math.Sqrt(2) * 1.1;
        double latitudeSpan = reach / 110_574 + 0.01;
        double longitudeSpan = reach / (111_320 * Math.Max(Math.Cos(origin.Latitude * Math.PI / 180), 0.01)) + 0.01;

        int south = (int)Math.Floor(Math.Max(origin.Latitude - latitudeSpan, -90));
        int north = (int)Math.Floor(Math.Min(origin.Latitude + latitudeSpan, 89.999999));
        int west = (int)Math.Floor(origin.Longitude - longitudeSpan);
        int east = (int)Math.Floor(origin.Longitude + longitudeSpan);
        var tiles = new List<(int, int)>();

        for (int latitude = south; latitude <= north; latitude++)
        {
            for (int longitude = west; longitude <= east; longitude++)
                tiles.Add((latitude, (int)Math.Floor(GeoPoint.Normalize(longitude + 0.5))));
        }

        return tiles.Distinct().ToList();
    }

    private async Task DownloadAsync(int latitude, int longitude, string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(CacheDirectory);

        string url = TileUrl(latitude, longitude);
        string partial = path + ".part";

        using (HttpResponseMessage response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
        {
            if (response.StatusCode == HttpStatusCode.NotFound && MissingTileIsSea)
            {
                // Над морем плиток нет: уровень моря, 2 × 2 узла
                await File.WriteAllBytesAsync(partial, new byte[8], cancellationToken).ConfigureAwait(false);
            }
            else
            {
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Плитка {SrtmTile.TileName(latitude, longitude)} не загружена: {(int)response.StatusCode} {response.ReasonPhrase} ({url})");

                await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using Stream source = url.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                    ? new GZipStream(body, CompressionMode.Decompress)
                    : body;
                await using FileStream file = File.Create(partial);
                await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }
        }

        File.Move(partial, path, overwrite: true);
    }
}
