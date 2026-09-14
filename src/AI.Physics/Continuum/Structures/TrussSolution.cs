#nullable enable
using AI.DataStructs.Algebraic;
using AI.Geometry.Primitives;

namespace AI.Physics.Continuum.Structures;

/// <summary>Решение пространственной фермы; все величины в СИ</summary>
/// <param name="Forces">Продольное усилие в стержне, Н: растяжение плюс, сжатие минус</param>
/// <param name="Stresses">Нормальное напряжение в стержне, Па</param>
/// <param name="Reactions">Реакция опоры в узле, Н; у свободного узла ноль</param>
/// <param name="Displacements">Перемещение узла, м</param>
/// <param name="BucklingFactors">Запас устойчивости стержня: критическая сила Эйлера на сжимающее усилие; у растянутого бесконечность</param>
public sealed record TrussSolution(
    Vector Forces,
    Vector Stresses,
    IReadOnlyList<Vector3> Reactions,
    IReadOnlyList<Vector3> Displacements,
    Vector BucklingFactors)
{
    /// <summary>
    /// Запас: во сколько раз можно поднять нагрузку до предела прочности в самом нагруженном стержне
    /// или до потери устойчивости самого опасного сжатого
    /// </summary>
    /// <param name="strength">Предел прочности материала, Па</param>
    public double SafetyFactor(double strength)
    {
        double stress = Stresses.Select(Math.Abs).DefaultIfEmpty(0).Max();
        double byStrength = stress > 0 ? strength / stress : double.PositiveInfinity;

        return Math.Min(byStrength, BucklingFactors.DefaultIfEmpty(double.PositiveInfinity).Min());
    }
}
