using AI.ImageEditor.Pixels;

namespace AI.ImageEditor.Filters;

/// <summary>
/// Быстрые свёрточные примитивы. Вынесены отдельно, потому что переиспользуются
/// несколькими фильтрами (шумоподавление, свечение, ретинекс, нерезкая маска).
/// </summary>
public static class Convolution
{
    /// <summary>
    /// Порог, выше которого точная свёртка уступает по скорости box-аппроксимации.
    /// </summary>
    private const double BoxApproxSigma = 3.0;

    /// <summary>
    /// Гауссово размытие. Две стратегии, обе быстрые:
    /// <list type="bullet">
    /// <item>малая сигма — точная <b>сепарабельная</b> свёртка, O(W·H·k);</item>
    /// <item>большая сигма — три прохода box-blur со скользящим окном, O(W·H)
    /// <b>независимо от радиуса</b> (классическая аппроксимация гаусса).</item>
    /// </list>
    /// Без второй ветки ретинекс с sigma=24 давал ядро в 145 отсчётов и сотни
    /// миллисекунд — неприемлемо для интерактива. Альфа не изменяется.
    /// </summary>
    public static void GaussianBlur(PixelBuffer buffer, double sigma)
    {
        if (sigma <= 0.05) return;

        if (sigma > BoxApproxSigma)
        {
            BoxBlurApprox(buffer, sigma);
            return;
        }

        var kernel = BuildGaussianKernel(sigma);
        var radius = kernel.Length / 2;

        var tmp = new byte[buffer.Data.Length];
        BlurPass(buffer.Data, tmp, buffer.Width, buffer.Height, kernel, radius, horizontal: true);
        BlurPass(tmp, buffer.Data, buffer.Width, buffer.Height, kernel, radius, horizontal: false);
    }

    /// <summary>
    /// Аппроксимация гаусса тремя box-blur. Радиусы подбираются так, чтобы
    /// суммарная дисперсия совпала с заданной сигмой (метод Кутскира/W3C).
    /// </summary>
    private static void BoxBlurApprox(PixelBuffer buffer, double sigma)
    {
        var sizes = BoxSizesForGauss(sigma, 3);
        var tmp = new byte[buffer.Data.Length];

        foreach (var size in sizes)
        {
            var radius = (size - 1) / 2;
            if (radius < 1) continue;

            BoxPass(buffer.Data, tmp, buffer.Width, buffer.Height, radius, horizontal: true);
            BoxPass(tmp, buffer.Data, buffer.Width, buffer.Height, radius, horizontal: false);
        }
    }

    /// <summary>Размеры окон box-blur, дающие в сумме нужную гауссову дисперсию.</summary>
    private static int[] BoxSizesForGauss(double sigma, int n)
    {
        var wIdeal = Math.Sqrt(12.0 * sigma * sigma / n + 1);
        var wl = (int)Math.Floor(wIdeal);
        if (wl % 2 == 0) wl--;
        var wu = wl + 2;

        var mIdeal = (12.0 * sigma * sigma - n * wl * wl - 4.0 * n * wl - 3.0 * n) / (-4.0 * wl - 4.0);
        var m = (int)Math.Round(mIdeal);

        var sizes = new int[n];
        for (var i = 0; i < n; i++) sizes[i] = i < m ? wl : wu;
        return sizes;
    }

    /// <summary>
    /// Один проход box-blur со скользящим окном: сумма обновляется добавлением
    /// входящего и вычитанием уходящего пикселя — по 3 операции на канал, сколько
    /// бы ни был велик радиус.
    /// </summary>
    private static void BoxPass(byte[] src, byte[] dst, int width, int height, int radius, bool horizontal)
    {
        var lineCount = horizontal ? height : width;
        var lineLength = horizontal ? width : height;
        var window = radius * 2 + 1;

        for (var line = 0; line < lineCount; line++)
        {
            int Idx(int pos) => horizontal
                ? (line * width + pos) * PixelBuffer.Bpp
                : (pos * width + line) * PixelBuffer.Bpp;

            // Инициализация окна с зажимом краёв.
            int sb = 0, sg = 0, sr = 0;
            for (var k = -radius; k <= radius; k++)
            {
                var i = Idx(Math.Clamp(k, 0, lineLength - 1));
                sb += src[i]; sg += src[i + 1]; sr += src[i + 2];
            }

            for (var pos = 0; pos < lineLength; pos++)
            {
                var o = Idx(pos);
                dst[o] = (byte)(sb / window);
                dst[o + 1] = (byte)(sg / window);
                dst[o + 2] = (byte)(sr / window);
                dst[o + 3] = src[o + 3];

                // Сдвиг окна: +входящий, −уходящий.
                var iIn = Idx(Math.Clamp(pos + radius + 1, 0, lineLength - 1));
                var iOut = Idx(Math.Clamp(pos - radius, 0, lineLength - 1));
                sb += src[iIn] - src[iOut];
                sg += src[iIn + 1] - src[iOut + 1];
                sr += src[iIn + 2] - src[iOut + 2];
            }
        }
    }

    /// <summary>Нормированное одномерное гауссово ядро.</summary>
    private static double[] BuildGaussianKernel(double sigma)
    {
        var radius = Math.Max(1, (int)Math.Ceiling(sigma * 3));
        var size = radius * 2 + 1;
        var kernel = new double[size];
        var twoSigmaSq = 2 * sigma * sigma;
        double sum = 0;

        for (var i = 0; i < size; i++)
        {
            var d = i - radius;
            kernel[i] = Math.Exp(-(d * d) / twoSigmaSq);
            sum += kernel[i];
        }

        for (var i = 0; i < size; i++) kernel[i] /= sum;
        return kernel;
    }

    /// <summary>Один проход размытия вдоль оси (края — зажим ближайшего пикселя).</summary>
    private static void BlurPass(byte[] src, byte[] dst, int width, int height,
        double[] kernel, int radius, bool horizontal)
    {
        var lineCount = horizontal ? height : width;
        var lineLength = horizontal ? width : height;

        for (var line = 0; line < lineCount; line++)
        {
            for (var pos = 0; pos < lineLength; pos++)
            {
                double b = 0, g = 0, r = 0;

                for (var k = -radius; k <= radius; k++)
                {
                    var p = Math.Clamp(pos + k, 0, lineLength - 1);   // зажим на краях
                    var idx = horizontal
                        ? (line * width + p) * PixelBuffer.Bpp
                        : (p * width + line) * PixelBuffer.Bpp;

                    var w = kernel[k + radius];
                    b += src[idx] * w;
                    g += src[idx + 1] * w;
                    r += src[idx + 2] * w;
                }

                var o = horizontal
                    ? (line * width + pos) * PixelBuffer.Bpp
                    : (pos * width + line) * PixelBuffer.Bpp;

                dst[o] = ToByte(b);
                dst[o + 1] = ToByte(g);
                dst[o + 2] = ToByte(r);
                dst[o + 3] = src[o + 3];   // альфа проходит насквозь
            }
        }
    }

    /// <summary>
    /// Свёртка ядром 3×3 по каналам RGB (края — зажим). Используется для резкости
    /// и тиснения: ядра совпадают с теми, что заданы в AI.ComputerVision.
    /// </summary>
    public static void Convolve3x3(PixelBuffer buffer, double[] kernel, double bias = 0)
    {
        if (kernel.Length != 9) throw new ArgumentException("Ядро должно быть 3×3.", nameof(kernel));

        var src = buffer.Clone().Data;
        var dst = buffer.Data;
        int w = buffer.Width, h = buffer.Height;

        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            double b = 0, g = 0, r = 0;

            for (var ky = -1; ky <= 1; ky++)
            for (var kx = -1; kx <= 1; kx++)
            {
                var sx = Math.Clamp(x + kx, 0, w - 1);
                var sy = Math.Clamp(y + ky, 0, h - 1);
                var idx = (sy * w + sx) * PixelBuffer.Bpp;
                var k = kernel[(ky + 1) * 3 + (kx + 1)];

                b += src[idx] * k;
                g += src[idx + 1] * k;
                r += src[idx + 2] * k;
            }

            var o = (y * w + x) * PixelBuffer.Bpp;
            dst[o] = ToByte(b + bias);
            dst[o + 1] = ToByte(g + bias);
            dst[o + 2] = ToByte(r + bias);
        }
    }

    /// <summary>Ограничение в диапазон байта.</summary>
    public static byte ToByte(double v) => (byte)(v <= 0 ? 0 : v >= 255 ? 255 : v + 0.5);
}

/// <summary>
/// Повышение резкости. Ядро взято из <c>Sharpness</c> (AI.ComputerVision):
/// все −1, центр 8+sharp — сумма равна <c>sharp</c>, поэтому яркость сохраняется.
/// </summary>
public sealed class SharpenFilter : IImageFilter
{
    private readonly double[] _kernel;

    /// <summary>Создаёт фильтр резкости.</summary>
    public SharpenFilter(double sharp = 1.0)
    {
        var s = Math.Clamp(sharp, 0.1, 5.0);
        // Нормируем на сумму (= s), иначе изображение уезжает по яркости.
        _kernel = new double[9];
        for (var i = 0; i < 9; i++) _kernel[i] = -1.0 / s;
        _kernel[4] = (8.0 + s) / s;
    }

    /// <inheritdoc />
    public void Apply(PixelBuffer buffer) => Convolution.Convolve3x3(buffer, _kernel);
}

/// <summary>
/// Мягкое шумоподавление гауссовым размытием (портирован <c>GaussianBlurFilter</c>,
/// но сепарабельный и с настраиваемым радиусом).
/// </summary>
public sealed class DenoiseFilter : IImageFilter
{
    private readonly double _sigma;

    /// <summary>Создаёт фильтр шумоподавления.</summary>
    public DenoiseFilter(double strength = 1.0) => _sigma = Math.Clamp(strength, 0.1, 10.0);

    /// <inheritdoc />
    public void Apply(PixelBuffer buffer) => Convolution.GaussianBlur(buffer, _sigma);
}

/// <summary>Простое размытие (аналог <c>Smoothing</c>), радиус в пикселях.</summary>
public sealed class BlurFilter : IImageFilter
{
    private readonly double _sigma;

    /// <summary>Создаёт фильтр размытия.</summary>
    public BlurFilter(double radius = 2.0) => _sigma = Math.Clamp(radius, 0.1, 40.0);

    /// <inheritdoc />
    public void Apply(PixelBuffer buffer) => Convolution.GaussianBlur(buffer, _sigma);
}

/// <summary>
/// Медианный фильтр 3×3 — убирает импульсный шум («соль и перец»), который
/// гауссово размытие только размазывает. Сортировка сетью для 9 элементов.
/// </summary>
public sealed class MedianFilter : IImageFilter
{
    /// <inheritdoc />
    public void Apply(PixelBuffer buffer)
    {
        var src = buffer.Clone().Data;
        var dst = buffer.Data;
        int w = buffer.Width, h = buffer.Height;

        Span<byte> window = stackalloc byte[9];

        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var o = (y * w + x) * PixelBuffer.Bpp;

            for (var c = 0; c < 3; c++)   // B, G, R — альфу не трогаем
            {
                var n = 0;
                for (var ky = -1; ky <= 1; ky++)
                for (var kx = -1; kx <= 1; kx++)
                {
                    var sx = Math.Clamp(x + kx, 0, w - 1);
                    var sy = Math.Clamp(y + ky, 0, h - 1);
                    window[n++] = src[(sy * w + sx) * PixelBuffer.Bpp + c];
                }

                dst[o + c] = Median9(window);
            }
        }
    }

    /// <summary>Медиана девяти байт вставками — для n=9 быстрее generic-сортировки.</summary>
    private static byte Median9(Span<byte> v)
    {
        for (var i = 1; i < 9; i++)
        {
            var key = v[i];
            var j = i - 1;
            while (j >= 0 && v[j] > key) { v[j + 1] = v[j]; j--; }
            v[j + 1] = key;
        }
        return v[4];
    }
}

/// <summary>
/// Выделение границ оператором Собеля (портирован <c>SobelTransform</c>):
/// модуль градиента по яркости.
/// </summary>
public sealed class SobelEdgeFilter : IImageFilter
{
    private static readonly int[] Gx = [-1, 0, 1, -2, 0, 2, -1, 0, 1];
    private static readonly int[] Gy = [-1, -2, -1, 0, 0, 0, 1, 2, 1];

    /// <inheritdoc />
    public void Apply(PixelBuffer buffer)
    {
        var src = buffer.Clone().Data;
        var dst = buffer.Data;
        int w = buffer.Width, h = buffer.Height;

        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            double sx = 0, sy = 0;

            for (var ky = -1; ky <= 1; ky++)
            for (var kx = -1; kx <= 1; kx++)
            {
                var px = Math.Clamp(x + kx, 0, w - 1);
                var py = Math.Clamp(y + ky, 0, h - 1);
                var idx = (py * w + px) * PixelBuffer.Bpp;

                // Яркость Rec.601 — градиент считаем по ней, а не по трём каналам.
                var lum = (src[idx + 2] * 299 + src[idx + 1] * 587 + src[idx] * 114) / 1000.0;
                var k = (ky + 1) * 3 + (kx + 1);
                sx += lum * Gx[k];
                sy += lum * Gy[k];
            }

            var g = Convolution.ToByte(Math.Sqrt(sx * sx + sy * sy));
            var o = (y * w + x) * PixelBuffer.Bpp;
            dst[o] = dst[o + 1] = dst[o + 2] = g;
        }
    }
}
