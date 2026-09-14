#nullable enable
namespace AI.Physics.Mechanics.Ballistics;

/// <summary>
/// Коэффициент лобового сопротивления снаряда как функция числа Маха.
/// </summary>
/// <remarks>
/// Табличные законы сопротивления (G1, G7 и другие) подключаются через <see cref="FromMach"/> с интерполяцией
/// по таблице; самих таблиц здесь нет, их нужно брать из проверенного источника.
/// </remarks>
public sealed class DragModel
{
    private readonly Func<double, double>? _coefficient;

    private DragModel(Func<double, double>? coefficient)
    {
        _coefficient = coefficient;
    }

    /// <summary>Сопротивления нет: полет безвоздушный</summary>
    public static DragModel None { get; } = new(null);

    /// <summary>Нет ли сопротивления</summary>
    public bool IsNone => _coefficient is null;

    /// <summary>Постоянный коэффициент сопротивления</summary>
    /// <param name="coefficient">Коэффициент C_x, не меньше нуля</param>
    public static DragModel Constant(double coefficient)
    {
        if (!(coefficient >= 0) || double.IsInfinity(coefficient))
            throw new ArgumentOutOfRangeException(nameof(coefficient), "Коэффициент сопротивления должен быть конечным и неотрицательным");

        return new DragModel(_ => coefficient);
    }

    /// <summary>Коэффициент сопротивления, зависящий от числа Маха</summary>
    /// <param name="coefficientOfMach">Функция: число Маха в коэффициент C_x</param>
    public static DragModel FromMach(Func<double, double> coefficientOfMach)
    {
        ArgumentNullException.ThrowIfNull(coefficientOfMach);
        return new DragModel(coefficientOfMach);
    }

    /// <summary>Коэффициент сопротивления при данном числе Маха</summary>
    /// <param name="mach">Число Маха</param>
    public double Coefficient(double mach) => _coefficient?.Invoke(mach) ?? 0;
}
