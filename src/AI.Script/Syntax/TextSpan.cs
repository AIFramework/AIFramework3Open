namespace AI.Script.Syntax;

/// <summary>
/// Отрезок исходного текста: смещение от начала файла и длина.
/// </summary>
/// <remarks>
/// Позиция хранится смещением, а не парой «строка/колонка»: строка и колонка нужны только
/// человеку, в момент печати диагностики, и считаются из <see cref="SourceText"/> один раз.
/// </remarks>
public readonly struct TextSpan : IEquatable<TextSpan>
{
    /// <summary>Смещение начала отрезка от начала файла.</summary>
    public int Start { get; }

    /// <summary>Длина отрезка в символах.</summary>
    public int Length { get; }

    /// <summary>Смещение первого символа за отрезком.</summary>
    public int End => Start + Length;

    /// <summary>Создаёт отрезок по смещению и длине.</summary>
    public TextSpan(int start, int length)
    {
        Start = start < 0 ? 0 : start;
        Length = length < 0 ? 0 : length;
    }

    /// <summary>Создаёт отрезок по границам [start, end).</summary>
    public static TextSpan FromBounds(int start, int end) => new(start, end - start);

    /// <summary>Наименьший отрезок, содержащий оба.</summary>
    public TextSpan Union(TextSpan other) =>
        FromBounds(Math.Min(Start, other.Start), Math.Max(End, other.End));

    /// <inheritdoc/>
    public bool Equals(TextSpan other) => Start == other.Start && Length == other.Length;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TextSpan span && Equals(span);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Start, Length);

    /// <inheritdoc/>
    public override string ToString() => $"[{Start}..{End})";
}
