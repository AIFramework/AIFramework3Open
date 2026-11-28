using System;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.MathUtils.SpecialFunction;

namespace AI.DSP.IIR;

/// <summary>
/// Минимальный порядок БИХ-фильтра по ТЗ и собственные частоты для дизайна (как buttord, cheb1ord, cheb2ord и
/// ellipord из scipy.signal: предыскажение частот, пересчёт к ФНЧ-прототипу, для РФ — уточнение границ полосы
/// пропускания поиском минимума).
/// </summary>
public static class IirOrder
{
    /// <summary>
    /// Минимальный порядок и частоты среза, Гц.
    /// </summary>
    /// <param name="family">Тип прототипа (Бессель — подбором порядка, только ФНЧ и ФВЧ).</param>
    /// <param name="specification">ТЗ.</param>
    public static IirOrderEstimate Estimate(IirFamily family, IirSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (family == IirFamily.Bessel)
        {
            return BesselBySearch(specification);
        }

        double fs = specification.SampleRate;
        double[] wp = specification.PassbandEdges.Select(f => 2 * f / fs).ToArray();
        double[] ws = specification.StopbandEdges.Select(f => 2 * f / fs).ToArray();
        int type = (2 * (wp.Length - 1)) + 1 + (wp[0] >= ws[0] ? 1 : 0);   // 1 ФНЧ, 2 ФВЧ, 3 РФ, 4 ПФ
        double[] passb = wp.Select(w => Math.Tan(Math.PI * w / 2.0)).ToArray();
        double[] stopb = ws.Select(w => Math.Tan(Math.PI * w / 2.0)).ToArray();
        double gpass = specification.PassbandRippleDb;
        double gstop = specification.StopbandAttenuationDb;
        double nat = NaturalFrequency(stopb, passb, gpass, gstop, type, family);
        double gStop = Math.Pow(10, 0.1 * Math.Abs(gstop));
        double gPass = Math.Pow(10, 0.1 * Math.Abs(gpass));
        int order;
        double[] wn;

        switch (family)
        {
            case IirFamily.Butterworth:
            {
                order = (int)Math.Ceiling(Math.Log10((gStop - 1.0) / (gPass - 1.0)) / (2 * Math.Log10(nat)));
                double w0 = Math.Pow(gPass - 1.0, -1.0 / (2.0 * order));
                wn = type switch
                {
                    1 => [w0 * passb[0]],
                    2 => [passb[0] / w0],
                    3 => ButterStop(passb, w0),
                    _ => ButterPass(passb, w0),
                };
                break;
            }

            case IirFamily.ChebyshevI:
                order = (int)Math.Ceiling(Math.Acosh(Math.Sqrt((gStop - 1.0) / (gPass - 1.0))) / Math.Acosh(nat));
                wn = passb;
                break;

            case IirFamily.ChebyshevII:
            {
                double v = Math.Acosh(Math.Sqrt((gStop - 1.0) / (gPass - 1.0)));
                order = (int)Math.Ceiling(v / Math.Acosh(nat));
                double newFreq = 1.0 / Math.Cosh(1.0 / order * v);
                wn = type switch
                {
                    1 => [passb[0] / newFreq],
                    2 => [passb[0] * newFreq],
                    3 => Pair((newFreq / 2.0 * (passb[0] - passb[1])) + Math.Sqrt((newFreq * newFreq * Math.Pow(passb[1] - passb[0], 2) / 4.0) + (passb[1] * passb[0])), passb),
                    _ => Pair((1.0 / (2.0 * newFreq) * (passb[0] - passb[1])) + Math.Sqrt((Math.Pow(passb[1] - passb[0], 2) / (4.0 * newFreq * newFreq)) + (passb[1] * passb[0])), passb),
                };
                break;
            }

            default:
            {
                double arg1Sq = Pow10m1(0.1 * gpass) / Pow10m1(0.1 * gstop);
                double arg0 = 1.0 / nat;
                double d0 = EllipticFunctions.CompleteK(arg0 * arg0), d0m = EllipticFunctions.CompleteKm1(arg0 * arg0);
                double d1 = EllipticFunctions.CompleteK(arg1Sq), d1m = EllipticFunctions.CompleteKm1(arg1Sq);
                order = (int)Math.Ceiling(d0 * d1m / (d0m * d1));
                wn = passb;
                break;
            }
        }

        // Обратно из предыскажённых частот в Гц
        return new IirOrderEstimate(order, new Vector(wn.Select(w => Math.Atan(w) * 2.0 / Math.PI * fs / 2)));
    }

    private static double[] Pair(double first, double[] passb) => [first, passb[1] * passb[0] / first];

    private static double[] ButterStop(double[] passb, double w0)
    {
        double discr = Math.Sqrt(Math.Pow(passb[1] - passb[0], 2) + (4 * w0 * w0 * passb[0] * passb[1]));
        double a = ((passb[1] - passb[0]) + discr) / (2 * w0);
        double b = ((passb[1] - passb[0]) - discr) / (2 * w0);
        return new[] { Math.Abs(a), Math.Abs(b) }.OrderBy(v => v).ToArray();
    }

    private static double[] ButterPass(double[] passb, double w0)
        => new[] { -w0, w0 }
            .Select(w => Math.Abs((-w * (passb[1] - passb[0]) / 2.0) + Math.Sqrt((w * w / 4.0 * Math.Pow(passb[1] - passb[0], 2)) + (passb[0] * passb[1]))))
            .OrderBy(v => v).ToArray();

    private static double NaturalFrequency(double[] stopb, double[] passb, double gpass, double gstop, int type, IirFamily family)
    {
        double[] nat;
        switch (type)
        {
            case 1:
                nat = [stopb[0] / passb[0]];
                break;
            case 2:
                nat = [passb[0] / stopb[0]];
                break;
            case 3:
                // Обе границы уточняются от исходных значений (как в scipy 1.17)
                double[] original = (double[])passb.Clone();
                double wp0 = Brent.Minimize(w => BandStopOrder(w, 0, original, stopb, gpass, gstop, family), original[0], stopb[0] - 1e-12);
                double wp1 = Brent.Minimize(w => BandStopOrder(w, 1, original, stopb, gpass, gstop, family), stopb[1] + 1e-12, original[1]);
                passb[0] = wp0;
                passb[1] = wp1;
                nat = stopb.Select(s => s * (passb[0] - passb[1]) / ((s * s) - (passb[0] * passb[1]))).ToArray();
                break;
            default:
                nat = stopb.Select(s => ((s * s) - (passb[0] * passb[1])) / (s * (passb[0] - passb[1]))).ToArray();
                break;
        }

        return nat.Min(Math.Abs);
    }

    /// <summary>Порядок РФ как функция границы полосы пропускания (scipy band_stop_obj).</summary>
    private static double BandStopOrder(double wp, int index, double[] passb, double[] stopb, double gpass, double gstop, IirFamily family)
    {
        double[] p = (double[])passb.Clone();
        p[index] = wp;
        double nat = stopb.Select(s => s * (p[0] - p[1]) / ((s * s) - (p[0] * p[1]))).Min(Math.Abs);
        double gStop = Math.Pow(10, 0.1 * Math.Abs(gstop));
        double gPass = Math.Pow(10, 0.1 * Math.Abs(gpass));
        return family switch
        {
            IirFamily.Butterworth => Math.Log10((gStop - 1.0) / (gPass - 1.0)) / (2 * Math.Log10(nat)),
            IirFamily.Elliptic => EllipticRatio(1.0 / nat, Math.Sqrt((gPass - 1.0) / (gStop - 1.0))),
            _ => Math.Acosh(Math.Sqrt((gStop - 1.0) / (gPass - 1.0))) / Math.Acosh(nat),
        };
    }

    private static double EllipticRatio(double arg0, double arg1)
    {
        double d00 = EllipticFunctions.CompleteK(arg0 * arg0), d01 = EllipticFunctions.CompleteK(1 - (arg0 * arg0));
        double d10 = EllipticFunctions.CompleteK(arg1 * arg1), d11 = EllipticFunctions.CompleteK(1 - (arg1 * arg1));
        return d00 * d11 / (d01 * d10);
    }

    private static double Pow10m1(double x)
    {
        double y = Math.Log(10) * x;
        double u = Math.Exp(y);
        return u == 1.0 ? y : (u - 1.0) * y / Math.Log(u);
    }

    /// <summary>
    /// Бессель: наименьший порядок, при котором фильтр с нормировкой по АЧХ, срез которого подобран так, чтобы
    /// ослабление на границе полосы пропускания равнялось допустимому, даёт требуемое затухание.
    /// </summary>
    private static IirOrderEstimate BesselBySearch(IirSpecification spec)
    {
        if (spec.BandType is not (IirBandType.LowPass or IirBandType.HighPass))
        {
            throw new NotSupportedException("Порядок фильтра Бесселя по ТЗ подбирается для ФНЧ и ФВЧ; для ПФ и РФ задайте порядок явно.");
        }

        double fs = spec.SampleRate;
        double fp = spec.PassbandEdges[0];
        bool low = spec.BandType == IirBandType.LowPass;
        for (int n = 1; n <= 40; n++)
        {
            ZeroPoleGain prototype = AnalogPrototype.Bessel(n, BesselNormalization.Magnitude);
            double lo = 1e-6 * fs, hi = fs / 2 * (1 - 1e-9);
            for (int i = 0; i < 80; i++)
            {
                double mid = (lo + hi) / 2;
                double loss = -20 * Math.Log10(DigitalMagnitude(prototype, low, mid, fp, fs));
                if (low == loss > spec.PassbandRippleDb)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            // Срез с той стороны интервала, где требование к полосе пропускания ещё выполнено
            double cutoff = low ? hi : lo;
            IirFilterDesign design = IirDesign.Design(IirFamily.Bessel, spec.BandType, n, fs, new Vector(cutoff), normalization: BesselNormalization.Magnitude);
            if (design.Verify(spec).Satisfied)
            {
                return new IirOrderEstimate(n, new Vector(cutoff));
            }
        }

        throw new ArgumentException("ТЗ не выполняется фильтром Бесселя порядка до 40.");
    }

    private static double DigitalMagnitude(ZeroPoleGain prototype, bool low, double cutoff, double f, double fs)
    {
        double w = FrequencyTransform.Prewarp(cutoff, fs);
        ZeroPoleGain analog = low ? FrequencyTransform.LowPassToLowPass(prototype, w) : FrequencyTransform.LowPassToHighPass(prototype, w);
        ZeroPoleGain d = FrequencyTransform.Bilinear(analog, 2.0);
        System.Numerics.Complex z = System.Numerics.Complex.Exp(System.Numerics.Complex.ImaginaryOne * 2 * Math.PI * f / fs);
        System.Numerics.Complex h = d.Gain;
        foreach (System.Numerics.Complex zero in d.ZeroArray)
        {
            h *= z - zero;
        }

        foreach (System.Numerics.Complex pole in d.PoleArray)
        {
            h /= z - pole;
        }

        return h.Magnitude;
    }
}
