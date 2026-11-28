using System;
using System.Collections.Generic;

namespace AI.DSP.Multirate;

/// <summary>
/// Фильтр Фэрроу: передискретизация с произвольным коэффициентом и дробной задержкой.
/// Положение выходного отсчёта в индексах входа хранит фазовый накопитель с 32 дробными битами
/// (как в синтезаторе srcsynth); целая часть выбирает узлы, дробная — задержку μ.
/// </summary>
/// <remarks>
/// При рациональном шаге a/b накопитель дополняется остатком по модулю b (как у алгоритма Брезенхэма):
/// положение равно ⌊τ·2^32⌋ точно и не уходит за тысячи отсчётов, а μ совпадает с вычисленным по точной дроби.
/// </remarks>
public sealed class FarrowResampler : MultirateBlock
{
    private const double Scale32 = 4294967296.0;

    /// <summary>
    /// Создаёт фильтр Фэрроу.
    /// </summary>
    /// <param name="step">Шаг по входу на один выходной отсчёт: F_вх / F_вых (например, 48000/44100).</param>
    /// <param name="kernel">Ядро интерполяции; null — кубический Лагранж (p = 3).</param>
    /// <param name="startPosition">
    /// Положение первого выходного отсчёта в индексах входа (дробная задержка); null — первый отсчёт,
    /// у которого есть все левые узлы (−FirstNode).
    /// </param>
    /// <param name="arithmetic">Арифметика.</param>
    /// <param name="options">Правила Q16.</param>
    public FarrowResampler(double step, InterpolationKernel kernel = null, double? startPosition = null,
        Arithmetic arithmetic = Arithmetic.Double, Q16Options options = null)
        : base(arithmetic, options)
    {
        if (!(step > 0) || double.IsInfinity(step))
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Шаг должен быть положительным.");
        }

        Kernel = kernel ?? new LagrangeKernel();
        Step = step;
        StartPosition = startPosition ?? -Kernel.FirstNode;
        if (StartPosition < -Kernel.FirstNode)
        {
            throw new ArgumentOutOfRangeException(nameof(startPosition), $"Начальное положение не меньше {-Kernel.FirstNode}: нужны левые узлы.");
        }

        Increment = (long)Math.Round(step * Scale32);
        Denominator = 1;
    }

    /// <summary>
    /// Создаёт фильтр Фэрроу с рациональным шагом a/b и точным фазовым накопителем.
    /// </summary>
    /// <param name="stepNumerator">a — числитель шага (например, F_вх).</param>
    /// <param name="stepDenominator">b — знаменатель шага (например, F_вых).</param>
    /// <param name="kernel">Ядро интерполяции; null — кубический Лагранж.</param>
    /// <param name="startPosition">Положение первого выходного отсчёта; округляется до кратного 1/b.</param>
    /// <param name="arithmetic">Арифметика.</param>
    /// <param name="options">Правила Q16.</param>
    public FarrowResampler(long stepNumerator, long stepDenominator, InterpolationKernel kernel = null, double? startPosition = null,
        Arithmetic arithmetic = Arithmetic.Double, Q16Options options = null)
        : this(stepNumerator / (double)stepDenominator, kernel, startPosition, arithmetic, options)
    {
        if (stepNumerator <= 0 || stepDenominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stepNumerator), "Числитель и знаменатель шага положительны.");
        }

        long g = (long)System.Numerics.BigInteger.GreatestCommonDivisor(stepNumerator, stepDenominator);
        Denominator = stepDenominator / g;
        Int128 scaled = (Int128)(stepNumerator / g) << 32;
        Increment = (long)(scaled / Denominator);
        IncrementRemainder = (long)(scaled % Denominator);
    }

    /// <summary>Ядро интерполяции.</summary>
    public InterpolationKernel Kernel { get; }

    /// <summary>Шаг по входу на один выходной отсчёт (F_вх / F_вых).</summary>
    public double Step { get; }

    /// <summary>Положение первого выходного отсчёта в индексах входа.</summary>
    public double StartPosition { get; }

    /// <summary>Приращение фазового накопителя (шаг с 32 дробными битами, округлённый вниз при рациональном шаге).</summary>
    public long Increment { get; }

    /// <summary>Знаменатель b рационального шага (1 — шаг задан числом double).</summary>
    public long Denominator { get; }

    /// <summary>Остаток приращения по модулю <see cref="Denominator"/>.</summary>
    public long IncrementRemainder { get; }

    /// <inheritdoc/>
    public override double RateFactor => Scale32 / (Increment + (IncrementRemainder / (double)Denominator));

    /// <inheritdoc/>
    public override double Delay => 0;

    /// <inheritdoc/>
    public override OperationCount OperationsPerOutput => Kernel.Operations(Arithmetic, Options);

    /// <summary>
    /// Первое положение вида m·step + delay, у которого есть все левые узлы ядра (как в экспериментах главы 3:
    /// отсчёты τ_m = m·r + d с ⌊τ_m⌋ &lt; −FirstNode отбрасываются).
    /// </summary>
    /// <param name="step">Шаг по входу.</param>
    /// <param name="delay">Дробная задержка d.</param>
    /// <param name="kernel">Ядро; null — кубический Лагранж.</param>
    public static double FirstPosition(double step, double delay, InterpolationKernel kernel = null)
    {
        int lo = -(kernel ?? new LagrangeKernel()).FirstNode;
        long m = 0;
        while (Math.Floor((m * step) + delay) < lo)
        {
            m++;
        }

        return (m * step) + delay;
    }

    private protected override IBlockCore CreateCore<T>(ISampleArithmetic<T> math) => new Core<T>(this, math);

    private sealed class Core<T> : IBlockCore
        where T : struct
    {
        private readonly FarrowResampler _owner;
        private readonly ISampleArithmetic<T> _math;
        private readonly IKernelEvaluator<T> _kernel;
        private readonly List<T> _buffer = new();
        private long _dropped;      // сколько старых отсчётов удалено из начала буфера
        private long _inputs;
        private long _position;     // положение выхода, 32 дробных бита
        private long _remainder;    // остаток положения по модулю Denominator

        public Core(FarrowResampler owner, ISampleArithmetic<T> math)
        {
            _owner = owner;
            _math = math;
            _kernel = owner.Kernel.Bind(math);
            Reset();
        }

        public void Process(ReadOnlySpan<double> input, List<double> output)
        {
            int first = _owner.Kernel.FirstNode;
            int last = _owner.Kernel.LastNode;
            int count = _owner.Kernel.NodeCount;

            foreach (double x in input)
            {
                _buffer.Add(_math.FromInput(x));
                _inputs++;

                while ((_position >> 32) + last <= _inputs - 1)
                {
                    long index = _position >> 32;
                    T mu = _math.Fraction((ulong)_position & 0xFFFFFFFFUL);
                    int start = (int)(index + first - _dropped);
                    ReadOnlySpan<T> nodes = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_buffer).Slice(start, count);
                    output.Add(_math.ToOutput(_kernel.Evaluate(nodes, mu)));
                    _position += _owner.Increment;
                    _remainder += _owner.IncrementRemainder;
                    if (_remainder >= _owner.Denominator)
                    {
                        _remainder -= _owner.Denominator;
                        _position++;
                    }
                }

                Compact(first);
            }
        }

        public void Flush(List<double> output)
        {
        }

        public void Reset()
        {
            _buffer.Clear();
            _dropped = 0;
            _inputs = 0;
            long den = _owner.Denominator;
            Int128 start = (Int128)Math.Round(_owner.StartPosition * den) << 32;
            _position = (long)(start / den);
            _remainder = (long)(start % den);
        }

        private void Compact(int first)
        {
            long keepFrom = (_position >> 32) + first;
            long excess = keepFrom - _dropped;
            if (excess > 4096)
            {
                _buffer.RemoveRange(0, (int)excess);
                _dropped += excess;
            }
        }
    }
}
