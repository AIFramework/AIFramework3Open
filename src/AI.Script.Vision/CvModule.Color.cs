using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using SkiaSharp;

namespace AI.Script.Vision;

/// <summary>
/// Цветные изображения: загрузка, каналы и то, как с ними работают фильтры.
/// </summary>
/// <remarks>
/// Правило для всего пространства одно. Фильтр, который меняет пиксели (размытие, резкость,
/// размер), применяется к каждому каналу, и цвет сохраняется. Признак, который считается по
/// яркости (контуры, гистограмма, текстура), берёт яркость изображения. Так любая функция
/// <c>cv</c> принимает и матрицу, и цветной снимок, а скрипт не пишет разложение на каналы руками.
/// </remarks>
public static partial class CvModule
{
    [ScriptFn("image", "Загружает цветное изображение: каналы red, green, blue",
        Example = "let img = io.load(\"фото.jpg\")",
        Reads = "png,jpg,jpeg,bmp,gif,webp", Returns = ColorImage.TypeName)]
    public static async Task<ScriptHandle> Image(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path,
        [ScriptParam("ширина при загрузке; 0 — как есть")] int width = 0,
        [ScriptParam("высота при загрузке; 0 — как есть")] int height = 0)
    {
        if (await context.Sandbox.InfoAsync(path, context.Cancellation).ConfigureAwait(false) == null)
            throw new ScriptError(DiagnosticCodes.FileNotFound, $"cv.image: файл не найден — {path}");

        byte[] bytes = await context.Sandbox.ReadAsync(path, context.Cancellation).ConfigureAwait(false);

        using SKBitmap bitmap = SKBitmap.Decode(bytes)
            ?? throw new ScriptError(DiagnosticCodes.BadFileFormat, $"cv.image: не удалось прочитать изображение — {path}");

        ColorImage image = ColorImage.From(bitmap);

        if (width > 0 && height > 0) image = image.Map(channel => Resized(channel, width, height));

        context.CountAllocation(3L * image.Width * image.Height);

        return Handle(image);
    }

    [ScriptFn("channel", "Канал цветного изображения матрицей: red, green, blue, gray, hue",
        Example = "img |> cv.channel(\"red\") |> cv.sobel()")]
    public static Matrix Channel(
        [ScriptParam("цветное изображение")] ScriptHandle image,
        [ScriptParam("канал: \"red\", \"green\", \"blue\", \"gray\" либо \"hue\"")] string name)
    {
        ColorImage color = Color(image, "cv.channel");

        return name switch
        {
            "red" => color.Red,
            "green" => color.Green,
            "blue" => color.Blue,
            "gray" => color.Gray(),
            "hue" => Hue(color),
            _ => throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"cv.channel: неизвестный канал '{name}'",
                "известны: \"red\", \"green\", \"blue\", \"gray\", \"hue\""),
        };
    }

    [ScriptFn("merge", "Собирает цветное изображение из трёх каналов",
        Example = "cv.merge(r, g, cv.blur(b))", Returns = ColorImage.TypeName)]
    public static ScriptHandle Merge(
        [ScriptParam("красный канал")] Matrix red,
        [ScriptParam("зелёный канал")] Matrix green,
        [ScriptParam("синий канал")] Matrix blue)
    {
        if (red.Height != green.Height || red.Height != blue.Height || red.Width != green.Width || red.Width != blue.Width)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"cv.merge: каналы разного размера — {red.Height}×{red.Width}, {green.Height}×{green.Width}, {blue.Height}×{blue.Width}");
        }

        return Handle(new ColorImage(red, green, blue));
    }

    /// <summary>
    /// Преобразование пикселей: у цветного изображения — по каналам, у матрицы — как есть.
    /// </summary>
    internal static ScriptValue Pixels(IScriptContext context, ScriptValue image, string what, Func<Matrix, Matrix> change)
    {
        if (image.Type == ScriptType.Handle)
        {
            ColorImage color = Color(image.AsHandle(), what);

            context.CountAllocation(3L * color.Width * color.Height);

            return ScriptValue.Handle(Handle(color.Map(change)));
        }

        Matrix matrix = Brightness(image, what);

        context.CountAllocation((long)matrix.Height * matrix.Width);

        return ScriptValue.Mat(change(matrix));
    }

    /// <summary>Яркость: у цветного изображения — по весам глаза, у матрицы — она сама.</summary>
    internal static Matrix Brightness(ScriptValue image, string what)
    {
        if (image.Type == ScriptType.Handle) return Color(image.AsHandle(), what).Gray();

        if (image.Type == ScriptType.Mat) return image.AsMatrix();

        throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"{what}: ожидались изображение либо матрица яркостей, получено {image.Type.ToName()}",
            "изображение читает io.load либо cv.image, матрицу яркостей — cv.load");
    }

    private static ColorImage Color(ScriptHandle handle, string what) =>
        handle.Target as ColorImage ?? throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"{what}: дескриптор '{handle.TypeName}' — не изображение",
            "изображение читает io.load либо cv.image");

    private static ScriptHandle Handle(ColorImage image) => new(ColorImage.TypeName, image, image.ToString());

    private static Matrix Hue(ColorImage image)
    {
        using SKBitmap bitmap = image.ToBitmap();

        return AI.ComputerVision.ImageMatrixConverter.BmpToHMatr(bitmap);
    }
}
