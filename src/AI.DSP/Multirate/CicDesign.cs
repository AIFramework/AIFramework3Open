using System;
using System.Numerics;
using AI.DataStructs.Algebraic;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.DSP.Multirate;

/// <summary>
/// Расчёт ИГФ: АЧХ (2.3), корректирующий фильтр (2.5)–(2.8), правило минимального числа каскадов (2.26)–(2.27)
/// и наибольшее число каскадов по спаду в полосе (2.16).
/// </summary>
public static class CicDesign
{
    /// <summary>
    /// Нормированная АЧХ одного каскада ИГФ длины N: |sin(ωN/2) / (N sin(ω/2))|, (2.3).
    /// </summary>
    /// <param name="omega">Частота, рад/отсчёт.</param>
    /// <param name="length">N — длина скользящей суммы.</param>
    public static double StageAmplitude(double omega, int length)
    {
        double w = Math.Abs(omega) < 1e-12 ? 1e-12 : omega;
        return Math.Abs(Math.Sin(w * length / 2) / (length * Math.Sin(w / 2)));
    }

    /// <summary>
    /// Корректирующий (при понижении) или предыскажающий (при повышении) КИХ-фильтр по (2.5)–(2.8):
    /// A_кор(ω) = A_обр(ω/R)·A_фнч(ω), H = обратное преобразование Фурье на сетке, окно Кайзера.
    /// </summary>
    /// <param name="length">N — длина ИГФ.</param>
    /// <param name="stages">K — число каскадов ИГФ.</param>
    /// <param name="factor">R — во сколько раз частота корректора ниже частоты ИГФ.</param>
    /// <param name="taps">Число коэффициентов O (нечётное, как в экспериментах главы 3).</param>
    /// <param name="band">Граница полосы ω₀ в долях частоты дискретизации корректора (0…0,5).</param>
    /// <param name="beta">Параметр окна Кайзера.</param>
    /// <param name="gridSize">Число точек частотной сетки.</param>
    public static Vector CorrectionFilter(int length, int stages, int factor, int taps, double band, double beta = 5.0,
        int gridSize = 1 << 15)
    {
        if (taps < 1 || taps % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(taps), "Число коэффициентов корректора должно быть нечётным.");
        }

        if (!(band > 0 && band < 0.5))
        {
            throw new ArgumentOutOfRangeException(nameof(band), "Граница полосы должна лежать в (0; 0,5).");
        }

        // Действительная АЧХ симметрична: обратное ДПФ сводится к сумме косинусов по положительным частотам
        int half = gridSize / 2;
        var a = new double[half];
        for (int k = 0; k < half; k++)
        {
            double w = 2 * Math.PI * k / gridSize;
            a[k] = w <= 2 * Math.PI * band ? Math.Pow(StageAmplitude(w / factor, length), -stages) : 0.0;
        }

        Vector window = KaiserFir.Window(taps, beta);
        var h = new double[taps];
        for (int i = 0; i < taps; i++)
        {
            int n = i - (taps / 2);
            double sum = a[0];
            for (int k = 1; k < half && a[k] != 0; k++)
            {
                sum += 2 * a[k] * Math.Cos(2 * Math.PI * k * n / gridSize);
            }

            h[i] = sum / gridSize * window[i];
        }

        return new Vector(h);
    }

    /// <summary>
    /// Ослабление наложений одним каскадом на краю полосы, (2.26):
    /// A₁(b) = −20 lg |sin(π(1 − b)) / (k sin(π(1 − b)/k))|.
    /// </summary>
    /// <param name="factor">k — коэффициент изменения частоты (N = k).</param>
    /// <param name="bandRatio">b = B / F_низ — полоса сигнала, отнесённая к меньшей частоте ступени.</param>
    public static double StageAliasAttenuation(int factor, double bandRatio)
    {
        double x = Math.PI * (1 - bandRatio);
        return -20 * Math.Log10(Math.Abs(Math.Sin(x) / (factor * Math.Sin(x / factor))));
    }

    /// <summary>
    /// Минимальное число каскадов ИГФ по допустимому уровню наложений, правило 1, (2.27): K = ⌈A_нал / A₁(b)⌉.
    /// </summary>
    /// <param name="factor">k — коэффициент изменения частоты.</param>
    /// <param name="bandRatio">b = B / F_низ.</param>
    /// <param name="aliasAttenuationDb">Требуемое ослабление наложений A_нал, дБ.</param>
    public static int MinimumStages(int factor, double bandRatio, double aliasAttenuationDb)
        => (int)Math.Ceiling(aliasAttenuationDb / StageAliasAttenuation(factor, bandRatio));

    /// <summary>
    /// Наибольшее число каскадов по допустимому спаду АЧХ на границе полосы, (2.16).
    /// </summary>
    /// <param name="length">N — длина ИГФ.</param>
    /// <param name="bandEdge">ω_b — граница полосы сигнала, рад/отсчёт на частоте ИГФ.</param>
    /// <param name="droopDb">Допустимый спад δ_пр, дБ.</param>
    public static int MaximumStages(int length, double bandEdge, double droopDb)
        => (int)Math.Floor(droopDb / (-20 * Math.Log10(StageAmplitude(bandEdge, length))));

    /// <summary>
    /// Проверка параметров ИГФ: 64-битного накопителя должно хватать на рост K·log₂R разрядов.
    /// </summary>
    internal static void Validate(int factor, int stages)
    {
        if (factor < 1 || stages < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "R и K должны быть положительными.");
        }

        if (stages * Math.Log2(factor) > 46)
        {
            throw new ArgumentOutOfRangeException(nameof(stages),
                $"Рост разрядности K·log₂R = {stages * Math.Log2(factor):F1} бит не помещается в 64-битный накопитель вместе с Q16.");
        }
    }

    /// <summary>
    /// Затраты нормировки на один выходной отсчёт: сдвиг для степени двойки, иначе деление.
    /// </summary>
    internal static OperationCount NormalizationCost(BigInteger gain, Arithmetic arithmetic)
    {
        if (gain.IsOne)
        {
            return default;
        }

        return arithmetic == Arithmetic.Q16 && gain.IsPowerOfTwo ? new(0, 0, 1, 0) : new(0, 0, 0, 1);
    }
}
