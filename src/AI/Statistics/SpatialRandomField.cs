using System;
using System.Collections.Generic;

namespace AI.Statistics;

/// <summary>
/// Стационарное гауссово случайное поле на плоскости с экспоненциальной корреляцией exp(−r/d)
/// </summary>
/// <remarks>
/// <para>
/// Поле строится спектральным методом — суммой M гармоник со случайными волновыми векторами и фазами:
/// <c>g(x) = √(2/M)·Σ cos(kᵢ·x + bᵢ)</c>, bᵢ ~ U(0, 2π). По теореме Бохнера корреляция такой суммы, усреднённая
/// по реализациям, равна характеристической функции распределения волновых векторов, поэтому kᵢ берутся из
/// спектральной плотности экспоненциальной корреляции. На плоскости это S(k) = d²/(2π·(1 + d²k²)^(3/2)):
/// модуль |k| распределён с функцией распределения 1 − 1/√(1 + d²k²) и разыгрывается обращением
/// <c>|k| = √(1/(1 − u)² − 1)/d</c>, направление равномерно. Тогда E[g(x)·g(y)] = exp(−|x − y|/d) точно,
/// дисперсия равна единице, а значение в точке — сумма M независимых слагаемых — при большом M распределено
/// почти нормально.
/// </para>
/// <para>
/// Поле вычисляется в любой точке без сетки: O(M) операций на точку и O(M) памяти. Так строят карты
/// затенения и крупномасштабных параметров радиоканала, по которым движутся абоненты (TR 38.901, раздел 7.6.3),
/// и вообще любые пространственно коррелированные случайные величины.
/// </para>
/// </remarks>
public sealed class SpatialRandomField
{
    private readonly double[] _kx;
    private readonly double[] _ky;
    private readonly double[] _phase;
    private readonly double _scale;

    /// <summary>Разыгрывает реализацию поля</summary>
    /// <param name="correlationDistance">Расстояние, на котором корреляция спадает в e раз</param>
    /// <param name="rng">Генератор случайных чисел</param>
    /// <param name="harmonics">Число гармоник M: чем больше, тем ближе распределение к нормальному</param>
    public SpatialRandomField(double correlationDistance, Random rng, int harmonics = 512)
    {
        if (!(correlationDistance > 0) || double.IsInfinity(correlationDistance))
            throw new ArgumentOutOfRangeException(nameof(correlationDistance), correlationDistance, "Расстояние корреляции должно быть конечным и положительным");

        ArgumentNullException.ThrowIfNull(rng);

        if (harmonics < 1)
            throw new ArgumentOutOfRangeException(nameof(harmonics), harmonics, "Нужна хотя бы одна гармоника");

        CorrelationDistance = correlationDistance;
        _kx = new double[harmonics];
        _ky = new double[harmonics];
        _phase = new double[harmonics];

        for (int i = 0; i < harmonics; i++)
        {
            double tail = 1 - rng.NextDouble();
            double k = Math.Sqrt((1 / (tail * tail)) - 1) / correlationDistance;
            double direction = 2 * Math.PI * rng.NextDouble();

            _kx[i] = k * Math.Cos(direction);
            _ky[i] = k * Math.Sin(direction);
            _phase[i] = 2 * Math.PI * rng.NextDouble();
        }

        _scale = Math.Sqrt(2.0 / harmonics);
    }

    /// <summary>Расстояние корреляции</summary>
    public double CorrelationDistance { get; }

    /// <summary>Число гармоник</summary>
    public int Harmonics => _phase.Length;

    /// <summary>Значение поля в точке: среднее 0, дисперсия 1</summary>
    /// <param name="x">Первая координата</param>
    /// <param name="y">Вторая координата</param>
    public double Value(double x, double y)
    {
        double sum = 0;

        for (int i = 0; i < _phase.Length; i++)
            sum += Math.Cos((_kx[i] * x) + (_ky[i] * y) + _phase[i]);

        return _scale * sum;
    }

    /// <summary>
    /// Значения поля в узлах прямоугольной сетки: элемент [i, j] — точка (xs[j], ys[i])
    /// </summary>
    /// <remarks>
    /// Гармоника разделяется по осям, cos(kₓx + k_y·y + b) = cos(kₓx + b)·cos(k_y·y) − sin(kₓx + b)·sin(k_y·y), поэтому
    /// тригонометрия считается O(M·(n + m)) раз, а не O(M·n·m), как при вызове <see cref="Value"/> в каждом узле.
    /// Результат совпадает с поточечным до округления.
    /// </remarks>
    /// <param name="xs">Первые координаты столбцов</param>
    /// <param name="ys">Вторые координаты строк</param>
    public double[,] Sample(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        ArgumentNullException.ThrowIfNull(xs);
        ArgumentNullException.ThrowIfNull(ys);

        int columns = xs.Count, rows = ys.Count;
        var result = new double[rows, columns];
        var cosX = new double[columns];
        var sinX = new double[columns];
        var cosY = new double[rows];
        var sinY = new double[rows];

        for (int i = 0; i < _phase.Length; i++)
        {
            for (int c = 0; c < columns; c++)
            {
                double a = (_kx[i] * xs[c]) + _phase[i];
                cosX[c] = Math.Cos(a);
                sinX[c] = Math.Sin(a);
            }

            for (int r = 0; r < rows; r++)
            {
                double b = _ky[i] * ys[r];
                cosY[r] = Math.Cos(b);
                sinY[r] = Math.Sin(b);
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                    result[r, c] += (cosX[c] * cosY[r]) - (sinX[c] * sinY[r]);
            }
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
                result[r, c] *= _scale;
        }

        return result;
    }

    /// <summary>Корреляция значений поля в двух точках на расстоянии r: exp(−r/d)</summary>
    /// <param name="distance">Расстояние между точками</param>
    /// <param name="correlationDistance">Расстояние корреляции</param>
    public static double Correlation(double distance, double correlationDistance)
        => Math.Exp(-Math.Abs(distance) / correlationDistance);
}
