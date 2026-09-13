using System.Numerics;

namespace AI.Microwave.Propagation;

/// <summary>Коэффициенты отражения для двух поляризаций</summary>
/// <param name="Perpendicular">Поле перпендикулярно плоскости падения (TE, s): для земли — горизонтальная поляризация</param>
/// <param name="Parallel">Поле в плоскости падения (TM, p): для земли — вертикальная поляризация</param>
public readonly record struct ReflectionCoefficients(Complex Perpendicular, Complex Parallel);

/// <summary>
/// Отражение плоской волны от плоской границы воздуха и материала — формулы Френеля.
/// </summary>
/// <remarks>
/// <para>
/// Угол скольжения ψ отсчитывается от поверхности. <c>Γ⊥ = (sin ψ − √(ε − cos²ψ))/(sin ψ + √(ε − cos²ψ))</c>,
/// <c>Γ∥ = (ε·sin ψ − √(ε − cos²ψ))/(ε·sin ψ + √(ε − cos²ψ))</c>. Корень берётся с неотрицательной
/// действительной частью.
/// </para>
/// <para>
/// Базис поляризации: s — единичный вектор, перпендикулярный плоскости падения, p = s × k для падающей
/// и отражённой волн. В этом базисе при скользящем падении оба коэффициента стремятся к −1 — поэтому
/// двухлучевая модель даёт спад 40 дБ на декаду при любой поляризации, — а у идеального проводника
/// Γ⊥ = −1 и Γ∥ = +1, как требует теория изображений. У Γ∥ есть угол Брюстера, где отражение почти
/// пропадает: для материала без потерь tg ψ_B = 1/√ε.
/// </para>
/// </remarks>
public static class FresnelReflection
{
    /// <summary>Коэффициенты отражения по комплексной проницаемости</summary>
    /// <param name="relativePermittivity">Комплексная относительная проницаемость</param>
    /// <param name="grazingAngleRad">Угол скольжения, радианы, от 0 до π/2</param>
    public static ReflectionCoefficients Coefficients(Complex relativePermittivity, double grazingAngleRad)
    {
        if (!(grazingAngleRad >= 0 && grazingAngleRad <= Math.PI / 2 + 1e-12))
            throw new ArgumentOutOfRangeException(nameof(grazingAngleRad), grazingAngleRad, "Угол скольжения лежит на [0; π/2]");

        // Среда без скачка проницаемости не отражает
        if ((relativePermittivity - 1).Magnitude < 1e-15)
            return new ReflectionCoefficients(Complex.Zero, Complex.Zero);

        double sin = Math.Sin(grazingAngleRad);
        double cos = Math.Cos(grazingAngleRad);
        Complex root = Complex.Sqrt(relativePermittivity - (cos * cos));

        return new ReflectionCoefficients(
            (sin - root) / (sin + root),
            ((relativePermittivity * sin) - root) / ((relativePermittivity * sin) + root));
    }

    /// <summary>Коэффициенты отражения от материала на частоте</summary>
    /// <param name="material">Материал</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    /// <param name="grazingAngleRad">Угол скольжения, радианы</param>
    public static ReflectionCoefficients Coefficients(RadioMaterial material, double frequencyHz, double grazingAngleRad)
    {
        ArgumentNullException.ThrowIfNull(material);

        return material.IsPerfectConductor
            ? new ReflectionCoefficients(-1, 1)
            : Coefficients(material.ComplexPermittivity(frequencyHz), grazingAngleRad);
    }

    /// <summary>Угол Брюстера для материала без потерь, отсчитанный от поверхности, радианы</summary>
    /// <param name="relativePermittivity">Относительная проницаемость</param>
    public static double BrewsterGrazingAngleRad(double relativePermittivity)
    {
        Wave.RequirePositive(relativePermittivity, nameof(relativePermittivity));

        return Math.Atan(1 / Math.Sqrt(relativePermittivity));
    }
}
