using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.ComputerVision;

public sealed class ComputerVisionModule : ILibraryModule
{
    public string Id => "computer-vision";
    public string Name => "AI.ComputerVision";
    public string Description => "Фильтры, градиенты, HOG, бинарный анализ, статистика изображений";
    public string Color => "emerald";
    public string TutorialFolder => "ComputerVision";
    public string IconSvg => """<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>""";

    private static readonly AlgoChoice[] SpatialFilterChoices =
    [
        new(0, "Исходное"),
        new(1, "Сглаживание 3×3"),
        new(2, "Гаусс 3×3"),
        new(3, "Резкость"),
    ];

    private static readonly AlgoChoice[] GradientTypeChoices =
    [
        new(0, "Sobel — модуль"),
        new(1, "Sobel — GradX"),
        new(2, "Sobel — GradY"),
    ];

    private static readonly AlgoChoice[] FftFilterTypeChoices =
    [
        new(0, "Идеальный НЧ"),
        new(1, "Идеальный ВЧ"),
        new(2, "Гауссов НЧ"),
        new(3, "Гауссов ВЧ"),
        new(4, "Полосовой"),
    ];

    private static readonly AlgoChoice[] SpectrumChoices =
    [
        new(0, "Амплитуда (log)"),
        new(1, "Фаза"),
        new(2, "Амплитуда (линейная)"),
    ];

    private static readonly AlgoChoice[] ColorModeChoices =
    [
        new(0, "Серое (Gray)"),
        new(1, "Цвет (RGB)"),
    ];

    private static readonly AlgoChoice[] FftBackendChoices =
    [
        new(0, "CPU"),
        new(1, "CUDA (GPU)"),
    ];

    public IReadOnlyList<CategoryDef> Categories { get; } =
    [
        new("spatial", "Пространственные фильтры", "Сглаживание, Гаусс, резкость",
        [
            new("spatial_filter", "Пространственный фильтр",
                "Исходное / Smoothing / GaussianBlur / Sharpness",
                "AI.ComputerVision.SpatialFilters", "SpatialFilters.md",
                [
                    new("spatialFilter", "Фильтр", 0, 3, 0, 1, "", "Тип пространственного фильтра")
                        { Choices = SpatialFilterChoices },
                    new("sharpAmount", "Усиление резкости", 0.1, 5, 1, 0.1, "", "Только для режима «Резкость»"),
                    new("colorMode", "Режим", 0, 1, 0, 1, "", "Обрабатывать серое или цветное изображение")
                        { Choices = ColorModeChoices },
                    new("_needsImage","",0,1,1,1,Hint:"Загрузить изображение"),
                ]),
        ]),
        new("freq", "Частотная область (2D FFT)", "Двумерное преобразование Фурье, частотные фильтры, спектр",
        [
            new("fft_spectrum", "Спектр изображения", "Амплитудный и фазовый спектр (2D FFT)",
                "AI.ComputerVision.FrequencyDomain.FFT2D", "FFT2D.md",
                [
                    new("specType", "Тип спектра", 0, 2, 0, 1, "", "Что визуализировать")
                        { Choices = SpectrumChoices },
                    new("colorMode", "Режим", 0, 1, 0, 1, "", "Серое или цветное")
                        { Choices = ColorModeChoices },
                    new("fftBackend", "Бэкенд FFT", 0, 1, 0, 1, "", "CPU (Parallel Fft64) или CUDA (cuFFT на GPU)")
                        { Choices = FftBackendChoices },
                    new("_needsImage","",0,1,1,1),
                ]),
            new("fft_filter", "Частотная фильтрация", "НЧ / ВЧ / Гауссов / полосовой фильтр в спектральной области",
                "AI.ComputerVision.FrequencyDomain.FFT2D", "FFT2D.md",
                [
                    new("filterType", "Тип фильтра", 0, 4, 0, 1, "", "Выбор фильтра")
                        { Choices = FftFilterTypeChoices },
                    new("cutoff", "Радиус среза", 1, 100, 30, 1, "пикс.", "Частота среза (радиус в пикселях)"),
                    new("sigma", "Sigma (Гаусс)", 1, 100, 20, 1, "", "Параметр σ для гауссова фильтра"),
                    new("rHigh", "R-high (полос.)", 1, 100, 60, 1, "пикс.", "Верхняя частота полосового фильтра"),
                    new("colorMode", "Режим", 0, 1, 0, 1, "", "Серое или цветное")
                        { Choices = ColorModeChoices },
                    new("fftBackend", "Бэкенд FFT", 0, 1, 0, 1, "", "CPU (Parallel Fft64) или CUDA (cuFFT на GPU)")
                        { Choices = FftBackendChoices },
                    new("_needsImage","",0,1,1,1),
                ]),
            new("fft_color_channels", "Спектр по каналам RGB", "Амплитудный спектр для каждого канала цветного изображения",
                "AI.ComputerVision.FrequencyDomain.FFT2D", "FFT2D.md",
                [
                    new("fftBackend", "Бэкенд FFT", 0, 1, 0, 1, "", "CPU (Parallel Fft64) или CUDA (cuFFT на GPU)")
                        { Choices = FftBackendChoices },
                    new("_needsImage","",0,1,1,1),
                ]),
        ]),
        new("grad", "Градиенты и HOG", "Собель, фаза, HOG-дескрипторы",
        [
            new("gradient", "Градиент Sobel", "SobelTransform — модуль, GradX, GradY",
                "AI.ComputerVision.ImgTransforms.SobelTransform", "Transforms.md",
                [
                    new("gradType", "Компонента", 0, 2, 0, 1, "", "Что визуализировать: |G|, Gx или Gy")
                        { Choices = GradientTypeChoices },
                    new("colorMode", "Режим", 0, 1, 0, 1, "", "Серое или цветное") { Choices = ColorModeChoices },
                    new("_needsImage","",0,1,1,1),
                ]),
            new("hog", "HOG", "CalcHist, 8 бинов", "HOG", "Transforms.md",
                [new("hogBins","Бинов",4,16,8,1), new("_needsImage","",0,1,1,1)]),
        ]),
        new("hist", "Статистика и гистограмма", "Эквализация гистограммы, поканальная статистика",
        [
            new("equalize", "Эквализация", "ImageHistogram.Equalize", "ImageHistogram", "ImageStatistics.md",
                [
                    new("colorMode", "Режим", 0, 1, 0, 1, "", "Серое или цветное") { Choices = ColorModeChoices },
                    new("_needsImage","",0,1,1,1),
                ]),
        ]),
        new("binary", "Бинарные изображения", "Порог, подсчёт объектов",
        [
            new("binary", "BinaryImg", "Порог 0.5", "BinaryImg", "BinaryAnalysis.md",
                [new("threshold","Порог",0.01,0.99,0.5,0.01), new("_needsImage","",0,1,1,1)]),
        ]),
    ];

    public DemoResult RunDemo(string algoKey, IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string> textParams, DemoSettings settings)
    {
        if (!textParams.TryGetValue("_imageBase64", out var imgB64) || string.IsNullOrEmpty(imgB64))
            return new DemoResult { NeedsImageUpload = true };

        try
        {
            return CvDemoRunner.Run(algoKey, imgB64, numericParams);
        }
        catch (Exception ex)
        {
            return new DemoResult { Error = ex.Message };
        }
    }
}
