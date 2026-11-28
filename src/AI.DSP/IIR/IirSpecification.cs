using System;
using System.Linq;
using AI.DataStructs.Algebraic;

namespace AI.DSP.IIR;

/// <summary>
/// Техническое задание на БИХ-фильтр: вид полосы, частота дискретизации, границы полос пропускания и
/// задерживания (Гц), неравномерность в полосе пропускания и затухание в полосе задерживания (дБ).
/// </summary>
public sealed class IirSpecification
{
    /// <summary>
    /// Создаёт ТЗ и проверяет его согласованность.
    /// </summary>
    /// <param name="bandType">Вид полосы.</param>
    /// <param name="sampleRate">Частота дискретизации, Гц.</param>
    /// <param name="passbandEdges">Граница (ФНЧ, ФВЧ) или границы (ПФ, РФ) полосы пропускания, Гц.</param>
    /// <param name="stopbandEdges">Граница или границы полосы задерживания, Гц.</param>
    /// <param name="passbandRippleDb">Наибольшее ослабление в полосе пропускания, дБ (&gt; 0).</param>
    /// <param name="stopbandAttenuationDb">Наименьшее затухание в полосе задерживания, дБ.</param>
    /// <exception cref="ArgumentException">ТЗ противоречиво; сообщение называет нарушенное условие.</exception>
    public IirSpecification(IirBandType bandType, double sampleRate, Vector passbandEdges, Vector stopbandEdges,
        double passbandRippleDb, double stopbandAttenuationDb)
    {
        ArgumentNullException.ThrowIfNull(passbandEdges);
        ArgumentNullException.ThrowIfNull(stopbandEdges);
        BandType = bandType;
        SampleRate = sampleRate;
        PassbandEdges = new Vector(passbandEdges);
        StopbandEdges = new Vector(stopbandEdges);
        PassbandRippleDb = passbandRippleDb;
        StopbandAttenuationDb = stopbandAttenuationDb;
        Validate();
    }

    /// <summary>Вид полосы.</summary>
    public IirBandType BandType { get; }

    /// <summary>Частота дискретизации, Гц.</summary>
    public double SampleRate { get; }

    /// <summary>Границы полосы пропускания, Гц.</summary>
    public Vector PassbandEdges { get; }

    /// <summary>Границы полосы задерживания, Гц.</summary>
    public Vector StopbandEdges { get; }

    /// <summary>Наибольшее ослабление в полосе пропускания, дБ.</summary>
    public double PassbandRippleDb { get; }

    /// <summary>Наименьшее затухание в полосе задерживания, дБ.</summary>
    public double StopbandAttenuationDb { get; }

    /// <summary>ФНЧ: пропускание до passEdge, задерживание от stopEdge.</summary>
    public static IirSpecification LowPass(double sampleRate, double passEdge, double stopEdge, double rippleDb, double attenuationDb)
        => new(IirBandType.LowPass, sampleRate, new Vector(passEdge), new Vector(stopEdge), rippleDb, attenuationDb);

    /// <summary>ФВЧ: задерживание до stopEdge, пропускание от passEdge.</summary>
    public static IirSpecification HighPass(double sampleRate, double passEdge, double stopEdge, double rippleDb, double attenuationDb)
        => new(IirBandType.HighPass, sampleRate, new Vector(passEdge), new Vector(stopEdge), rippleDb, attenuationDb);

    /// <summary>Полосовой: пропускание [passLow; passHigh], задерживание до stopLow и от stopHigh.</summary>
    public static IirSpecification BandPass(double sampleRate, double passLow, double passHigh, double stopLow, double stopHigh,
        double rippleDb, double attenuationDb)
        => new(IirBandType.BandPass, sampleRate, new Vector(passLow, passHigh), new Vector(stopLow, stopHigh), rippleDb, attenuationDb);

    /// <summary>Режекторный: задерживание [stopLow; stopHigh], пропускание до passLow и от passHigh.</summary>
    public static IirSpecification BandStop(double sampleRate, double passLow, double passHigh, double stopLow, double stopHigh,
        double rippleDb, double attenuationDb)
        => new(IirBandType.BandStop, sampleRate, new Vector(passLow, passHigh), new Vector(stopLow, stopHigh), rippleDb, attenuationDb);

    /// <summary>
    /// ТЗ по линейным уровням: АЧХ в полосе пропускания не ниже 1 − δ₁, в полосе задерживания не выше δ₂.
    /// </summary>
    public static IirSpecification FromLinear(IirBandType bandType, double sampleRate, Vector passbandEdges, Vector stopbandEdges,
        double passbandDeviation, double stopbandLevel)
        => new(bandType, sampleRate, passbandEdges, stopbandEdges, RippleFromLinear(passbandDeviation), AttenuationFromLinear(stopbandLevel));

    /// <summary>Неравномерность в дБ по линейному отклонению: −20 lg(1 − δ₁).</summary>
    public static double RippleFromLinear(double passbandDeviation)
        => passbandDeviation is > 0 and < 1
            ? -20 * Math.Log10(1 - passbandDeviation)
            : throw new ArgumentOutOfRangeException(nameof(passbandDeviation), "Отклонение δ₁ должно лежать в (0; 1).");

    /// <summary>Затухание в дБ по линейному уровню: −20 lg δ₂.</summary>
    public static double AttenuationFromLinear(double stopbandLevel)
        => stopbandLevel is > 0 and < 1
            ? -20 * Math.Log10(stopbandLevel)
            : throw new ArgumentOutOfRangeException(nameof(stopbandLevel), "Уровень δ₂ должен лежать в (0; 1).");

    private void Validate()
    {
        if (!(SampleRate > 0))
        {
            throw new ArgumentException($"Частота дискретизации должна быть положительной, дано {SampleRate}.");
        }

        int edges = BandType is IirBandType.LowPass or IirBandType.HighPass ? 1 : 2;
        if (PassbandEdges.Count != edges || StopbandEdges.Count != edges)
        {
            throw new ArgumentException($"Для {BandType} нужно по {edges} границе(ы) полос пропускания и задерживания.");
        }

        double nyquist = SampleRate / 2;
        if (PassbandEdges.Concat(StopbandEdges).Any(f => !(f > 0 && f < nyquist)))
        {
            throw new ArgumentException($"Границы полос должны лежать в (0; {nyquist}) Гц — между нулём и половиной частоты дискретизации.");
        }

        double wp = PassbandEdges[0];
        double ws = StopbandEdges[0];
        bool ordered = BandType switch
        {
            IirBandType.LowPass => wp < ws,
            IirBandType.HighPass => ws < wp,
            IirBandType.BandPass => ws < wp && wp < PassbandEdges[1] && PassbandEdges[1] < StopbandEdges[1],
            _ => wp < ws && ws < StopbandEdges[1] && StopbandEdges[1] < PassbandEdges[1],
        };

        if (!ordered)
        {
            throw new ArgumentException(BandType switch
            {
                IirBandType.LowPass => $"ФНЧ: граница пропускания {wp} Гц должна быть ниже границы задерживания {ws} Гц.",
                IirBandType.HighPass => $"ФВЧ: граница задерживания {ws} Гц должна быть ниже границы пропускания {wp} Гц.",
                IirBandType.BandPass => "Полосовой: нужно задерживание < пропускание (низ) < пропускание (верх) < задерживание.",
                _ => "Режекторный: нужно пропускание (низ) < задерживание (низ) < задерживание (верх) < пропускание (верх).",
            });
        }

        if (!(PassbandRippleDb > 0))
        {
            throw new ArgumentException($"Неравномерность в полосе пропускания должна быть положительной, дано {PassbandRippleDb} дБ.");
        }

        if (!(StopbandAttenuationDb > PassbandRippleDb))
        {
            throw new ArgumentException(
                $"Затухание в полосе задерживания ({StopbandAttenuationDb} дБ) должно превышать неравномерность в полосе пропускания ({PassbandRippleDb} дБ).");
        }
    }
}
