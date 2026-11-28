using System;
using System.Collections.Generic;
using AI.DataStructs.Algebraic;

namespace AI.DSP.Multirate;

/// <summary>
/// Передискретизация сигнала движущегося источника по известной полиномиальной траектории (п. 2.6.2):
/// отсчёт, излучённый в момент nT, восстанавливается из принятой последовательности в точке
/// τ_n = n + R(nT)/(cT), (2.32). Для траектории степени не выше 3 τ_n — кубический полином от n, и он
/// вычисляется рекуррентно конечными разностями (2.35) тремя сложениями целых с F дробными битами;
/// каждые B отсчётов разности заново привязываются к точному значению. Интерполяция — ядром фильтра Фэрроу.
/// </summary>
public sealed class MovingSourceResampler : MultirateBlock
{
    private readonly decimal[] _r;

    /// <summary>
    /// Создаёт передискретизатор.
    /// </summary>
    /// <param name="trajectory">Коэффициенты R(e) = r₀ + r₁e + r₂e² + r₃e³ (м, м/с, м/с², м/с³), (2.34).</param>
    /// <param name="propagationSpeed">Скорость распространения c, м/с.</param>
    /// <param name="sampleRate">Частота дискретизации 1/T, Гц.</param>
    /// <param name="fractionBits">F — дробные биты рекуррентного вычислителя (16…60).</param>
    /// <param name="rebindPeriod">B — период привязки к точному значению; 0 — без привязки.</param>
    /// <param name="kernel">Ядро интерполяции; null — кубический Лагранж.</param>
    /// <param name="arithmetic">Арифметика.</param>
    /// <param name="options">Правила Q16.</param>
    public MovingSourceResampler(Vector trajectory, double propagationSpeed, double sampleRate, int fractionBits = 32,
        int rebindPeriod = 256, InterpolationKernel kernel = null, Arithmetic arithmetic = Arithmetic.Double,
        Q16Options options = null)
        : base(arithmetic, options)
    {
        ArgumentNullException.ThrowIfNull(trajectory);
        if (trajectory.Count is < 1 or > 4)
        {
            throw new ArgumentException("Траектория задаётся полиномом степени от 0 до 3.", nameof(trajectory));
        }

        if (fractionBits is < 16 or > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(fractionBits), "F должно быть от 16 до 60.");
        }

        if (!(propagationSpeed > 0) || !(sampleRate > 0) || rebindPeriod < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(propagationSpeed), "c и Fs положительны, B неотрицательно.");
        }

        _r = new decimal[4];
        for (int i = 0; i < trajectory.Count; i++)
        {
            _r[i] = (decimal)trajectory[i];
        }

        PropagationSpeed = propagationSpeed;
        SampleRate = sampleRate;
        FractionBits = fractionBits;
        RebindPeriod = rebindPeriod;
        Kernel = kernel ?? new LagrangeKernel();
    }

    /// <summary>Скорость распространения, м/с.</summary>
    public double PropagationSpeed { get; }

    /// <summary>Частота дискретизации, Гц.</summary>
    public double SampleRate { get; }

    /// <summary>F — дробные биты вычислителя положения.</summary>
    public int FractionBits { get; }

    /// <summary>B — период привязки (0 — без привязки).</summary>
    public int RebindPeriod { get; }

    /// <summary>Ядро интерполяции.</summary>
    public InterpolationKernel Kernel { get; }

    /// <inheritdoc/>
    public override double RateFactor => 1.0 / (1.0 + ((double)_r[1] / PropagationSpeed));

    /// <inheritdoc/>
    public override double Delay => 0;

    /// <summary>
    /// Затраты на выходной отсчёт: 3 сложения и сдвиг вычислителя положения, ядро, 3 умножения привязки раз в B отсчётов.
    /// </summary>
    public override OperationCount OperationsPerOutput
        => Kernel.Operations(Arithmetic, Options) + new OperationCount(3, RebindPeriod > 0 ? 3.0 / RebindPeriod : 0, 1, 0);

    /// <summary>
    /// Точное положение τ_n = n + R(nT)/(cT), (2.32), в индексах принятой последовательности.
    /// </summary>
    public double Position(long n) => (double)ExactPosition(n);

    private protected override IBlockCore CreateCore<T>(ISampleArithmetic<T> math) => new Core<T>(this, math);

    private decimal ExactPosition(long n)
    {
        decimal fs = (decimal)SampleRate;
        decimal e = n / fs;
        decimal range = _r[0] + (e * (_r[1] + (e * (_r[2] + (e * _r[3])))));
        return n + (range * fs / (decimal)PropagationSpeed);
    }

    private sealed class Core<T> : IBlockCore
        where T : struct
    {
        private readonly MovingSourceResampler _owner;
        private readonly ISampleArithmetic<T> _math;
        private readonly IKernelEvaluator<T> _kernel;
        private readonly List<T> _buffer = new();
        private Int128 _tau;
        private Int128 _d1;
        private Int128 _d2;
        private Int128 _d3;
        private long _n;
        private long _inputs;

        public Core(MovingSourceResampler owner, ISampleArithmetic<T> math)
        {
            _owner = owner;
            _math = math;
            _kernel = owner.Kernel.Bind(math);
            Reset();
        }

        public void Process(ReadOnlySpan<double> input, List<double> output)
        {
            int f = _owner.FractionBits;
            Int128 mask = (Int128.One << f) - 1;
            foreach (double x in input)
            {
                _buffer.Add(_math.FromInput(x));
                _inputs++;

                while ((long)(_tau >> f) + _owner.Kernel.LastNode <= _inputs - 1)
                {
                    long index = (long)(_tau >> f);
                    ulong frac32 = (ulong)(((_tau & mask) << 32) >> f);
                    int start = (int)index + _owner.Kernel.FirstNode;
                    if (start < 0)
                    {
                        throw new InvalidOperationException("Положение отсчёта раньше начала принятого сигнала: нужна начальная задержка R₀/c.");
                    }

                    ReadOnlySpan<T> nodes = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_buffer).Slice(start, _owner.Kernel.NodeCount);
                    output.Add(_math.ToOutput(_kernel.Evaluate(nodes, _math.Fraction(frac32))));
                    Advance();
                }
            }
        }

        public void Flush(List<double> output)
        {
        }

        public void Reset()
        {
            _buffer.Clear();
            _inputs = 0;
            _n = 0;
            Rebind();
        }

        private void Advance()
        {
            // (2.35): три сложения на отсчёт
            _tau += _d1;
            _d1 += _d2;
            _d2 += _d3;
            _n++;
            if (_owner.RebindPeriod > 0 && _n % _owner.RebindPeriod == 0)
            {
                Rebind();
            }
        }

        private void Rebind()
        {
            // Точное τ и его конечные разности в точке n (схема Горнера, раз в B отсчётов)
            decimal one = 1m;
            for (int i = 0; i < _owner.FractionBits; i++)
            {
                one *= 2;
            }

            decimal t0 = _owner.ExactPosition(_n);
            decimal t1 = _owner.ExactPosition(_n + 1);
            decimal t2 = _owner.ExactPosition(_n + 2);
            decimal t3 = _owner.ExactPosition(_n + 3);
            _tau = Round(t0 * one);
            _d1 = Round((t1 - t0) * one);
            _d2 = Round((t2 - (2 * t1) + t0) * one);
            _d3 = Round((t3 - (3 * t2) + (3 * t1) - t0) * one);
        }

        private static Int128 Round(decimal v) => (Int128)decimal.Round(v, MidpointRounding.ToEven);
    }
}
