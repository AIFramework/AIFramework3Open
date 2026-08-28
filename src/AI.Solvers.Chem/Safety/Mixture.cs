namespace AI.Solvers.Chem.Safety;

/// <summary>
/// Путь поступления вещества в организм
/// </summary>
public enum ExposureRoute
{
    /// <summary>Через рот</summary>
    Oral,

    /// <summary>Через кожу</summary>
    Dermal,

    /// <summary>Через дыхательные пути</summary>
    Inhalation
}

/// <summary>
/// Компонент смеси с его классификацией опасности
/// </summary>
public sealed class MixtureComponent
{
    /// <summary>Наименование</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Номер CAS</summary>
    public string CasNumber { get; init; } = string.Empty;

    /// <summary>Номер EC</summary>
    public string EcNumber { get; init; } = string.Empty;

    /// <summary>Химическая формула, если известна</summary>
    public string Formula { get; init; } = string.Empty;

    /// <summary>Содержание в смеси, % по массе</summary>
    public double ContentPercent { get; init; }

    /// <summary>Классификация компонента</summary>
    public IReadOnlyList<HazardCategory> Classifications { get; init; } = Array.Empty<HazardCategory>();

    /// <summary>
    /// Оценки острой токсичности по путям поступления: мг/кг для орального
    /// и накожного, мг/л (пары) для ингаляционного
    /// </summary>
    public IReadOnlyDictionary<ExposureRoute, double> AcuteToxicityEstimates { get; init; }
        = new Dictionary<ExposureRoute, double>();

    /// <summary>Коэффициент M для острой опасности для водной среды</summary>
    public double AcuteMFactor { get; init; } = 1;

    /// <summary>Коэффициент M для хронической опасности для водной среды</summary>
    public double ChronicMFactor { get; init; } = 1;

    /// <summary>Кинематическая вязкость, мм²/с (для аспирационной опасности)</summary>
    public double? KinematicViscosity { get; init; }

    /// <summary>Номер ООН для перевозки</summary>
    public string UnNumber { get; init; } = string.Empty;

    /// <summary>Класс опасности груза по ДОПОГ</summary>
    public string TransportClass { get; init; } = string.Empty;

    /// <summary>Группа упаковки</summary>
    public string PackingGroup { get; init; } = string.Empty;

    /// <summary>Надлежащее отгрузочное наименование</summary>
    public string ShippingName { get; init; } = string.Empty;

    /// <summary>Отнесён ли компонент к указанному классу опасности</summary>
    /// <param name="hazardClass">Класс опасности</param>
    /// <param name="categories">Допустимые категории; пусто - любая</param>
    public bool HasHazard(HazardClass hazardClass, params string[] categories)
        => Classifications.Any(c => c.Class == hazardClass
            && (categories.Length == 0 || categories.Contains(c.Category, StringComparer.OrdinalIgnoreCase)));

    /// <summary>Категория компонента по классу опасности; null - не отнесён</summary>
    /// <param name="hazardClass">Класс опасности</param>
    public string CategoryOf(HazardClass hazardClass)
        => Classifications.FirstOrDefault(c => c.Class == hazardClass).Category;
}

/// <summary>
/// Агрегатное состояние смеси
/// </summary>
public enum PhysicalState
{
    /// <summary>Жидкость</summary>
    Liquid,

    /// <summary>Твёрдое вещество</summary>
    Solid,

    /// <summary>Газ</summary>
    Gas
}

/// <summary>
/// Смесь: состав и свойства, необходимые для классификации и паспорта безопасности
/// </summary>
public sealed class Mixture
{
    private readonly List<MixtureComponent> _components = new();

    /// <summary>Торговое наименование</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Назначение продукции</summary>
    public string Use { get; init; } = string.Empty;

    /// <summary>Агрегатное состояние</summary>
    public PhysicalState State { get; init; } = PhysicalState.Liquid;

    /// <summary>Компоненты</summary>
    public IReadOnlyList<MixtureComponent> Components => _components;

    /// <summary>
    /// Физические виды опасности смеси: определяются испытанием, а не расчётом,
    /// поэтому задаются явно
    /// </summary>
    public IReadOnlyList<HazardCategory> PhysicalHazards { get; init; } = Array.Empty<HazardCategory>();

    /// <summary>Кинематическая вязкость смеси, мм²/с</summary>
    public double? KinematicViscosity { get; init; }

    /// <summary>Суммарное содержание компонентов, %</summary>
    public double TotalContentPercent => _components.Sum(c => c.ContentPercent);

    /// <summary>Добавляет компонент</summary>
    /// <param name="component">Компонент</param>
    public Mixture Add(MixtureComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        _components.Add(component);
        return this;
    }

    /// <summary>Классифицирует смесь по правилам СГС</summary>
    public MixtureClassification Classify() => MixtureClassifier.Classify(this);

    /// <summary>Создаёт паспорт безопасности по классификации</summary>
    public SafetyDataSheet CreateSafetyDataSheet() => new(this, Classify());
}
