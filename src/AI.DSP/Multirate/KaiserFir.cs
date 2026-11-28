using System;
using AI.BackEnds.DSP.NWaves.Utils;
using AI.DataStructs.Algebraic;

namespace AI.DSP.Multirate;

/// <summary>
/// КИХ-фильтры по ТЗ методом окна Кайзера: длина и параметр β по формулам Кайзера, окно (2.8), функция I₀ (2.9).
/// </summary>
public static class KaiserFir
{
    /// <summary>
    /// Модифицированная функция Бесселя первого рода нулевого порядка, ряд (2.9): Σ (x/2)^{2k} / (k!)².
    /// </summary>
    /// <remarks>В исходном ноутбуке автора знаменатель был k!(k+1)! (таблица 3.1); здесь — верный ряд.</remarks>
    /// <param name="x">Аргумент.</param>
    public static double BesselI0(double x)
    {
        double q = x * x / 4;
        double term = 1;
        double sum = 1;

        for (int k = 1; term > sum * 1e-17; k++)
        {
            term *= q / ((double)k * k);
            sum += term;
        }

        return sum;
    }

    /// <summary>
    /// Параметр окна Кайзера по затуханию в полосе задерживания (Kaiser, 1974; при A &gt; 50 дБ — формула (2.18)).
    /// </summary>
    /// <param name="attenuationDb">Затухание A, дБ.</param>
    public static double Beta(double attenuationDb)
    {
        if (attenuationDb > 50)
        {
            return 0.1102 * (attenuationDb - 8.7);
        }

        return attenuationDb >= 21
            ? (0.5842 * Math.Pow(attenuationDb - 21, 0.4)) + (0.07886 * (attenuationDb - 21))
            : 0.0;
    }

    /// <summary>
    /// Длина КИХ-фильтра по Кайзеру, N = ⌈(A − 7,95) / (14,36 Δf)⌉ + 1, дополненная до нечётной.
    /// </summary>
    /// <remarks>
    /// Это форма scipy.signal.kaiserord и экспериментов главы 3; форма (2.18) через √(δ₁δ₂) даёт близкую оценку.
    /// </remarks>
    /// <param name="attenuationDb">Затухание A, дБ.</param>
    /// <param name="transitionWidth">Ширина переходной полосы в долях частоты дискретизации.</param>
    public static int Length(double attenuationDb, double transitionWidth)
    {
        if (transitionWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(transitionWidth), "Ширина переходной полосы должна быть положительной.");
        }

        int n = (int)Math.Ceiling((attenuationDb - 7.95) / (14.36 * transitionWidth)) + 1;
        return n + (1 - (n % 2));
    }

    /// <summary>
    /// Окно Кайзера (2.8) длины n.
    /// </summary>
    public static Vector Window(int length, double beta)
    {
        var w = new double[length];
        double i0Beta = BesselI0(beta);

        for (int i = 0; i < length; i++)
        {
            double r = length == 1 ? 0 : (2.0 * i / (length - 1)) - 1;
            w[i] = BesselI0(beta * Math.Sqrt(Math.Max(0, 1 - (r * r)))) / i0Beta;
        }

        return new Vector(w);
    }

    /// <summary>
    /// ФНЧ методом окна: sinc с окном Кайзера, нормированный на сумму коэффициентов.
    /// </summary>
    /// <param name="length">Число коэффициентов.</param>
    /// <param name="cutoff">Частота среза в долях частоты Найквиста (0…1).</param>
    /// <param name="beta">Параметр окна.</param>
    /// <param name="gain">Коэффициент передачи на нулевой частоте (P для интерполятора).</param>
    public static Vector LowPass(int length, double cutoff, double beta, double gain = 1.0)
    {
        Vector w = Window(length, beta);
        var h = new double[length];
        double sum = 0;

        for (int i = 0; i < length; i++)
        {
            double n = i - ((length - 1) / 2.0);
            h[i] = cutoff * MathUtilsDSP.Sinc(cutoff * n) * w[i];
            sum += h[i];
        }

        for (int i = 0; i < length; i++)
        {
            h[i] = gain * h[i] / sum;
        }

        return new Vector(h);
    }

    /// <summary>
    /// ФНЧ по ТЗ: граница полосы пропускания, граница полосы задерживания, затухание.
    /// Срез — посередине переходной полосы.
    /// </summary>
    /// <param name="sampleRate">Частота дискретизации фильтра, Гц.</param>
    /// <param name="passEdge">Граница полосы пропускания, Гц.</param>
    /// <param name="stopEdge">Граница полосы задерживания, Гц.</param>
    /// <param name="attenuationDb">Затухание в полосе задерживания, дБ.</param>
    /// <param name="gain">Коэффициент передачи на нулевой частоте.</param>
    public static Vector LowPass(double sampleRate, double passEdge, double stopEdge, double attenuationDb, double gain = 1.0)
    {
        if (!(passEdge > 0 && passEdge < stopEdge && stopEdge <= sampleRate / 2))
        {
            throw new ArgumentException($"Нужно 0 < полоса пропускания < полоса задерживания ≤ Fs/2; дано {passEdge}, {stopEdge}, Fs = {sampleRate}.");
        }

        int length = Length(attenuationDb, (stopEdge - passEdge) / sampleRate);
        return LowPass(length, (passEdge + stopEdge) / sampleRate, Beta(attenuationDb), gain);
    }
}
