using System.Globalization;

namespace AI.Solvers.Chem.Signals;

/// <summary>
/// Хроматографический (или спектральный) пик с характеристиками,
/// которые требуются для количественного анализа и проверки пригодности системы
/// </summary>
public sealed class Peak
{
    /// <summary>Индекс вершины</summary>
    public int ApexIndex { get; init; }

    /// <summary>Индекс начала пика</summary>
    public int StartIndex { get; init; }

    /// <summary>Индекс конца пика</summary>
    public int EndIndex { get; init; }

    /// <summary>Время удерживания (положение вершины по оси x)</summary>
    public double RetentionTime { get; init; }

    /// <summary>Начало пика по оси x</summary>
    public double StartTime { get; init; }

    /// <summary>Конец пика по оси x</summary>
    public double EndTime { get; init; }

    /// <summary>Высота над базовой линией</summary>
    public double Height { get; init; }

    /// <summary>Площадь над базовой линией</summary>
    public double Area { get; init; }

    /// <summary>Ширина на половине высоты</summary>
    public double WidthAtHalfHeight { get; init; }

    /// <summary>Расстояние от вершины до левой границы на 5% высоты</summary>
    public double LeftWidthAt5Percent { get; init; }

    /// <summary>Расстояние от вершины до правой границы на 5% высоты</summary>
    public double RightWidthAt5Percent { get; init; }

    /// <summary>Расстояние от вершины до левой границы на 10% высоты</summary>
    public double LeftWidthAt10Percent { get; init; }

    /// <summary>Расстояние от вершины до правой границы на 10% высоты</summary>
    public double RightWidthAt10Percent { get; init; }

    /// <summary>Уровень базовой линии под вершиной</summary>
    public double BaselineAtApex { get; init; }

    /// <summary>Доля площади пика в сумме площадей, %</summary>
    public double AreaPercent { get; internal set; }

    /// <summary>Название компонента, если пик идентифицирован</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ширина пика по основанию (оценка по ширине на половине высоты)</summary>
    public double BaseWidth => WidthAtHalfHeight * 1.699;

    /// <summary>
    /// Число теоретических тарелок N = 5.54·(tR/w½)²
    /// </summary>
    public double TheoreticalPlates => WidthAtHalfHeight > 0
        ? 5.54 * Math.Pow(RetentionTime / WidthAtHalfHeight, 2)
        : double.NaN;

    /// <summary>
    /// Высота, эквивалентная теоретической тарелке, для колонки заданной длины
    /// </summary>
    /// <param name="columnLength">Длина колонки в тех же единицах, что и результат</param>
    public double PlateHeight(double columnLength) => columnLength / TheoreticalPlates;

    /// <summary>
    /// Фактор асимметрии по 10% высоты: As = B/A
    /// </summary>
    public double AsymmetryFactor => LeftWidthAt10Percent > 0
        ? RightWidthAt10Percent / LeftWidthAt10Percent
        : double.NaN;

    /// <summary>
    /// Фактор хвостования по USP (5% высоты): T = (A + B)/(2A)
    /// </summary>
    public double UspTailing => LeftWidthAt5Percent > 0
        ? (LeftWidthAt5Percent + RightWidthAt5Percent) / (2 * LeftWidthAt5Percent)
        : double.NaN;

    /// <summary>
    /// Коэффициент удерживания k' = (tR - t0)/t0
    /// </summary>
    /// <param name="holdupTime">Мёртвое время колонки t0</param>
    public double CapacityFactor(double holdupTime)
        => holdupTime > 0 ? (RetentionTime - holdupTime) / holdupTime : double.NaN;

    /// <summary>
    /// Разрешение пары пиков: Rs = 1.18·Δt/(w½₁ + w½₂)
    /// </summary>
    /// <param name="first">Первый пик</param>
    /// <param name="second">Второй пик</param>
    public static double Resolution(Peak first, Peak second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        double widths = first.WidthAtHalfHeight + second.WidthAtHalfHeight;

        return widths > 0
            ? 1.18 * Math.Abs(second.RetentionTime - first.RetentionTime) / widths
            : double.NaN;
    }

    /// <summary>
    /// Селективность пары пиков относительно мёртвого времени: α = k'₂/k'₁
    /// </summary>
    /// <param name="first">Первый пик</param>
    /// <param name="second">Второй пик</param>
    /// <param name="holdupTime">Мёртвое время</param>
    public static double Selectivity(Peak first, Peak second, double holdupTime)
    {
        double k1 = first.CapacityFactor(holdupTime);
        double k2 = second.CapacityFactor(holdupTime);

        return k1 > 0 ? k2 / k1 : double.NaN;
    }

    /// <summary>Краткое описание пика</summary>
    public override string ToString()
    {
        var culture = CultureInfo.InvariantCulture;
        string name = string.IsNullOrEmpty(Name) ? string.Empty : $" {Name}";

        return $"пик{name}: tR = {RetentionTime.ToString("G5", culture)}, "
             + $"S = {Area.ToString("G5", culture)}, h = {Height.ToString("G5", culture)}, "
             + $"w½ = {WidthAtHalfHeight.ToString("G4", culture)}";
    }
}
