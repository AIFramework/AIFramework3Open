using AI.Charts.JS;
using AI.ComputerVision;
using AI.ComputerVision.FiltersEachElements;
using AI.ComputerVision.FrequencyDomain;
using AI.ComputerVision.ImgTransforms;
using AI.ComputerVision.SpatialFilters;
using AI.ComputerVision.Statistics;
using AI.DataStructs.Algebraic;
using AI.HighLevelFunctions;
using AiFrameworkDemo.Core;
using SkiaSharp;
using System.Diagnostics;
using System.Text;

namespace AiFrameworkDemo.Modules.ComputerVision;

public static class CvDemoRunner
{
    public static DemoResult Run(string key, string imageBase64, IReadOnlyDictionary<string, double> p)
    {
        byte[] bytes = Convert.FromBase64String(imageBase64);
        using var bmp = SKBitmap.Decode(bytes);
        var gray = ImageMatrixConverter.BmpToMatr(bmp);

        double G(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;
        int colorMode = (int)G("colorMode", 0);
        bool color = colorMode == 1;
        FftBackend backend = (int)G("fftBackend", 0) == 1 ? FftBackend.Cuda : FftBackend.Cpu;

        static DemoResult Img(string png, string? text = null) =>
            new() { PngDataUrl = png, TextOutput = text };

        return key switch
        {
            // -- объединённые блоки ----------------------------------------------
            "spatial_filter"    => SpatialFilter(bmp, gray, (int)G("spatialFilter", 0),
                                       G("sharpAmount", 1), color),
            "gradient"          => Gradient(bmp, gray, (int)G("gradType", 0), color),

            // -- устаревшие ключи (обратная совместимость) ----------------------
            "gray"     => Img(color ? BitmapToPng(bmp) : SmartPng(gray)),
            "smooth"   => Img(color ? ApplyColorFilter(bmp, m => new Smoothing().Filtration(m))
                                    : SmartPng(new Smoothing().Filtration(gray))),
            "gauss"    => Img(color ? ApplyColorFilter(bmp, m => new GaussianBlurFilter().Filtration(m))
                                    : SmartPng(new GaussianBlurFilter().Filtration(gray))),
            "sharp"    => Img(color ? ApplyColorFilter(bmp, m => new Sharpness(G("sharpAmount", 1)).Filtration(m))
                                    : SmartPng(new Sharpness(G("sharpAmount", 1)).Filtration(gray))),
            "sobel"    => Img(color ? ApplyColorFilter(bmp, SobelMagMatrix)
                                    : SmartPng(SobelMagMatrix(gray))),
            "sobel_gx" => Img(color ? ApplyColorFilter(bmp, SobelGxMatrix)
                                    : SmartPng(SobelGxMatrix(gray))),

            // -- прочие ---------------------------------------------------------
            "hog"                => Img(Hog(gray, (int)G("hogBins", 8))),
            "equalize"           => Img(color ? ApplyColorFilter(bmp, m => ImageHistogram.Equalize(m))
                                              : SmartPng(ImageHistogram.Equalize(gray))),
            "binary"             => Img(Binary(gray, G("threshold", 0.5))),
            "fft_spectrum"       => FFTSpectrum(bmp, gray, (int)G("specType", 0), colorMode, backend),
            "fft_filter"         => FFTFilter(bmp, gray, (int)G("filterType", 0),
                                       G("cutoff", 30), G("sigma", 20), G("rHigh", 60), colorMode, backend),
            "fft_color_channels" => FFTColorChannels(bmp, backend),
            _                    => Img(CvImageHelper.PlaceholderPngDataUrl("Неизвестный алгоритм: " + key)),
        };
    }

    #region Утилиты сокращения

    private static string SmartPng(Matrix m) => CvImageHelper.MatrixToPngDataUrlSmart(m);
    private static string BitmapToPng(SKBitmap bmp) => CvImageHelper.BitmapToPngDataUrl(bmp);

    #endregion

    #region Поканальная обработка цветных изображений

    /// <summary>
    /// Применяет фильтр к каждому каналу R/G/B отдельно и собирает обратно в цветное изображение.
    /// </summary>
    private static string ApplyColorFilter(SKBitmap bmp, Func<Matrix, Matrix> filter)
    {
        var mR = ImageMatrixConverter.BmpToMatrRed(bmp);
        var mG = ImageMatrixConverter.BmpToMatrGreen(bmp);
        var mB = ImageMatrixConverter.BmpToMatrBlue(bmp);

        Matrix rR = null!, rG = null!, rB = null!;
        System.Threading.Tasks.Parallel.Invoke(
            () => rR = filter(mR),
            () => rG = filter(mG),
            () => rB = filter(mB)
        );

        using var result = ChannelsToBitmap(rR, rG, rB);
        return BitmapToPng(result);
    }

    /// <summary>
    /// Собирает три матрицы (R, G, B) в цветной SKBitmap.
    /// Значения автоматически нормализуются в [0, 255] если выходят за диапазон.
    /// </summary>
    private static unsafe SKBitmap ChannelsToBitmap(Matrix r, Matrix g, Matrix b)
    {
        int h = r.Height, w = r.Width;

        bool needsNorm = false;
        double minR = double.MaxValue, maxR = double.MinValue;
        double minG = double.MaxValue, maxG = double.MinValue;
        double minB = double.MaxValue, maxB = double.MinValue;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double rv = r[y, x], gv = g[y, x], bv = b[y, x];
                if (rv < minR) minR = rv; if (rv > maxR) maxR = rv;
                if (gv < minG) minG = gv; if (gv > maxG) maxG = gv;
                if (bv < minB) minB = bv; if (bv > maxB) maxB = bv;
            }

        if (minR < -0.5 || maxR > 256 || minG < -0.5 || maxG > 256 || minB < -0.5 || maxB > 256)
            needsNorm = true;

        double scaleR = 1, offR = 0, scaleG = 1, offG = 0, scaleB = 1, offB = 0;
        if (needsNorm)
        {
            double rangeR = maxR - minR; if (rangeR < 1e-12) rangeR = 1;
            double rangeG = maxG - minG; if (rangeG < 1e-12) rangeG = 1;
            double rangeB = maxB - minB; if (rangeB < 1e-12) rangeB = 1;
            scaleR = 255.0 / rangeR; offR = minR;
            scaleG = 255.0 / rangeG; offG = minG;
            scaleB = 255.0 / rangeB; offB = minB;
        }
        else if (maxR <= 1.001 && maxG <= 1.001 && maxB <= 1.001 && minR >= 0 && minG >= 0 && minB >= 0)
        {
            scaleR = scaleG = scaleB = 255.0;
        }

        var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bmp = new SKBitmap(info);

        byte* basePtr = (byte*)bmp.GetPixels().ToPointer();
        int stride = bmp.RowBytes;

        System.Threading.Tasks.Parallel.For(0, h, y =>
        {
            byte* row = basePtr + y * stride;
            for (int x = 0; x < w; x++)
            {
                byte* px = row + x * 4;
                px[0] = Clamp((b[y, x] - offB) * scaleB);
                px[1] = Clamp((g[y, x] - offG) * scaleG);
                px[2] = Clamp((r[y, x] - offR) * scaleR);
                px[3] = 255;
            }
        });

        return bmp;
    }

    private static byte Clamp(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);

    #endregion

    #region Пространственные фильтры и градиенты

    /// <summary>Объединённый пространственный фильтр: Исходное / Сглаживание / Гаусс / Резкость.</summary>
    private static DemoResult SpatialFilter(SKBitmap bmp, Matrix gray,
        int filterIdx, double sharpAmount, bool color)
    {
        Func<Matrix, Matrix> fn = filterIdx switch
        {
            1 => m => new Smoothing().Filtration(m),
            2 => m => new GaussianBlurFilter().Filtration(m),
            3 => m => new Sharpness(sharpAmount).Filtration(m),
            _ => m => m,
        };

        string png;
        if (color)
            png = filterIdx == 0 ? BitmapToPng(bmp) : ApplyColorFilter(bmp, fn);
        else
            png = SmartPng(fn(gray));

        return new DemoResult { PngDataUrl = png };
    }

    /// <summary>Объединённый градиент: Sobel |G| / Gx / Gy.</summary>
    private static DemoResult Gradient(SKBitmap bmp, Matrix gray, int gradType, bool color)
    {
        Func<Matrix, Matrix> fn = gradType switch
        {
            1 => SobelGxMatrix,
            2 => SobelGyMatrix,
            _ => SobelMagMatrix,
        };

        string png = color ? ApplyColorFilter(bmp, fn) : SmartPng(fn(gray));
        return new DemoResult { PngDataUrl = png };
    }

    private static Matrix SobelMagMatrix(Matrix gray)
    {
        var s = new SobelTransform().Transform(gray);
        return CvImageHelper.NormalizeTo255(s.GradImg);
    }

    private static Matrix SobelGxMatrix(Matrix gray)
    {
        var s = new SobelTransform().Transform(gray);
        return CvImageHelper.NormalizeTo255(s.GradX);
    }

    private static Matrix SobelGyMatrix(Matrix gray)
    {
        var s = new SobelTransform().Transform(gray);
        return CvImageHelper.NormalizeTo255(s.GradY);
    }

    private static string Hog(Matrix gray, int bins)
    {
        var h = new HOG(bins).CalcHist(gray, normalyze: true, centrNorm: true);
        return CvImageHelper.VectorToBarChartPngDataUrl(h, 360, 180, $"HOG ({bins} бинов)");
    }

    private static string Binary(Matrix gray, double threshold)
    {
        var th = ActivationFunctions.Threshold(gray / 255.0, threshold);
        using var bm = new AI.ComputerVision.BinaryImg(th).ToBmp();
        return BitmapToPng(bm);
    }

    #endregion

    #region 2D FFT

    private static string FftInfoText(FftBackend requested, Stopwatch sw)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Информация о бэкенде FFT ===");
        sb.AppendLine();

        bool cudaAvail;
        string statusMsg;
        try
        {
            cudaAvail = CudaFftInfo.IsAvailable;
            statusMsg = CudaFftInfo.StatusMessage;
        }
        catch
        {
            cudaAvail = false;
            statusMsg = "Ошибка при определении CUDA (DLL не найдены)";
        }

        sb.AppendLine($"CUDA статус: {(cudaAvail ? "доступна" : "недоступна")}");
        sb.AppendLine($"  {statusMsg}");
        sb.AppendLine();

        if (requested == FftBackend.Cuda && !cudaAvail)
        {
            sb.AppendLine("Внимание: запрошен CUDA-бэкенд, но cuFFT/cudart DLL не найдены.");
            sb.AppendLine("  Автоматический fallback на CPU (Parallel Fft64).");
            sb.AppendLine();
        }

        string usedBackend = (requested == FftBackend.Cuda && cudaAvail) ? "CUDA (cuFFT)" : "CPU (Parallel Fft64)";
        sb.AppendLine($"Использован бэкенд: {usedBackend}");
        sb.AppendLine($"Время FFT: {sw.Elapsed.TotalMilliseconds:F1} мс");

        return sb.ToString();
    }

    /// <summary>
    /// Максимальное разрешение сетки 3D-поверхности спектра (пикселей по каждой оси).
    /// Большие изображения субдискретизируются для производительности в браузере.
    /// </summary>
    private const int MaxSpectrumGrid = 96;

    private static DemoResult FFTSpectrum(SKBitmap bmp, Matrix gray, int specType, int colorMode, FftBackend backend)
    {
        var sw = Stopwatch.StartNew();
        Matrix spectrum;

        if (colorMode == 1)
        {
            if (specType == 1)
            {
                var (sR, _, _) = FFT2D.ForwardColor(bmp, backend);
                spectrum = FFT2D.FFTShift(FFT2D.PhaseSpectrum(sR.Re, sR.Im));
            }
            else
            {
                var avgMag = FFT2D.ColorMagnitudeSpectrum(bmp, logScale: specType != 2, backend);
                spectrum = FFT2D.FFTShift(avgMag);
            }
        }
        else
        {
            var (re, im, _, _) = FFT2D.Forward(gray, backend);
            spectrum = specType switch
            {
                1 => FFT2D.FFTShift(FFT2D.PhaseSpectrum(re, im)),
                2 => FFT2D.FFTShift(FFT2D.MagnitudeSpectrum(re, im, logScale: false)),
                _ => FFT2D.FFTShift(FFT2D.MagnitudeSpectrum(re, im, logScale: true)),
            };
        }

        sw.Stop();
        string infoText = FftInfoText(backend, sw);

        // Субдискретизация для 3D-графика
        int H = spectrum.Height, W = spectrum.Width;
        int stepY = Math.Max(1, H / MaxSpectrumGrid);
        int stepX = Math.Max(1, W / MaxSpectrumGrid);
        int gH = H / stepY, gW = W / stepX;

        var xGrid = new double[gW];
        var yGrid = new double[gH];
        for (int j = 0; j < gW; j++) xGrid[j] = j * stepX - W / 2.0;
        for (int i = 0; i < gH; i++) yGrid[i] = i * stepY - H / 2.0;

        var z = new double[gH, gW];
        for (int i = 0; i < gH; i++)
            for (int j = 0; j < gW; j++)
                z[i, j] = spectrum[i * stepY, j * stepX];

        string specLabel = specType switch { 1 => "Фазовый спектр", 2 => "Амплитуда (лин.)", _ => "Амплитуда log(1+|F|)" };
        var plotly = new PlotlyBuilder
        {
            Title  = $"2D FFT — {specLabel}",
            AxisX  = "u (частота, пикс.)",
            AxisY  = "v (частота, пикс.)",
            AxisZ  = specLabel,
            CameraEyeX = 1.4,
            CameraEyeY = 1.4,
            CameraEyeZ = 1.1,
        };
        plotly.AddSurface(xGrid, yGrid, z, specLabel, colorscale: "Jet", showEdges: false);

        string png = SmartPng(FFT2D.NormalizeTo255(spectrum));
        return new DemoResult { PngDataUrl = png, PlotlyJson = plotly.Build(), TextOutput = infoText };
    }

    private static DemoResult FFTFilter(SKBitmap bmp, Matrix gray,
        int filterType, double cutoff, double sigma, double rHigh, int colorMode, FftBackend backend)
    {
        Action<double[,], double[,]> filterAction = filterType switch
        {
            1 => (re, im) => FFT2D.HighPassFilter(re, im, cutoff),
            2 => (re, im) => FFT2D.GaussianFilter(re, im, sigma, lowPass: true),
            3 => (re, im) => FFT2D.GaussianFilter(re, im, sigma, lowPass: false),
            4 => (re, im) => FFT2D.BandPassFilter(re, im, cutoff, rHigh),
            _ => (re, im) => FFT2D.LowPassFilter(re, im, cutoff),
        };

        var sw = Stopwatch.StartNew();
        string png;

        if (colorMode == 1)
        {
            using var result = FFT2D.FilterColor(bmp, filterAction, backend);
            png = BitmapToPng(result);
        }
        else
        {
            var (re, im, _, _) = FFT2D.Forward(gray, backend);
            filterAction(re, im);
            var restored = FFT2D.Inverse(re, im, gray.Height, gray.Width, backend);
            png = SmartPng(restored);
        }

        sw.Stop();
        return new DemoResult { PngDataUrl = png, TextOutput = FftInfoText(backend, sw) };
    }

    private static DemoResult FFTColorChannels(SKBitmap bmp, FftBackend backend)
    {
        var sw = Stopwatch.StartNew();
        var (sR, sG, sB) = FFT2D.ForwardColor(bmp, backend);
        sw.Stop();

        var specR = FFT2D.NormalizeTo255(FFT2D.FFTShift(FFT2D.MagnitudeSpectrum(sR.Re, sR.Im)));
        var specG = FFT2D.NormalizeTo255(FFT2D.FFTShift(FFT2D.MagnitudeSpectrum(sG.Re, sG.Im)));
        var specB = FFT2D.NormalizeTo255(FFT2D.FFTShift(FFT2D.MagnitudeSpectrum(sB.Re, sB.Im)));

        int h = specR.Height, w = specR.Width;
        int totalW = w * 3 + 4;
        var info = new SKImageInfo(totalW, h, SKColorType.Bgra8888);
        using var canvas_bmp = new SKBitmap(info);
        using var canvas = new SKCanvas(canvas_bmp);
        canvas.Clear(new SKColor(20, 24, 32));

        DrawChannelSpectrum(canvas, specR, 0,         h, w, new SKColor(255, 80, 80));
        DrawChannelSpectrum(canvas, specG, w + 2,     h, w, new SKColor(80, 255, 80));
        DrawChannelSpectrum(canvas, specB, 2 * w + 4, h, w, new SKColor(80, 80, 255));

        canvas.Flush();
        string png = BitmapToPng(canvas_bmp);
        return new DemoResult { PngDataUrl = png, TextOutput = FftInfoText(backend, sw) };
    }

    private static void DrawChannelSpectrum(SKCanvas canvas, Matrix spec, int offsetX, int h, int w, SKColor tint)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte v = (byte)Math.Clamp(spec[y, x], 0, 255);
                byte r = (byte)(v * tint.Red / 255);
                byte g = (byte)(v * tint.Green / 255);
                byte b = (byte)(v * tint.Blue / 255);
                using var paint = new SKPaint { Color = new SKColor(r, g, b) };
                canvas.DrawPoint(offsetX + x, y, paint);
            }
    }

    #endregion
}
