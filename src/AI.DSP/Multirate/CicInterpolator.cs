using System;
using System.Collections.Generic;
using System.Numerics;

namespace AI.DSP.Multirate;

/// <summary>
/// ИГФ повышения частоты по Хогенауэру: K гребенок на входной частоте, вставка R − 1 нулей, K интеграторов
/// на выходной частоте. Эквивалентен K-кратной скользящей сумме длины R после вставки нулей (2.2), (2.3).
/// </summary>
/// <remarks>
/// Нормировка на R^(K−1): единичный коэффициент передачи с учётом вставки нулей. Первые <see cref="Skip"/>
/// выходов отбрасываются; ⌊K(R − 1)/2⌋ компенсирует задержку фильтра. В double фильтр считается каскадом
/// скользящих сумм после вставки нулей: интеграторы в double теряли бы точность.
/// </remarks>
public sealed class CicInterpolator : MultirateBlock
{
    /// <summary>
    /// Создаёт интерполятор.
    /// </summary>
    /// <param name="factor">R — коэффициент интерполяции.</param>
    /// <param name="stages">K — число каскадов.</param>
    /// <param name="skip">Число отбрасываемых начальных выходов; null — ⌊K(R − 1)/2⌋.</param>
    /// <param name="normalize">Нормировать ли на R^(K−1).</param>
    /// <param name="arithmetic">Арифметика.</param>
    /// <param name="options">Правила Q16.</param>
    public CicInterpolator(int factor, int stages, int? skip = null, bool normalize = true,
        Arithmetic arithmetic = Arithmetic.Double, Q16Options options = null)
        : base(arithmetic, options)
    {
        CicDesign.Validate(factor, stages);
        Factor = factor;
        Stages = stages;
        Skip = skip ?? (stages * (factor - 1) / 2);
        if (Skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), "Число отбрасываемых отсчётов не может быть отрицательным.");
        }

        Gain = normalize ? BigInteger.Pow(factor, stages - 1) : BigInteger.One;
    }

    /// <summary>R — коэффициент интерполяции.</summary>
    public int Factor { get; }

    /// <summary>K — число каскадов.</summary>
    public int Stages { get; }

    /// <summary>Число отбрасываемых начальных выходов.</summary>
    public int Skip { get; }

    /// <summary>Делитель нормировки (R^(K−1) или 1).</summary>
    public BigInteger Gain { get; }

    /// <inheritdoc/>
    public override double RateFactor => Factor;

    /// <inheritdoc/>
    public override double Delay => (Stages * (Factor - 1) / 2.0) - Skip;

    /// <inheritdoc/>
    /// <remarks>Q16 (Хогенауэр): K + K/R сложений; double (скользящие суммы): 2K сложений; плюс нормировка.</remarks>
    public override OperationCount OperationsPerOutput
        => new OperationCount(Arithmetic == Arithmetic.Q16 ? Stages + (Stages / (double)Factor) : 2 * Stages, 0, 0, 0)
           + CicDesign.NormalizationCost(Gain, Arithmetic);

    private protected override IBlockCore CreateCore<T>(ISampleArithmetic<T> math) => new Core<T>(this, math);

    private sealed class Core<T> : IBlockCore
        where T : struct
    {
        private readonly ISampleArithmetic<T> _math;
        private readonly CicInterpolator _owner;
        private readonly T[] _integrators;
        private readonly T[] _combs;
        private readonly MovingSumCascade<T> _sums;
        private long _outputs;

        public Core(CicInterpolator owner, ISampleArithmetic<T> math)
        {
            _owner = owner;
            _math = math;
            _integrators = new T[owner.Stages];
            _combs = new T[owner.Stages];
            _sums = owner.Arithmetic == Arithmetic.Double ? new MovingSumCascade<T>(math, owner.Factor, owner.Stages) : null;
        }

        public void Process(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (double x in input)
            {
                if (_sums != null)
                {
                    for (int i = 0; i < _owner.Factor; i++)
                    {
                        T sum = _sums.Push(i == 0 ? _math.FromInput(x) : default);
                        if (_outputs++ >= _owner.Skip)
                        {
                            output.Add(_math.ToOutput(_math.Normalize(sum, _owner.Gain)));
                        }
                    }

                    continue;
                }

                T c = _math.FromInput(x);
                for (int k = 0; k < _combs.Length; k++)
                {
                    T y = _math.Subtract(c, _combs[k]);
                    _combs[k] = c;
                    c = y;
                }

                for (int i = 0; i < _owner.Factor; i++)
                {
                    T acc = i == 0 ? c : default;
                    for (int k = 0; k < _integrators.Length; k++)
                    {
                        _integrators[k] = _math.Add(_integrators[k], acc);
                        acc = _integrators[k];
                    }

                    if (_outputs++ >= _owner.Skip)
                    {
                        output.Add(_math.ToOutput(_math.Normalize(acc, _owner.Gain)));
                    }
                }
            }
        }

        public void Flush(List<double> output)
        {
        }

        public void Reset()
        {
            Array.Clear(_integrators);
            Array.Clear(_combs);
            _sums?.Reset();
            _outputs = 0;
        }
    }
}
