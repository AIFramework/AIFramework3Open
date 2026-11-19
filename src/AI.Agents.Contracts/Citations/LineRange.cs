namespace AI.Agents.Contracts.Citations;

/// <summary>
/// Строки источника: с <see cref="Start"/> по <see cref="End"/> включительно, нумерация с 1.
/// </summary>
/// <remarks>
/// Одна строка задается равными <see cref="Start"/> и <see cref="End"/>. Конец раньше начала отвергается при
/// создании и при чтении JSON, в каком бы порядке ни пришли поля.
/// </remarks>
public sealed record LineRange
{
    private readonly int? _start;
    private readonly int? _end;

    /// <summary>Первая строка, с 1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Номер меньше 1 или больше <see cref="End"/>.</exception>
    public required int Start
    {
        get => _start ?? 0;
        init
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(Start), value, "Строки считаются с 1.");

            if (_end is int end && end < value)
                throw OrderError(value, end);

            _start = value;
        }
    }

    /// <summary>Последняя строка включительно, с 1; не раньше <see cref="Start"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Номер меньше 1 или меньше <see cref="Start"/>.</exception>
    public required int End
    {
        get => _end ?? 0;
        init
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(End), value, "Строки считаются с 1.");

            if (_start is int start && value < start)
                throw OrderError(start, value);

            _end = value;
        }
    }

    private static ArgumentOutOfRangeException OrderError(int start, int end) =>
        new(nameof(End), end, $"Конец строк ({end}) раньше начала ({start}).");
}
