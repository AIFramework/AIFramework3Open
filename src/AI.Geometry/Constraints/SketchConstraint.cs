#nullable enable

using System;
using System.Collections.Generic;

namespace AI.Geometry.Constraints;

/// <summary>
/// Ограничение эскиза: блок невязок, зависящий от набора именованных неизвестных.
/// Ограничение выполнено, когда все невязки блока равны нулю.
/// </summary>
/// <remarks>
/// Невязки вычисляются по локальному массиву значений неизвестных в порядке <see cref="Unknowns"/>.
/// Если аналитических производных нет (или они не определены в точке), решатель считает их
/// центральными разностями только по неизвестным этого блока.
/// </remarks>
public sealed class SketchConstraint
{
    private readonly Func<double[], double[]> _residuals;
    private readonly Func<double[], double[,]?>? _derivatives;

    internal SketchConstraint(
        string name,
        string kind,
        string[] unknowns,
        int count,
        Func<double[], double[]> residuals,
        Func<double[], double[,]?>? derivatives)
    {
        Name = name;
        Kind = kind;
        Unknowns = unknowns;
        Count = count;
        _residuals = residuals;
        _derivatives = derivatives;
    }

    /// <summary>
    /// Уникальное в пределах эскиза имя ограничения; по нему ограничение называется в отчете.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Вид ограничения (distance, parallel, tangent и т. п.).
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Имена неизвестных, от которых зависит блок, в порядке локального массива значений.
    /// Имена, начинающиеся с «#», обозначают скрытые постоянные (числа, заданные в ограничении).
    /// </summary>
    public IReadOnlyList<string> Unknowns { get; }

    /// <summary>
    /// Число скалярных уравнений в блоке.
    /// </summary>
    public int Count { get; }

    internal double[] Residuals(double[] local) => _residuals(local);

    internal double[,]? Derivatives(double[] local) => _derivatives?.Invoke(local);
}
