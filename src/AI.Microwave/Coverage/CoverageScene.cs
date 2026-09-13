using AI.Microwave.Propagation;
using AI.Microwave.Safety;
using AI.Statistics;
using Vector3 = AI.Geometry.Primitives.Vector3;

namespace AI.Microwave.Coverage;

/// <summary>Прямоугольная сетка точек расчёта: столбцы вдоль X (восток), строки вдоль Y (север)</summary>
public sealed class CoverageGrid
{
    /// <summary>Создаёт сетку</summary>
    /// <param name="minX">Западный край, м</param>
    /// <param name="minY">Южный край, м</param>
    /// <param name="cellSizeM">Размер ячейки, м</param>
    /// <param name="columns">Число столбцов</param>
    /// <param name="rows">Число строк</param>
    public CoverageGrid(double minX, double minY, double cellSizeM, int columns, int rows)
    {
        if (!double.IsFinite(minX) || !double.IsFinite(minY))
            throw new ArgumentOutOfRangeException(nameof(minX), "Край сетки — конечное число");

        Guard.RequirePositive(cellSizeM, nameof(cellSizeM));

        if (columns < 1 || rows < 1)
            throw new ArgumentOutOfRangeException(nameof(columns), "Нужна хотя бы одна строка и один столбец");

        MinX = minX;
        MinY = minY;
        CellSizeM = cellSizeM;
        Columns = columns;
        Rows = rows;
    }

    /// <summary>Квадрат со стороной не меньше 2·halfSizeM вокруг точки</summary>
    /// <param name="centerX">Центр по востоку, м</param>
    /// <param name="centerY">Центр по северу, м</param>
    /// <param name="halfSizeM">Половина стороны, м</param>
    /// <param name="cellSizeM">Размер ячейки, м</param>
    public static CoverageGrid Square(double centerX, double centerY, double halfSizeM, double cellSizeM)
    {
        Guard.RequirePositive(halfSizeM, nameof(halfSizeM));
        Guard.RequirePositive(cellSizeM, nameof(cellSizeM));

        int cells = (int)Math.Ceiling(2 * halfSizeM / cellSizeM);
        double half = cells * cellSizeM / 2;

        return new CoverageGrid(centerX - half, centerY - half, cellSizeM, cells, cells);
    }

    /// <summary>Западный край, м</summary>
    public double MinX { get; }

    /// <summary>Южный край, м</summary>
    public double MinY { get; }

    /// <summary>Размер ячейки, м</summary>
    public double CellSizeM { get; }

    /// <summary>Число столбцов</summary>
    public int Columns { get; }

    /// <summary>Число строк</summary>
    public int Rows { get; }

    /// <summary>Число ячеек</summary>
    public int Count => Columns * Rows;

    /// <summary>Координата X центра столбца, м</summary>
    public double X(int column) => MinX + ((column + 0.5) * CellSizeM);

    /// <summary>Координата Y центра строки, м</summary>
    public double Y(int row) => MinY + ((row + 0.5) * CellSizeM);

    internal double[] Xs() => Enumerable.Range(0, Columns).Select(X).ToArray();

    internal double[] Ys() => Enumerable.Range(0, Rows).Select(Y).ToArray();
}

/// <summary>Абонентский приёмник: высота, антенна, шум, полоса и место — улица, здание или машина</summary>
public sealed record CoverageReceiver
{
    /// <summary>Высота антенны над землёй, м</summary>
    public double HeightM { get; init; } = 1.5;

    /// <summary>Усиление антенны, дБи</summary>
    public double GainDbi { get; init; }

    /// <summary>Коэффициент шума, дБ; у телефонов обычно 7–9</summary>
    public double NoiseFigureDb { get; init; } = 7;

    /// <summary>Полоса канала, Гц</summary>
    public double BandwidthHz { get; init; } = 20e6;

    /// <summary>Прочие потери — тело, кабель, запас на замирания, дБ</summary>
    public double OtherLossesDb { get; init; }

    /// <summary>Где абонент</summary>
    public TerminalEnvironment Environment { get; init; } = TerminalEnvironment.Outdoor;

    /// <summary>Расстояние от стены внутрь здания, м</summary>
    public double IndoorDistanceM { get; init; } = PenetrationLoss.MeanIndoorDistanceM;

    /// <summary>Мощность шума в полосе, дБм</summary>
    public double NoiseDbm => LinkBudget.ThermalNoiseDbm(BandwidthHz, NoiseFigureDb);

    /// <summary>Разброс потерь на проникновение, дБ</summary>
    public double PenetrationSigmaDb => PenetrationLoss.SigmaDb(Environment);
}

/// <summary>Покрытие в точке по средним потерям</summary>
/// <param name="X">Координата на восток, м</param>
/// <param name="Y">Координата на север, м</param>
/// <param name="ServingSector">Номер обслуживающего сектора — с наибольшей средней мощностью</param>
/// <param name="ReceivedPowerDbm">Средняя мощность от обслуживающего сектора в полосе, дБм</param>
/// <param name="InterferenceDbm">Суммарная помеха от секторов той же несущей, дБм; −∞, если их нет</param>
/// <param name="SinrDb">Отношение сигнала к помехе и шуму, дБ</param>
/// <param name="CoverageProbability">Вероятность, что сигнал обслуживающего сектора с затенением выше порога</param>
/// <param name="SpectralEfficiency">Спектральная эффективность, бит/с/Гц</param>
/// <param name="ThroughputBps">Скорость одного абонента на всей полосе, бит/с</param>
public sealed record PointCoverage(
    double X,
    double Y,
    int ServingSector,
    double ReceivedPowerDbm,
    double InterferenceDbm,
    double SinrDb,
    double CoverageProbability,
    double SpectralEfficiency,
    double ThroughputBps);

/// <summary>
/// Сеть базовых станций и расчёт её покрытия: принимаемая мощность, обслуживающий сектор, SINR, вероятность
/// покрытия и скорость по сетке точек.
/// </summary>
/// <remarks>
/// <para>
/// Сектор — <see cref="RadiationSource"/>: положение, азимут от севера, наклон, мощность, фидер, усиление и
/// диаграмма, в том числе паспортная из файла MSI. Для покрытия берётся пиковая ЭИИМ: доля времени излучения и
/// коэффициент снижения мощности служат усреднению облучения и здесь не учитываются. Потери даёт любая
/// <see cref="IPropagationModel"/>, к ним добавляются потери на проникновение (<see cref="PenetrationLoss"/>).
/// </para>
/// <para>
/// Карта по средним (<see cref="Compute"/>) строится без случайности: мощность каждого сектора усреднена по
/// состоянию прямой видимости, обслуживающий сектор — с наибольшей мощностью, помеха — сумма секторов той же
/// несущей с долей нагрузки, шум — k·T·B·NF. Вероятность покрытия по сигналу при этом точная для обслуживающего
/// сектора: смесь нормальных распределений по состояниям видимости с затенением и разбросом проникновения.
/// </para>
/// <para>
/// Моделирование (<see cref="Simulate"/>) разыгрывает затенение и видимость как пространственно согласованные
/// карты: у каждого сайта своё поле затенения с расстоянием корреляции модели, смешанное с общим полем так, что
/// корреляция затенения между сайтами равна заданной (0,5 по TR 36.942), секторы одного сайта затенены
/// одинаково. Так получается распределение SINR по площади — то, чем в 3GPP сравнивают конфигурации сетей.
/// </para>
/// </remarks>
public sealed class CoverageScene
{
    // Секторы с несущими ближе этого мешают друг другу
    private const double SameCarrierToleranceHz = 1;

    // Гармоник в картах затенения: для карты покрытия распределение уже близко к нормальному
    private const int FieldHarmonics = 256;

    /// <summary>Создаёт сцену</summary>
    /// <param name="model">Модель потерь</param>
    public CoverageScene(IPropagationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        Model = model;
    }

    /// <summary>Модель потерь</summary>
    public IPropagationModel Model { get; }

    /// <summary>Секторы сети</summary>
    public List<RadiationSource> Sectors { get; } = [];

    /// <summary>Абонентский приёмник</summary>
    public CoverageReceiver Receiver { get; init; } = new();

    /// <summary>Порог мощности сигнала — чувствительность, дБм</summary>
    public double SignalThresholdDbm { get; init; } = -100;

    /// <summary>Порог SINR, ниже которого связи нет, дБ</summary>
    public double SinrThresholdDb { get; init; } = -5;

    /// <summary>Нагрузка соседних секторов — доля ресурса, на которой они излучают, от 0 до 1</summary>
    public double NeighbourLoad { get; init; } = 1;

    /// <summary>Корреляция затенения между разными сайтами</summary>
    public double InterSiteShadowCorrelation { get; init; } = 0.5;

    /// <summary>Пересчёт SINR в скорость</summary>
    public ThroughputMapping Throughput { get; init; } = ThroughputMapping.Lte36942Downlink;

    /// <summary>Добавляет сектор и возвращает его</summary>
    /// <param name="sector">Сектор</param>
    public RadiationSource Add(RadiationSource sector)
    {
        ArgumentNullException.ThrowIfNull(sector);

        Sectors.Add(sector);
        return sector;
    }

    /// <summary>
    /// Добавляет гексагональную сеть: центральный сайт и <paramref name="rings"/> колец вокруг, у каждого сайта
    /// одинаковые секторы по образцу
    /// </summary>
    /// <remarks>
    /// Соседние сайты стоят на расстоянии <paramref name="interSiteDistanceM"/> по направлениям 0°, 60°, … от оси X,
    /// как в TR 38.901 (рисунок 7.8-1); три сектора смотрят по азимутам 60°, 180° и 300° от севера — это 30°, 270° и
    /// 150° от оси X, в промежутки между соседями. Колец 1 — 7 сайтов, 2 — 19.
    /// </remarks>
    /// <param name="rings">Число колец вокруг центра</param>
    /// <param name="interSiteDistanceM">Расстояние между соседними сайтами, м</param>
    /// <param name="template">Образец сектора: высота берётся из его Z, мощность, частота и диаграмма — все его</param>
    /// <param name="sectorsPerSite">Секторов на сайте</param>
    public IReadOnlyList<RadiationSource> AddHexagonalNetwork(int rings, double interSiteDistanceM, RadiationSource template, int sectorsPerSite = 3)
    {
        ArgumentNullException.ThrowIfNull(template);
        Guard.RequirePositive(interSiteDistanceM, nameof(interSiteDistanceM));

        if (rings < 0)
            throw new ArgumentOutOfRangeException(nameof(rings), rings, "Число колец неотрицательно");

        if (sectorsPerSite < 1)
            throw new ArgumentOutOfRangeException(nameof(sectorsPerSite), sectorsPerSite, "Нужен хотя бы один сектор");

        var added = new List<RadiationSource>();
        int site = 0;

        for (int q = -rings; q <= rings; q++)
        {
            for (int r = Math.Max(-rings, -q - rings); r <= Math.Min(rings, -q + rings); r++)
            {
                double x = interSiteDistanceM * (q + (r / 2.0));
                double y = interSiteDistanceM * r * Math.Sqrt(3) / 2;
                site++;

                for (int k = 0; k < sectorsPerSite; k++)
                {
                    RadiationSource sector = Copy(template);
                    sector.Name = $"{template.Name} {site}.{k + 1}";
                    sector.Position = new SitePoint(x, y, template.Position.Z);
                    sector.AzimuthDeg = (60 + (360.0 * k / sectorsPerSite)) % 360;
                    added.Add(Add(sector));
                }
            }
        }

        return added;
    }

    /// <summary>Покрытие в точке по средним потерям</summary>
    /// <param name="x">Координата на восток, м</param>
    /// <param name="y">Координата на север, м</param>
    public PointCoverage Evaluate(double x, double y)
    {
        RequireSectors();

        var links = new SectorLink[Sectors.Count];
        FillLinks(links, 0, x, y, SharedLosses());

        return Summarize(x, y, links, Carriers(), Math.Pow(10, Receiver.NoiseDbm / 10));
    }

    /// <summary>Карта покрытия по средним потерям</summary>
    /// <param name="grid">Сетка точек</param>
    public CoverageMap Compute(CoverageGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        RequireSectors();

        int[] carriers = Carriers();
        int[] shared = SharedLosses();
        double noise = Math.Pow(10, Receiver.NoiseDbm / 10);
        var cells = new PointCoverage[grid.Rows, grid.Columns];

        Parallel.For(0, grid.Rows, row =>
        {
            var links = new SectorLink[Sectors.Count];

            for (int column = 0; column < grid.Columns; column++)
            {
                double x = grid.X(column), y = grid.Y(row);

                FillLinks(links, 0, x, y, shared);
                cells[row, column] = Summarize(x, y, links, carriers, noise);
            }
        });

        return new CoverageMap(this, grid, cells);
    }

    /// <summary>
    /// Моделирование покрытия: <paramref name="drops"/> розыгрышей согласованных карт затенения и видимости
    /// </summary>
    /// <param name="grid">Сетка точек</param>
    /// <param name="drops">Число розыгрышей</param>
    /// <param name="rng">Генератор случайных чисел</param>
    public CoverageSimulation Simulate(CoverageGrid grid, int drops, Random rng)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(rng);
        RequireSectors();

        if (drops < 1)
            throw new ArgumentOutOfRangeException(nameof(drops), drops, "Нужен хотя бы один розыгрыш");

        int sectors = Sectors.Count, pixels = grid.Count, columns = grid.Columns;
        int[] carriers = Carriers();
        int[] site = Sites(out int sites);
        double noise = Math.Pow(10, Receiver.NoiseDbm / 10);
        double correlation = Math.Clamp(InterSiteShadowCorrelation, 0, 1);
        double common = Math.Sqrt(correlation), own = Math.Sqrt(1 - correlation);
        double penetrationSigma = Receiver.PenetrationSigmaDb;

        // Детерминированная часть — усиление и статистика потерь — одна на все розыгрыши
        var links = new SectorLink[pixels * sectors];

        int[] shared = SharedLosses();

        Parallel.For(0, grid.Rows, row =>
        {
            for (int column = 0; column < columns; column++)
                FillLinks(links, ((row * columns) + column) * sectors, grid.X(column), grid.Y(row), shared);
        });

        bool shadowed = links.Any(l => l.Loss.LineOfSightSigmaDb > 0 || l.Loss.NonLineOfSightSigmaDb > 0);
        bool twoStates = links.Any(l => l.Loss.LineOfSightProbability > 0 && l.Loss.LineOfSightProbability < 1);
        double[] xs = grid.Xs(), ys = grid.Ys();

        var signalCovered = new int[pixels];
        var sinrCovered = new int[pixels];
        var efficiencySum = new double[pixels];
        var sinrSamples = new double[drops * pixels];

        for (int drop = 0; drop < drops; drop++)
        {
            // Поля разыгрываются по порядку — ради воспроизводимости, а на сетке считаются параллельно
            SpatialRandomField? commonSource = shadowed ? Field(Model.ShadowCorrelationDistanceM, rng) : null;
            var shadowSources = new SpatialRandomField?[sites];
            var stateSources = new SpatialRandomField?[sites];

            for (int s = 0; s < sites; s++)
            {
                if (shadowed)
                    shadowSources[s] = Field(Model.ShadowCorrelationDistanceM, rng);

                if (twoStates)
                    stateSources[s] = Field(Model.StateCorrelationDistanceM, rng);
            }

            double[,]? commonField = commonSource?.Sample(xs, ys);
            var shadowFields = new double[sites][,];
            var stateFields = new double[sites][,];

            Parallel.For(0, sites, s =>
            {
                shadowFields[s] = shadowSources[s]?.Sample(xs, ys)!;
                stateFields[s] = stateSources[s]?.Sample(xs, ys)!;
            });

            var penetration = new double[pixels];

            if (penetrationSigma > 0)
            {
                for (int p = 0; p < pixels; p++)
                    penetration[p] = penetrationSigma * RandomEngine.NextGaussian(rng);
            }

            int offset = drop * pixels;

            Parallel.For(0, grid.Rows, row =>
            {
                var power = new double[sectors];
                var shadow = new double[sites];
                var uniform = new double[sites];

                for (int column = 0; column < columns; column++)
                {
                    int p = (row * columns) + column;

                    for (int s = 0; s < sites; s++)
                    {
                        shadow[s] = shadowed ? (common * commonField![row, column]) + (own * shadowFields[s][row, column]) : 0;
                        uniform[s] = twoStates ? StatInference.NormalCdf(stateFields[s][row, column]) : 0.5;
                    }

                    int best = 0;

                    for (int k = 0; k < sectors; k++)
                    {
                        SectorLink link = links[(p * sectors) + k];
                        LinkLoss loss = link.Loss;
                        bool lineOfSight = uniform[site[k]] < loss.LineOfSightProbability;
                        double pathLoss = lineOfSight
                            ? loss.LineOfSightDb + (loss.LineOfSightSigmaDb * shadow[site[k]])
                            : loss.NonLineOfSightDb + (loss.NonLineOfSightSigmaDb * shadow[site[k]]);

                        power[k] = link.GainDb - pathLoss - penetration[p];

                        if (power[k] > power[best])
                            best = k;
                    }

                    double sinr = Sinr(power, best, carriers, noise, out _);

                    if (power[best] >= SignalThresholdDbm)
                        signalCovered[p]++;

                    if (sinr >= SinrThresholdDb)
                        sinrCovered[p]++;

                    efficiencySum[p] += Throughput.SpectralEfficiency(sinr);
                    sinrSamples[offset + p] = sinr;
                }
            });
        }

        return new CoverageSimulation(this, grid, drops, signalCovered, sinrCovered, efficiencySum, sinrSamples);
    }

    private readonly record struct SectorLink(LinkLoss Loss, double GainDb);

    // Связи всех секторов с точкой; потери считаются один раз на сайт и несущую — с рельефом это профиль трассы
    private void FillLinks(SectorLink[] target, int offset, double x, double y, int[] shared)
    {
        for (int k = 0; k < shared.Length; k++)
            target[offset + k] = Link(Sectors[k], x, y, shared[k] < k ? target[offset + shared[k]].Loss : null);
    }

    // Для каждого сектора — первый сектор с тем же положением и несущей: у них одни потери на трассе
    private int[] SharedLosses()
    {
        var shared = new int[Sectors.Count];

        for (int k = 0; k < shared.Length; k++)
        {
            shared[k] = k;

            for (int j = 0; j < k; j++)
            {
                if (Sectors[j].Position == Sectors[k].Position && Math.Abs(Sectors[j].FrequencyHz - Sectors[k].FrequencyHz) < SameCarrierToleranceHz)
                {
                    shared[k] = j;
                    break;
                }
            }
        }

        return shared;
    }

    private SectorLink Link(RadiationSource sector, double x, double y, LinkLoss? known = null)
    {
        (double azimuth, double elevation, _) = sector.DirectionTo(new SitePoint(x, y, Receiver.HeightM));

        // Пиковая ЭИИМ: мощность передатчика за вычетом фидера плюс усиление в максимуме
        double eirpDbm = (10 * Math.Log10(sector.TransmitPowerW * 1000)) - sector.FeederLossDb + sector.GainDbi;
        double gain = eirpDbm
            - sector.Pattern.AttenuationDb(azimuth, elevation)
            + Receiver.GainDbi
            - Receiver.OtherLossesDb
            - PenetrationLoss.LossDb(Receiver.Environment, sector.FrequencyHz, Receiver.IndoorDistanceM);

        LinkLoss loss = known ?? Model.Loss(
            new Vector3(sector.Position.X, sector.Position.Y, sector.Position.Z),
            new Vector3(x, y, Receiver.HeightM),
            sector.FrequencyHz);

        return new SectorLink(loss, gain);
    }

    private PointCoverage Summarize(double x, double y, SectorLink[] links, int[] carriers, double noise)
    {
        var power = new double[links.Length];
        int best = 0;

        for (int k = 0; k < links.Length; k++)
        {
            power[k] = links[k].GainDb - links[k].Loss.MeanDb;

            if (power[k] > power[best])
                best = k;
        }

        double sinr = Sinr(power, best, carriers, noise, out double interference);
        double coverage = links[best].Loss.ProbabilityWithin(links[best].GainDb - SignalThresholdDbm, Receiver.PenetrationSigmaDb);
        double efficiency = Throughput.SpectralEfficiency(sinr);

        return new PointCoverage(
            x,
            y,
            best,
            power[best],
            interference > 0 ? 10 * Math.Log10(interference) : double.NegativeInfinity,
            sinr,
            coverage,
            efficiency,
            efficiency * Receiver.BandwidthHz);
    }

    private double Sinr(double[] powerDbm, int best, int[] carriers, double noise, out double interference)
    {
        interference = 0;

        for (int k = 0; k < powerDbm.Length; k++)
        {
            if (k != best && carriers[k] == carriers[best])
                interference += NeighbourLoad * Math.Pow(10, powerDbm[k] / 10);
        }

        return powerDbm[best] - (10 * Math.Log10(interference + noise));
    }

    // Номер несущей каждого сектора: секторы с одной несущей получают один номер
    private int[] Carriers()
    {
        var carriers = new int[Sectors.Count];

        for (int k = 0; k < carriers.Length; k++)
        {
            carriers[k] = k;

            for (int j = 0; j < k; j++)
            {
                if (Math.Abs(Sectors[j].FrequencyHz - Sectors[k].FrequencyHz) < SameCarrierToleranceHz)
                {
                    carriers[k] = carriers[j];
                    break;
                }
            }
        }

        return carriers;
    }

    // Номер сайта каждого сектора: секторы в одной точке — один сайт с общим затенением
    private int[] Sites(out int count)
    {
        var positions = new List<SitePoint>();
        var site = new int[Sectors.Count];

        for (int k = 0; k < site.Length; k++)
        {
            int index = positions.IndexOf(Sectors[k].Position);

            if (index < 0)
            {
                index = positions.Count;
                positions.Add(Sectors[k].Position);
            }

            site[k] = index;
        }

        count = positions.Count;
        return site;
    }

    private static SpatialRandomField Field(double correlationDistanceM, Random rng)
        => new(correlationDistanceM, rng, FieldHarmonics);

    private void RequireSectors()
    {
        if (Sectors.Count == 0)
            throw new InvalidOperationException("В сцене нет ни одного сектора");
    }

    private static RadiationSource Copy(RadiationSource source) => new()
    {
        Name = source.Name,
        Position = source.Position,
        AzimuthDeg = source.AzimuthDeg,
        DowntiltDeg = source.DowntiltDeg,
        FrequencyHz = source.FrequencyHz,
        TransmitPowerW = source.TransmitPowerW,
        FeederLossDb = source.FeederLossDb,
        GainDbi = source.GainDbi,
        Pattern = source.Pattern,
        DutyCycle = source.DutyCycle,
        PowerReductionFactor = source.PowerReductionFactor,
        ApertureHeightM = source.ApertureHeightM,
        ApertureWidthM = source.ApertureWidthM,
        ApertureEfficiency = source.ApertureEfficiency,
    };
}
