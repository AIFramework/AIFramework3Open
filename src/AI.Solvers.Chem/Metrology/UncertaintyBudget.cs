using AI.Statistics;
using AI.Units;
using System.Globalization;
using System.Text;
using QuantityUnit = AI.Units.Unit;

namespace AI.Solvers.Chem.Metrology;

/// <summary>
/// Закон распределения источника неопределённости; определяет делитель,
/// переводящий заданную границу в стандартную неопределённость
/// </summary>
public enum DistributionKind
{
    /// <summary>Нормальное: значение уже является стандартным отклонением</summary>
    Normal,

    /// <summary>Равномерное (допуск, разряд прибора): делитель √3</summary>
    Rectangular,

    /// <summary>Треугольное: делитель √6</summary>
    Triangular,

    /// <summary>Арксинусное (U-образное, колебания температуры): делитель √2</summary>
    UShaped
}

/// <summary>
/// Составляющая бюджета неопределённости по GUM
/// </summary>
public sealed class UncertaintyComponent
{
    /// <summary>Название источника (навеска, объём колбы, градуировка, ...)</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Единицы измерения источника</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>
    /// Заданная величина: СКО для нормального закона либо граница интервала для остальных
    /// </summary>
    public double Value { get; init; }

    /// <summary>Закон распределения</summary>
    public DistributionKind Distribution { get; init; } = DistributionKind.Normal;

    /// <summary>Коэффициент чувствительности ∂y/∂x</summary>
    public double Sensitivity { get; init; } = 1.0;

    /// <summary>Число степеней свободы; int.MaxValue означает «бесконечность»</summary>
    public int DegreesOfFreedom { get; init; } = int.MaxValue;

    /// <summary>Оценка типа A получена статистически из серии измерений</summary>
    public bool IsTypeA { get; init; }

    /// <summary>Делитель, переводящий границу в стандартную неопределённость</summary>
    public double Divisor => Distribution switch
    {
        DistributionKind.Rectangular => Math.Sqrt(3.0),
        DistributionKind.Triangular => Math.Sqrt(6.0),
        DistributionKind.UShaped => Math.Sqrt(2.0),
        _ => 1.0
    };

    /// <summary>Стандартная неопределённость u(x)</summary>
    public double StandardUncertainty => Math.Abs(Value) / Divisor;

    /// <summary>Вклад в неопределённость результата: c·u(x)</summary>
    public double Contribution => Math.Abs(Sensitivity) * StandardUncertainty;

    /// <summary>
    /// Оценка типа A по серии параллельных измерений: u = s/√n, ν = n - 1
    /// </summary>
    /// <param name="name">Название источника</param>
    /// <param name="values">Серия измерений</param>
    /// <param name="sensitivity">Коэффициент чувствительности</param>
    /// <param name="unit">Единицы измерения</param>
    public static UncertaintyComponent FromSeries(string name, double[] values, double sensitivity = 1.0, string unit = "")
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length < 2)
            throw new ArgumentException("A type A estimate requires at least two measurements");

        double mean = values.Average();
        double variance = values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1);

        return new UncertaintyComponent
        {
            Name = name,
            Unit = unit,
            Value = Math.Sqrt(variance / values.Length),
            Distribution = DistributionKind.Normal,
            Sensitivity = sensitivity,
            DegreesOfFreedom = values.Length - 1,
            IsTypeA = true
        };
    }

    /// <summary>
    /// Оценка типа B из границ допуска (равномерное распределение)
    /// </summary>
    /// <param name="name">Название источника</param>
    /// <param name="halfWidth">Половина интервала допуска</param>
    /// <param name="sensitivity">Коэффициент чувствительности</param>
    /// <param name="unit">Единицы измерения</param>
    public static UncertaintyComponent FromTolerance(string name, double halfWidth, double sensitivity = 1.0, string unit = "")
        => new()
        {
            Name = name,
            Unit = unit,
            Value = halfWidth,
            Distribution = DistributionKind.Rectangular,
            Sensitivity = sensitivity
        };
}

/// <summary>
/// Бюджет неопределённости измерения по GUM: суммирование составляющих,
/// эффективное число степеней свободы и расширенная неопределённость.
/// </summary>
/// <remarks>
/// Составляющие считаются некоррелированными: суммарная стандартная неопределённость
/// равна корню из суммы квадратов вкладов. Коэффициент охвата берётся из распределения
/// Стьюдента с эффективным числом степеней свободы (формула Уэлча-Саттертуэйта),
/// а не фиксируется равным двум.
/// </remarks>
public sealed partial class UncertaintyBudget
{
    private readonly List<UncertaintyComponent> _components = new();

    /// <summary>Измеряемая величина</summary>
    public string Measurand { get; }

    /// <summary>Оценка измеряемой величины</summary>
    public double Value { get; }

    /// <summary>Единицы измерения результата</summary>
    public string Unit { get; }

    /// <summary>Составляющие бюджета</summary>
    public IReadOnlyList<UncertaintyComponent> Components => _components;

    /// <summary>Создаёт бюджет неопределённости</summary>
    /// <param name="measurand">Название измеряемой величины</param>
    /// <param name="value">Оценка результата</param>
    /// <param name="unit">Единицы измерения</param>
    public UncertaintyBudget(string measurand, double value, string unit = "")
    {
        Measurand = measurand;
        Value = value;
        Unit = unit;
    }

    /// <summary>
    /// Создаёт бюджет неопределённости с типизированной единицей измерения.
    /// Такой бюджет умеет отдавать результат как <see cref="Measurement"/>.
    /// </summary>
    /// <param name="measurand">Название измеряемой величины</param>
    /// <param name="value">Оценка результата в единице <paramref name="unit"/></param>
    /// <param name="unit">Единица измерения результата</param>
    public UncertaintyBudget(string measurand, double value, QuantityUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        Measurand = measurand;
        Value = value;
        Unit = unit.Symbol;
        MeasurementUnit = unit;
    }

    /// <summary>
    /// Типизированная единица измерения результата, если бюджет создан с ней;
    /// иначе <c>null</c> — единица известна только как строка <see cref="Unit"/>
    /// </summary>
    public QuantityUnit? MeasurementUnit { get; }

    /// <summary>Добавляет составляющую</summary>
    public UncertaintyBudget Add(UncertaintyComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        _components.Add(component);
        return this;
    }

    /// <summary>Добавляет составляющую типа B из границ допуска</summary>
    /// <param name="name">Название источника</param>
    /// <param name="halfWidth">Половина интервала</param>
    /// <param name="sensitivity">Коэффициент чувствительности</param>
    public UncertaintyBudget Add(string name, double halfWidth, double sensitivity = 1.0)
        => Add(UncertaintyComponent.FromTolerance(name, halfWidth, sensitivity));

    /// <summary>Суммарная стандартная неопределённость u_c</summary>
    public double CombinedStandardUncertainty
        => Math.Sqrt(_components.Sum(c => c.Contribution * c.Contribution));

    /// <summary>
    /// Эффективное число степеней свободы по формуле Уэлча-Саттертуэйта
    /// </summary>
    public double EffectiveDegreesOfFreedom
    {
        get
        {
            double uc = CombinedStandardUncertainty;

            if (uc <= 0)
                return double.PositiveInfinity;

            double denominator = 0;

            foreach (var component in _components)
            {
                if (component.DegreesOfFreedom == int.MaxValue || component.DegreesOfFreedom <= 0)
                    continue;

                double u = component.Contribution;
                denominator += Math.Pow(u, 4) / component.DegreesOfFreedom;
            }

            return denominator <= 0 ? double.PositiveInfinity : Math.Pow(uc, 4) / denominator;
        }
    }

    /// <summary>Коэффициент охвата k для заданной доверительной вероятности</summary>
    /// <param name="confidence">Доверительная вероятность</param>
    public double CoverageFactor(double confidence = 0.95)
    {
        double veff = EffectiveDegreesOfFreedom;
        double p = 1 - ((1 - confidence) / 2);

        if (double.IsInfinity(veff) || veff > 1000)
            return StatInference.NormalQuantile(p);

        return StatInference.TQuantile(p, Math.Max(1, (int)Math.Floor(veff)));
    }

    /// <summary>Расширенная неопределённость U = k·u_c</summary>
    /// <param name="confidence">Доверительная вероятность</param>
    public double ExpandedUncertainty(double confidence = 0.95)
        => CoverageFactor(confidence) * CombinedStandardUncertainty;

    /// <summary>Относительная расширенная неопределённость, %</summary>
    /// <param name="confidence">Доверительная вероятность</param>
    public double RelativeExpandedPercent(double confidence = 0.95)
        => Value == 0 ? double.NaN : 100.0 * ExpandedUncertainty(confidence) / Math.Abs(Value);

    /// <summary>Доля составляющей в суммарной неопределённости, %</summary>
    public double ContributionPercent(UncertaintyComponent component)
    {
        double uc = CombinedStandardUncertainty;
        return uc <= 0 ? 0 : 100.0 * component.Contribution * component.Contribution / (uc * uc);
    }

    /// <summary>Составляющие, упорядоченные по убыванию вклада</summary>
    public IReadOnlyList<UncertaintyComponent> Ranked
        => _components.OrderByDescending(c => c.Contribution).ToList();

    /// <summary>Отчёт по бюджету неопределённости</summary>
    /// <param name="confidence">Доверительная вероятность</param>
    public string Report(double confidence = 0.95)
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine($"Бюджет неопределённости: {Measurand} = {Value.ToString("G6", culture)} {Unit}".TrimEnd());
        text.AppendLine("  источник                        тип   u(x)        вклад       доля");

        foreach (var component in Ranked)
        {
            text.AppendLine(string.Format(culture,
                "  {0,-30} {1,-5} {2,-11:G4} {3,-11:G4} {4,5:F1}%",
                Truncate(component.Name, 30),
                component.IsTypeA ? "A" : "B",
                component.StandardUncertainty,
                component.Contribution,
                ContributionPercent(component)));
        }

        double veff = EffectiveDegreesOfFreedom;

        text.AppendLine($"  u_c = {CombinedStandardUncertainty.ToString("G4", culture)} {Unit}".TrimEnd());
        text.AppendLine($"  ν_eff = {(double.IsInfinity(veff) ? "∞" : veff.ToString("F1", culture))}, "
            + $"k = {CoverageFactor(confidence).ToString("F2", culture)}");
        text.AppendLine($"  U = {ExpandedUncertainty(confidence).ToString("G4", culture)} {Unit}".TrimEnd()
            + $" ({RelativeExpandedPercent(confidence).ToString("F1", culture)}%, P = {confidence:P0})");
        text.AppendLine($"  Результат: {Value.ToString("G6", culture)} ± {ExpandedUncertainty(confidence).ToString("G3", culture)} {Unit}".TrimEnd());

        return text.ToString();
    }

    /// <summary>
    /// Результат бюджета как физическая величина
    /// </summary>
    /// <exception cref="InvalidOperationException">Бюджет создан без типизированной единицы</exception>
    public Quantity ToQuantity() => Quantity.Of(Value, RequireUnit());

    /// <summary>
    /// Результат бюджета как измерение со <b>стандартной</b> (не расширенной) неопределённостью.
    /// </summary>
    /// <remarks>
    /// В <see cref="Measurement"/> хранится стандартная неопределённость <c>u_c</c>: именно она
    /// переносится через арифметику по линейному закону. Расширенная неопределённость получается
    /// из неё коэффициентом охвата — либо через <see cref="ExpandedUncertainty"/> здесь, где
    /// коэффициент берётся из распределения Стьюдента по эффективному числу степеней свободы,
    /// либо через <c>Measurement.Interval(k)</c> с явно заданным k.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Бюджет создан без типизированной единицы</exception>
    public Measurement ToMeasurement()
    {
        QuantityUnit unit = RequireUnit();
        return Measurement.Of(Value, CombinedStandardUncertainty, unit);
    }

    private QuantityUnit RequireUnit()
    {
        return MeasurementUnit
            ?? throw new InvalidOperationException(
                $"Бюджет «{Measurand}» создан без типизированной единицы измерения: "
                + "используйте конструктор, принимающий AI.Units.Unit");
    }

    private static string Truncate(string value, int length)
        => value.Length <= length ? value : value.Substring(0, length - 1) + "…";
}
