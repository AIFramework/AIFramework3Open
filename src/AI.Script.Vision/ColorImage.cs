using AI.ComputerVision;
using AI.DataStructs.Algebraic;
using AI.Script.Hosting;
using SkiaSharp;

namespace AI.Script.Vision;

/// <summary>
/// Цветное изображение: каналы красного, зелёного и синего яркостями 0..255.
/// </summary>
/// <remarks>
/// Матрица яркостей цвет не хранит, и притворяться, что хранит, нельзя: снимок, прошедший через
/// фильтр и сохранённый обратно, выходил серым. Каналы остаются матрицами — над каждым работает
/// всё, что умеет язык, — а изображение держит их вместе и собирает обратно при сохранении.
/// </remarks>
public sealed class ColorImage : IScriptArtifactSource, IScriptImageSource
{
    /// <summary>Тип-тег дескриптора в языке.</summary>
    public const string TypeName = "cv.image";

    /// <summary>Создаёт изображение из трёх каналов одного размера.</summary>
    public ColorImage(Matrix red, Matrix green, Matrix blue)
    {
        if (red.Height != green.Height || red.Height != blue.Height || red.Width != green.Width || red.Width != blue.Width)
            throw new ArgumentException("каналы изображения разного размера");

        Red = red;
        Green = green;
        Blue = blue;
    }

    /// <summary>Красный канал.</summary>
    public Matrix Red { get; }

    /// <summary>Зелёный канал.</summary>
    public Matrix Green { get; }

    /// <summary>Синий канал.</summary>
    public Matrix Blue { get; }

    /// <summary>Ширина в пикселях.</summary>
    public int Width => Red.Width;

    /// <summary>Высота в пикселях.</summary>
    public int Height => Red.Height;

    /// <inheritdoc/>
    public string ArtifactKind => "image";

    /// <inheritdoc/>
    public string ArtifactTitle => string.Empty;

    /// <summary>Картинка для хоста: PNG строкой data URL.</summary>
    public object? ArtifactPayload => "data:image/png;base64," + Convert.ToBase64String(Png());

    /// <inheritdoc/>
    public string ImageTitle => string.Empty;

    /// <summary>Читает изображение из растра.</summary>
    public static ColorImage From(SKBitmap bitmap) => new(
        ImageMatrixConverter.BmpToMatrRed(bitmap),
        ImageMatrixConverter.BmpToMatrGreen(bitmap),
        ImageMatrixConverter.BmpToMatrBlue(bitmap));

    /// <summary>Яркость по весам библиотеки: глаз видит зелёный ярче синего.</summary>
    public Matrix Gray()
    {
        using SKBitmap bitmap = ToBitmap();

        return ImageMatrixConverter.BmpToMatr(bitmap);
    }

    /// <summary>То же изображение, где к каждому каналу применено преобразование.</summary>
    public ColorImage Map(Func<Matrix, Matrix> change) => new(change(Red), change(Green), change(Blue));

    /// <summary>Растр изображения; значения каналов обрезаются до 0..255.</summary>
    public SKBitmap ToBitmap()
    {
        var bitmap = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new SKColor[Width * Height];

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
                pixels[(i * Width) + j] = new SKColor(Byte(Red[i, j]), Byte(Green[i, j]), Byte(Blue[i, j]));
        }

        bitmap.Pixels = pixels;

        return bitmap;
    }

    /// <summary>Изображение в PNG.</summary>
    public byte[] Png()
    {
        using SKBitmap bitmap = ToBitmap();
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    /// <inheritdoc/>
    /// <remarks>Снимок не перерисовывается под размер: документ сам впишет его в полосу страницы.</remarks>
    public byte[] RenderPng(int width, int height) => Png();

    /// <inheritdoc/>
    public override string ToString() => $"<image {Width}×{Height}×3>";

    private static byte Byte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
