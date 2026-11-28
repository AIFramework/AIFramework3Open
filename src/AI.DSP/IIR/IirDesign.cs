using System;
using System.Linq;
using AI.DataStructs.Algebraic;

namespace AI.DSP.IIR;

/// <summary>
/// Дизайн БИХ-фильтров одним путём: ТЗ → минимальный порядок → аналоговый прототип (нули, полюсы, усиление) →
/// преобразование частот (ФНЧ, ФВЧ, ПФ, РФ) → билинейное преобразование с предыскажением → звенья второго порядка.
/// Результаты совпадают со scipy.signal (butter, cheby1, cheby2, ellip, bessel с output='sos').
/// </summary>
public static class IirDesign
{
    /// <summary>
    /// Фильтр минимального порядка по ТЗ.
    /// </summary>
    /// <param name="family">Тип прототипа.</param>
    /// <param name="specification">ТЗ.</param>
    public static IirFilterDesign Design(IirFamily family, IirSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        IirOrderEstimate estimate = IirOrder.Estimate(family, specification);
        return Design(family, specification.BandType, estimate.Order, specification.SampleRate, estimate.Cutoff,
            specification.PassbandRippleDb, specification.StopbandAttenuationDb,
            family == IirFamily.Bessel ? BesselNormalization.Magnitude : BesselNormalization.Phase);
    }

    /// <summary>
    /// Фильтр заданного порядка с заданной частотой (частотами) среза.
    /// </summary>
    /// <param name="family">Тип прототипа.</param>
    /// <param name="bandType">Вид полосы.</param>
    /// <param name="order">Порядок прототипа (у ПФ и РФ итоговый порядок вдвое больше).</param>
    /// <param name="sampleRate">Частота дискретизации, Гц.</param>
    /// <param name="cutoff">Частота среза (ФНЧ, ФВЧ) или две частоты (ПФ, РФ), Гц. Для Чебышёва II — граница полосы задерживания.</param>
    /// <param name="rippleDb">Неравномерность в полосе пропускания (Чебышёв I, эллиптический), дБ.</param>
    /// <param name="attenuationDb">Затухание в полосе задерживания (Чебышёв II, эллиптический), дБ.</param>
    /// <param name="normalization">Нормировка частоты фильтра Бесселя.</param>
    public static IirFilterDesign Design(IirFamily family, IirBandType bandType, int order, double sampleRate, Vector cutoff,
        double rippleDb = 1.0, double attenuationDb = 40.0, BesselNormalization normalization = BesselNormalization.Phase)
    {
        ArgumentNullException.ThrowIfNull(cutoff);
        Validate(bandType, sampleRate, cutoff);

        ZeroPoleGain prototype = family switch
        {
            IirFamily.Butterworth => AnalogPrototype.Butterworth(order),
            IirFamily.ChebyshevI => AnalogPrototype.ChebyshevI(order, rippleDb),
            IirFamily.ChebyshevII => AnalogPrototype.ChebyshevII(order, attenuationDb),
            IirFamily.Elliptic => AnalogPrototype.Elliptic(order, rippleDb, attenuationDb),
            _ => AnalogPrototype.Bessel(order, normalization),
        };

        double[] warped = cutoff.Select(f => FrequencyTransform.Prewarp(f, sampleRate)).ToArray();
        ZeroPoleGain analog = bandType switch
        {
            IirBandType.LowPass => FrequencyTransform.LowPassToLowPass(prototype, warped[0]),
            IirBandType.HighPass => FrequencyTransform.LowPassToHighPass(prototype, warped[0]),
            IirBandType.BandPass => FrequencyTransform.LowPassToBandPass(prototype, Math.Sqrt(warped[0] * warped[1]), warped[1] - warped[0]),
            _ => FrequencyTransform.LowPassToBandStop(prototype, Math.Sqrt(warped[0] * warped[1]), warped[1] - warped[0]),
        };

        ZeroPoleGain digital = FrequencyTransform.Bilinear(analog, 2.0);
        return new IirFilterDesign(family, bandType, order, sampleRate, new Vector(cutoff), digital, SecondOrderSections.FromZeroPoleGain(digital));
    }

    private static void Validate(IirBandType bandType, double sampleRate, Vector cutoff)
    {
        if (!(sampleRate > 0))
        {
            throw new ArgumentException($"Частота дискретизации должна быть положительной, дано {sampleRate}.");
        }

        int need = bandType is IirBandType.LowPass or IirBandType.HighPass ? 1 : 2;
        if (cutoff.Count != need)
        {
            throw new ArgumentException($"Для {bandType} нужно {need} частоты среза, дано {cutoff.Count}.");
        }

        if (cutoff.Any(f => !(f > 0 && f < sampleRate / 2)))
        {
            throw new ArgumentException($"Частоты среза должны лежать в (0; {sampleRate / 2}) Гц.");
        }

        if (need == 2 && !(cutoff[0] < cutoff[1]))
        {
            throw new ArgumentException("Нижняя частота полосы должна быть меньше верхней.");
        }
    }
}
