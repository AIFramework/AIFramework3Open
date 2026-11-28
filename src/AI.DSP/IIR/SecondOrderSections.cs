using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.DSP.Multirate;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.DSP.IIR;

/// <summary>
/// Цифровой БИХ-фильтр как каскад звеньев второго порядка (SOS): строка [b₀ b₁ b₂ 1 a₁ a₂] на звено.
/// Фильтрация, нулевая фаза, частотные характеристики, нули и полюсы, устойчивость.
/// </summary>
public sealed class SecondOrderSections
{
    private readonly double[,] _sos;

    /// <summary>
    /// Создаёт каскад по матрице n×6 (a₀ каждого звена приводится к 1).
    /// </summary>
    public SecondOrderSections(Matrix coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        if (coefficients.Width != 6 || coefficients.Height < 1)
        {
            throw new ArgumentException("Матрица звеньев должна иметь размер n×6.", nameof(coefficients));
        }

        _sos = new double[coefficients.Height, 6];
        for (int i = 0; i < coefficients.Height; i++)
        {
            double a0 = coefficients[i, 3];
            if (a0 == 0)
            {
                throw new ArgumentException($"В звене {i} коэффициент a₀ равен нулю.", nameof(coefficients));
            }

            for (int j = 0; j < 6; j++)
            {
                _sos[i, j] = coefficients[i, j] / a0;
            }
        }
    }

    internal SecondOrderSections(double[,] sos) => _sos = sos;

    /// <summary>Число звеньев.</summary>
    public int SectionCount => _sos.GetLength(0);

    /// <summary>Коэффициенты звеньев, матрица n×6.</summary>
    public Matrix Coefficients
    {
        get
        {
            var m = new Matrix(SectionCount, 6);
            for (int i = 0; i < SectionCount; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    m[i, j] = _sos[i, j];
                }
            }

            return m;
        }
    }

    /// <summary>Нули всех звеньев.</summary>
    public ComplexVector Zeros => new(Roots(0));

    /// <summary>Полюсы всех звеньев.</summary>
    public ComplexVector Poles => new(Roots(3));

    /// <summary>Устойчивость: все полюсы строго внутри единичной окружности.</summary>
    public bool IsStable => Roots(3).All(p => p.Magnitude < 1);

    internal double[,] Raw => _sos;

    /// <summary>
    /// Разбивает цифровой фильтр из нулей и полюсов на звенья (алгоритм zpk2sos, pairing = 'nearest').
    /// </summary>
    public static SecondOrderSections FromZeroPoleGain(ZeroPoleGain digital)
    {
        ArgumentNullException.ThrowIfNull(digital);
        return new SecondOrderSections(ZpkToSos.Convert(digital.ZeroArray, digital.PoleArray, digital.Gain));
    }

    /// <summary>
    /// Фильтрация с нулевыми начальными условиями (как scipy.signal.sosfilt), прямая форма II транспонированная.
    /// </summary>
    public Vector Filter(Vector signal) => CreateFilter().ProcessBlock(signal);

    /// <summary>
    /// Фильтрация с нулевой фазой: вперёд и назад, нечётное продолжение краёв на 3(2n + 1) отсчётов и начальные
    /// состояния установившегося режима (как scipy.signal.sosfiltfilt).
    /// </summary>
    public Vector FilterZeroPhase(Vector signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        double[] x = signal.ToArray();
        int n = SectionCount;
        int zeroB2 = Enumerable.Range(0, n).Count(i => _sos[i, 2] == 0);
        int zeroA2 = Enumerable.Range(0, n).Count(i => _sos[i, 5] == 0);
        int pad = 3 * ((2 * n) + 1 - Math.Min(zeroB2, zeroA2));
        if (x.Length <= pad)
        {
            throw new ArgumentException($"Сигнал должен быть длиннее {pad} отсчётов (продолжение краёв).", nameof(signal));
        }

        var ext = new double[x.Length + (2 * pad)];
        for (int i = 0; i < pad; i++)
        {
            ext[i] = (2 * x[0]) - x[pad - i];
            ext[pad + x.Length + i] = (2 * x[^1]) - x[x.Length - 2 - i];
        }

        Array.Copy(x, 0, ext, pad, x.Length);
        double[,] zi = SteadyStateInitial();
        double[] y = Run(ext, Scale(zi, ext[0]));
        Array.Reverse(y);
        y = Run(y, Scale(zi, y[0]));
        Array.Reverse(y);
        return new Vector(y.Skip(pad).Take(x.Length));
    }

    /// <summary>
    /// Потоковый фильтр (блок многоскоростной обработки с коэффициентом 1) в double или Q16.
    /// </summary>
    public SosFilter CreateFilter(Arithmetic arithmetic = Arithmetic.Double, Q16Options options = null) => new(this, arithmetic, options);

    /// <summary>
    /// Комплексная частотная характеристика на частоте f.
    /// </summary>
    /// <param name="frequency">Частота, Гц.</param>
    /// <param name="sampleRate">Частота дискретизации, Гц.</param>
    public Complex Response(double frequency, double sampleRate)
    {
        Complex zi = Complex.Exp(-Complex.ImaginaryOne * 2 * Math.PI * frequency / sampleRate);
        Complex h = Complex.One;
        for (int i = 0; i < SectionCount; i++)
        {
            Complex num = _sos[i, 0] + (zi * (_sos[i, 1] + (zi * _sos[i, 2])));
            Complex den = _sos[i, 3] + (zi * (_sos[i, 4] + (zi * _sos[i, 5])));
            h *= num / den;
        }

        return h;
    }

    /// <summary>Комплексная частотная характеристика на сетке частот.</summary>
    public ComplexVector FrequencyResponse(Vector frequencies, double sampleRate)
        => new(frequencies.Select(f => Response(f, sampleRate)));

    /// <summary>АЧХ в дБ: 20 lg |H|.</summary>
    public Vector MagnitudeDb(Vector frequencies, double sampleRate)
        => new(frequencies.Select(f => 20 * Math.Log10(Response(f, sampleRate).Magnitude)));

    /// <summary>ФЧХ, рад, с устранёнными скачками на 2π.</summary>
    public Vector Phase(Vector frequencies, double sampleRate)
    {
        double[] phase = frequencies.Select(f => Response(f, sampleRate).Phase).ToArray();
        for (int i = 1; i < phase.Length; i++)
        {
            double d = phase[i] - phase[i - 1];
            phase[i] -= 2 * Math.PI * Math.Round(d / (2 * Math.PI));
        }

        return new Vector(phase);
    }

    /// <summary>
    /// Групповая задержка в отсчётах: τ = Re(Σ k c_k e^{−jωk} / Σ c_k e^{−jωk}) числителя минус знаменателя, по звеньям.
    /// </summary>
    public Vector GroupDelay(Vector frequencies, double sampleRate)
        => new(frequencies.Select(f =>
        {
            Complex zi = Complex.Exp(-Complex.ImaginaryOne * 2 * Math.PI * f / sampleRate);
            double tau = 0;
            for (int i = 0; i < SectionCount; i++)
            {
                tau += PolyDelay(_sos[i, 0], _sos[i, 1], _sos[i, 2], zi) - PolyDelay(_sos[i, 3], _sos[i, 4], _sos[i, 5], zi);
            }

            return tau;
        }));

    /// <summary>
    /// Передаточная функция B(z)/A(z) — перемножение звеньев (для совместимости с <see cref="IIRFilter"/>).
    /// </summary>
    /// <returns>(b, a), a₀ = 1.</returns>
    public (Vector B, Vector A) ToTransferFunction()
    {
        double[] b = [1.0];
        double[] a = [1.0];
        for (int i = 0; i < SectionCount; i++)
        {
            b = Convolve(b, [_sos[i, 0], _sos[i, 1], _sos[i, 2]]);
            a = Convolve(a, [_sos[i, 3], _sos[i, 4], _sos[i, 5]]);
        }

        return (new Vector(b), new Vector(a));
    }

    /// <summary>
    /// Прежний фильтр по передаточной функции. Для высоких порядков каскад звеньев устойчивее.
    /// </summary>
    public IIRFilter ToIirFilter()
    {
        (Vector b, Vector a) = ToTransferFunction();
        return new IIRFilter(a, b);
    }

    /// <summary>
    /// Фактическое выполнение ТЗ: наибольшее ослабление в полосе пропускания и наименьшее затухание в полосе
    /// задерживания на равномерной сетке частот (с границами полос).
    /// </summary>
    /// <param name="specification">ТЗ.</param>
    /// <param name="gridPoints">Число точек сетки на полосу.</param>
    public IirSpecificationReport Verify(IirSpecification specification, int gridPoints = 2048)
    {
        ArgumentNullException.ThrowIfNull(specification);
        double fs = specification.SampleRate;
        double nyq = fs / 2;
        double[] wp = specification.PassbandEdges.ToArray();
        double[] ws = specification.StopbandEdges.ToArray();
        (double Lo, double Hi)[] pass = specification.BandType switch
        {
            IirBandType.LowPass => [(0, wp[0])],
            IirBandType.HighPass => [(wp[0], nyq)],
            IirBandType.BandPass => [(wp[0], wp[1])],
            _ => [(0, wp[0]), (wp[1], nyq)],
        };
        (double Lo, double Hi)[] stop = specification.BandType switch
        {
            IirBandType.LowPass => [(ws[0], nyq)],
            IirBandType.HighPass => [(0, ws[0])],
            IirBandType.BandPass => [(0, ws[0]), (ws[1], nyq)],
            _ => [(ws[0], ws[1])],
        };

        double peak = Grid([(0, nyq)], gridPoints * 4).Max(f => Response(f, fs).Magnitude);
        double passMin = pass.SelectMany(b => Grid([b], gridPoints)).Min(f => Response(f, fs).Magnitude);
        double stopMax = stop.SelectMany(b => Grid([b], gridPoints)).Max(f => Response(f, fs).Magnitude);
        double ripple = 20 * Math.Log10(peak / passMin);
        double attenuation = -20 * Math.Log10(stopMax / peak);
        return new IirSpecificationReport(ripple, attenuation, ripple <= specification.PassbandRippleDb * (1 + 1e-6) + 1e-9,
            attenuation >= specification.StopbandAttenuationDb * (1 - 1e-6));
    }

    internal double[] Run(double[] x, double[,] zi)
    {
        int n = SectionCount;
        var y = new double[x.Length];
        for (int t = 0; t < x.Length; t++)
        {
            double cur = x[t];
            for (int s = 0; s < n; s++)
            {
                double next = (_sos[s, 0] * cur) + zi[s, 0];
                zi[s, 0] = (_sos[s, 1] * cur) - (_sos[s, 4] * next) + zi[s, 1];
                zi[s, 1] = (_sos[s, 2] * cur) - (_sos[s, 5] * next);
                cur = next;
            }

            y[t] = cur;
        }

        return y;
    }

    private static IEnumerable<double> Grid((double Lo, double Hi)[] bands, int points)
        => bands.SelectMany(b => Enumerable.Range(0, points + 1).Select(i => b.Lo + ((b.Hi - b.Lo) * i / points)));

    private static double PolyDelay(double c0, double c1, double c2, Complex zi)
    {
        Complex p = c0 + (zi * (c1 + (zi * c2)));
        Complex dp = zi * (c1 + (2 * zi * c2));
        return p == Complex.Zero ? 0 : (dp / p).Real;
    }

    private static double[] Convolve(double[] a, double[] b)
    {
        var r = new double[a.Length + b.Length - 1];
        for (int i = 0; i < a.Length; i++)
        {
            for (int j = 0; j < b.Length; j++)
            {
                r[i + j] += a[i] * b[j];
            }
        }

        return r;
    }

    private static double[,] Scale(double[,] zi, double v)
    {
        var r = (double[,])zi.Clone();
        for (int i = 0; i < r.GetLength(0); i++)
        {
            r[i, 0] *= v;
            r[i, 1] *= v;
        }

        return r;
    }

    /// <summary>Начальные состояния установившегося режима при единичном входе (scipy sosfilt_zi).</summary>
    private double[,] SteadyStateInitial()
    {
        int n = SectionCount;
        var zi = new double[n, 2];
        double scale = 1.0;
        for (int s = 0; s < n; s++)
        {
            double b0 = _sos[s, 0], b1 = _sos[s, 1], b2 = _sos[s, 2];
            double a1 = _sos[s, 4], a2 = _sos[s, 5];

            // (I − Aᵀ)·zi = b[1:] − a[1:]·b₀, матрица [[1 + a₁, −1], [a₂, 1]]
            double r0 = b1 - (a1 * b0);
            double r1 = b2 - (a2 * b0);
            double det = 1 + a1 + a2;
            zi[s, 0] = scale * (r0 + r1) / det;
            zi[s, 1] = scale * (((1 + a1) * r1) - (a2 * r0)) / det;
            scale *= (b0 + b1 + b2) / (1 + a1 + a2);
        }

        return zi;
    }

    private Complex[] Roots(int offset)
    {
        var roots = new List<Complex>();
        for (int s = 0; s < SectionCount; s++)
        {
            double c0 = _sos[s, offset], c1 = _sos[s, offset + 1], c2 = _sos[s, offset + 2];
            if (c0 != 0)
            {
                Complex d = Complex.Sqrt((c1 * c1) - (4 * c0 * c2));
                if (c2 != 0)
                {
                    roots.Add((-c1 + d) / (2 * c0));
                    roots.Add((-c1 - d) / (2 * c0));
                }
                else if (c1 != 0)
                {
                    roots.Add(new Complex(-c1 / c0, 0));
                }
            }
            else if (c1 != 0)
            {
                roots.Add(new Complex(-c2 / c1, 0));
            }
        }

        return [.. roots];
    }
}
