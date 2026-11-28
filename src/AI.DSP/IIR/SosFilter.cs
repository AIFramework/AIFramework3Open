using System;
using System.Collections.Generic;
using AI.DSP.Multirate;

namespace AI.DSP.IIR;

/// <summary>
/// Потоковый каскад звеньев второго порядка как блок многоскоростной обработки (коэффициент 1).
/// В double — прямая форма II транспонированная (как scipy.signal.sosfilt); в Q16 — прямая форма I
/// с 64-битным накопителем по правилам <see cref="Q16Options"/>.
/// </summary>
/// <remarks>
/// В Q16 коэффициенты квантуются с шагом 2⁻¹⁶: у узкополосных фильтров высокого порядка это сдвигает полюсы
/// и может нарушить устойчивость (п. 2.4); проверяйте выход относительно double.
/// </remarks>
public sealed class SosFilter : MultirateBlock
{
    private readonly SecondOrderSections _sections;

    /// <summary>
    /// Создаёт фильтр.
    /// </summary>
    public SosFilter(SecondOrderSections sections, Arithmetic arithmetic = Arithmetic.Double, Q16Options options = null)
        : base(arithmetic, options)
        => _sections = sections ?? throw new ArgumentNullException(nameof(sections));

    /// <summary>Звенья фильтра.</summary>
    public SecondOrderSections Sections => _sections;

    /// <inheritdoc/>
    public override double RateFactor => 1;

    /// <inheritdoc/>
    public override double Delay => 0;

    /// <summary>Пять умножений и четыре сложения на звено.</summary>
    public override OperationCount OperationsPerOutput
    {
        get
        {
            int n = _sections.SectionCount;
            bool wide = Arithmetic == Arithmetic.Q16 && Options.WideAccumulator;
            double shifts = Arithmetic == Arithmetic.Double ? 0 : wide ? n : 5 * n;
            return new OperationCount(4 * n, 5 * n, shifts, 0);
        }
    }

    private protected override IBlockCore CreateCore<T>(ISampleArithmetic<T> math) => new Core<T>(this, math);

    private sealed class Core<T> : IBlockCore
        where T : struct
    {
        private readonly ISampleArithmetic<T> _math;
        private readonly bool _transposed;
        private readonly T[,] _c;
        private readonly T[,] _state;   // DF-IIT: z₀, z₁; DF-I: x₁, x₂, y₁, y₂

        public Core(SosFilter owner, ISampleArithmetic<T> math)
        {
            _math = math;
            _transposed = owner.Arithmetic == Arithmetic.Double;
            double[,] sos = owner._sections.Raw;
            int n = sos.GetLength(0);
            _c = new T[n, 6];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    _c[i, j] = math.Coefficient(sos[i, j]);
                }
            }

            _state = new T[n, 4];
        }

        public void Process(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (double x in input)
            {
                T cur = _math.FromInput(x);
                for (int s = 0; s < _c.GetLength(0); s++)
                {
                    cur = _transposed ? Transposed(s, cur) : DirectI(s, cur);
                }

                output.Add(_math.ToOutput(cur));
            }
        }

        public void Flush(List<double> output)
        {
        }

        public void Reset() => Array.Clear(_state);

        private T Transposed(int s, T x)
        {
            T y = _math.Add(_math.Finish(_math.Accumulate(default, x, _c[s, 0])), _state[s, 0]);
            T z0 = _math.Finish(_math.Accumulate(default, x, _c[s, 1]));
            z0 = _math.Subtract(z0, _math.Finish(_math.Accumulate(default, y, _c[s, 4])));
            _state[s, 0] = _math.Add(z0, _state[s, 1]);
            _state[s, 1] = _math.Subtract(_math.Finish(_math.Accumulate(default, x, _c[s, 2])), _math.Finish(_math.Accumulate(default, y, _c[s, 5])));
            return y;
        }

        private T DirectI(int s, T x)
        {
            T acc = _math.Accumulate(default, x, _c[s, 0]);
            acc = _math.Accumulate(acc, _state[s, 0], _c[s, 1]);
            acc = _math.Accumulate(acc, _state[s, 1], _c[s, 2]);
            T feedback = _math.Accumulate(default, _state[s, 2], _c[s, 4]);
            feedback = _math.Accumulate(feedback, _state[s, 3], _c[s, 5]);
            T y = _math.Subtract(_math.Finish(acc), _math.Finish(feedback));
            _state[s, 1] = _state[s, 0];
            _state[s, 0] = x;
            _state[s, 3] = _state[s, 2];
            _state[s, 2] = y;
            return y;
        }
    }
}
