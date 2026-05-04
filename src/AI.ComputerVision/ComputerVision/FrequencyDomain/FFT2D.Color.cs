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
    #region Цветные изображения (поканальный 2D FFT)

    /// <summary>
    /// Результат 2D FFT для одного канала
    /// </summary>
    public readonly struct ChannelSpectrum
    {
        public readonly double[,] Re;
        public readonly double[,] Im;
        public readonly int FftH;
        public readonly int FftW;
        public readonly int OrigH;
        public readonly int OrigW;

        public ChannelSpectrum(double[,] re, double[,] im, int fftH, int fftW, int origH, int origW)
        {
            Re = re; Im = im; FftH = fftH; FftW = fftW; OrigH = origH; OrigW = origW;
        }
    }

    /// <summary>
    /// 2D FFT для цветного изображения с выбором бэкенда.
    /// </summary>
    public static (ChannelSpectrum R, ChannelSpectrum G, ChannelSpectrum B) ForwardColor(SKBitmap bitmap, FftBackend backend)
    {
        if (backend == FftBackend.Cuda)
        {
            try
            {
                if (CudaFftInfo.IsAvailable)
                {
                    var mR = ImageMatrixConverter.BmpToMatrRed(bitmap);
                    var mG = ImageMatrixConverter.BmpToMatrGreen(bitmap);
                    var mB = ImageMatrixConverter.BmpToMatrBlue(bitmap);
                    int h = bitmap.Height, w = bitmap.Width;

                    ChannelSpectrum sR = default, sG = default, sB = default;
                    Parallel.Invoke(
                        () => { var r = Forward(mR, FftBackend.Cuda); sR = new ChannelSpectrum(r.re, r.im, r.fftH, r.fftW, h, w); },
                        () => { var r = Forward(mG, FftBackend.Cuda); sG = new ChannelSpectrum(r.re, r.im, r.fftH, r.fftW, h, w); },
                        () => { var r = Forward(mB, FftBackend.Cuda); sB = new ChannelSpectrum(r.re, r.im, r.fftH, r.fftW, h, w); }
                    );
                    return (sR, sG, sB);
                }
            }
            catch { /* CUDA unavailable — fallback to CPU */ }
        }
        return ForwardColor(bitmap);
    }

    /// <summary>
    /// Фильтрация цветного изображения с выбором бэкенда.
    /// </summary>
    public static SKBitmap FilterColor(SKBitmap bitmap, Action<double[,], double[,]> filter, FftBackend backend)
    {
        var (sR, sG, sB) = ForwardColor(bitmap, backend);
        Parallel.Invoke(
            () => filter(sR.Re, sR.Im),
            () => filter(sG.Re, sG.Im),
            () => filter(sB.Re, sB.Im)
        );
        return InverseColor(sR, sG, sB, backend);
    }

    /// <summary>
    /// Обратное 2D FFT цветного -> SKBitmap с выбором бэкенда.
    /// </summary>
    public static SKBitmap InverseColor(ChannelSpectrum sR, ChannelSpectrum sG, ChannelSpectrum sB, FftBackend backend)
    {
        Matrix mR = null!, mG = null!, mB = null!;
        Parallel.Invoke(
            () => mR = Inverse(sR.Re, sR.Im, sR.OrigH, sR.OrigW, backend),
            () => mG = Inverse(sG.Re, sG.Im, sG.OrigH, sG.OrigW, backend),
            () => mB = Inverse(sB.Re, sB.Im, sB.OrigH, sB.OrigW, backend)
        );

        int h = sR.OrigH, w = sR.OrigW;
        var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bmp = new SKBitmap(info);
        unsafe
        {
            byte* basePtr = (byte*)bmp.GetPixels().ToPointer();
            int stride = bmp.RowBytes;
            Parallel.For(0, h, y =>
            {
                byte* row = basePtr + y * stride;
                for (int x = 0; x < w; x++)
                {
                    byte* px = row + x * 4;
                    px[0] = Clamp(mB[y, x]);
                    px[1] = Clamp(mG[y, x]);
                    px[2] = Clamp(mR[y, x]);
                    px[3] = 255;
                }
            });
        }
        return bmp;
    }

    /// <summary>
    /// Средний амплитудный спектр по RGB с выбором бэкенда.
    /// </summary>
    public static Matrix ColorMagnitudeSpectrum(SKBitmap bitmap, bool logScale, FftBackend backend)
    {
        var (sR, sG, sB) = ForwardColor(bitmap, backend);
        Matrix magR = null!, magG = null!, magB = null!;
        Parallel.Invoke(
            () => magR = MagnitudeSpectrum(sR.Re, sR.Im, logScale),
            () => magG = MagnitudeSpectrum(sG.Re, sG.Im, logScale),
            () => magB = MagnitudeSpectrum(sB.Re, sB.Im, logScale)
        );
        int h = magR.Height, w = magR.Width;
        var avg = new Matrix(h, w);
        Parallel.For(0, h, r =>
        {
            for (int c = 0; c < w; c++)
                avg[r, c] = (magR[r, c] + magG[r, c] + magB[r, c]) * (1.0 / 3.0);
        });
        return avg;
    }

    /// <summary>
    /// 2D FFT для цветного изображения (3 канала параллельно)
    /// </summary>
    public static (ChannelSpectrum R, ChannelSpectrum G, ChannelSpectrum B) ForwardColor(SKBitmap bitmap)
    {
        var mR = ImageMatrixConverter.BmpToMatrRed(bitmap);
        var mG = ImageMatrixConverter.BmpToMatrGreen(bitmap);
        var mB = ImageMatrixConverter.BmpToMatrBlue(bitmap);

        int h = bitmap.Height, w = bitmap.Width;

        ChannelSpectrum sR = default, sG = default, sB = default;

        Parallel.Invoke(
            () => { var (re, im, fh, fw) = Forward(mR); sR = new ChannelSpectrum(re, im, fh, fw, h, w); },
            () => { var (re, im, fh, fw) = Forward(mG); sG = new ChannelSpectrum(re, im, fh, fw, h, w); },
            () => { var (re, im, fh, fw) = Forward(mB); sB = new ChannelSpectrum(re, im, fh, fw, h, w); }
        );

        return (sR, sG, sB);
    }

    /// <summary>
    /// Обратное 2D FFT цветного -> SKBitmap (unsafe-запись пикселей)
    /// </summary>
    public static SKBitmap InverseColor(ChannelSpectrum sR, ChannelSpectrum sG, ChannelSpectrum sB)
    {
        Matrix mR = null!, mG = null!, mB = null!;

        Parallel.Invoke(
            () => mR = Inverse(sR.Re, sR.Im, sR.OrigH, sR.OrigW),
            () => mG = Inverse(sG.Re, sG.Im, sG.OrigH, sG.OrigW),
            () => mB = Inverse(sB.Re, sB.Im, sB.OrigH, sB.OrigW)
        );

        int h = sR.OrigH, w = sR.OrigW;
        var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bmp = new SKBitmap(info);

        unsafe
        {
            byte* basePtr = (byte*)bmp.GetPixels().ToPointer();
            int stride = bmp.RowBytes;

            Parallel.For(0, h, y =>
            {
                byte* row = basePtr + y * stride;
                for (int x = 0; x < w; x++)
                {
                    byte* px = row + x * 4;
                    px[0] = Clamp(mB[y, x]);
                    px[1] = Clamp(mG[y, x]);
                    px[2] = Clamp(mR[y, x]);
                    px[3] = 255;
                }
            });
        }

        return bmp;
    }

    /// <summary>
    /// Фильтрация цветного изображения в частотной области
    /// </summary>
    public static SKBitmap FilterColor(SKBitmap bitmap, Action<double[,], double[,]> filter)
    {
        var (sR, sG, sB) = ForwardColor(bitmap);

        Parallel.Invoke(
            () => filter(sR.Re, sR.Im),
            () => filter(sG.Re, sG.Im),
            () => filter(sB.Re, sB.Im)
        );

        return InverseColor(sR, sG, sB);
    }

    /// <summary>
    /// Средний амплитудный спектр по RGB каналам
    /// </summary>
    public static Matrix ColorMagnitudeSpectrum(SKBitmap bitmap, bool logScale = true)
    {
        var (sR, sG, sB) = ForwardColor(bitmap);

        Matrix magR = null!, magG = null!, magB = null!;

        Parallel.Invoke(
            () => magR = MagnitudeSpectrum(sR.Re, sR.Im, logScale),
            () => magG = MagnitudeSpectrum(sG.Re, sG.Im, logScale),
            () => magB = MagnitudeSpectrum(sB.Re, sB.Im, logScale)
        );

        int h = magR.Height, w = magR.Width;
        var avg = new Matrix(h, w);

        Parallel.For(0, h, r =>
        {
            for (int c = 0; c < w; c++)
                avg[r, c] = (magR[r, c] + magG[r, c] + magB[r, c]) * (1.0 / 3.0);
        });

        return avg;
    }

    #endregion
}
