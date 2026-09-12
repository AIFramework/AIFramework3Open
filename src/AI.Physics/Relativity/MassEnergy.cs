using AI.Units;

namespace AI.Physics.Relativity;

/// <summary>
/// Эквивалентность массы и энергии: <c>E = mc²</c>.
/// </summary>
/// <remarks>
/// В физике частиц массы привычно записывают в энергии — «масса пиона 139.57 МэВ», — а импульсы
/// в МэВ/c. Поэтому методы модуля принимают массу и как массу, и как энергию покоя, а импульс —
/// и в кг·м/с, и как <c>pc</c> в единицах энергии. Что имелось в виду, решает размерность:
/// подставить вместо массы что-то третье по-прежнему нельзя.
/// </remarks>
public static class MassEnergy
{
    /// <summary>Размерность импульса, кг·м/с</summary>
    public static Dimension Momentum { get; } = Dimension.MassDim * Dimension.Velocity;

    /// <summary>Энергия покоя <c>mc²</c></summary>
    /// <param name="mass">Масса либо уже энергия покоя</param>
    public static Quantity RestEnergy(Quantity mass) => new(RestEnergyJoules(mass, nameof(mass)), Dimension.Energy);

    /// <summary>Масса по энергии покоя <c>m = E/c²</c></summary>
    /// <param name="restEnergy">Энергия покоя либо уже масса</param>
    public static Quantity Mass(Quantity restEnergy)
    {
        double c = PhysicalConstants.SpeedOfLight.SiValue;

        return new Quantity(RestEnergyJoules(restEnergy, nameof(restEnergy)) / (c * c), Dimension.MassDim);
    }

    /// <summary>Энергия покоя в джоулях из массы или энергии</summary>
    internal static double RestEnergyJoules(Quantity massOrEnergy, string paramName)
    {
        if (massOrEnergy.Dimension == Dimension.MassDim)
        {
            double c = PhysicalConstants.SpeedOfLight.SiValue;

            return massOrEnergy.SiValue * c * c;
        }

        return massOrEnergy.RequireSi(Dimension.Energy, paramName);
    }

    /// <summary>Импульс, умноженный на c, в джоулях — из импульса или из <c>pc</c></summary>
    internal static double MomentumJoules(Quantity momentum, string paramName)
    {
        if (momentum.Dimension == Momentum)
            return momentum.SiValue * PhysicalConstants.SpeedOfLight.SiValue;

        return momentum.RequireSi(Dimension.Energy, paramName);
    }
}
