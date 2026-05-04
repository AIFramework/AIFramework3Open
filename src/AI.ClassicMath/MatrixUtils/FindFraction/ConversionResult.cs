using System.Numerics;

namespace AI.ClassicMath.MatrixUtils.FindFraction;

/// <summary>
/// Тип результата преобразования числа
/// </summary>
public enum ConversionType
{
    Integer,
    Terminating,
    Repeating,
    Algebraic,
    Transcendental,
    Irrational,
    Root
}

public class ConversionResult
{
    public ConversionType Type { get; set; }
    public string Fraction { get; set; }
    public string Description { get; set; }
    public BigInteger Numerator { get; set; }
    public BigInteger Denominator { get; set; }
}
