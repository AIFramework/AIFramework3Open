using System;
using System.Collections.Generic;
using System.Numerics;
using AI.DataStructs.Algebraic;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.DSP.Multirate;

/// <summary>
/// Построение схем преобразования частоты дискретизации главы 3: эталонная «стандартная схема» и
/// синтезированная схема рис. 3.1 (ИГФ, корректор, фильтр Фэрроу).
/// </summary>
public static class ResamplerDesign
{
    /// <summary>
    /// Стандартная схема (п. 3.1.2): вставка нулей в P раз, КИХ-ФНЧ с окном Кайзера на частоте P·F_S, прореживание
    /// в Q раз — в полифазной реализации. Переходная полоса — от границы полосы сигнала до половины меньшей частоты.
    /// </summary>
    /// <param name="sourceRate">F_S, Гц.</param>
    /// <param name="targetRate">F_T, Гц.</param>
    /// <param name="band">Граница полосы сигнала B, Гц.</param>
    /// <param name="attenuationDb">Затухание в полосе задерживания, дБ (по умолчанию 80).</param>
    /// <param name="arithmetic">Арифметика.</param>
    /// <param name="options">Правила Q16.</param>
    public static PolyphaseResampler Standard(int sourceRate, int targetRate, double band, double attenuationDb = 80,
        Arithmetic arithmetic = Arithmetic.Double, Q16Options options = null)
    {
        ValidateRates(sourceRate, targetRate, band);
        int g = (int)BigInteger.GreatestCommonDivisor(sourceRate, targetRate);
        int p = targetRate / g;
        int q = sourceRate / g;
        double upRate = (double)p * sourceRate;
        double edge = Math.Min(sourceRate, targetRate) / 2.0;
        int length = KaiserFir.Length(attenuationDb, (edge - band) / upRate);
        Vector h = KaiserFir.LowPass(length, (band + edge) / 2 / (upRate / 2), KaiserFir.Beta(attenuationDb), p);
        return new PolyphaseResampler(p, q, h, null, arithmetic, options);
    }

    /// <summary>
    /// Синтезированная схема рис. 3.1. При понижении: k_D = ⌊F_S/F_T⌋ (3.1), ИГФ из K каскадов длины k_D и
    /// корректирующий фильтр (2.5)–(2.8); при повышении: k_U = ⌈F_T/F_S⌉ (3.2), предыскажающий фильтр и ИГФ.
    /// Оставшееся преобразование, близкое к 1, выполняет фильтр Фэрроу. Задержки ИГФ и фильтров компенсируются.
    /// </summary>
    /// <param name="sourceRate">F_S, Гц.</param>
    /// <param name="targetRate">F_T, Гц.</param>
    /// <param name="band">Граница полосы сигнала B, Гц.</param>
    /// <param name="stages">K — число каскадов ИГФ.</param>
    /// <param name="correctorTaps">O — число коэффициентов корректора (нечётное).</param>
    /// <param name="beta">Параметр окна Кайзера корректора.</param>
    /// <param name="kernel">Ядро фильтра Фэрроу; null — кубический Лагранж.</param>
    /// <param name="arithmetic">Арифметика.</param>
    /// <param name="options">Правила Q16.</param>
    public static MultirateChain CicCorrectorFarrow(int sourceRate, int targetRate, double band, int stages = 4,
        int correctorTaps = 31, double beta = 5.0, InterpolationKernel kernel = null,
        Arithmetic arithmetic = Arithmetic.Double, Q16Options options = null)
    {
        ValidateRates(sourceRate, targetRate, band);
        kernel ??= new LagrangeKernel();
        var blocks = new List<IMultirateBlock>();
        double fractionalDelay;
        long stepNumerator;     // шаг Фэрроу F_сред / F_T как дробь
        long stepDenominator;

        if (sourceRate >= targetRate)
        {
            int k = sourceRate / targetRate;
            double middleRate = sourceRate / (double)k;
            (stepNumerator, stepDenominator) = (sourceRate, (long)targetRate * k);
            fractionalDelay = 0;
            if (k > 1)
            {
                // Остаток задержки ИГФ в полотсчёта уходит в начальное положение фильтра Фэрроу
                fractionalDelay = stages * (k - 1) % 2 / (2.0 * k);
                Vector h = CicDesign.CorrectionFilter(k, stages, k, correctorTaps, (band + (targetRate / 2.0)) / 2 / middleRate, beta);
                blocks.Add(new CicDecimator(k, stages, null, true, arithmetic, options));
                blocks.Add(new PolyphaseResampler(1, 1, h, null, arithmetic, options));
            }
        }
        else
        {
            int k = (targetRate + sourceRate - 1) / sourceRate;
            (stepNumerator, stepDenominator) = ((long)sourceRate * k, targetRate);
            fractionalDelay = stages * (k - 1) % 2 / 2.0;
            Vector h = CicDesign.CorrectionFilter(k, stages, k, correctorTaps, (band + (sourceRate / 2.0)) / 2 / sourceRate, beta);
            blocks.Add(new PolyphaseResampler(1, 1, h, null, arithmetic, options));
            blocks.Add(new CicInterpolator(k, stages, null, true, arithmetic, options));
        }

        if (stepNumerator != stepDenominator)
        {
            double step = stepNumerator / (double)stepDenominator;
            double start = FarrowResampler.FirstPosition(step, fractionalDelay, kernel);
            blocks.Add(new FarrowResampler(stepNumerator, stepDenominator, kernel, start, arithmetic, options));
        }

        return new MultirateChain(blocks.ToArray());
    }

    private static void ValidateRates(int sourceRate, int targetRate, double band)
    {
        if (sourceRate <= 0 || targetRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRate), "Частоты дискретизации должны быть положительными.");
        }

        if (!(band > 0 && band < Math.Min(sourceRate, targetRate) / 2.0))
        {
            throw new ArgumentOutOfRangeException(nameof(band), $"Полоса сигнала {band} Гц должна быть меньше половины меньшей частоты.");
        }
    }
}
