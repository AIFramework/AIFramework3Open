using System;
using System.Collections.Generic;
using AI.DataStructs.Algebraic;

namespace AI.DSP.Multirate;

/// <summary>
/// Полифазный КИХ-фильтр преобразования частоты в P/Q раз (повышение, понижение, рациональный коэффициент;
/// при P = Q = 1 — обычный КИХ-фильтр). Эквивалентен вставке P − 1 нулей (2.2), фильтрации на частоте P·F
/// и прореживанию в Q раз (2.1), но вычисляет только нужные выходные отсчёты и не умножает на нули.
/// </summary>
/// <remarks>
/// Выход m: t = mQ + D, y[m] = Σ_j h[t mod P + jP]·x[⌊t/P⌋ − j]. Смещение D (в отсчётах частоты P·F)
/// компенсирует задержку фильтра; D = (L − 1)/2 даёт центрированный фильтр без задержки.
/// Ветвь r = t mod P берёт отсчёты от нового к старому — ошибка обратного порядка ветвей класса
/// PolyphaseFilter из ноутбука автора (таблица 3.7) здесь не повторяется.
/// </remarks>
public sealed class PolyphaseResampler : MultirateBlock
{
    private readonly double[] _h;

    /// <summary>
    /// Создаёт полифазный фильтр.
    /// </summary>
    /// <param name="interpolation">P — коэффициент повышения.</param>
    /// <param name="decimation">Q — коэффициент понижения.</param>
    /// <param name="coefficients">Коэффициенты КИХ-фильтра на частоте P·F (с усилением P для интерполятора).</param>
    /// <param name="offset">D, отсчёты частоты P·F; null — (L − 1)/2 (компенсация задержки).</param>
    /// <param name="arithmetic">Арифметика.</param>
    /// <param name="options">Правила Q16.</param>
    public PolyphaseResampler(int interpolation, int decimation, Vector coefficients, int? offset = null,
        Arithmetic arithmetic = Arithmetic.Double, Q16Options options = null)
        : base(arithmetic, options)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        if (interpolation < 1 || decimation < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(interpolation), "P и Q должны быть положительными.");
        }

        if (coefficients.Count == 0)
        {
            throw new ArgumentException("Нужен хотя бы один коэффициент.", nameof(coefficients));
        }

        Interpolation = interpolation;
        Decimation = decimation;
        Length = coefficients.Count;
        Offset = offset ?? ((Length - 1) / 2);
        if (Offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Смещение не может быть отрицательным.");
        }

        Taps = (Length + interpolation - 1) / interpolation;
        _h = new double[Taps * interpolation];
        coefficients.ToArray().CopyTo(_h, 0);
    }

    /// <summary>P — коэффициент повышения.</summary>
    public int Interpolation { get; }

    /// <summary>Q — коэффициент понижения.</summary>
    public int Decimation { get; }

    /// <summary>L — длина фильтра.</summary>
    public int Length { get; }

    /// <summary>D — смещение (компенсированная часть задержки), отсчёты частоты P·F.</summary>
    public int Offset { get; }

    /// <summary>J = ⌈L/P⌉ — число отводов одной ветви.</summary>
    public int Taps { get; }

    /// <inheritdoc/>
    public override double RateFactor => Interpolation / (double)Decimation;

    /// <inheritdoc/>
    public override double Delay => (((Length - 1) / 2.0) - Offset) / Decimation;

    /// <inheritdoc/>
    public override OperationCount OperationsPerOutput => Arithmetic == Arithmetic.Double
        ? new(Taps - 1, Taps, 0, 0)
        : Options.WideAccumulator ? new(Taps, Taps, 1, 0) : new(Taps - 1, Taps, Taps, 0);

    private protected override IBlockCore CreateCore<T>(ISampleArithmetic<T> math) => new Core<T>(this, math);

    private sealed class Core<T> : IBlockCore
        where T : struct
    {
        private readonly ISampleArithmetic<T> _math;
        private readonly T[] _h;
        private readonly int _p;
        private readonly int _q;
        private readonly int _j;
        private readonly long _d;
        private readonly T[] _history;    // каждый отсчёт записан дважды: окно из J отсчётов всегда непрерывно
        private int _write;
        private long _inputs;
        private long _next;

        public Core(PolyphaseResampler owner, ISampleArithmetic<T> math)
        {
            _math = math;
            _p = owner.Interpolation;
            _q = owner.Decimation;
            _j = owner.Taps;
            _d = owner.Offset;
            _h = Array.ConvertAll(owner._h, math.Coefficient);
            _history = new T[2 * _j];
        }

        public void Process(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (double x in input)
            {
                Push(_math.FromInput(x), output, long.MaxValue);
            }
        }

        public void Flush(List<double> output)
        {
            // Всего выходов столько же, сколько у вставки нулей с прореживанием: ⌊N·P/Q⌋
            long total = _inputs * _p / _q;
            while (_next < total)
            {
                Push(default, output, total);
            }
        }

        public void Reset()
        {
            Array.Clear(_history);
            _write = 0;
            _inputs = 0;
            _next = 0;
        }

        private void Push(T x, List<double> output, long limit)
        {
            _history[_write] = x;
            _history[_write + _j] = x;
            int newest = _write + _j;
            _write = _write + 1 < _j ? _write + 1 : 0;
            _inputs++;

            long t = (_next * _q) + _d;
            while (t / _p == _inputs - 1 && _next < limit)
            {
                int r = (int)(t % _p);
                int count = (int)Math.Min(_j, _inputs);
                T acc = default;
                for (int j = 0; j < count; j++)
                {
                    acc = _math.Accumulate(acc, _history[newest - j], _h[r + (j * _p)]);
                }

                output.Add(_math.ToOutput(_math.Finish(acc)));
                _next++;
                t += _q;
            }
        }
    }
}
