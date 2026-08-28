namespace AI.Script.Runtime;

/// <summary>
/// Полуоткрытый диапазон чисел <c>[Start, End)</c> с шагом.
/// </summary>
/// <remarks>
/// Отдельный тип, а не список: <c>for i in 0..1_000_000</c> не должен материализовать миллион
/// значений ради того, чтобы по ним пройти.
/// </remarks>
public readonly struct ScriptRange
{
    /// <summary>Начало включительно.</summary>
    public double Start { get; }

    /// <summary>Конец исключительно.</summary>
    public double End { get; }

    /// <summary>Шаг; не равен нулю.</summary>
    public double Step { get; }

    /// <summary>Создаёт диапазон.</summary>
    public ScriptRange(double start, double end, double step = 1)
    {
        Start = start;
        End = end;
        Step = step == 0 ? 1 : step;
    }

    /// <summary>Число элементов.</summary>
    public int Count
    {
        get
        {
            double span = End - Start;
            if (Step > 0 && span <= 0) return 0;
            if (Step < 0 && span >= 0) return 0;

            return (int)Math.Ceiling(span / Step);
        }
    }

    /// <summary>Элемент по индексу.</summary>
    public double this[int index] => Start + (Step * index);

    /// <summary>Перечисляет значения диапазона.</summary>
    public IEnumerable<double> Values()
    {
        int count = Count;
        for (int i = 0; i < count; i++) yield return Start + (Step * i);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        Step == 1
            ? $"{Format(Start)}..{Format(End)}"
            : $"{Format(Start)}..{Format(End)} by {Format(Step)}";

    private static string Format(double value) =>
        value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? ((long)value).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
