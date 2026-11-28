using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;

namespace AI.DSP.Multirate;

/// <summary>
/// Цепочка блоков многоскоростной обработки; сама является блоком.
/// Затраты и задержка пересчитываются на выходной отсчёт цепочки.
/// </summary>
public sealed class MultirateChain : IMultirateBlock
{
    private readonly IMultirateBlock[] _blocks;

    /// <summary>
    /// Создаёт цепочку.
    /// </summary>
    /// <param name="blocks">Блоки в порядке прохождения сигнала.</param>
    public MultirateChain(params IMultirateBlock[] blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (blocks.Length == 0 || blocks.Any(b => b is null))
        {
            throw new ArgumentException("Цепочка состоит хотя бы из одного блока, без пустых.", nameof(blocks));
        }

        _blocks = blocks;
    }

    /// <summary>Блоки цепочки.</summary>
    public IReadOnlyList<IMultirateBlock> Blocks => _blocks;

    /// <inheritdoc/>
    public double RateFactor => _blocks.Aggregate(1.0, (r, b) => r * b.RateFactor);

    /// <inheritdoc/>
    public double Delay
    {
        get
        {
            double delay = 0;
            foreach (IMultirateBlock b in _blocks)
            {
                delay = (delay * b.RateFactor) + b.Delay;
            }

            return delay;
        }
    }

    /// <inheritdoc/>
    public OperationCount OperationsPerOutput
    {
        get
        {
            // Блок i выдаёт Π_{j>i} 1/r_j своих отсчётов на один выход цепочки
            OperationCount total = default;
            double perOutput = 1;
            for (int i = _blocks.Length - 1; i >= 0; i--)
            {
                total += _blocks[i].OperationsPerOutput * perOutput;
                perOutput /= _blocks[i].RateFactor;
            }

            return total;
        }
    }

    /// <summary>Q16, если все блоки целочисленные, иначе double.</summary>
    public Arithmetic Arithmetic => _blocks.All(b => b.Arithmetic == Arithmetic.Q16) ? Arithmetic.Q16 : Arithmetic.Double;

    /// <inheritdoc/>
    public Vector Process(Vector input)
    {
        Vector data = input;
        foreach (IMultirateBlock b in _blocks)
        {
            data = b.Process(data);
        }

        return data;
    }

    /// <inheritdoc/>
    public Vector Process(double sample) => Process(new Vector(new[] { sample }));

    /// <inheritdoc/>
    public Vector Flush()
    {
        // Хвост блока i проходит через все следующие блоки до их собственного завершения
        var data = new List<double>();
        foreach (IMultirateBlock b in _blocks)
        {
            var next = new List<double>(b.Process(new Vector(data)));
            next.AddRange(b.Flush());
            data = next;
        }

        return new Vector(data);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        foreach (IMultirateBlock b in _blocks)
        {
            b.Reset();
        }
    }

    /// <inheritdoc/>
    public Vector ProcessBlock(Vector input)
    {
        Reset();
        var all = new List<double>(Process(input));
        all.AddRange(Flush());
        return new Vector(all);
    }
}
