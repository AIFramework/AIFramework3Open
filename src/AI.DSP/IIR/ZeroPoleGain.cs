using System;
using System.Linq;
using System.Numerics;
using AI.DataStructs.WithComplexElements;

namespace AI.DSP.IIR;

/// <summary>
/// Передаточная функция в виде нулей, полюсов и коэффициента усиления: H = k·Π(x − z_i) / Π(x − p_i),
/// x = s (аналоговая) или z (цифровая).
/// </summary>
public sealed class ZeroPoleGain
{
    /// <summary>
    /// Создаёт описание нулей, полюсов и усиления.
    /// </summary>
    public ZeroPoleGain(ComplexVector zeros, ComplexVector poles, double gain)
        : this(zeros?.ToArray() ?? throw new ArgumentNullException(nameof(zeros)),
            poles?.ToArray() ?? throw new ArgumentNullException(nameof(poles)), gain)
    {
    }

    internal ZeroPoleGain(Complex[] zeros, Complex[] poles, double gain)
    {
        ZeroArray = zeros;
        PoleArray = poles;
        Gain = gain;
    }

    /// <summary>Нули.</summary>
    public ComplexVector Zeros => new(ZeroArray);

    /// <summary>Полюсы.</summary>
    public ComplexVector Poles => new(PoleArray);

    /// <summary>Коэффициент усиления k.</summary>
    public double Gain { get; }

    /// <summary>
    /// Устойчивость цифрового фильтра: все полюсы строго внутри единичной окружности.
    /// </summary>
    public bool IsStableDigital => PoleArray.All(p => p.Magnitude < 1);

    internal Complex[] ZeroArray { get; }

    internal Complex[] PoleArray { get; }
}
