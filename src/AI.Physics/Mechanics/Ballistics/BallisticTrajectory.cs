#nullable enable
using AI.DataStructs.Algebraic;
using AI.Geometry.Primitives;

namespace AI.Physics.Mechanics.Ballistics;

/// <summary>
/// Траектория точечного снаряда до падения на землю; все величины в СИ. Если снаряд не упал за предельное время
/// (восходящий поток, выстрел вверх при слабой тяжести), <see cref="Landed"/> ложно, а величины падения не определены (NaN)
/// </summary>
/// <param name="Landed">Упал ли снаряд</param>
/// <param name="Range">Дальность по горизонтали от точки выстрела до точки падения, м</param>
/// <param name="FlightTime">Время полета, с</param>
/// <param name="ImpactSpeed">Скорость в момент падения относительно земли, м/с</param>
/// <param name="ImpactAngleDegrees">Угол падения ниже горизонта, градусы</param>
/// <param name="ImpactPoint">Точка падения, м</param>
/// <param name="ImpactVelocity">Скорость в момент падения, м/с</param>
/// <param name="ApexHeight">Наибольшая высота над землей, м</param>
/// <param name="ApexTime">Момент наибольшей высоты, с</param>
/// <param name="ApexPoint">Вершина траектории, м</param>
/// <param name="Times">Моменты узлов траектории, с</param>
/// <param name="Path">Положения в узлах траектории, м</param>
/// <param name="Velocities">Скорости в узлах траектории, м/с</param>
public sealed record BallisticTrajectory(
    bool Landed,
    double Range,
    double FlightTime,
    double ImpactSpeed,
    double ImpactAngleDegrees,
    Vector3 ImpactPoint,
    Vector3 ImpactVelocity,
    double ApexHeight,
    double ApexTime,
    Vector3 ApexPoint,
    Vector Times,
    IReadOnlyList<Vector3> Path,
    IReadOnlyList<Vector3> Velocities)
{
    /// <summary>Кинетическая энергия в момент падения, Дж</summary>
    /// <param name="mass">Масса снаряда, кг</param>
    public double ImpactEnergy(double mass) => 0.5 * mass * ImpactSpeed * ImpactSpeed;
}
