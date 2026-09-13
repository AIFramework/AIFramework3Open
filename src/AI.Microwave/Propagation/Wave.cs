using System.Numerics;
using AI.Microwave.Physics;

namespace AI.Microwave.Propagation;

/// <summary>Общее для моделей распространения: сферическое расхождение и проверки аргументов</summary>
internal static class Wave
{
    /// <summary>Скорость света, м/с</summary>
    public const double SpeedOfLight = MicrowavePhysics.SpeedOfLight;

    /// <summary>
    /// Амплитуда в свободном пространстве на длине пути L: λ/(4πL)·e^(−j2πL/λ).
    /// Квадрат модуля — величина, обратная потерям <see cref="PathLoss.FreeSpaceDb"/>
    /// </summary>
    public static Complex FreeSpaceGain(double lengthMetres, double wavelengthMetres)
        => Complex.FromPolarCoordinates(
            wavelengthMetres / (4 * Math.PI * lengthMetres),
            -2 * Math.PI * (lengthMetres / wavelengthMetres % 1.0));

    public static void RequirePositive(double value, string name)
    {
        if (!(value > 0) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name, value, "Значение должно быть конечным и положительным");
    }
}
