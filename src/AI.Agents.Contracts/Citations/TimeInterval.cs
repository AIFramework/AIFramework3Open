namespace AI.Agents.Contracts.Citations;

/// <summary>
/// Отрезок записи (звук, видео) от ее начала: с <see cref="Start"/> по <see cref="End"/>.
/// </summary>
/// <remarks>
/// Момент задается равными <see cref="Start"/> и <see cref="End"/>. Отрицательное время и конец раньше
/// начала отвергаются при создании и при чтении JSON, в каком бы порядке ни пришли поля.
/// </remarks>
public sealed record TimeInterval
{
    private readonly TimeSpan? _start;
    private readonly TimeSpan? _end;

    /// <summary>Начало отрезка от начала записи.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Время отрицательное или позже <see cref="End"/>.</exception>
    public required TimeSpan Start
    {
        get => _start ?? TimeSpan.Zero;
        init
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(Start), value, "Время отсчитывается от начала записи и не бывает отрицательным.");

            if (_end is TimeSpan end && end < value)
                throw OrderError(value, end);

            _start = value;
        }
    }

    /// <summary>Конец отрезка от начала записи; не раньше <see cref="Start"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Время отрицательное или раньше <see cref="Start"/>.</exception>
    public required TimeSpan End
    {
        get => _end ?? TimeSpan.Zero;
        init
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(End), value, "Время отсчитывается от начала записи и не бывает отрицательным.");

            if (_start is TimeSpan start && value < start)
                throw OrderError(start, value);

            _end = value;
        }
    }

    private static ArgumentOutOfRangeException OrderError(TimeSpan start, TimeSpan end) =>
        new(nameof(End), end, $"Конец отрезка ({end}) раньше начала ({start}).");
}
