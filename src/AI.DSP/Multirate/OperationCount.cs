namespace AI.DSP.Multirate;

/// <summary>
/// Число операций (обычно на один выходной отсчёт блока), показатель затрат (2.25).
/// </summary>
/// <param name="Additions">Сложения и вычитания.</param>
/// <param name="Multiplications">Умножения.</param>
/// <param name="Shifts">Сдвиги.</param>
/// <param name="Divisions">Деления.</param>
public readonly record struct OperationCount(double Additions, double Multiplications, double Shifts, double Divisions)
{
    /// <summary>Всего операций.</summary>
    public double Total => Additions + Multiplications + Shifts + Divisions;

    /// <summary>Сумма затрат.</summary>
    public static OperationCount operator +(OperationCount a, OperationCount b)
        => new(a.Additions + b.Additions, a.Multiplications + b.Multiplications, a.Shifts + b.Shifts, a.Divisions + b.Divisions);

    /// <summary>Пересчёт затрат на другой отсчёт (например, на выходной отсчёт цепочки).</summary>
    public static OperationCount operator *(OperationCount a, double scale)
        => new(a.Additions * scale, a.Multiplications * scale, a.Shifts * scale, a.Divisions * scale);
}
