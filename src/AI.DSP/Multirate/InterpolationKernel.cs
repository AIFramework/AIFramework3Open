using System;
using AI.DataStructs.Algebraic;

namespace AI.DSP.Multirate;

/// <summary>
/// Ядро интерполяции фильтра Фэрроу: значение сигнала между отсчётами x[0] и x[1] по узлам
/// x[<see cref="FirstNode"/>] … x[<see cref="LastNode"/>] при дробной задержке μ ∈ [0; 1).
/// </summary>
public abstract class InterpolationKernel
{
    /// <summary>Первый узел относительно текущего отсчёта (например, −1).</summary>
    public abstract int FirstNode { get; }

    /// <summary>Последний узел относительно текущего отсчёта (например, 2).</summary>
    public abstract int LastNode { get; }

    /// <summary>Число узлов.</summary>
    public int NodeCount => LastNode - FirstNode + 1;

    /// <summary>
    /// Число операций на один выходной отсчёт.
    /// </summary>
    /// <param name="arithmetic">Арифметика.</param>
    /// <param name="options">Правила Q16.</param>
    public abstract OperationCount Operations(Arithmetic arithmetic, Q16Options options);

    /// <summary>
    /// Значение в точке μ по узлам (в double).
    /// </summary>
    /// <param name="nodes">Отсчёты x[FirstNode] … x[LastNode].</param>
    /// <param name="mu">Дробная задержка, 0 ≤ μ &lt; 1.</param>
    public double Interpolate(Vector nodes, double mu)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count != NodeCount)
        {
            throw new ArgumentException($"Нужно {NodeCount} узлов, дано {nodes.Count}.", nameof(nodes));
        }

        return Bind(DoubleArithmetic.Instance).Evaluate(nodes.ToArray(), mu);
    }

    /// <summary>
    /// Подготовка вычислителя в заданной арифметике (квантование коэффициентов делается один раз).
    /// </summary>
    internal abstract IKernelEvaluator<T> Bind<T>(ISampleArithmetic<T> math)
        where T : struct;
}

/// <summary>
/// Вычислитель ядра в одной арифметике.
/// </summary>
internal interface IKernelEvaluator<T>
    where T : struct
{
    /// <summary>Значение по узлам x[FirstNode] … x[LastNode] при дробной задержке μ.</summary>
    T Evaluate(ReadOnlySpan<T> nodes, T mu);
}
