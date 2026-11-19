namespace AI.Agents.Contracts.Citations;

/// <summary>
/// Прямоугольная область на последнем сегменте пути (слайде, картинке, странице) в долях его ширины и
/// высоты: от 0 до 1, начало отсчета в левом верхнем углу.
/// </summary>
/// <remarks>
/// Доли не зависят от разрешения и масштаба показа. Каждое значение лежит в 0..1, и область не выходит за
/// край: <c>X + Width ≤ 1</c>, <c>Y + Height ≤ 1</c>. Нарушение отвергается при создании и при чтении JSON.
/// </remarks>
public sealed record RelativeRect
{
    /// <summary>Допуск округления при проверке края: 0,1 + 0,9 в double может дать чуть больше 1.</summary>
    private const double Tolerance = 1e-9;

    private readonly double? _x;
    private readonly double? _y;
    private readonly double? _width;
    private readonly double? _height;

    /// <summary>Левый край в долях ширины, 0..1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Значение вне 0..1 или область выходит за правый край.</exception>
    public required double X
    {
        get => _x ?? 0;
        init => _x = Checked(value, _width, nameof(X), nameof(Width));
    }

    /// <summary>Верхний край в долях высоты, 0..1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Значение вне 0..1 или область выходит за нижний край.</exception>
    public required double Y
    {
        get => _y ?? 0;
        init => _y = Checked(value, _height, nameof(Y), nameof(Height));
    }

    /// <summary>Ширина в долях, 0..1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Значение вне 0..1 или область выходит за правый край.</exception>
    public required double Width
    {
        get => _width ?? 0;
        init => _width = Checked(value, _x, nameof(Width), nameof(X));
    }

    /// <summary>Высота в долях, 0..1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Значение вне 0..1 или область выходит за нижний край.</exception>
    public required double Height
    {
        get => _height ?? 0;
        init => _height = Checked(value, _y, nameof(Height), nameof(Y));
    }

    private static double Checked(double value, double? pair, string name, string pairName)
    {
        if (!(value >= 0 && value <= 1))
            throw new ArgumentOutOfRangeException(name, value, $"{name} задается долей от 0 до 1.");

        if (pair is double other && value + other > 1 + Tolerance)
        {
            throw new ArgumentOutOfRangeException(
                name, value, $"Область выходит за край: {name} + {pairName} = {value + other}, а должно быть не больше 1.");
        }

        return value;
    }
}
