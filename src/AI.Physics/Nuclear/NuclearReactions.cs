using AI.Physics.Relativity;
using AI.Units;

namespace AI.Physics.Nuclear;

/// <summary>
/// Энергетика ядерных реакций и распадов: энергия реакции Q и удельное энерговыделение.
/// </summary>
/// <remarks>
/// <para>
/// Массы — атомные, как в таблицах масс. Когда заряд сохраняется, электроны атомов до и после
/// сокращаются, и атомные массы можно подставлять как есть. Бета-распады — особый случай:
/// при испускании позитрона дочерний атом остаётся с лишним электроном, и из разности атомных
/// масс вычитаются две массы электрона. Об этом легко забыть, поэтому для бета-распадов здесь
/// отдельные методы.
/// </para>
/// <para>
/// Порог реакции с отрицательным Q считает <see cref="RelativisticKinematics.Threshold"/>: формула
/// там точная, и для ядерных реакций совпадает с классической <c>−Q(1 + m_a/m_b)</c>.
/// </para>
/// </remarks>
public static class NuclearReactions
{
    /// <summary>
    /// Энергия реакции: разность суммарных масс до и после, умноженная на c²
    /// </summary>
    /// <param name="initial">Массы частиц до реакции — массы или энергии покоя</param>
    /// <param name="final">Массы частиц после реакции</param>
    /// <returns>Положительна, если реакция выделяет энергию</returns>
    public static Quantity QValue(IEnumerable<Quantity> initial, IEnumerable<Quantity> final)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(final);

        double before = Sum(initial, nameof(initial));
        double after = Sum(final, nameof(final));

        return new Quantity(before - after, Dimension.Energy);
    }

    /// <summary>Энергия бета-минус-распада по массам атомов: <c>(M_P − M_D)c²</c></summary>
    /// <param name="parentAtomicMass">Масса родительского атома</param>
    /// <param name="daughterAtomicMass">Масса дочернего атома (Z + 1)</param>
    public static Quantity BetaMinusQ(Quantity parentAtomicMass, Quantity daughterAtomicMass)
        => Difference(parentAtomicMass, daughterAtomicMass, 0);

    /// <summary>
    /// Энергия позитронного распада по массам атомов: <c>(M_P − M_D − 2m_e)c²</c>
    /// </summary>
    /// <remarks>
    /// Отрицательное значение при положительной энергии захвата электрона — не ошибка:
    /// если разность масс атомов меньше 1.022 МэВ, позитронный распад запрещён, и ядро распадается
    /// только захватом электрона, как бериллий-7.
    /// </remarks>
    /// <param name="parentAtomicMass">Масса родительского атома</param>
    /// <param name="daughterAtomicMass">Масса дочернего атома (Z − 1)</param>
    public static Quantity PositronEmissionQ(Quantity parentAtomicMass, Quantity daughterAtomicMass)
        => Difference(parentAtomicMass, daughterAtomicMass, 2);

    /// <summary>
    /// Энергия захвата электрона по массам атомов: <c>(M_P − M_D)c²</c>; энергия связи захваченного
    /// электрона не вычтена
    /// </summary>
    /// <param name="parentAtomicMass">Масса родительского атома</param>
    /// <param name="daughterAtomicMass">Масса дочернего атома (Z − 1)</param>
    public static Quantity ElectronCaptureQ(Quantity parentAtomicMass, Quantity daughterAtomicMass)
        => Difference(parentAtomicMass, daughterAtomicMass, 0);

    /// <summary>Энерговыделение на килограмм исходных веществ</summary>
    /// <param name="qValue">Энергия одной реакции</param>
    /// <param name="reactantMasses">Массы вступающих в реакцию частиц</param>
    public static Quantity SpecificEnergy(Quantity qValue, IEnumerable<Quantity> reactantMasses)
    {
        ArgumentNullException.ThrowIfNull(reactantMasses);

        double q = qValue.RequireSi(Dimension.Energy, nameof(qValue));
        double c = PhysicalConstants.SpeedOfLight.SiValue;
        double mass = Sum(reactantMasses, nameof(reactantMasses)) / (c * c);

        if (!(mass > 0))
            throw new ArgumentException("Суммарная масса реагентов должна быть положительной", nameof(reactantMasses));

        return new Quantity(q / mass, Dimension.Energy / Dimension.MassDim);
    }

    private static Quantity Difference(Quantity parent, Quantity daughter, int electrons)
    {
        double electron = MassEnergy.RestEnergyJoules(PhysicalConstants.ElectronMass, "electron");
        double q = MassEnergy.RestEnergyJoules(parent, nameof(parent))
            - MassEnergy.RestEnergyJoules(daughter, nameof(daughter))
            - (electrons * electron);

        return new Quantity(q, Dimension.Energy);
    }

    private static double Sum(IEnumerable<Quantity> masses, string paramName)
    {
        double sum = 0;
        int count = 0;

        foreach (Quantity mass in masses)
        {
            double energy = MassEnergy.RestEnergyJoules(mass, paramName);

            if (!(energy >= 0) || double.IsInfinity(energy))
                throw new ArgumentOutOfRangeException(paramName, "Массы — конечные неотрицательные числа");

            sum += energy;
            count++;
        }

        if (count == 0)
            throw new ArgumentException("Нужна хотя бы одна частица", paramName);

        return sum;
    }
}
