using SkiaSharp;

namespace AI.ImageEditor.Pixels;

/// <summary>
/// Буфер пикселей BGRA8888 (unpremultiplied), плоский <see cref="byte"/>[].
/// <para>
/// Почему свой буфер, а не Matrix/Tensor из AI: фильтры редактора работают в горячем
/// пути (пользователь двигает ползунок), и конвертация в double-матрицу по каналам
/// даёт лишние аллокации и кратно больше памяти. Здесь — один линейный массив,
/// stride = Width*4, порядок B,G,R,A.
/// </para>
/// </summary>
public sealed class PixelBuffer
{
    /// <summary>Число байт на пиксель (BGRA).</summary>
    public const int Bpp = 4;

    /// <summary>Формат, в котором работает редактор.</summary>
    public static SKImageInfo InfoFor(int width, int height) =>
        new(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

    /// <summary>Ширина в пикселях.</summary>
    public int Width { get; }

    /// <summary>Высота в пикселях.</summary>
    public int Height { get; }

    /// <summary>Сырые байты BGRA, длина = Width*Height*4.</summary>
    public byte[] Data { get; }

    /// <summary>Длина строки в байтах.</summary>
    public int Stride => Width * Bpp;

    /// <summary>Создаёт пустой (полностью прозрачный) буфер.</summary>
    public PixelBuffer(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Размеры буфера должны быть положительными.");

        Width = width;
        Height = height;
        Data = new byte[width * height * Bpp];
    }

    /// <summary>Оборачивает готовый массив байт (без копирования).</summary>
    public PixelBuffer(int width, int height, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != width * height * Bpp)
            throw new ArgumentException("Размер массива не совпадает с width*height*4.", nameof(data));

        Width = width;
        Height = height;
        Data = data;
    }

    /// <summary>Индекс первого байта пикселя (x, y).</summary>
    public int Index(int x, int y) => (y * Width + x) * Bpp;

    /// <summary>Копия буфера (для фильтров, которым нужен неизменённый источник).</summary>
    public PixelBuffer Clone()
    {
        var copy = new byte[Data.Length];
        Buffer.BlockCopy(Data, 0, copy, 0, Data.Length);
        return new PixelBuffer(Width, Height, copy);
    }

    // ── Мост в SkiaSharp ────────────────────────────────────────────────────

    /// <summary>Читает пиксели из растра, при необходимости перекодируя в BGRA.</summary>
    public static PixelBuffer FromBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var buffer = new PixelBuffer(bitmap.Width, bitmap.Height);

        // Рисуем исходник в растр нужного формата: канва сама приведёт цветовой тип
        // и альфу, каким бы ни был вход (RGBA, premultiplied, индексированный и т.п.).
        using var target = new SKBitmap(InfoFor(bitmap.Width, bitmap.Height));
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(bitmap, 0, 0);
        }

        target.GetPixelSpan().CopyTo(buffer.Data);
        return buffer;
    }

    /// <summary>Создаёт независимый <see cref="SKBitmap"/> с копией текущих пикселей.</summary>
    public SKBitmap ToBitmap()
    {
        var info = InfoFor(Width, Height);
        var bitmap = new SKBitmap(info);

        // Копирующая запись: растр владеет своей памятью, закреплять массив не нужно.
        if (!bitmap.InstallPixels(info, CopyToNative(), Stride, (addr, _) =>
                System.Runtime.InteropServices.Marshal.FreeHGlobal(addr)))
        {
            bitmap.Dispose();
            throw new InvalidOperationException("Не удалось установить пиксели растра.");
        }

        return bitmap;
    }

    /// <summary>Копия данных в неуправляемую память (освобождается растром).</summary>
    private IntPtr CopyToNative()
    {
        var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(Data.Length);
        System.Runtime.InteropServices.Marshal.Copy(Data, 0, ptr, Data.Length);
        return ptr;
    }
}
