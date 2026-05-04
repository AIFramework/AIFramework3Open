using AI.BackEnds.DSP.NWaves.Transforms;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Complex = System.Numerics.Complex;

namespace AI.ComputerVision.FrequencyDomain;

/// <summary>
/// Быстрое двумерное преобразование Фурье для изображений.
/// Внутреннее ядро — Fft64 (in-place, double[]).
/// Все тяжёлые циклы параллелизованы через Parallel.For/Invoke.
/// </summary>
[Serializable]
public static partial class FFT2D
{
    #region Прямое / обратное 2D FFT

    /// <summary>
    /// Прямое 2D FFT с выбором бэкенда.
    /// </summary>
    public static (double[,] re, double[,] im, int fftH, int fftW) Forward(Matrix image, FftBackend backend)
    {
        if (backend == FftBackend.Cuda)
        {
            try
            {
                var result = ForwardGpu(image);
                if (result.HasValue) return result.Value;
            }
            catch { /* CUDA unavailable — fallback to CPU */ }
        }
        return Forward(image);
    }

    /// <summary>
    /// Обратное 2D FFT с выбором бэкенда.
    /// </summary>
    public static Matrix Inverse(double[,] re, double[,] im, int origH, int origW, FftBackend backend)
    {
        if (backend == FftBackend.Cuda)
        {
            try
            {
                var result = InverseGpu(re, im, origH, origW);
                if (result != null) return result;
            }
            catch { /* CUDA unavailable — fallback to CPU */ }
        }
        return Inverse(re, im, origH, origW);
    }

    /// <summary>
    /// Прямое 2D FFT серого изображения (CPU-бэкенд).
    /// Результат: (re, im) — пара матриц размера fftH × fftW (степени двойки).
    /// </summary>
    public static (double[,] re, double[,] im, int fftH, int fftW) Forward(Matrix image)
    {
        int h = image.Height, w = image.Width;
        int fftH = NextPow2(h), fftW = NextPow2(w);

        var re = new double[fftH, fftW];
        var im = new double[fftH, fftW];

        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
                re[r, c] = image[r, c];

        int rowBytes = fftW * sizeof(double);

        Parallel.For(0, fftH,
            () => (fft: new Fft64(fftW), rowRe: new double[fftW], rowIm: new double[fftW]),
            (r, _, loc) =>
            {
                Buffer.BlockCopy(re, r * rowBytes, loc.rowRe, 0, rowBytes);
                Array.Clear(loc.rowIm, 0, fftW);
                loc.fft.Direct(loc.rowRe, loc.rowIm);
                Buffer.BlockCopy(loc.rowRe, 0, re, r * rowBytes, rowBytes);
                Buffer.BlockCopy(loc.rowIm, 0, im, r * rowBytes, rowBytes);
                return loc;
            },
            _ => { });

        Parallel.For(0, fftW,
            () => (fft: new Fft64(fftH), colRe: new double[fftH], colIm: new double[fftH]),
            (c, _, loc) =>
            {
                for (int r = 0; r < fftH; r++) { loc.colRe[r] = re[r, c]; loc.colIm[r] = im[r, c]; }
                loc.fft.Direct(loc.colRe, loc.colIm);
                for (int r = 0; r < fftH; r++) { re[r, c] = loc.colRe[r]; im[r, c] = loc.colIm[r]; }
                return loc;
            },
            _ => { });

        return (re, im, fftH, fftW);
    }

    /// <summary>
    /// Обратное 2D FFT -> вещественная матрица исходного размера
    /// </summary>
    public static Matrix Inverse(double[,] re, double[,] im, int origH, int origW)
    {
        int fftH = re.GetLength(0), fftW = re.GetLength(1);
        int rowBytes = fftW * sizeof(double);

        var rr = (double[,])re.Clone();
        var ii = (double[,])im.Clone();

        Parallel.For(0, fftW,
            () => (fft: new Fft64(fftH), colRe: new double[fftH], colIm: new double[fftH]),
            (c, _, loc) =>
            {
                for (int r = 0; r < fftH; r++) { loc.colRe[r] = rr[r, c]; loc.colIm[r] = ii[r, c]; }
                loc.fft.InverseNorm(loc.colRe, loc.colIm);
                for (int r = 0; r < fftH; r++) { rr[r, c] = loc.colRe[r]; ii[r, c] = loc.colIm[r]; }
                return loc;
            },
            _ => { });

        var result = new Matrix(origH, origW);

        Parallel.For(0, fftH,
            () => (fft: new Fft64(fftW), rowRe: new double[fftW], rowIm: new double[fftW]),
            (r, _, loc) =>
            {
                Buffer.BlockCopy(rr, r * rowBytes, loc.rowRe, 0, rowBytes);
                Buffer.BlockCopy(ii, r * rowBytes, loc.rowIm, 0, rowBytes);
                loc.fft.InverseNorm(loc.rowRe, loc.rowIm);
                if (r < origH)
                    for (int c = 0; c < origW; c++)
                        result[r, c] = loc.rowRe[c];
                return loc;
            },
            _ => { });

        return result;
    }

    #endregion

    #region cuFFT GPU-реализация

    private static (double[,] re, double[,] im, int fftH, int fftW)? ForwardGpu(Matrix image)
    {
        int h = image.Height, w = image.Width;
        int fftH = NextPow2(h), fftW = NextPow2(w);

        using var handle = CuFftHandle.TryCreate(fftH, fftW, forward: true);
        if (handle == null) return null;

        var interleaved = new double[fftH * fftW * 2];
        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
                interleaved[(r * fftW + c) * 2] = image[r, c];

        if (!handle.Exec2D(interleaved, fftH, fftW, forward: true))
            return null;

        var re = new double[fftH, fftW];
        var im = new double[fftH, fftW];
        for (int r = 0; r < fftH; r++)
            for (int c = 0; c < fftW; c++)
            {
                int idx = (r * fftW + c) * 2;
                re[r, c] = interleaved[idx];
                im[r, c] = interleaved[idx + 1];
            }

        return (re, im, fftH, fftW);
    }

    private static Matrix InverseGpu(double[,] re, double[,] im, int origH, int origW)
    {
        int fftH = re.GetLength(0), fftW = re.GetLength(1);

        using var handle = CuFftHandle.TryCreate(fftH, fftW, forward: false);
        if (handle == null) return null;

        var interleaved = new double[fftH * fftW * 2];
        for (int r = 0; r < fftH; r++)
            for (int c = 0; c < fftW; c++)
            {
                int idx = (r * fftW + c) * 2;
                interleaved[idx] = re[r, c];
                interleaved[idx + 1] = im[r, c];
            }

        if (!handle.Exec2D(interleaved, fftH, fftW, forward: false))
            return null;

        var result = new Matrix(origH, origW);
        for (int r = 0; r < origH; r++)
            for (int c = 0; c < origW; c++)
                result[r, c] = interleaved[(r * fftW + c) * 2];

        return result;
    }

    #endregion

    #region Утилиты

    /// <summary>
    /// Нормализация матрицы в диапазон [0, 255]
    /// </summary>
    public static Matrix NormalizeTo255(Matrix m)
    {
        int h = m.Height, w = m.Width;
        double min = double.MaxValue, max = double.MinValue;

        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                double v = m[r, c];
                if (v < min) min = v;
                if (v > max) max = v;
            }

        double range = max - min;
        if (range < 1e-12) range = 1;
        double scale = 255.0 / range;

        var result = new Matrix(h, w);

        Parallel.For(0, h, r =>
        {
            for (int c = 0; c < w; c++)
                result[r, c] = (m[r, c] - min) * scale;
        });

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Clamp(double v) =>
        (byte)Math.Clamp(Math.Round(v), 0, 255);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NextPow2(int v)
    {
        v--;
        v |= v >> 1; v |= v >> 2; v |= v >> 4;
        v |= v >> 8; v |= v >> 16;
        return v + 1;
    }

    #endregion
}
