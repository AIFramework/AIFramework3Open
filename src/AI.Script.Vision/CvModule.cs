using AI.ComputerVision;
using AI.ComputerVision.FrequencyDomain;
using AI.ComputerVision.ImgTransforms;
using AI.ComputerVision.SpatialFilters;
using AI.ComputerVision.Statistics;
using AI.DataStructs.Algebraic;
using AI.Extensions;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using SkiaSharp;

namespace AI.Script.Vision;

/// <summary>
/// Пространство <c>cv</c>: обработка изображений.
/// </summary>
/// <remarks>
/// Изображение здесь — обычная матрица яркостей, а не отдельный тип языка. Это не упрощение
/// ради упрощения: матрицу уже умеют печатать, резать, нормировать и рисовать тепловой картой,
/// и всё это работает над картинкой без единой новой функции. Цветное изображение
/// раскладывается на каналы при загрузке — цвет в матрицу не помещается, и притворяться, что
/// помещается, было бы хуже, чем сказать об этом прямо.
/// <para>
/// Значения яркости — от 0 до 255, как их отдаёт библиотека. Приводить их к отрезку [0, 1]
/// самовольно значило бы рассогласовать язык с той библиотекой, которую он показывает.
/// </para>
/// </remarks>
[ScriptModule("cv", "Изображения матрицей яркостей 0..255: фильтры, контуры, спектр, признаки", Version = "0.1")]
public static class CvModule
{
    // --- ввод и вывод ---

    /// <summary>
    /// Загружает изображение матрицей яркостей.
    /// </summary>
    /// <remarks>
    /// Путь проходит через песочницу прогона, как и у <c>io</c>: картинка — такой же файл, и
    /// отдельная дверь для неё означала бы, что запрет читать вне рабочей папки соблюдается
    /// через раз.
    /// </remarks>
    [ScriptFn("load", "Загружает изображение матрицей яркостей 0..255",
        Example = "let img = cv.load(\"photo.png\", width: 256, height: 256)")]
    public static Matrix Load(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path,
        [ScriptParam("канал: \"gray\", \"red\", \"green\", \"blue\" либо \"hue\"")] string channel = "gray",
        [ScriptParam("ширина при загрузке; 0 — как есть")] int width = 0,
        [ScriptParam("высота при загрузке; 0 — как есть")] int height = 0)
    {
        string full = context.Sandbox.Resolve(path, forWriting: false);

        if (!File.Exists(full))
            throw new ScriptError(DiagnosticCodes.FileNotFound, $"cv.load: файл не найден — {path}");

        using SKBitmap bitmap = ImageMatrixConverter.GetBitmap(full);

        Matrix image = Channel(bitmap, channel, "cv.load");

        if (width > 0 && height > 0) image = Resized(image, width, height);

        context.CountAllocation((long)image.Height * image.Width);

        return image;
    }

    [ScriptFn("save", "Сохраняет матрицу яркостей изображением; возвращает путь",
        Example = "cv.save(img, \"result.png\")")]
    public static string Save(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image,
        [ScriptParam("путь относительно рабочей папки")] string path)
    {
        string full = context.Sandbox.Resolve(path, forWriting: true);

        using SKBitmap bitmap = ImageMatrixConverter.ToBitmap(image);
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.Create(full);

        data.SaveTo(stream);

        return path;
    }

    [ScriptFn("resize", "Меняет размер изображения", Example = "cv.resize(img, width: 128, height: 128)")]
    public static Matrix Resize(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image,
        [ScriptParam("ширина")] int width,
        [ScriptParam("высота")] int height)
    {
        if (width < 1 || height < 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "cv.resize: размеры должны быть положительны");

        context.CountAllocation((long)width * height);

        return Resized(image, width, height);
    }

    /// <summary>
    /// Приводит яркости к отрезку 0..255.
    /// </summary>
    /// <remarks>
    /// Нужна после спектра и после фильтров, растягивающих диапазон: сохранённая без
    /// приведения картинка выйдет чёрной или белой целиком, а разбираться в этом будут долго.
    /// </remarks>
    [ScriptFn("normalize", "Растягивает яркости на отрезок 0..255", Example = "cv.normalize(spectrum)")]
    public static Matrix Normalize(
        [ScriptParam("матрица яркостей")] Matrix image) => FFT2D.NormalizeTo255(image);

    // --- фильтрация ---

    [ScriptFn("filter", "Свёртка изображения с заданным ядром", Example = "cv.filter(img, kernel: k)")]
    public static Matrix Filter(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image,
        [ScriptParam("ядро свёртки")] Matrix kernel)
    {
        context.CountAllocation((long)image.Height * image.Width);

        return ImgFilters.SpatialFilter(image, kernel);
    }

    [ScriptFn("smooth", "Сглаживание изображения", Example = "cv.smooth(img)")]
    public static Matrix Smooth(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image)
    {
        context.CountAllocation((long)image.Height * image.Width);

        return new Smoothing().Filtration(image);
    }

    [ScriptFn("blur", "Гауссово размытие", Example = "cv.blur(img)")]
    public static Matrix Blur(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image)
    {
        context.CountAllocation((long)image.Height * image.Width);

        return new GaussianBlurFilter().Filtration(image);
    }

    [ScriptFn("sharpen", "Повышение резкости", Example = "cv.sharpen(img, amount: 1.5)")]
    public static Matrix Sharpen(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image,
        [ScriptParam("сила повышения")] double amount = 1)
    {
        context.CountAllocation((long)image.Height * image.Width);

        return new Sharpness(amount).Filtration(image);
    }

    /// <summary>
    /// Медианный фильтр.
    /// </summary>
    /// <remarks>
    /// Единственный из здешних фильтров, который убирает импульсный шум, не размазывая
    /// границы: усредняющий на точке-выбросе размажет и её, и всё вокруг.
    /// </remarks>
    [ScriptFn("median", "Медианный фильтр: убирает точечный шум, сохраняя границы",
        Example = "cv.median(img, size: 5)")]
    public static Matrix Median(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image,
        [ScriptParam("размер окна, нечётный")] int size = 3)
    {
        RequireOddWindow(size, "cv.median");

        context.CountAllocation((long)image.Height * image.Width);

        return ImgFilters.MedianFilter(image, size, size);
    }

    [ScriptFn("texture", "Локальное среднеквадратичное отклонение: мера зернистости",
        Example = "cv.texture(img, size: 5)")]
    public static Matrix Texture(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image,
        [ScriptParam("размер окна, нечётный")] int size = 3)
    {
        RequireOddWindow(size, "cv.texture");

        context.CountAllocation((long)image.Height * image.Width);

        return ImgFilters.StdFilter(image, Ones(size));
    }

    // --- признаки ---

    /// <summary>
    /// Контуры оператором Собеля.
    /// </summary>
    /// <remarks>
    /// Возвращаются и модуль градиента, и его направление: по одному модулю нельзя отличить
    /// вертикальную границу от горизонтальной, а именно это и нужно тому, кто ищет разметку,
    /// царапину или шов.
    /// </remarks>
    [ScriptFn("sobel", "Контуры: модуль и направление градиента", Example = "let edges = cv.sobel(img)")]
    public static ScriptRecord Sobel(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image)
    {
        SobelData data = new SobelTransform().Transform(image);

        context.CountAllocation((long)image.Height * image.Width * 4);

        return ScriptRecord.From(
        [
            new("edges", ScriptValue.Mat(data.GradImg)),
            new("direction", ScriptValue.Mat(data.PhGrad)),
            new("gx", ScriptValue.Mat(data.GradX)),
            new("gy", ScriptValue.Mat(data.GradY)),
        ]);
    }

    [ScriptFn("hog", "Гистограмма направленных градиентов: вектор признаков",
        Example = "let features = cv.hog(img, bins: 9)")]
    public static Vector Hog(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image,
        [ScriptParam("число направлений")] int bins = 8,
        [ScriptParam("нормировать результат")] bool normalize = true)
    {
        if (bins < 2) throw new ScriptError(DiagnosticCodes.BadOperand, "cv.hog: направлений должно быть хотя бы два");

        context.CountAllocation(bins);

        return new HOG(bins).CalcHist(image, normalize);
    }

    [ScriptFn("histogram", "Гистограмма яркостей изображения", Example = "show plot.bar(cv.histogram(img))")]
    public static Vector Histogram(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image)
    {
        context.CountAllocation(256);

        return ImageHistogram.GetHistogram(image);
    }

    [ScriptFn("equalize", "Выравнивание гистограммы: повышает контраст", Example = "cv.equalize(img)")]
    public static Matrix Equalize(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image)
    {
        context.CountAllocation((long)image.Height * image.Width);

        return ImageHistogram.Equalize(image);
    }

    /// <summary>
    /// Порогование в чёрно-белое.
    /// </summary>
    /// <remarks>
    /// Результат — матрица нулей и единиц, а не 0 и 255: над ней сразу работают <c>vec.sum</c>
    /// и <c>stat.mean</c>, и доля покрытия считается без пересчёта. Обратно в картинку её
    /// приводит <c>cv.normalize</c>.
    /// </remarks>
    [ScriptFn("binary", "Порогование: матрица нулей и единиц", Example = "cv.binary(img, threshold: 128)")]
    public static Matrix Binary(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image,
        [ScriptParam("порог яркости")] double threshold = 128)
    {
        var result = new Matrix(image.Height, image.Width);

        context.CountAllocation((long)image.Height * image.Width);

        for (int i = 0; i < image.Height; i++)
        {
            for (int j = 0; j < image.Width; j++) result[i, j] = image[i, j] >= threshold ? 1 : 0;
        }

        return result;
    }

    // --- частотная область ---

    /// <summary>
    /// Амплитудный спектр изображения.
    /// </summary>
    /// <remarks>
    /// Спектр смещён так, что нулевая частота в середине, и по умолчанию логарифмический:
    /// без этого видна одна яркая точка в углу, потому что постоянная составляющая больше
    /// остальных гармоник на порядки.
    /// </remarks>
    [ScriptFn("spectrum", "Амплитудный спектр изображения, нулевая частота в центре",
        Example = "show plot.heatmap(cv.spectrum(img))")]
    public static Matrix Spectrum(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image,
        [ScriptParam("логарифмический масштаб")] bool log = true)
    {
        (double[,] re, double[,] im, int _, int _) = FFT2D.Forward(image);

        context.CountAllocation((long)image.Height * image.Width * 2);

        return FFT2D.FFTShift(FFT2D.MagnitudeSpectrum(re, im, log));
    }

    [ScriptFn("lowpass", "Частотная фильтрация: оставить низкие частоты",
        Example = "cv.lowpass(img, radius: 40)")]
    public static Matrix LowPass(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image,
        [ScriptParam("радиус среза в отсчётах частоты")] double radius)
    {
        RequireRadius(radius, "cv.lowpass");

        return Filtered(context, image, (re, im) => FFT2D.LowPassFilter(re, im, radius));
    }

    [ScriptFn("highpass", "Частотная фильтрация: оставить высокие частоты",
        Example = "cv.highpass(img, radius: 10)")]
    public static Matrix HighPass(
        IScriptContext context,
        [ScriptParam("матрица яркостей")] Matrix image,
        [ScriptParam("радиус среза в отсчётах частоты")] double radius)
    {
        RequireRadius(radius, "cv.highpass");

        return Filtered(context, image, (re, im) => FFT2D.HighPassFilter(re, im, radius));
    }

    // --- внутреннее ---

    private static Matrix Filtered(
        IScriptContext context,
        Matrix image,
        Action<double[,], double[,]> filter)
    {
        (double[,] re, double[,] im, int _, int _) = FFT2D.Forward(image);

        filter(re, im);

        context.CountAllocation((long)image.Height * image.Width * 2);

        return FFT2D.Inverse(re, im, image.Height, image.Width);
    }

    private static Matrix Resized(Matrix image, int width, int height)
    {
        using SKBitmap bitmap = ImageMatrixConverter.ToBitmap(image);

        return bitmap.ToMatrix(width, height);
    }

    /// <summary>
    /// Канал изображения матрицей.
    /// </summary>
    /// <remarks>
    /// Веса серого — не среднее по каналам, а те, что заложены в библиотеке: глаз видит
    /// зелёный вчетверо ярче синего, и равные веса дают картинку, не похожую на исходную.
    /// </remarks>
    private static Matrix Channel(SKBitmap bitmap, string channel, string what) => channel switch
    {
        "gray" => ImageMatrixConverter.BmpToMatr(bitmap),
        "red" => ImageMatrixConverter.BmpToMatrRed(bitmap),
        "green" => ImageMatrixConverter.BmpToMatrGreen(bitmap),
        "blue" => ImageMatrixConverter.BmpToMatrBlue(bitmap),
        "hue" => ImageMatrixConverter.BmpToHMatr(bitmap),
        _ => throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"{what}: неизвестный канал '{channel}'",
            "известны: \"gray\", \"red\", \"green\", \"blue\", \"hue\""),
    };

    private static Matrix Ones(int size)
    {
        var kernel = new Matrix(size, size);

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++) kernel[i, j] = 1;
        }

        return kernel;
    }

    private static void RequireOddWindow(int size, string what)
    {
        if (size >= 3 && size % 2 == 1) return;

        throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"{what}: размер окна — нечётное число не меньше трёх",
            "у чётного окна нет центра, и результат оказался бы сдвинут на полпикселя");
    }

    private static void RequireRadius(double radius, string what)
    {
        if (radius > 0) return;

        throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: радиус должен быть положительным");
    }
}
