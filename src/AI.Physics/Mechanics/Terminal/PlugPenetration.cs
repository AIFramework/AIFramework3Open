#nullable enable
using AI.Units;

namespace AI.Physics.Mechanics.Terminal;

/// <summary>
/// Пробитие листа выбиванием пробки.
/// </summary>
/// <remarks>
/// <para>
/// Предел пробития получается из баланса энергии: снаряд выбивает пробку, срезая цилиндр диаметром с себя и высотой
/// в толщину листа. Касательная сила среза τ·π·d·(t − x) убывает по мере выдавливания пробки, и работа среза
/// равна τ·π·d·t²/2. Мягкий снаряд (свинец) тратит часть энергии на свое сплющивание: множитель деформации
/// больше единицы.
/// </para>
/// <para>
/// Это нижняя оценка для тонкого листа из пластичного металла при ударе по нормали: прогиб листа перед пробитием,
/// упрочнение при высокой скорости деформации, лепестковое разрушение и наклон удара не учтены. Скорость за листом
/// по Рехту и Ипсону: снаряд уносит с собой пробку, и скорость пары меньше в m/(m + m_пробки) раз.
/// </para>
/// </remarks>
public static class PlugPenetration
{
    /// <summary>Работа среза пробки: τ·π·d·t²/2</summary>
    /// <param name="diameter">Диаметр снаряда</param>
    /// <param name="thickness">Толщина листа</param>
    /// <param name="shearStrength">Предел прочности листа на срез</param>
    public static Quantity ShearWork(Quantity diameter, Quantity thickness, Quantity shearStrength)
    {
        double d = Positive(diameter, Dimension.LengthDim, nameof(diameter));
        double t = Positive(thickness, Dimension.LengthDim, nameof(thickness));
        double tau = Positive(shearStrength, Dimension.Pressure, nameof(shearStrength));

        return new Quantity(tau * Math.PI * d * t * t / 2, Dimension.Energy);
    }

    /// <summary>Предел пробития: ниже этой скорости лист не пробит, √(π·d·t²·τ·k/m)</summary>
    /// <param name="projectileMass">Масса снаряда</param>
    /// <param name="diameter">Диаметр снаряда</param>
    /// <param name="thickness">Толщина листа</param>
    /// <param name="shearStrength">Предел прочности листа на срез</param>
    /// <param name="deformationFactor">Во сколько раз энергия удара больше работы среза из-за сплющивания снаряда, не меньше единицы</param>
    public static Quantity BallisticLimit(
        Quantity projectileMass, Quantity diameter, Quantity thickness, Quantity shearStrength, double deformationFactor = 1)
    {
        double m = Positive(projectileMass, Dimension.MassDim, nameof(projectileMass));

        if (!(deformationFactor >= 1))
            throw new ArgumentOutOfRangeException(nameof(deformationFactor), "Множитель деформации не может быть меньше единицы");

        double work = ShearWork(diameter, thickness, shearStrength).SiValue * deformationFactor;

        return new Quantity(Math.Sqrt(2 * work / m), Dimension.Velocity);
    }

    /// <summary>Масса пробки: цилиндр листа диаметром со снаряд и высотой в толщину листа, ρ·π·d²·t/4</summary>
    /// <param name="diameter">Диаметр снаряда</param>
    /// <param name="thickness">Толщина листа</param>
    /// <param name="plateDensity">Плотность материала листа</param>
    public static Quantity PlugMass(Quantity diameter, Quantity thickness, Quantity plateDensity)
    {
        double d = Positive(diameter, Dimension.LengthDim, nameof(diameter));
        double t = Positive(thickness, Dimension.LengthDim, nameof(thickness));
        double rho = Positive(plateDensity, Dimension.Density, nameof(plateDensity));

        return new Quantity(rho * Math.PI * d * d * t / 4, Dimension.MassDim);
    }

    /// <summary>Скорость за листом без учета массы пробки: √(v² − v_пр²), верхняя оценка; ноль, если лист не пробит</summary>
    /// <param name="impactSpeed">Скорость удара</param>
    /// <param name="ballisticLimit">Предел пробития</param>
    public static Quantity ResidualVelocity(Quantity impactSpeed, Quantity ballisticLimit)
    {
        double v = impactSpeed.RequireSi(Dimension.Velocity, nameof(impactSpeed));
        double limit = ballisticLimit.RequireSi(Dimension.Velocity, nameof(ballisticLimit));

        return new Quantity(v > limit ? Math.Sqrt((v * v) - (limit * limit)) : 0, Dimension.Velocity);
    }

    /// <summary>Скорость за листом по Рехту и Ипсону: m/(m + m_пробки)·√(v² − v_пр²); ноль, если лист не пробит</summary>
    /// <param name="impactSpeed">Скорость удара</param>
    /// <param name="ballisticLimit">Предел пробития</param>
    /// <param name="projectileMass">Масса снаряда</param>
    /// <param name="plugMass">Масса выбитой пробки: <see cref="PlugMass"/></param>
    public static Quantity ResidualVelocity(Quantity impactSpeed, Quantity ballisticLimit, Quantity projectileMass, Quantity plugMass)
    {
        double m = Positive(projectileMass, Dimension.MassDim, nameof(projectileMass));
        double plug = plugMass.RequireSi(Dimension.MassDim, nameof(plugMass));

        if (!(plug >= 0))
            throw new ArgumentOutOfRangeException(nameof(plugMass), "Масса пробки не может быть отрицательной");

        return new Quantity(m / (m + plug) * ResidualVelocity(impactSpeed, ballisticLimit).SiValue, Dimension.Velocity);
    }

    private static double Positive(Quantity value, Dimension dimension, string name)
    {
        double si = value.RequireSi(dimension, name);

        return si > 0 && double.IsFinite(si) ? si : throw new ArgumentOutOfRangeException(name, "Величина должна быть положительной и конечной");
    }
}
