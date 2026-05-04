using AI.DataStructs.Algebraic;
using AI.HighLevelFunctions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AI.Statistics;

public partial class Statistic
{
    #region Uniform

    /// <summary>Вектор длины n, U(0,1), поток-локальный RNG.</summary>
    public static Vector UniformDistribution(int n)
        => FillVector(n, RandomEngine.Shared, gaussian: false);

    /// <summary>Вектор длины n, U(0,1), заданный RNG (для воспроизводимости).</summary>
    public static Vector UniformDistribution(int n, Random random)
        => FillVector(n, random, gaussian: false);

    /// <summary>Матрица m×n, U(0,1).</summary>
    public static Matrix UniformDistribution(int m, int n)
        => FillMatrix(m, n, RandomEngine.Shared, gaussian: false);

    /// <summary>Матрица m×n, U(0,1), заданный RNG.</summary>
    public static Matrix UniformDistribution(int m, int n, Random random)
        => FillMatrix(m, n, random, gaussian: false);

    /// <summary>Квадратная матрица n×n, U(0,1).</summary>
    public static Matrix UniformDistribution(short n)
        => FillMatrix(n, n, RandomEngine.Shared, gaussian: false);

    /// <summary>Тензор h×w×d, U(0,1).</summary>
    public static Tensor UniformDistribution(int h, int w, int d)
        => FillTensor(h, w, d, RandomEngine.Shared, gaussian: false);

    /// <summary>Тензор h×w×d, U(0,1), заданный RNG.</summary>
    public static Tensor UniformDistribution(int h, int w, int d, Random random)
        => FillTensor(h, w, d, random, gaussian: false);

    #endregion

    #region Gaussian N(0,1)

    /// <summary>
    /// Одно стандартное нормальное значение (полярный Box-Muller).
    /// </summary>
    public static double Gauss(Random rng) => RandomEngine.NextGaussian(rng);

    /// <summary>
    /// Нормальная величина как сумма iter равномерных, корректно
    /// нормированная до N(0, 1). Полезно как «бедный» ГПСЧ с
    /// контролируемой гладкостью (CLT).
    /// </summary>
    /// <param name="rng">Источник случайности</param>
    /// <param name="iter">Число слагаемых (≥ 2)</param>
    public static double Gauss2(Random rng, int iter)
    {
        if (iter < 2) iter = 2;
        double sum = 0.0;

        // сумма iter величин U(-1,1) имеет дисперсию iter/3,
        // поэтому нормируем делением на sqrt(iter/3).
        for (int i = 0; i < iter; i++)
            sum += 2.0 * (rng.NextDouble() - 0.5);

        return sum / Math.Sqrt(iter / 3.0);
    }

    /// <summary>Вектор длины n, N(0, 1).</summary>
    public static Vector RandNorm(int n)
        => FillVector(n, RandomEngine.Shared, gaussian: true);

    /// <summary>Вектор длины n, N(0, 1), заданный RNG.</summary>
    public static Vector RandNorm(int n, Random rng)
        => FillVector(n, rng, gaussian: true);

    /// <summary>Матрица m×n, N(0, 1).</summary>
    public static Matrix RandNorm(int m, int n)
        => FillMatrix(m, n, RandomEngine.Shared, gaussian: true);

    /// <summary>Матрица m×n, N(0, 1), заданный RNG.</summary>
    public static Matrix RandNorm(int m, int n, Random rng)
        => FillMatrix(m, n, rng, gaussian: true);

    /// <summary>
    /// Квадратная матрица n×n, N(0, 1). (Старая версия по ошибке
    /// возвращала U(0,1) — исправлено.)
    /// </summary>
    public static Matrix RandNorm(short n)
        => FillMatrix(n, n, RandomEngine.Shared, gaussian: true);

    /// <summary>Тензор h×w×d, N(0, 1).</summary>
    public static Tensor RandNorm(int h, int w, int d)
        => FillTensor(h, w, d, RandomEngine.Shared, gaussian: true);

    /// <summary>Тензор h×w×d, N(0, 1), заданный RNG.</summary>
    public static Tensor RandNorm(int h, int w, int d, Random random)
        => FillTensor(h, w, d, random, gaussian: true);

    /// <summary>Один N(0, 1) от поток-локального RNG.</summary>
    public double RandNorm() => RandomEngine.NextGaussian();

    /// <summary>
    /// Вектор длины n, N(0, 1), построенный через CLT (сумма iter
    /// U(0,1)-величин). Семантика: i-й элемент — независимая
    /// реализация N(0, 1) приближённо по CLT.
    /// </summary>
    public static Vector RandNormP(int n, int iter = 100)
        => RandNormP(n, RandomEngine.Shared, iter);

    /// <summary>
    /// Вектор длины n, N(0, 1), построенный через CLT на заданном RNG.
    /// Исправлена логика: теперь возвращает именно n независимых
    /// нормальных величин (раньше возвращалась усреднённая одна
    /// реализация, что нарушало контракт).
    /// </summary>
    public static Vector RandNormP(int n, Random rng, int iter = 100)
    {
        if (iter < 2) iter = 2;
        Vector v = new Vector(n);
        double norm = Math.Sqrt(iter / 3.0);

        for (int i = 0; i < n; i++)
        {
            double s = 0.0;
            for (int j = 0; j < iter; j++)
                s += 2.0 * (rng.NextDouble() - 0.5);
            v[i] = s / norm;
        }
        return v;
    }

    #endregion
}
