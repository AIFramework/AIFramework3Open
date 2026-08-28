using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Production;

/// <summary>
/// Роль компонента в рецептуре
/// </summary>
public enum ComponentRole
{
    /// <summary>Основное сырьё</summary>
    RawMaterial,

    /// <summary>Растворитель</summary>
    Solvent,

    /// <summary>Катализатор</summary>
    Catalyst,

    /// <summary>Вспомогательный материал</summary>
    Auxiliary,

    /// <summary>Упаковка</summary>
    Packaging
}

/// <summary>
/// Компонент рецептуры
/// </summary>
public sealed class RecipeComponent
{
    /// <summary>Название</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Формула, если компонент - индивидуальное вещество</summary>
    public string Formula { get; init; } = string.Empty;

    /// <summary>Потребность на партию в пересчёте на 100% вещества, кг</summary>
    public double Quantity { get; set; }

    /// <summary>Массовая доля основного вещества в закупаемом сырье (0..1)</summary>
    public double Purity { get; set; } = 1.0;

    /// <summary>Цена закупки, ден.ед./кг</summary>
    public double PricePerKg { get; set; }

    /// <summary>Роль компонента</summary>
    public ComponentRole Role { get; init; } = ComponentRole.RawMaterial;

    /// <summary>Доля регенерации (для растворителей): 0.8 - возвращается 80%</summary>
    public double RecoveryFraction { get; set; }

    /// <summary>
    /// Закупаемое количество с учётом чистоты сырья, кг
    /// </summary>
    public double GrossQuantity => Purity > 0 ? Quantity / Purity : Quantity;

    /// <summary>Затраты на компонент в партии</summary>
    public double Cost => GrossQuantity * PricePerKg * (1 - Math.Clamp(RecoveryFraction, 0, 1));
}

/// <summary>
/// Рецептура партии: состав, передел и экономика
/// </summary>
/// <remarks>
/// Себестоимость складывается из стоимости сырья (с поправкой на чистоту и регенерацию),
/// затрат передела и накладных расходов, начисляемых на прямые затраты.
/// </remarks>
public sealed class Recipe
{
    private readonly List<RecipeComponent> _components = new();

    /// <summary>Название продукта</summary>
    public string Product { get; }

    /// <summary>Выпуск продукта за партию, кг</summary>
    public double BatchSize { get; private set; }

    /// <summary>Выход по стадии (0..1); справочно для отчёта</summary>
    public double YieldFraction { get; set; } = 1.0;

    /// <summary>Трудозатраты на партию, ч</summary>
    public double LaborHours { get; set; }

    /// <summary>Ставка оплаты труда, ден.ед./ч</summary>
    public double LaborRatePerHour { get; set; }

    /// <summary>Энергозатраты на партию, ден.ед.</summary>
    public double EnergyCost { get; set; }

    /// <summary>Упаковка на партию, ден.ед.</summary>
    public double PackagingCost { get; set; }

    /// <summary>Накладные расходы, % от прямых затрат</summary>
    public double OverheadPercent { get; set; }

    /// <summary>Компоненты рецептуры</summary>
    public IReadOnlyList<RecipeComponent> Components => _components;

    /// <summary>Создаёт рецептуру</summary>
    /// <param name="product">Название продукта</param>
    /// <param name="batchSize">Выпуск за партию, кг</param>
    public Recipe(string product, double batchSize)
    {
        if (batchSize <= 0)
            throw new ArgumentException("Batch size must be positive", nameof(batchSize));

        Product = product;
        BatchSize = batchSize;
    }

    /// <summary>Добавляет компонент</summary>
    public Recipe Add(RecipeComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        _components.Add(component);
        return this;
    }

    /// <summary>Добавляет компонент по основным параметрам</summary>
    /// <param name="name">Название</param>
    /// <param name="quantity">Потребность на партию, кг</param>
    /// <param name="pricePerKg">Цена, ден.ед./кг</param>
    /// <param name="purity">Чистота сырья (0..1)</param>
    /// <param name="role">Роль компонента</param>
    public Recipe Add(string name, double quantity, double pricePerKg, double purity = 1.0,
        ComponentRole role = ComponentRole.RawMaterial)
        => Add(new RecipeComponent
        {
            Name = name,
            Quantity = quantity,
            PricePerKg = pricePerKg,
            Purity = purity,
            Role = role
        });

    /// <summary>
    /// Пропорционально пересчитывает рецептуру на другой размер партии
    /// </summary>
    /// <param name="batchSize">Новый выпуск, кг</param>
    public Recipe ScaleTo(double batchSize)
    {
        if (batchSize <= 0)
            throw new ArgumentException("Batch size must be positive", nameof(batchSize));

        double factor = batchSize / BatchSize;
        var scaled = new Recipe(Product, batchSize)
        {
            YieldFraction = YieldFraction,
            LaborHours = LaborHours * factor,
            LaborRatePerHour = LaborRatePerHour,
            EnergyCost = EnergyCost * factor,
            PackagingCost = PackagingCost * factor,
            OverheadPercent = OverheadPercent
        };

        foreach (var component in _components)
        {
            scaled.Add(new RecipeComponent
            {
                Name = component.Name,
                Formula = component.Formula,
                Quantity = component.Quantity * factor,
                Purity = component.Purity,
                PricePerKg = component.PricePerKg,
                Role = component.Role,
                RecoveryFraction = component.RecoveryFraction
            });
        }

        return scaled;
    }

    /// <summary>Считает себестоимость партии</summary>
    public BatchCost Cost()
    {
        double material = _components.Sum(c => c.Cost);
        double labor = LaborHours * LaborRatePerHour;
        double direct = material + labor + EnergyCost + PackagingCost;
        double overhead = direct * OverheadPercent / 100.0;

        var breakdown = _components
            .Select(c => new CostItem(c.Name, c.Cost, c.Role.ToString()))
            .Concat(new[]
            {
                new CostItem("Оплата труда", labor, "передел"),
                new CostItem("Энергия", EnergyCost, "передел"),
                new CostItem("Упаковка", PackagingCost, "передел"),
                new CostItem("Накладные", overhead, "накладные")
            })
            .Where(item => item.Cost > 0)
            .OrderByDescending(item => item.Cost)
            .ToList();

        return new BatchCost
        {
            Product = Product,
            BatchSize = BatchSize,
            MaterialCost = material,
            LaborCost = labor,
            EnergyCost = EnergyCost,
            PackagingCost = PackagingCost,
            OverheadCost = overhead,
            Items = breakdown
        };
    }

    /// <summary>
    /// Чувствительность себестоимости к ценам компонентов: на сколько процентов
    /// изменится себестоимость килограмма при изменении цены компонента
    /// </summary>
    /// <param name="relativeChange">Относительное изменение цены, например 0.10 для +10%</param>
    public IReadOnlyList<CostDriver> Sensitivity(double relativeChange = 0.10)
    {
        double baseline = Cost().CostPerKg;
        var drivers = new List<CostDriver>();

        foreach (var component in _components)
        {
            double originalPrice = component.PricePerKg;
            component.PricePerKg = originalPrice * (1 + relativeChange);

            double changed = Cost().CostPerKg;
            component.PricePerKg = originalPrice;

            double deltaPercent = baseline > 0 ? 100.0 * (changed - baseline) / baseline : 0;

            drivers.Add(new CostDriver(
                component.Name,
                component.Cost,
                baseline > 0 ? 100.0 * component.Cost / (baseline * BatchSize) : 0,
                changed,
                deltaPercent));
        }

        return drivers.OrderByDescending(d => Math.Abs(d.CostPerKgChangePercent)).ToList();
    }
}

/// <summary>
/// Строка калькуляции
/// </summary>
/// <param name="Name">Название статьи</param>
/// <param name="Cost">Сумма на партию</param>
/// <param name="Group">Группа: сырьё, передел, накладные</param>
public readonly record struct CostItem(string Name, double Cost, string Group);

/// <summary>
/// Влияние цены компонента на себестоимость
/// </summary>
/// <param name="Name">Компонент</param>
/// <param name="CostInBatch">Затраты на компонент в партии</param>
/// <param name="SharePercent">Доля в себестоимости, %</param>
/// <param name="NewCostPerKg">Себестоимость килограмма после изменения цены</param>
/// <param name="CostPerKgChangePercent">Изменение себестоимости, %</param>
public readonly record struct CostDriver(
    string Name,
    double CostInBatch,
    double SharePercent,
    double NewCostPerKg,
    double CostPerKgChangePercent);

/// <summary>
/// Себестоимость партии с расшифровкой
/// </summary>
public sealed class BatchCost
{
    /// <summary>Продукт</summary>
    public string Product { get; init; } = string.Empty;

    /// <summary>Выпуск за партию, кг</summary>
    public double BatchSize { get; init; }

    /// <summary>Стоимость сырья</summary>
    public double MaterialCost { get; init; }

    /// <summary>Оплата труда</summary>
    public double LaborCost { get; init; }

    /// <summary>Энергозатраты</summary>
    public double EnergyCost { get; init; }

    /// <summary>Упаковка</summary>
    public double PackagingCost { get; init; }

    /// <summary>Накладные расходы</summary>
    public double OverheadCost { get; init; }

    /// <summary>Статьи калькуляции по убыванию суммы</summary>
    public IReadOnlyList<CostItem> Items { get; init; } = Array.Empty<CostItem>();

    /// <summary>Полная себестоимость партии</summary>
    public double TotalCost => MaterialCost + LaborCost + EnergyCost + PackagingCost + OverheadCost;

    /// <summary>Себестоимость килограмма продукта</summary>
    public double CostPerKg => BatchSize > 0 ? TotalCost / BatchSize : double.NaN;

    /// <summary>Доля сырья в себестоимости, %</summary>
    public double MaterialSharePercent => TotalCost > 0 ? 100.0 * MaterialCost / TotalCost : 0;

    /// <summary>Прибыль с партии при заданной цене продажи</summary>
    /// <param name="pricePerKg">Цена продажи, ден.ед./кг</param>
    public double Profit(double pricePerKg) => (pricePerKg * BatchSize) - TotalCost;

    /// <summary>Рентабельность продаж при заданной цене, %</summary>
    /// <param name="pricePerKg">Цена продажи, ден.ед./кг</param>
    public double MarginPercent(double pricePerKg)
    {
        double revenue = pricePerKg * BatchSize;
        return revenue > 0 ? 100.0 * Profit(pricePerKg) / revenue : double.NaN;
    }

    /// <summary>Цена безубыточности, ден.ед./кг</summary>
    public double BreakEvenPrice => CostPerKg;

    /// <summary>
    /// Цена, обеспечивающая заданную рентабельность продаж
    /// </summary>
    /// <param name="targetMarginPercent">Целевая рентабельность, %</param>
    public double PriceForMargin(double targetMarginPercent)
    {
        if (targetMarginPercent >= 100)
            throw new ArgumentException("Margin must be below 100%", nameof(targetMarginPercent));

        return CostPerKg / (1 - (targetMarginPercent / 100.0));
    }

    /// <summary>Калькуляция в человекочитаемом виде</summary>
    /// <param name="pricePerKg">Цена продажи для расчёта маржи; 0 - не считать</param>
    public string Report(double pricePerKg = 0)
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine($"Калькуляция: {Product}, партия {BatchSize.ToString("G6", culture)} кг");
        text.AppendLine("  статья                        группа        сумма        доля");

        foreach (var item in Items)
        {
            text.AppendLine(string.Format(culture,
                "  {0,-28} {1,-12} {2,12:F2} {3,8:F1}%",
                item.Name, item.Group, item.Cost, TotalCost > 0 ? 100.0 * item.Cost / TotalCost : 0));
        }

        text.AppendLine();
        text.AppendLine($"  Сырьё: {MaterialCost.ToString("F2", culture)} ({MaterialSharePercent.ToString("F1", culture)}%)");
        text.AppendLine($"  Передел: {(LaborCost + EnergyCost + PackagingCost).ToString("F2", culture)}");
        text.AppendLine($"  Накладные: {OverheadCost.ToString("F2", culture)}");
        text.AppendLine($"  Итого: {TotalCost.ToString("F2", culture)} за партию, "
            + $"{CostPerKg.ToString("F2", culture)} за кг");

        if (pricePerKg > 0)
        {
            text.AppendLine($"  При цене {pricePerKg.ToString("F2", culture)}/кг: "
                + $"прибыль {Profit(pricePerKg).ToString("F2", culture)}, "
                + $"рентабельность {MarginPercent(pricePerKg).ToString("F1", culture)}%");
        }

        return text.ToString();
    }
}
