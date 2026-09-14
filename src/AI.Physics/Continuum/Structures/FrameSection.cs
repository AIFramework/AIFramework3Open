#nullable enable
namespace AI.Physics.Continuum.Structures;

/// <summary>Сечение элемента плоской рамы; все величины в СИ</summary>
/// <param name="Area">Площадь сечения, м²</param>
/// <param name="SecondMoment">Момент инерции при изгибе в плоскости рамы, м⁴</param>
/// <param name="SectionModulus">Момент сопротивления I/c при изгибе в плоскости рамы, м³</param>
/// <param name="WeakSecondMoment">Момент инерции из плоскости рамы, м⁴, для устойчивости сжатого элемента; если не задан, устойчивость не проверяется</param>
public sealed record FrameSection(double Area, double SecondMoment, double SectionModulus, double? WeakSecondMoment = null)
{
    /// <summary>Сплошной прямоугольник</summary>
    /// <param name="width">Ширина b из плоскости рамы, м</param>
    /// <param name="height">Высота h в плоскости рамы, м</param>
    public static FrameSection Rectangle(double width, double height)
    {
        if (!(width > 0 && height > 0))
            throw new ArgumentOutOfRangeException(nameof(width), "Размеры сечения должны быть положительными");

        return new FrameSection(
            width * height,
            width * height * height * height / 12,
            width * height * height / 6,
            height * width * width * width / 12);
    }
}
