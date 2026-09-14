#nullable enable
namespace AI.Physics.Continuum.Structures;

/// <summary>Решение плоской рамы</summary>
/// <param name="Displacements">Перемещения узлов: сдвиги по x и z, м, и поворот, рад</param>
/// <param name="Reactions">Реакции опор: силы по x и z, Н, и момент, Н·м; у свободного узла нули</param>
/// <param name="Elements">Усилия в элементах</param>
/// <param name="InPlaneBuckling">Во сколько раз можно поднять всю нагрузку до потери устойчивости рамы в ее плоскости;
/// без сжатых элементов бесконечность</param>
public sealed record FrameSolution(
    IReadOnlyList<FrameNodeValues> Displacements,
    IReadOnlyList<FrameNodeValues> Reactions,
    IReadOnlyList<FrameElementForces> Elements,
    double InPlaneBuckling)
{
    /// <summary>
    /// Запас: во сколько раз можно поднять нагрузку до текучести самого нагруженного сечения или до потери
    /// устойчивости рамы в плоскости либо элемента из плоскости
    /// </summary>
    /// <param name="yieldStrength">Предел текучести, Па</param>
    public double SafetyFactor(double yieldStrength)
    {
        double stress = Elements.Select(element => element.MaxStress).DefaultIfEmpty(0).Max();
        double byStrength = stress > 0 ? yieldStrength / stress : double.PositiveInfinity;
        double outOfPlane = Elements.Select(element => element.OutOfPlaneBuckling).DefaultIfEmpty(double.PositiveInfinity).Min();

        return Math.Min(byStrength, Math.Min(InPlaneBuckling, outOfPlane));
    }
}
