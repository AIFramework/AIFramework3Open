using System;
using System.Collections.Generic;
using System.Numerics;

namespace AI.DSP.Multirate;

/// <summary>
/// Интегрально-гребенчатый фильтр (ИГФ, CIC) понижения частоты по Хогенауэру: K интеграторов на входной частоте,
/// прореживание в R раз, K гребенок на выходной частоте; передаточная функция (2.3) при N = R.
/// </summary>
/// <remarks>
/// В Q16 вычисляется в 64-битных целых по модулю 2^64 и поэтому точно, без ошибок округления (п. 2.4):
/// переполнение интеграторов компенсируется гребенками. Нормировка на R^K — сдвигом, если R^K является степенью
/// двойки, иначе целочисленным делением с округлением вниз. В double тот же фильтр считается каскадом
/// скользящих сумм (1.1): интеграторы в double растут как n^K и теряли бы точность. Выходной отсчёт m соответствует входному отсчёту
/// offset + mR; смещение offset = ⌊K(R − 1)/2⌋ компенсирует задержку фильтра (как в экспериментах главы 3),
/// offset = R − 1 даёт классический дециматор Хогенауэра.
/// </remarks>
public sealed class CicDecimator : MultirateBlock
{
    /// <summary>
    /// Создаёт дециматор.
    /// </summary>
    /// <param name="factor">R — коэффициент прореживания и длина скользящей суммы.</param>
    /// <param name="stages">K — число каскадов.</param>
    /// <param name="offset">Номер входного отсчёта первого выхода; null — ⌊K(R − 1)/2⌋.</param>
    /// <param name="normalize">Нормировать ли на R^K (единичный коэффициент передачи на нулевой частоте).</param>
    /// <param name="arithmetic">Арифметика.</param>
    /// <param name="options">Правила Q16.</param>
    public CicDecimator(int factor, int stages, int? offset = null, bool normalize = true,
        Arithmetic arithmetic = Arithmetic.Double, Q16Options options = null)
        : base(arithmetic, options)
    {
        CicDesign.Validate(factor, stages);
        Factor = factor;
        Stages = stages;
        Offset = offset ?? (stages * (factor - 1) / 2);
        if (Offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Смещение не может быть отрицательным.");
        }

        Gain = normalize ? BigInteger.Pow(factor, stages) : BigInteger.One;
    }

    /// <summary>R — коэффициент прореживания.</summary>
    public int Factor { get; }

    /// <summary>K — число каскадов.</summary>
    public int Stages { get; }

    /// <summary>Номер входного отсчёта, которому соответствует первый выход.</summary>
    public int Offset { get; }

    /// <summary>Делитель нормировки (R^K или 1).</summary>
    public BigInteger Gain { get; }

    /// <inheritdoc/>
    public override double RateFactor => 1.0 / Factor;

    /// <inheritdoc/>
    public override double Delay => ((Stages * (Factor - 1) / 2.0) - Offset) / Factor;

    /// <inheritdoc/>
    /// <remarks>Q16 (Хогенауэр): K(R + 1) сложений; double (скользящие суммы): 2KR сложений; плюс нормировка.</remarks>
    public override OperationCount OperationsPerOutput
        => new OperationCount(Arithmetic == Arithmetic.Q16 ? Stages * (Factor + 1) : 2 * Stages * Factor, 0, 0, 0)
           + CicDesign.NormalizationCost(Gain, Arithmetic);

    private protected override IBlockCore CreateCore<T>(ISampleArithmetic<T> math) => new Core<T>(this, math);

    private sealed class Core<T> : IBlockCore
        where T : struct
    {
        private readonly ISampleArithmetic<T> _math;
        private readonly CicDecimator _owner;
        private readonly T[] _integrators;
        private readonly T[] _combs;
        private readonly MovingSumCascade<T> _sums;
        private long _inputs;

        public Core(CicDecimator owner, ISampleArithmetic<T> math)
        {
            _owner = owner;
            _math = math;
            _integrators = new T[owner.Stages];
            _combs = new T[owner.Stages];
            _sums = owner.Arithmetic == Arithmetic.Double ? new MovingSumCascade<T>(math, owner.Factor, owner.Stages) : null;
        }

        public void Process(ReadOnlySpan<double> input, List<double> output)
        {
            int r = _owner.Factor;
            int phase = _owner.Offset % r;
            long skip = _owner.Offset / r;

            foreach (double x in input)
            {
                if (_sums != null)
                {
                    T sum = _sums.Push(_math.FromInput(x));
                    long i = _inputs++;
                    if (i % r == phase && i / r >= skip)
                    {
                        output.Add(_math.ToOutput(_math.Normalize(sum, _owner.Gain)));
                    }

                    continue;
                }

                T acc = _math.FromInput(x);
                for (int k = 0; k < _integrators.Length; k++)
                {
                    _integrators[k] = _math.Add(_integrators[k], acc);
                    acc = _integrators[k];
                }

                long n = _inputs++;
                if (n % r != phase)
                {
                    continue;
                }

                // Гребенки работают и до первого выдаваемого отсчёта: их состояние — значения в моменты n − R
                for (int k = 0; k < _combs.Length; k++)
                {
                    T y = _math.Subtract(acc, _combs[k]);
                    _combs[k] = acc;
                    acc = y;
                }

                if (n / r >= skip)
                {
                    output.Add(_math.ToOutput(_math.Normalize(acc, _owner.Gain)));
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
            _inputs = 0;
        }
    }
}
