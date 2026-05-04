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
    #region Надстройка для ComplexMatrix

    /// <summary>
    /// Прямое 2D FFT -> ComplexMatrix
    /// </summary>
    public static ComplexMatrix ForwardMatrix(Matrix image)
    {
        var (re, im, fftH, fftW) = Forward(image);
        return ToComplexMatrix(re, im, fftH, fftW);
    }

    /// <summary>
    /// Обратное 2D FFT из ComplexMatrix -> Matrix
    /// </summary>
    public static Matrix InverseMatrix(ComplexMatrix spectrum, int origH, int origW)
    {
        var (re, im) = FromComplexMatrix(spectrum);
        return Inverse(re, im, origH, origW);
    }

    /// <summary>
    /// Амплитудный спектр из ComplexMatrix
    /// </summary>
    public static Matrix MagnitudeSpectrum(ComplexMatrix spectrum, bool logScale = true)
    {
        var (re, im) = FromComplexMatrix(spectrum);
        return MagnitudeSpectrum(re, im, logScale);
    }

    /// <summary>
    /// Фазовый спектр из ComplexMatrix
    /// </summary>
    public static Matrix PhaseSpectrum(ComplexMatrix spectrum)
    {
        var (re, im) = FromComplexMatrix(spectrum);
        return PhaseSpectrum(re, im);
    }

    /// <summary>
    /// Низкочастотный фильтр -> новая ComplexMatrix
    /// </summary>
    public static ComplexMatrix LowPassFilter(ComplexMatrix spectrum, double cutoffRadius)
    {
        var (re, im) = FromComplexMatrix(spectrum);
        LowPassFilter(re, im, cutoffRadius);
        return ToComplexMatrix(re, im, re.GetLength(0), re.GetLength(1));
    }

    /// <summary>
    /// Высокочастотный фильтр -> новая ComplexMatrix
    /// </summary>
    public static ComplexMatrix HighPassFilter(ComplexMatrix spectrum, double cutoffRadius)
    {
        var (re, im) = FromComplexMatrix(spectrum);
        HighPassFilter(re, im, cutoffRadius);
        return ToComplexMatrix(re, im, re.GetLength(0), re.GetLength(1));
    }

    /// <summary>
    /// Полосовой фильтр -> новая ComplexMatrix
    /// </summary>
    public static ComplexMatrix BandPassFilter(ComplexMatrix spectrum, double rLow, double rHigh)
    {
        var (re, im) = FromComplexMatrix(spectrum);
        BandPassFilter(re, im, rLow, rHigh);
        return ToComplexMatrix(re, im, re.GetLength(0), re.GetLength(1));
    }

    /// <summary>
    /// Гауссов фильтр -> новая ComplexMatrix
    /// </summary>
    public static ComplexMatrix GaussianFilter(ComplexMatrix spectrum, double sigma, bool lowPass = true)
    {
        var (re, im) = FromComplexMatrix(spectrum);
        GaussianFilter(re, im, sigma, lowPass);
        return ToComplexMatrix(re, im, re.GetLength(0), re.GetLength(1));
    }

    /// <summary>
    /// FFTShift для ComplexMatrix
    /// </summary>
    public static ComplexMatrix FFTShiftComplex(ComplexMatrix spectrum)
    {
        int h = spectrum.Height, w = spectrum.Width;
        int hh = h / 2, hw = w / 2;
        var shifted = new ComplexMatrix(h, w);

        Parallel.For(0, h, r =>
        {
            int srcR = (r + hh) % h;
            for (int c = 0; c < w; c++)
                shifted[r, c] = spectrum[srcR, (c + hw) % w];
        });

        return shifted;
    }

    private static ComplexMatrix ToComplexMatrix(double[,] re, double[,] im, int h, int w)
    {
        var cm = new ComplexMatrix(h, w);

        Parallel.For(0, h, r =>
        {
            for (int c = 0; c < w; c++)
                cm[r, c] = new Complex(re[r, c], im[r, c]);
        });

        return cm;
    }

    private static (double[,] re, double[,] im) FromComplexMatrix(ComplexMatrix cm)
    {
        int h = cm.Height, w = cm.Width;
        var re = new double[h, w];
        var im = new double[h, w];

        Parallel.For(0, h, r =>
        {
            for (int c = 0; c < w; c++)
            {
                re[r, c] = cm[r, c].Real;
                im[r, c] = cm[r, c].Imaginary;
            }
        });

        return (re, im);
    }

    #endregion
}
