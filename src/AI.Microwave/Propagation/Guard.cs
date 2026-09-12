namespace AI.Microwave.Propagation;

/// <summary>Проверки аргументов моделей распространения</summary>
internal static class Guard
{
    /// <summary>Значение — конечное положительное число</summary>
    public static void RequirePositive(double value, string name)
    {
        if (!(value > 0) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name, value, "Ожидается конечное положительное число");
    }
}
