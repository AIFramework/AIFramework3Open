using System;
using System.Collections.Generic;
using AI.DataStructs.Algebraic;

namespace AI.DSP.Multirate;

/// <summary>
/// Основа блоков передискретизации: перевод отсчётов в выбранную арифметику и потоковый интерфейс.
/// Алгоритм блока пишется один раз как обобщённое ядро над double и Q16.
/// </summary>
public abstract class MultirateBlock : IMultirateBlock
{
    private IBlockCore _core;
    private readonly List<double> _output = new();

    /// <summary>
    /// Создаёт блок с ядром в выбранной арифметике.
    /// </summary>
    /// <param name="arithmetic">Вид арифметики.</param>
    /// <param name="options">Правила Q16; null — <see cref="Q16Options.Recommended"/>.</param>
    private protected MultirateBlock(Arithmetic arithmetic, Q16Options options)
    {
        Arithmetic = arithmetic;
        Options = options ?? Q16Options.Recommended;
    }

    /// <inheritdoc/>
    public Arithmetic Arithmetic { get; }

    /// <summary>Правила вычислений Q16 (используются при <see cref="Arithmetic.Q16"/>).</summary>
    public Q16Options Options { get; }

    /// <inheritdoc/>
    public abstract double RateFactor { get; }

    /// <inheritdoc/>
    public abstract double Delay { get; }

    /// <inheritdoc/>
    public abstract OperationCount OperationsPerOutput { get; }

    // Ядро создаётся при первом обращении: в конструкторе базы параметры наследника ещё не заданы
    private IBlockCore Core => _core ??= Arithmetic == Arithmetic.Double
        ? CreateCore(DoubleArithmetic.Instance)
        : CreateCore(new Q16Arithmetic(Options));

    /// <inheritdoc/>
    public Vector Process(Vector input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _output.Clear();
        Core.Process(input.ToArray(), _output);
        return new Vector(_output);
    }

    /// <inheritdoc/>
    public Vector Process(double sample)
    {
        _output.Clear();
        Core.Process(new[] { sample }, _output);
        return new Vector(_output);
    }

    /// <inheritdoc/>
    public Vector Flush()
    {
        _output.Clear();
        Core.Flush(_output);
        return new Vector(_output);
    }

    /// <inheritdoc/>
    public void Reset() => Core.Reset();

    /// <inheritdoc/>
    public Vector ProcessBlock(Vector input)
    {
        Reset();
        var all = new List<double>(Process(input));
        all.AddRange(Flush());
        return new Vector(all);
    }

    /// <summary>
    /// Создаёт ядро блока в заданной арифметике.
    /// </summary>
    private protected abstract IBlockCore CreateCore<T>(ISampleArithmetic<T> math)
        where T : struct;

    /// <summary>
    /// Ядро блока: алгоритм над отсчётами одной арифметики.
    /// </summary>
    private protected interface IBlockCore
    {
        void Process(ReadOnlySpan<double> input, List<double> output);

        void Flush(List<double> output);

        void Reset();
    }
}
