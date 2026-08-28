namespace AI.Microwave.Models;

/// <summary>
/// Свойства конструкционного металла антенны.
/// </summary>
public class MaterialProperties
{
    public required string Name { get; set; }

    /// <summary>Удельная проводимость, См/м.</summary>
    public double Conductivity { get; set; }

    /// <summary>Плотность, кг/м^3.</summary>
    public double Density { get; set; }

    /// <summary>Теплопроводность, Вт/(м К).</summary>
    public double ThermalConductivity { get; set; }

    /// <summary>Относительная стоимость единицы массы (медь = 1.0).</summary>
    public double Cost { get; set; }

    /// <summary>Температура плавления, градусы Цельсия.</summary>
    public double MeltingPoint { get; set; }

    public static List<MaterialProperties> GetStandardMaterials() =>
    [
        new()
        {
            Name = "Медь (Cu)",
            Conductivity = 5.96e7,
            Density = 8960,
            ThermalConductivity = 401,
            Cost = 1.0,
            MeltingPoint = 1085,
        },
        new()
        {
            Name = "Алюминий (Al)",
            Conductivity = 3.77e7,
            Density = 2700,
            ThermalConductivity = 237,
            Cost = 0.3,
            MeltingPoint = 660,
        },
        new()
        {
            Name = "Латунь (Brass)",
            Conductivity = 1.5e7,
            Density = 8500,
            ThermalConductivity = 120,
            Cost = 0.8,
            MeltingPoint = 930,
        },
        new()
        {
            Name = "Медь посеребренная",
            Conductivity = 6.3e7,
            Density = 8960,
            ThermalConductivity = 401,
            Cost = 2.5,
            MeltingPoint = 1085,
        },
        new()
        {
            Name = "Медь позолоченная",
            Conductivity = 4.5e7,
            Density = 8960,
            ThermalConductivity = 318,
            Cost = 15.0,
            MeltingPoint = 1085,
        },
        new()
        {
            Name = "Нержавеющая сталь",
            Conductivity = 1.45e6,
            Density = 7900,
            ThermalConductivity = 16,
            Cost = 0.5,
            MeltingPoint = 1510,
        },
    ];
}
