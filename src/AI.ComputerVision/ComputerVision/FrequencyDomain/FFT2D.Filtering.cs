using AI.BackEnds.DSP.NWaves.Transforms;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Complex = System.Numerics.Complex;

namespace AI.ComputerVision.FrequencyDomain;

public static partial class FFT2D
{
    #region Спектр

    /// <summary>
    /// Амплитудный спектр (магнитуда), опционально в лог-масштабе
    /// </summary>
    public static Matrix MagnitudeSpectrum(double[,] re, double[,] im, bool logScale = true)
    {
        int h = re.GetLength(0), w = re.GetLength(1);
        var mag = new Matrix(h, w);

        Parallel.For(0, h, r =>
        {
            for (int c = 0; c < w; c++)
            {
                double rv = re[r, c], iv = im[r, c];
                double m = Math.Sqrt(rv * rv + iv * iv);
                mag[r, c] = logScale ? Math.Log(1 + m) : m;
            }
        });

        return mag;
    }

    /// <summary>
    /// Фазовый спектр
    /// </summary>
    public static Matrix PhaseSpectrum(double[,] re, double[,] im)
    {
        int h = re.GetLength(0), w = re.GetLength(1);
        var phase = new Matrix(h, w);

        Parallel.For(0, h, r =>
        {
            for (int c = 0; c < w; c++)
                phase[r, c] = Math.Atan2(im[r, c], re[r, c]);
        });

        return phase;
    }

    /// <summary>
    /// FFTShift — сдвиг нулевой частоты в центр (для визуализации)
    /// </summary>
    public static Matrix FFTShift(Matrix spectrum)
    {
        int h = spectrum.Height, w = spectrum.Width;
        int hh = h / 2, hw = w / 2;
        var shifted = new Matrix(h, w);

        Parallel.For(0, h, r =>
        {
            int srcR = (r + hh) % h;
            for (int c = 0; c < w; c++)
                shifted[r, c] = spectrum[srcR, (c + hw) % w];
        });

        return shifted;
    }

    #endregion

    #region Частотные фильтры (in-place, работают с re/im напрямую)

    /// <summary>
    /// Идеальный низкочастотный фильтр
    /// </summary>
    public static void LowPassFilter(double[,] re, double[,] im, double cutoffRadius)
    {
        ApplyCircularMask(re, im, cutoffRadius, lowPass: true);
    }

    /// <summary>
    /// Идеальный высокочастотный фильтр
    /// </summary>
    public static void HighPassFilter(double[,] re, double[,] im, double cutoffRadius)
    {
        ApplyCircularMask(re, im, cutoffRadius, lowPass: false);
    }

    /// <summary>
    /// Полосовой фильтр
    /// </summary>
    public static void BandPassFilter(double[,] re, double[,] im, double rLow, double rHigh)
    {
        int h = re.GetLength(0), w = re.GetLength(1);
        double rLow2 = rLow * rLow, rHigh2 = rHigh * rHigh;

        Parallel.For(0, h, r =>
        {
            double dr = Math.Min(r, h - r);
            double dr2 = dr * dr;
            for (int c = 0; c < w; c++)
            {
                double dc = Math.Min(c, w - c);
                double dist2 = dr2 + dc * dc;
                if (dist2 < rLow2 || dist2 > rHigh2)
                {
                    re[r, c] = 0;
                    im[r, c] = 0;
                }
            }
        });
    }

    /// <summary>
    /// Гауссов фильтр (НЧ или ВЧ)
    /// </summary>
    public static void GaussianFilter(double[,] re, double[,] im, double sigma, bool lowPass = true)
    {
        int h = re.GetLength(0), w = re.GetLength(1);
        double twoSigma2 = 2 * sigma * sigma;

        Parallel.For(0, h, r =>
        {
            double dr = Math.Min(r, h - r);
            double dr2 = dr * dr;
            for (int c = 0; c < w; c++)
            {
                double dc = Math.Min(c, w - c);
                double gauss = Math.Exp(-(dr2 + dc * dc) / twoSigma2);
                double weight = lowPass ? gauss : 1 - gauss;
                re[r, c] *= weight;
                im[r, c] *= weight;
            }
        });
    }

    private static void ApplyCircularMask(double[,] re, double[,] im, double radius, bool lowPass)
    {
        int h = re.GetLength(0), w = re.GetLength(1);
        double radius2 = radius * radius;

        Parallel.For(0, h, r =>
        {
            double dr = Math.Min(r, h - r);
            double dr2 = dr * dr;
            for (int c = 0; c < w; c++)
            {
                double dc = Math.Min(c, w - c);
                if (lowPass != (dr2 + dc * dc <= radius2))
                {
                    re[r, c] = 0;
                    im[r, c] = 0;
                }
            }
        });
    }

    #endregion
}
