using SkiaSharp;
using System;

namespace AI.ComputerVision.UInt8;

/// <summary>
/// Изображение серое UInt16
/// </summary>
[Serializable]
public class ImgUInt16Gray
{
    /// <summary>
    /// Изображение
    /// </summary>
    public short[,] img;
    /// <summary>
    /// Ширина
    /// </summary>
    public readonly int Width;
    /// <summary>
    /// Высота
    /// </summary>
    public readonly int Height;

    /// <summary>
    /// Доступ к пикселам
    /// </summary>
    public short this[int i, int j]
    {
        get => img[i, j];
        set => img[i, j] = value;
    }

    /// <summary>
    /// Создание черного изображения указанных размеров
    /// </summary>
    public ImgUInt16Gray(int h, int w)
    {
        img = new short[h, w];
        Height = h;
        Width = w;
    }

    /// <summary>
    /// Загрузка картинки (с переводом в чб)
    /// </summary>
    public ImgUInt16Gray(SKBitmap bitmap)
    {
        Width = bitmap.Width;
        Height = bitmap.Height;
        img = new short[Height, Width];

        using var bmp32 = bitmap.ColorType == SKColorType.Bgra8888
            ? bitmap.Copy()
            : bitmap.Copy(SKColorType.Bgra8888);

        var span = bmp32.GetPixelSpan();

        for (int j = 0; j < Height; j++)
        {
            int rowBase = j * Width * 4;
            for (int k = 0; k < Width; k++)
            {
                int off = rowBase + k * 4;
                int b = span[off + 0];
                int g = span[off + 1];
                int r = span[off + 2];
                img[j, k] = (short)((r + g + b) / 3);
            }
        }
    }

    /// <summary>
    /// Сумма
    /// </summary>
    public static ImgUInt16Gray operator +(ImgUInt16Gray img, int k)
    {
        ImgUInt16Gray outp = new ImgUInt16Gray(img.Height, img.Width);
        for (int i = 0; i < img.Height; i++)
            for (int j = 0; j < img.Width; j++)
                outp[i, j] = (short)(img[i, j] + k);
        return outp;
    }

    /// <summary>
    /// Разность
    /// </summary>
    public static ImgUInt16Gray operator -(ImgUInt16Gray img, int k)
    {
        ImgUInt16Gray outp = new ImgUInt16Gray(img.Height, img.Width);
        for (int i = 0; i < img.Height; i++)
            for (int j = 0; j < img.Width; j++)
                outp[i, j] = (short)(img[i, j] - k);
        return outp;
    }

    /// <summary>
    /// Сумма
    /// </summary>
    public static ImgUInt16Gray operator +(int k, ImgUInt16Gray img)
    {
        ImgUInt16Gray outp = new ImgUInt16Gray(img.Height, img.Width);
        for (int i = 0; i < img.Height; i++)
            for (int j = 0; j < img.Width; j++)
                outp[i, j] = (short)(img[i, j] + k);
        return outp;
    }

    /// <summary>
    /// Разность
    /// </summary>
    public static ImgUInt16Gray operator -(int k, ImgUInt16Gray img)
    {
        ImgUInt16Gray outp = new ImgUInt16Gray(img.Height, img.Width);
        for (int i = 0; i < img.Height; i++)
            for (int j = 0; j < img.Width; j++)
                outp[i, j] = (short)(k - img[i, j]);
        return outp;
    }

    /// <summary>
    /// Умножение
    /// </summary>
    public static ImgUInt16Gray operator *(ImgUInt16Gray img, double k)
    {
        ImgUInt16Gray outp = new ImgUInt16Gray(img.Height, img.Width);
        for (int i = 0; i < img.Height; i++)
            for (int j = 0; j < img.Width; j++)
                outp[i, j] = (short)(img[i, j] * k);
        return outp;
    }

    /// <summary>
    /// Деление
    /// </summary>
    public static ImgUInt16Gray operator /(ImgUInt16Gray img, double k)
    {
        ImgUInt16Gray outp = new ImgUInt16Gray(img.Height, img.Width);
        for (int i = 0; i < img.Height; i++)
            for (int j = 0; j < img.Width; j++)
                outp[i, j] = (short)(img[i, j] / k);
        return outp;
    }

    /// <summary>
    /// Деление (int)
    /// </summary>
    public static ImgUInt16Gray operator /(ImgUInt16Gray img, int k)
    {
        ImgUInt16Gray outp = new ImgUInt16Gray(img.Height, img.Width);
        for (int i = 0; i < img.Height; i++)
            for (int j = 0; j < img.Width; j++)
                outp[i, j] = (short)(img[i, j] / k);
        return outp;
    }

    /// <summary>
    /// Умножение (k * img)
    /// </summary>
    public static ImgUInt16Gray operator *(double k, ImgUInt16Gray img)
    {
        ImgUInt16Gray outp = new ImgUInt16Gray(img.Height, img.Width);
        for (int i = 0; i < img.Height; i++)
            for (int j = 0; j < img.Width; j++)
                outp[i, j] = (short)(img[i, j] * k);
        return outp;
    }

    /// <summary>
    /// Деление (k / img)
    /// </summary>
    public static ImgUInt16Gray operator /(double k, ImgUInt16Gray img)
    {
        ImgUInt16Gray outp = new ImgUInt16Gray(img.Height, img.Width);
        for (int i = 0; i < img.Height; i++)
            for (int j = 0; j < img.Width; j++)
                outp[i, j] = (short)(k / img[i, j]);
        return outp;
    }

    /// <summary>
    /// Перевод изображения в SKBitmap
    /// </summary>
    public SKBitmap ToBitmap()
    {
        var info = new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var bmp = new SKBitmap(info);
        var pixels = bmp.GetPixels();

        unsafe
        {
            byte* ptr = (byte*)pixels;
            for (int i = 0; i < Height; i++)
                for (int j = 0; j < Width; j++)
                {
                    short d = img[i, j];
                    byte c = (byte)Math.Clamp(d, (short)0, (short)255);
                    int off = (i * Width + j) * 4;
                    ptr[off + 0] = c;   // B
                    ptr[off + 1] = c;   // G
                    ptr[off + 2] = c;   // R
                    ptr[off + 3] = 255; // A
                }
        }

        return bmp;
    }
}
