using System;
using AI.DataStructs.Algebraic;

namespace AI.DSP.Multirate;

/// <summary>
/// Интерполяционный полином Лагранжа степени p в структуре Фэрроу: p + 1 КИХ-фильтров с постоянными
/// коэффициентами дают v_k, значение — по схеме Горнера (2.20): y = ((v₃μ + v₂)μ + v₁)μ + v₀.
/// Узлы: p + 1 отсчётов вокруг интервала [0; 1), для p = 3 — x[−1], x[0], x[1], x[2] (таблица 2.1).
/// </summary>
public sealed class LagrangeKernel : InterpolationKernel
{
    // Постоянные модели точности (2.28) по эксперименту главы 3: C₁, C₃, C₅, дБ
    private static readonly double[] AccuracyConstants = { double.NaN, -4.0, double.NaN, -20.9, double.NaN, -38.6 };

    private readonly double[,] _c;

    /// <summary>
    /// Создаёт ядро Лагранжа.
    /// </summary>
    /// <param name="order">Степень p (по умолчанию 3 — кубический фильтр Фэрроу).</param>
    public LagrangeKernel(int order = 3)
    {
        if (order < 1 || order > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Степень полинома должна быть от 1 до 9.");
        }

        Order = order;
        FirstNode = -((order - 1) / 2);
        LastNode = FirstNode + order;
        _c = BuildMatrix(order, FirstNode);
    }

    /// <summary>Степень полинома p.</summary>
    public int Order { get; }

    /// <inheritdoc/>
    public override int FirstNode { get; }

    /// <inheritdoc/>
    public override int LastNode { get; }

    /// <summary>
    /// Матрица Фэрроу C[k, j]: коэффициент при μ^k для узла j (таблица 2.1 при p = 3).
    /// </summary>
    public Matrix Coefficients
    {
        get
        {
            var m = new Matrix(Order + 1, Order + 1);
            for (int k = 0; k <= Order; k++)
            {
                for (int j = 0; j <= Order; j++)
                {
                    m[k, j] = _c[k, j];
                }
            }

            return m;
        }
    }

    /// <summary>
    /// Затраты: коэффициенты 0 и ±1 умножений не требуют; для p = 3 — 12 умножений и 11 сложений (п. 2.3).
    /// </summary>
    public override OperationCount Operations(Arithmetic arithmetic, Q16Options options)
    {
        int mults = Order;
        int adds = Order;
        int products = 0;
        for (int k = 0; k <= Order; k++)
        {
            int nonZero = 0;
            for (int j = 0; j <= Order; j++)
            {
                if (Math.Abs(_c[k, j]) > 1e-12)
                {
                    nonZero++;
                    if (Math.Abs(Math.Abs(_c[k, j]) - 1) > 1e-12)
                    {
                        mults++;
                    }

                    products++;
                }
            }

            adds += Math.Max(0, nonZero - 1);
        }

        if (arithmetic == Arithmetic.Double)
        {
            return new(adds, mults, 0, 0);
        }

        options ??= Q16Options.Recommended;
        double shifts = options.WideAccumulator ? (2 * Order) + 1 : mults;
        return new(adds, mults, shifts, 0);
    }

    /// <summary>
    /// Наибольшая допустимая относительная частота сигнала f/F_вх для требуемого ОСШ, правило 2, (2.28)–(2.29).
    /// Постоянные модели известны для p = 1, 3, 5.
    /// </summary>
    /// <param name="order">Степень полинома (1, 3 или 5).</param>
    /// <param name="requiredSnrDb">Требуемое ОСШ, дБ.</param>
    public static double MaxRelativeFrequency(int order, double requiredSnrDb)
    {
        if (order is not (1 or 3 or 5))
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Модель (2.28) получена для p = 1, 3, 5.");
        }

        return Math.Pow(10, -(requiredSnrDb - AccuracyConstants[order]) / (20.0 * (order + 1)));
    }

    internal override IKernelEvaluator<T> Bind<T>(ISampleArithmetic<T> math) => new Evaluator<T>(this, math);

    private static double[,] BuildMatrix(int order, int first)
    {
        var c = new double[order + 1, order + 1];
        for (int j = 0; j <= order; j++)
        {
            // Базисный полином узла j: Π (μ − x_m)/(x_j − x_m), коэффициенты по возрастанию степени
            var poly = new double[order + 1];
            poly[0] = 1;
            int degree = 0;
            for (int m = 0; m <= order; m++)
            {
                if (m == j)
                {
                    continue;
                }

                double xm = first + m;
                double scale = 1.0 / (j - m);
                degree++;
                for (int d = degree; d >= 0; d--)
                {
                    double shifted = d > 0 ? poly[d - 1] : 0;
                    poly[d] = (shifted - (xm * poly[d])) * scale;
                }
            }

            for (int k = 0; k <= order; k++)
            {
                c[k, j] = poly[k];
            }
        }

        return c;
    }

    private sealed class Evaluator<T> : IKernelEvaluator<T>
        where T : struct
    {
        private readonly ISampleArithmetic<T> _math;
        private readonly int _order;
        private readonly T[,] _c;
        private readonly bool[,] _used;

        public Evaluator(LagrangeKernel kernel, ISampleArithmetic<T> math)
        {
            _math = math;
            _order = kernel.Order;
            _c = new T[_order + 1, _order + 1];
            _used = new bool[_order + 1, _order + 1];
            for (int k = 0; k <= _order; k++)
            {
                for (int j = 0; j <= _order; j++)
                {
                    _c[k, j] = math.Coefficient(kernel._c[k, j]);
                    _used[k, j] = !_c[k, j].Equals(default(T));
                }
            }
        }

        public T Evaluate(ReadOnlySpan<T> nodes, T mu)
        {
            T y = default;
            for (int k = _order; k >= 0; k--)
            {
                T acc = default;
                for (int j = 0; j <= _order; j++)
                {
                    if (_used[k, j])
                    {
                        acc = _math.Accumulate(acc, nodes[j], _c[k, j]);
                    }
                }

                T v = _math.Finish(acc);
                y = k == _order ? v : _math.Add(_math.Multiply(y, mu), v);
            }

            return y;
        }
    }
}
