using AI.ComputerVision;
using AI.DataStructs.Algebraic;
using SkiaSharp;

namespace AiFrameworkDemo.Modules.ComputerVision;

/// <summary>Нормализация матриц и экспорт в PNG data URL (кроссплатформенно через SkiaSharp).</summary>
public static class CvImageHelper
{
    public static string MatrixToPngDataUrl(Matrix matrix)
    {
        using var bmp = ImageMatrixConverter.ToBitmap(matrix);
        return BitmapToPngDataUrl(bmp);
    }

    public static string BitmapToPngDataUrl(SKBitmap bmp)
    {
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
    }

    public static SKBitmap ResizeToFit(SKBitmap bmp, int maxWidth, int maxHeight)
    {
        if (bmp.Width <= maxWidth && bmp.Height <= maxHeight)
            return bmp.Copy();

        double ratioX = (double)maxWidth / bmp.Width;
        double ratioY = (double)maxHeight / bmp.Height;
        double ratio = Math.Min(ratioX, ratioY);

        int newW = Math.Max(1, (int)(bmp.Width * ratio));
        int newH = Math.Max(1, (int)(bmp.Height * ratio));

        return bmp.Resize(new SKImageInfo(newW, newH), new SKSamplingOptions(SKCubicResampler.Mitchell));
    }

    /// <summary>Растяжение значений в [0, 255].</summary>
    public static Matrix NormalizeTo255(Matrix m)
    {
        if (m is null) throw new ArgumentNullException(nameof(m));

        double min = double.PositiveInfinity;
        double max = double.NegativeInfinity;
        for (int i = 0; i < m.Height; i++)
            for (int j = 0; j < m.Width; j++)
            {
                double v = m[i, j];
                if (v < min) min = v;
                if (v > max) max = v;
            }

        if (max <= min) return new Matrix(m.Height, m.Width);

        var r = new Matrix(m.Height, m.Width);
        double scale = 255.0 / (max - min);
        for (int i = 0; i < m.Height; i++)
            for (int j = 0; j < m.Width; j++)
                r[i, j] = (m[i, j] - min) * scale;

        return r;
    }

    /// <summary>Auto-scale to 0…255 for display.</summary>
    public static string MatrixToPngDataUrlSmart(Matrix m)
    {
        if (m is null) throw new ArgumentNullException(nameof(m));

        double min = double.PositiveInfinity;
        double max = double.NegativeInfinity;
        for (int i = 0; i < m.Height; i++)
            for (int j = 0; j < m.Width; j++)
            {
                double v = m[i, j];
                if (v < min) min = v;
                if (v > max) max = v;
            }

        if (max <= 1.0001 && min >= 0 && max > 0)
            return MatrixToPngDataUrl(m * 255.0);
        if (min < 0 || max > 255)
            return MatrixToPngDataUrl(NormalizeTo255(m));
        return MatrixToPngDataUrl(m);
    }

    /// <summary>Вектор как горизонтальная гистограмма.</summary>
    public static string VectorToBarChartPngDataUrl(Vector v, int width = 480, int height = 200, string? title = null)
    {
        if (v is null || v.Count == 0)
            return PlaceholderPngDataUrl("пустой вектор", width, height);

        int n = v.Count;
        int show = Math.Min(400, n);
        int step = n > show ? n / show : 1;

        var vals = new double[show];
        for (int i = 0; i < show; i++)
        {
            int j = i * step;
            if (j >= n) j = n - 1;
            vals[i] = v[j];
        }

        double vmax = 0.0, vmin = 0.0;
        for (int i = 0; i < show; i++)
        {
            if (vals[i] > vmax) vmax = vals[i];
            if (vals[i] < vmin) vmin = vals[i];
        }
        if (vmax < vmin) (vmin, vmax) = (vmax, vmin);
        double range = Math.Max(1e-9, Math.Max(Math.Abs(vmax - vmin), Math.Max(Math.Abs(vmax), Math.Abs(vmin))));

        int topPad = string.IsNullOrEmpty(title) ? 8 : 32;
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888);
        using var bmp = new SKBitmap(info);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(new SKColor(24, 27, 36));

        if (!string.IsNullOrEmpty(title))
        {
            using var font = new SKFont(SKTypeface.Default, 12);
            using var paint = new SKPaint { Color = SKColors.Gainsboro, IsAntialias = true };
            canvas.DrawText(title, 8, 16, SKTextAlign.Left, font, paint);
        }

        int barW = Math.Max(1, (width - 16) / show);
        int baseY = height - 8;
        int plotH = height - topPad - 8;
        using var barPaint = new SKPaint { Color = new SKColor(100, 180, 255), IsAntialias = false };

        for (int i = 0; i < show; i++)
        {
            int bh = (int)(plotH * (Math.Abs(vals[i]) / range));
            bh = Math.Min(bh, plotH);
            int x = 8 + i * barW;
            canvas.DrawRect(x, baseY - bh, barW - 1, bh, barPaint);
        }

        canvas.Flush();
        return BitmapToPngDataUrl(bmp);
    }

    public static string PlaceholderPngDataUrl(string line, int w = 400, int h = 80)
    {
        var info = new SKImageInfo(w, h, SKColorType.Bgra8888);
        using var bmp = new SKBitmap(info);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(new SKColor(32, 36, 48));

        using var font = new SKFont(SKTypeface.Default, 13);
        using var paint = new SKPaint { Color = new SKColor(192, 192, 192), IsAntialias = true };
        canvas.DrawText(line, 8, h / 2f + 5, SKTextAlign.Left, font, paint);
        canvas.Flush();
        return BitmapToPngDataUrl(bmp);
    }

    public static string TextOverlayPngDataUrl(string title, int count, int w, int h)
    {
        var info = new SKImageInfo(w, h, SKColorType.Bgra8888);
        using var bmp = new SKBitmap(info);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(new SKColor(20, 24, 32));

        using var tf = new SKFont(SKTypeface.Default, 15) { Embolden = true };
        using var tp = new SKPaint { Color = SKColors.Gainsboro, IsAntialias = true };
        canvas.DrawText(title, 12, 28, SKTextAlign.Left, tf, tp);

        using var vf = new SKFont(SKTypeface.Default, 22);
        using var vp = new SKPaint { Color = new SKColor(70, 130, 180), IsAntialias = true };
        canvas.DrawText(count.ToString(), 12, 60, SKTextAlign.Left, vf, vp);
        canvas.Flush();
        return BitmapToPngDataUrl(bmp);
    }
}
