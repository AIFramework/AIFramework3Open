using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Statistics;

namespace AI.Microwave.Coverage;

/// <summary>Карта покрытия по средним потерям: значения в каждой ячейке и итоги по площади</summary>
public sealed class CoverageMap : IInterpretable
{
    private readonly PointCoverage[,] _cells;

    internal CoverageMap(CoverageScene scene, CoverageGrid grid, PointCoverage[,] cells)
    {
        Scene = scene;
        Grid = grid;
        _cells = cells;
    }

    /// <summary>Сцена, по которой построена карта</summary>
    public CoverageScene Scene { get; }

    /// <summary>Сетка</summary>
    public CoverageGrid Grid { get; }

    /// <summary>Ячейка</summary>
    /// <param name="row">Строка — по северу</param>
    /// <param name="column">Столбец — по востоку</param>
    public PointCoverage this[int row, int column] => _cells[row, column];

    /// <summary>Все ячейки по строкам</summary>
    public IEnumerable<PointCoverage> Cells => _cells.Cast<PointCoverage>();

    /// <summary>
    /// Доля площади с уверенным приёмом — средняя по ячейкам вероятность, что сигнал выше порога с учётом затенения
    /// </summary>
    public double AreaCoverage => Cells.Average(c => c.CoverageProbability);

    /// <summary>Доля площади, где средний SINR не ниже порога</summary>
    public double SinrCoverage => Cells.Count(c => c.SinrDb >= Scene.SinrThresholdDb) / (double)Grid.Count;

    /// <summary>Доля площади, где помеха меньше шума — сеть ограничена шумом, а не помехами</summary>
    public double NoiseLimitedShare => Cells.Count(c => c.InterferenceDbm < Scene.Receiver.NoiseDbm) / (double)Grid.Count;

    /// <summary>Средняя по площади скорость одного абонента, бит/с</summary>
    public double MeanThroughputBps => Cells.Average(c => c.ThroughputBps);

    /// <summary>Доля площади, которую обслуживает сектор</summary>
    /// <param name="sector">Номер сектора</param>
    public double ServingShare(int sector) => Cells.Count(c => c.ServingSector == sector) / (double)Grid.Count;

    /// <summary>Квантиль SINR по площади, дБ</summary>
    /// <param name="q">Уровень от 0 до 1</param>
    public double SinrPercentileDb(double q) => CoverageStatistics.Quantile(Cells.Select(c => c.SinrDb).ToArray(), q);

    /// <summary>Слой карты как матрица [строка, столбец] — для вывода и графиков</summary>
    /// <param name="selector">Что взять из ячейки</param>
    public double[,] Layer(Func<PointCoverage, double> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var layer = new double[Grid.Rows, Grid.Columns];

        for (int row = 0; row < Grid.Rows; row++)
        {
            for (int column = 0; column < Grid.Columns; column++)
                layer[row, column] = selector(_cells[row, column]);
        }

        return layer;
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double area = AreaCoverage, sinr = SinrCoverage, median = SinrPercentileDb(0.5), edge = SinrPercentileDb(0.05);
        double noiseLimited = NoiseLimitedShare;

        return CoverageStatistics.Describe(
                "Карта покрытия по средним потерям",
                Scene,
                area,
                sinr,
                median,
                edge,
                MeanThroughputBps)
            .FindingIf(noiseLimited < 0.5,
                $"На {Fmt.Num((1 - noiseLimited) * 100, 0)} % площади помеха соседей сильнее шума: сеть ограничена помехами, "
                + "и прибавка мощности поднимет помеху вместе с сигналом. Помогают наклон антенн, разнос частот и снижение нагрузки.")
            .FindingIf(noiseLimited >= 0.5 && area < 0.9,
                "Сеть ограничена шумом: покрытие упирается в мощность и потери, а не во взаимные помехи. Помогают мощность, "
                + "высота и усиление антенн или дополнительные сайты.")
            .Warning("Карта по средним: SINR посчитан без затенения, а вероятность покрытия — только по обслуживающему сектору. "
                + "Распределение SINR с затенением и видимостью даёт моделирование, CoverageScene.Simulate.")
            .Build();
    }
}

/// <summary>Моделирование покрытия: вероятности по ячейкам и распределение SINR по площади и розыгрышам</summary>
public sealed class CoverageSimulation : IInterpretable
{
    private readonly int[] _signalCovered;
    private readonly int[] _sinrCovered;
    private readonly double[] _efficiencySum;
    private readonly double[] _sinrSamples;

    internal CoverageSimulation(CoverageScene scene, CoverageGrid grid, int drops, int[] signalCovered, int[] sinrCovered, double[] efficiencySum, double[] sinrSamples)
    {
        Scene = scene;
        Grid = grid;
        Drops = drops;
        _signalCovered = signalCovered;
        _sinrCovered = sinrCovered;
        _efficiencySum = efficiencySum;
        _sinrSamples = sinrSamples;
    }

    /// <summary>Сцена</summary>
    public CoverageScene Scene { get; }

    /// <summary>Сетка</summary>
    public CoverageGrid Grid { get; }

    /// <summary>Число розыгрышей</summary>
    public int Drops { get; }

    /// <summary>Все значения SINR по ячейкам и розыгрышам, дБ</summary>
    public IReadOnlyList<double> SinrSamplesDb => _sinrSamples;

    /// <summary>Доля розыгрышей, в которых сигнал в ячейке выше порога</summary>
    public double SignalCoverage(int row, int column) => _signalCovered[Index(row, column)] / (double)Drops;

    /// <summary>Доля розыгрышей, в которых SINR в ячейке не ниже порога</summary>
    public double SinrCoverage(int row, int column) => _sinrCovered[Index(row, column)] / (double)Drops;

    /// <summary>Средняя по розыгрышам скорость в ячейке, бит/с</summary>
    public double MeanThroughputBps(int row, int column) => _efficiencySum[Index(row, column)] / Drops * Scene.Receiver.BandwidthHz;

    /// <summary>Доля площади и розыгрышей, где сигнал выше порога</summary>
    public double AreaSignalCoverage => _signalCovered.Sum() / (double)(Drops * Grid.Count);

    /// <summary>Доля площади и розыгрышей, где SINR не ниже порога</summary>
    public double AreaSinrCoverage => _sinrCovered.Sum() / (double)(Drops * Grid.Count);

    /// <summary>Средняя скорость по площади и розыгрышам, бит/с</summary>
    public double AreaThroughputBps => _efficiencySum.Sum() / (Drops * Grid.Count) * Scene.Receiver.BandwidthHz;

    /// <summary>Квантиль SINR по площади и розыгрышам, дБ</summary>
    /// <param name="q">Уровень от 0 до 1</param>
    public double SinrPercentileDb(double q) => CoverageStatistics.Quantile(_sinrSamples, q);

    /// <summary>
    /// Скорость на краю соты — у абонента с 5-процентным SINR, бит/с: пересчёт монотонен, поэтому квантиль
    /// скорости равен пересчитанному квантилю SINR
    /// </summary>
    public double CellEdgeThroughputBps => Scene.Throughput.SpectralEfficiency(SinrPercentileDb(0.05)) * Scene.Receiver.BandwidthHz;

    /// <inheritdoc />
    public Interpretation Interpret()
        => CoverageStatistics.Describe(
                $"Моделирование покрытия, розыгрышей {Drops}",
                Scene,
                AreaSignalCoverage,
                AreaSinrCoverage,
                SinrPercentileDb(0.5),
                SinrPercentileDb(0.05),
                AreaThroughputBps)
            .Metric("Скорость на краю соты", Fmt.Num(CellEdgeThroughputBps / 1e6, 2), "Мбит/с", "абонент с 5-процентным SINR")
            .Warning("Затенение и видимость согласованы в пространстве, быстрые замирания не разыгрываются: SINR — средний по полосе "
                + "и по времени, как «геометрия» в калибровках 3GPP.")
            .Build();

    private int Index(int row, int column)
    {
        if (row < 0 || row >= Grid.Rows || column < 0 || column >= Grid.Columns)
            throw new ArgumentOutOfRangeException(nameof(row), "Ячейка вне сетки");

        return (row * Grid.Columns) + column;
    }
}

/// <summary>Общее для итогов покрытия: квантили и начало объяснения</summary>
internal static class CoverageStatistics
{
    public static double Quantile(double[] values, double q)
    {
        var weights = new double[values.Length];
        Array.Fill(weights, 1.0);

        return WeightedStatistics.Quantile(new Vector(values), new Vector(weights), q);
    }

    public static InterpretationBuilder Describe(string title, CoverageScene scene, double signal, double sinr, double median, double edge, double throughput)
        => new InterpretationBuilder(title)
            .Summary($"Секторов {scene.Sectors.Count}, модель — {scene.Model.Name}. Сигнал выше {Fmt.Num(scene.SignalThresholdDbm, 0)} дБм "
                + $"на {Fmt.Num(signal * 100, 1)} % площади, SINR не ниже {Fmt.Num(scene.SinrThresholdDb, 0)} дБ — на {Fmt.Num(sinr * 100, 1)} %; "
                + $"медиана SINR {Fmt.Num(median, 1)} дБ, на краю (5 %) — {Fmt.Num(edge, 1)} дБ.")
            .Metric("Покрытие по сигналу", Fmt.Num(signal * 100, 1), "%", $"порог {Fmt.Num(scene.SignalThresholdDbm, 0)} дБм")
            .Metric("Покрытие по SINR", Fmt.Num(sinr * 100, 1), "%", $"порог {Fmt.Num(scene.SinrThresholdDb, 0)} дБ")
            .Metric("Медиана SINR", Fmt.Num(median, 2), "дБ", null)
            .Metric("SINR на краю соты", Fmt.Num(edge, 2), "дБ", "5-процентный квантиль по площади")
            .Metric("Средняя скорость абонента", Fmt.Num(throughput / 1e6, 2), "Мбит/с", $"полоса {Fmt.Num(scene.Receiver.BandwidthHz / 1e6, 0)} МГц, вся полоса одному абоненту")
            .Metric("Шум приёмника", Fmt.Num(scene.Receiver.NoiseDbm, 1), "дБм", $"k·T·B, NF {Fmt.Num(scene.Receiver.NoiseFigureDb, 0)} дБ")
            .FindingIf(edge < scene.SinrThresholdDb,
                $"На краю сот SINR ниже порога {Fmt.Num(scene.SinrThresholdDb, 0)} дБ: абоненты там теряют связь или работают на "
                + "самой устойчивой и медленной схеме.");
}
