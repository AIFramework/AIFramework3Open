using AI.DataStructs.Algebraic;
using AI.HighLevelFunctions;
using AI.Statistics;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.ComputerVision;

/// <summary>
/// Конвертирование изображений
/// в разные математические типы
/// и обратно
/// </summary>
public static class ImageMatrixConverter
{
    /// <summary>
    /// Загрузка картинки
    /// </summary>
    /// <param name="path">Имя</param>
    /// <returns>изображение</returns>
    public static SKBitmap GetBitmap(string path)
    {
        return SKBitmap.Decode(path)
            ?? throw new IOException($"Не удалось загрузить изображение: {path}");
    }

    /// <summary>
    /// Загрузить изображение как матрицу
    /// </summary>
    /// <param name="path">Путь до изображения</param>
    public static Matrix LoadAsMatrix(string path)
    {
        using SKBitmap bmp = GetBitmap(path);
        return BmpToMatr(bmp);
    }

    /// <summary>
    /// Загрузить изображение как матрицу
    /// </summary>
    /// <param name="path">Путь до изображения</param>
    /// <param name="colorW">Вектор весов цветов, при расчете серого</param>
    public static Matrix LoadAsMatrix(string path, Vector colorW)
    {
        using SKBitmap bmp = GetBitmap(path);
        return BmpToMatr(bmp, colorW);
    }

    /// <summary>
    /// Загрузить изображение как матрицу (С изменением размера)
    /// </summary>
    /// <param name="path">Путь до изображения</param>
    /// <param name="colorW">Вектор весов цветов, при расчете серого</param>
    /// <param name="width">Новая ширина</param>
    /// <param name="height">Новая высота</param>
    public static Matrix LoadAsMatrix(string path, Vector colorW, int width, int height)
    {
        using SKBitmap src = GetBitmap(path);
        using SKBitmap bmp = src.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        return BmpToMatr(bmp, colorW);
    }

    /// <summary>
    /// Загрузить изображение как тензор 3-го ранга
    /// </summary>
    /// <param name="path">Путь до изображения</param>
    public static Tensor LoadAsTensor(string path)
    {
        using SKBitmap bmp = GetBitmap(path);
        return BmpToTensor(bmp);
    }

    /// <summary>
    /// Получение массива байт (PNG) для сохранения или передачи по сети
    /// </summary>
    /// <param name="bitmap">Изображение</param>
    public static byte[] ImgToByteArray(SKBitmap bitmap)
    {
        using SKImage img = SKImage.FromBitmap(bitmap);
        using SKData data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static double[,,] BaseTransformBmp(SKBitmap bmp)
    {
        int width = bmp.Width, height = bmp.Height;
        double[,,] outp = new double[3, height, width];

        using var bmp32 = EnsureBgra(bmp);
        var span = bmp32.GetPixelSpan();

        for (int h = 0; h < height; h++)
        {
            int rowBase = h * width * 4;
            for (int w = 0; w < width; w++)
            {
                int off = rowBase + w * 4;
                outp[0, h, w] = span[off + 2]; // R (GDI+ compat: outp[0] = first channel = R)
                outp[1, h, w] = span[off + 1]; // G
                outp[2, h, w] = span[off + 0]; // B
            }
        }

        return outp;
    }

    private static SKBitmap EnsureBgra(SKBitmap bmp)
    {
        if (bmp.ColorType == SKColorType.Bgra8888)
            return bmp.Copy();
        return bmp.Copy(SKColorType.Bgra8888);
    }

    private static SKBitmap TensorToBmp(Tensor data)
    {
        int width = data.Width, height = data.Height;
        double[,,] outp = new double[3, height, width];

        for (int i = 0; i < height; i++)
            for (int j = 0; j < width; j++)
            {
                outp[0, i, j] = data[i, j, 0];
                outp[1, i, j] = data[i, j, 1];
                outp[2, i, j] = data[i, j, 2];
            }

        return DbsToBitmap(outp);
    }

    private static SKBitmap DbsToBitmap(double[,,] rgb)
    {
        int width = rgb.GetLength(2), height = rgb.GetLength(1);
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var result = new SKBitmap(info);

        var pixels = result.GetPixels();
        unsafe
        {
            byte* ptr = (byte*)pixels;
            for (int h = 0; h < height; h++)
                for (int w = 0; w < width; w++)
                {
                    int off = (h * width + w) * 4;
                    ptr[off + 0] = Limit(rgb[2, h, w]); // B
                    ptr[off + 1] = Limit(rgb[1, h, w]); // G
                    ptr[off + 2] = Limit(rgb[0, h, w]); // R
                    ptr[off + 3] = 255;                  // A
                }
        }

        return result;
    }

    private static SKBitmap Dbs2DToBitmap(double[,] gray)
    {
        int width = gray.GetLength(1), height = gray.GetLength(0);
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var result = new SKBitmap(info);

        var pixels = result.GetPixels();
        unsafe
        {
            byte* ptr = (byte*)pixels;
            for (int h = 0; h < height; h++)
                for (int w = 0; w < width; w++)
                {
                    byte c = Limit(gray[h, w]);
                    int off = (h * width + w) * 4;
                    ptr[off + 0] = c;   // B
                    ptr[off + 1] = c;   // G
                    ptr[off + 2] = c;   // R
                    ptr[off + 3] = 255; // A
                }
        }

        return result;
    }

    private static byte Limit(double x)
    {
        if (x < 0) return 0;
        if (x > 255) return 255;
        return (byte)x;
    }

    /// <summary>
    /// Преобразование изображения в тензор 3-го ранга
    /// </summary>
    /// <param name="Bmp">Изображение</param>
    public static Tensor BmpToTensor(SKBitmap Bmp)
    {
        Tensor Out = new Tensor(Bmp.Height, Bmp.Width, 3);
        double[,,] d = BaseTransformBmp(Bmp);

        for (int i = 0; i < Bmp.Height; i++)
            for (int j = 0; j < Bmp.Width; j++)
            {
                Out[i, j, 0] = d[0, i, j]; // R
                Out[i, j, 1] = d[1, i, j]; // G
                Out[i, j, 2] = d[2, i, j]; // B
            }

        return Out;
    }

    /// <summary>
    /// Изображение в полутоновую матрицу
    /// </summary>
    /// <param name="Bmp">Изображение</param>
    public static Matrix BmpToMatr(SKBitmap Bmp)
    {
        int W = Bmp.Width, H = Bmp.Height;
        Matrix Out = new Matrix(H, W);
        double[,,] b = BaseTransformBmp(Bmp);

        for (int i = 0; i < W; i++)
            for (int j = 0; j < H; j++)
                Out[j, i] = (b[0, j, i] + b[1, j, i] + b[2, j, i]) / 3.0;

        return Out;
    }

    /// <summary>
    /// Изображение в полутоновую матрицу
    /// </summary>
    /// <param name="Bmp">Изображение</param>
    /// <param name="colorW">Вектор весов цветов, при расчете серого</param>
    public static Matrix BmpToMatr(SKBitmap Bmp, Vector colorW)
    {
        int W = Bmp.Width, H = Bmp.Height;
        Matrix Out = new Matrix(H, W);
        double[,,] b = BaseTransformBmp(Bmp);

        for (int i = 0; i < W; i++)
            for (int j = 0; j < H; j++)
                Out[j, i] = colorW[0] * b[0, j, i]
                          + colorW[1] * b[1, j, i]
                          + colorW[2] * b[2, j, i];

        return Out;
    }

    /// <summary>
    /// Изображение в матрицу синего канала
    /// </summary>
    /// <remarks>
    /// Внутренняя раскладка BaseTransformBmp: b[0]=R, b[1]=G, b[2]=B.
    /// </remarks>
    public static Matrix BmpToMatrBlue(SKBitmap Bmp)
    {
        int W = Bmp.Width, H = Bmp.Height;
        Matrix Out = new Matrix(H, W);
        double[,,] b = BaseTransformBmp(Bmp);

        for (int i = 0; i < W; i++)
            for (int j = 0; j < H; j++)
                Out[j, i] = b[2, j, i];

        return Out;
    }

    /// <summary>
    /// Изображение в матрицу зеленого канала
    /// </summary>
    public static Matrix BmpToMatrGreen(SKBitmap Bmp)
    {
        int W = Bmp.Width, H = Bmp.Height;
        Matrix Out = new Matrix(H, W);
        double[,,] b = BaseTransformBmp(Bmp);

        for (int i = 0; i < W; i++)
            for (int j = 0; j < H; j++)
                Out[j, i] = b[1, j, i];

        return Out;
    }

    /// <summary>
    /// Изображение в матрицу красного канала
    /// </summary>
    public static Matrix BmpToMatrRed(SKBitmap Bmp)
    {
        int W = Bmp.Width, H = Bmp.Height;
        Matrix Out = new Matrix(H, W);
        double[,,] b = BaseTransformBmp(Bmp);

        for (int i = 0; i < W; i++)
            for (int j = 0; j < H; j++)
                Out[j, i] = b[0, j, i];

        return Out;
    }

    /// <summary>
    /// Преобразование картинки в матрицу H компонент.
    /// H принадлежит интервалу [0,1]
    /// </summary>
    public static Matrix BmpToHMatr(SKBitmap Bmp)
    {
        int W = Bmp.Width, H = Bmp.Height;
        Matrix Out = new Matrix(H, W);
        Tensor tensor = BmpToTensor(Bmp);

        for (int i = 0; i < W; i++)
            for (int j = 0; j < H; j++)
                Out[j, i] = HComponent(new int[]
                {
                    (int)(tensor[j, i, 0] * 255.0),
                    (int)(tensor[j, i, 1] * 255.0),
                    (int)(tensor[j, i, 2] * 255.0)
                });

        return Out;
    }

    /// <summary>
    /// Поворот изображения на заданный угол
    /// </summary>
    /// <param name="bmp">Исходное изображение</param>
    /// <param name="angleRotate">Угол поворота (в градусах)</param>
    public static SKBitmap RotateBitmap(SKBitmap bmp, float angleRotate)
    {
        float radians = angleRotate * (float)Math.PI / 180f;
        float sin = (float)Math.Abs(Math.Sin(radians));
        float cos = (float)Math.Abs(Math.Cos(radians));
        int newW = (int)Math.Ceiling(sin * bmp.Height + cos * bmp.Width);
        int newH = (int)Math.Ceiling(sin * bmp.Width + cos * bmp.Height);

        var info = new SKImageInfo(newW, newH, bmp.ColorType, bmp.AlphaType);
        var result = new SKBitmap(info);

        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.White);
        canvas.Translate(newW / 2f, newH / 2f);
        canvas.RotateDegrees(angleRotate);
        canvas.Translate(-bmp.Width / 2f, -bmp.Height / 2f);
        canvas.DrawBitmap(bmp, 0, 0);
        canvas.Flush();

        return result;
    }

    /// <summary>
    /// Вертикальное зеркальное отображение
    /// </summary>
    public static SKBitmap VerticalReflectionBitmap(SKBitmap bmp)
    {
        var result = new SKBitmap(bmp.Info);
        using var canvas = new SKCanvas(result);
        canvas.Scale(1, -1, 0, bmp.Height / 2f);
        canvas.DrawBitmap(bmp, 0, 0);
        canvas.Flush();
        return result;
    }

    /// <summary>
    /// Горизонтальное зеркальное отображение
    /// </summary>
    public static SKBitmap HorizontalReflectionBitmap(SKBitmap bmp)
    {
        var result = new SKBitmap(bmp.Info);
        using var canvas = new SKCanvas(result);
        canvas.Scale(-1, 1, bmp.Width / 2f, 0);
        canvas.DrawBitmap(bmp, 0, 0);
        canvas.Flush();
        return result;
    }

    /// <summary>
    /// Пропорционально изменение размеров с помощью явного указания ширины
    /// </summary>
    public static SKBitmap BmpResizeW(SKBitmap bitmap, int newW)
    {
        double k = (double)newW / bitmap.Width;
        int newH = (int)(bitmap.Height * k);
        return bitmap.Resize(new SKImageInfo(newW, newH), new SKSamplingOptions(SKCubicResampler.Mitchell));
    }

    /// <summary>
    /// Пропорционально изменение размеров с помощью явного указания высоты
    /// </summary>
    public static SKBitmap BmpResizeH(SKBitmap bitmap, int newH)
    {
        double k = (double)newH / bitmap.Height;
        int newW = (int)(bitmap.Width * k);
        return bitmap.Resize(new SKImageInfo(newW, newH), new SKSamplingOptions(SKCubicResampler.Mitchell));
    }

    /// <summary>
    /// Пропорционально изменение размеров (максимальная сторона)
    /// </summary>
    public static SKBitmap BmpResizeM(SKBitmap bitmap, int newM)
    {
        return bitmap.Height > bitmap.Width
            ? BmpResizeH(bitmap, newM)
            : BmpResizeW(bitmap, newM);
    }

    private static double HComponent(int[] rgb)
    {
        int max = rgb.Max();
        int min = rgb.Min();
        int indexMax = Array.IndexOf(rgb, max);
        double d = max - min;

        double H;
        if (d == 0) { H = 0; }
        else if (indexMax == 0)
        {
            double dd = 60.0 / d;
            H = rgb[1] >= rgb[2]
                ? dd * (rgb[1] - rgb[2])
                : dd * (rgb[1] - rgb[2]) + 360;
        }
        else if (indexMax == 1) { H = 60.0 / d * (rgb[2] - rgb[0]) + 120; }
        else { H = 60.0 / d * (rgb[0] - rgb[1]) + 240; }

        return H / 360.0;
    }

    /// <summary>
    /// Вычисление H компоненты
    /// </summary>
    public static double HComponent(SKColor rgb)
    {
        return HComponent(new int[] { rgb.Red, rgb.Green, rgb.Blue });
    }

    private static int BiueInt(double intensiv)
    {
        return 120 / ((int)intensiv + 1);
    }

    private static int RedInt(double intensiv)
    {
        try { return (int)intensiv / 220; }
        catch { return 0; }
    }

    /// <summary>
    /// Визуализация матрицы
    /// </summary>
    public static SKBitmap Visualization(Matrix matr)
    {
        var info = new SKImageInfo(matr.Width, matr.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var bmp = new SKBitmap(info);

        Vector a = matr.Data;
        double max = new Statistic(FunctionsForEachElements.Abs(a)).MaxValue;
        double k = 250.0 / max;

        var pixels = bmp.GetPixels();
        unsafe
        {
            byte* ptr = (byte*)pixels;
            for (int i = 0; i < matr.Height; i++)
                for (int j = 0; j < matr.Width; j++)
                {
                    double intensiv = Math.Abs(k * matr[i, j]);
                    int r = (int)(RedInt(intensiv) * intensiv);
                    int g = (int)(0.2 * intensiv);
                    int b = (int)(BiueInt(intensiv) * intensiv);
                    r = Math.Clamp(r, 0, 255);
                    g = Math.Clamp(g, 0, 255);
                    b = Math.Clamp(b, 0, 255);

                    int off = (i * matr.Width + j) * 4;
                    ptr[off + 0] = (byte)b;
                    ptr[off + 1] = (byte)g;
                    ptr[off + 2] = (byte)r;
                    ptr[off + 3] = 255;
                }
        }

        return bmp;
    }

    /// <summary>
    /// Перевод матрицы в полутоновое изображение
    /// </summary>
    public static SKBitmap ToBitmap(Matrix matrix)
    {
        int width = matrix.Width, height = matrix.Height;
        double[,] gray = new double[height, width];

        for (int i = 0; i < height; i++)
            for (int j = 0; j < width; j++)
                gray[i, j] = matrix[i, j];

        return Dbs2DToBitmap(gray);
    }

    /// <summary>
    /// Тензор в картинку
    /// </summary>
    public static SKBitmap ToBitmap(Tensor tensor)
    {
        return TensorToBmp(tensor);
    }
}
