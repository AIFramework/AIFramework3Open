#nullable enable
using AI.DataStructs.Algebraic;

namespace AI.Physics.Continuum.Structures;

/// <summary>
/// Усилия в элементе плоской рамы в его осях: ось x от начала к концу, ось y повернута от нее на 90° против
/// часовой стрелки. Изгибающий момент положителен, когда растянуты волокна со стороны отрицательной оси y
/// (у горизонтального элемента с началом слева это провисание)
/// </summary>
/// <param name="Length">Длина элемента, м</param>
/// <param name="AxialStart">Продольная сила в начале, Н: растяжение плюс</param>
/// <param name="AxialEnd">Продольная сила в конце, Н: растяжение плюс</param>
/// <param name="ShearStart">Поперечная сила в начале, Н: сила узла на элемент по оси y</param>
/// <param name="MomentStart">Изгибающий момент в начале, Н·м</param>
/// <param name="MomentEnd">Изгибающий момент в конце, Н·м</param>
/// <param name="TransverseLoad">Поперечная равномерная нагрузка в осях элемента, Н/м, по оси y</param>
/// <param name="MaxMoment">Наибольший по модулю момент вдоль элемента, Н·м</param>
/// <param name="MaxStress">Наибольшее нормальное напряжение |N|/A + |M|/W, Па</param>
/// <param name="Ends">Закрепление концов при потере устойчивости из плоскости рамы</param>
/// <param name="OutOfPlaneBuckling">Запас устойчивости из плоскости по Эйлеру: критическая сила на сжимающую;
/// у растянутого элемента и без момента инерции из плоскости бесконечность</param>
public sealed record FrameElementForces(
    double Length,
    double AxialStart,
    double AxialEnd,
    double ShearStart,
    double MomentStart,
    double MomentEnd,
    double TransverseLoad,
    double MaxMoment,
    double MaxStress,
    ColumnEnds Ends,
    double OutOfPlaneBuckling)
{
    /// <summary>Коэффициент приведения длины для устойчивости из плоскости по закреплению концов</summary>
    public double EffectiveLengthFactor => Beams.EffectiveLengthFactor(Ends);

    /// <summary>Изгибающий момент на расстоянии от начала элемента: M(x) = M₁ + V₁·x + q·x²/2, Н·м</summary>
    /// <param name="distance">Расстояние от начала, м</param>
    public double MomentAt(double distance)
        => MomentStart + (ShearStart * distance) + (TransverseLoad * distance * distance / 2);

    /// <summary>Эпюра моментов: значения в равноотстоящих точках от начала до конца элемента, Н·м</summary>
    /// <param name="points">Число точек, не меньше двух</param>
    public Vector MomentDiagram(int points = 21)
    {
        if (points < 2)
            throw new ArgumentOutOfRangeException(nameof(points), "Нужно не меньше двух точек");

        return new Vector(Enumerable.Range(0, points).Select(i => MomentAt(Length * i / (points - 1))));
    }
}
