namespace AI.ImageEditor.Filters;

/// <summary>Раздел меню, в который попадает фильтр.</summary>
public static class FilterCategories
{
    /// <summary>Тональная и цветовая коррекция.</summary>
    public const string Correction = "Коррекция";
    /// <summary>Свёрточные и художественные эффекты.</summary>
    public const string Effects = "Фильтры";
    /// <summary>Преобразования вида «в другой формат представления».</summary>
    public const string Convert = "Преобразования";
}

/// <summary>Описание фильтра для UI: имя, подпись, раздел и параметры со значениями по умолчанию.</summary>
/// <param name="Id">Машинное имя (уходит в команде с клиента).</param>
/// <param name="Title">Подпись для пользователя.</param>
/// <param name="Parameters">Параметры: имя → (мин, макс, по умолчанию).</param>
/// <param name="Category">Раздел меню (см. <see cref="FilterCategories"/>).</param>
public sealed record FilterInfo(
    string Id,
    string Title,
    IReadOnlyList<FilterParameter> Parameters,
    string Category = FilterCategories.Effects);

/// <summary>Один настраиваемый параметр фильтра.</summary>
/// <param name="Name">Ключ параметра.</param>
/// <param name="Title">Подпись.</param>
/// <param name="Min">Минимум.</param>
/// <param name="Max">Максимум.</param>
/// <param name="Default">Значение по умолчанию.</param>
public sealed record FilterParameter(string Name, string Title, double Min, double Max, double Default);

/// <summary>
/// Реестр фильтров: имя → фабрика. Потребитель (MAS) знает только строковый идентификатор
/// и список параметров — добавление нового фильтра не требует правок в UI-коде (OCP).
/// </summary>
public static class FilterRegistry
{
    private static readonly Dictionary<string, Func<FilterParams, IImageFilter>> Factories = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<FilterInfo> Infos = [];

    static FilterRegistry()
    {
        const string Corr = FilterCategories.Correction;
        const string Fx   = FilterCategories.Effects;
        const string Conv = FilterCategories.Convert;

        // ── Коррекция: точечные операции через LUT (самые быстрые) ──────────
        Register(new FilterInfo("brightness_contrast", "Яркость / контраст",
            [
                new("brightness", "Яркость", -100, 100, 0),
                new("contrast", "Контраст", -100, 100, 0)
            ], Corr),
            p => new BrightnessContrastFilter(p.Get("brightness", 0), p.Get("contrast", 0)));

        Register(new FilterInfo("nonlinear_contrast", "Нелинейная контрастность",
            [
                new("betta", "Крутизна", 1, 30, 10),
                new("offset", "Смещение", -1, 0, -0.5)
            ], Corr),
            p => new NonlinearContrastFilter(p.Get("offset", -0.5), p.Get("betta", 10)));

        Register(new FilterInfo("gamma", "Гамма",
            [new("gamma", "Гамма", 0.1, 4, 1)], Corr),
            p => new GammaFilter(p.Get("gamma", 1)));

        Register(new FilterInfo("saturation", "Насыщенность",
            [new("amount", "Сила", 0, 3, 1)], Corr),
            p => new SaturationFilter(p.Get("amount", 1)));

        Register(new FilterInfo("retinex", "Ретинекс",
            [
                new("sigma", "Масштаб", 4, 80, 24),
                new("strength", "Сила", 0, 1, 0.8)
            ], Corr),
            p => new RetinexFilter(p.Get("sigma", 24), p.Get("strength", 0.8)));

        // ── Фильтры: свёртки и художественные эффекты ───────────────────────
        Register(new FilterInfo("sharpen", "Резкость",
            [new("sharp", "Сила", 0.2, 3, 1)], Fx),
            p => new SharpenFilter(p.Get("sharp", 1)));

        Register(new FilterInfo("denoise", "Удаление шума",
            [new("strength", "Сила", 0.3, 5, 1)], Fx),
            p => new DenoiseFilter(p.Get("strength", 1)));

        Register(new FilterInfo("median", "Удаление шума (импульсного)", [], Fx),
            _ => new MedianFilter());

        Register(new FilterInfo("blur", "Размытие",
            [new("radius", "Радиус", 0.5, 30, 3)], Fx),
            p => new BlurFilter(p.Get("radius", 3)));

        Register(new FilterInfo("glow", "Свечение",
            [
                new("threshold", "Порог", 60, 250, 180),
                new("radius", "Радиус", 1, 40, 8),
                new("intensity", "Сила", 0, 2, 0.8)
            ], Fx),
            p => new GlowFilter(p.Get("threshold", 180), p.Get("radius", 8), p.Get("intensity", 0.8)));

        // ── Преобразования: смена представления изображения ─────────────────
        Register(new FilterInfo("grayscale", "Оттенки серого", [], Conv), _ => new GrayscaleFilter());
        Register(new FilterInfo("invert", "Негатив", [], Conv), _ => new InvertFilter());

        Register(new FilterInfo("threshold", "Порог (ч/б)",
            [new("threshold", "Порог", 0, 255, 128)], Conv),
            p => new ThresholdFilter(p.Get("threshold", 128)));

        Register(new FilterInfo("edges", "Границы (Собель)", [], Conv), _ => new SobelEdgeFilter());
    }

    /// <summary>Регистрирует фильтр (можно добавлять свои снаружи).</summary>
    public static void Register(FilterInfo info, Func<FilterParams, IImageFilter> factory)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(factory);

        Factories[info.Id] = factory;
        Infos.RemoveAll(i => string.Equals(i.Id, info.Id, StringComparison.OrdinalIgnoreCase));
        Infos.Add(info);
    }

    /// <summary>Все доступные фильтры — для построения панели инструментов.</summary>
    public static IReadOnlyList<FilterInfo> Available => Infos;

    /// <summary>Создаёт фильтр по имени. <c>null</c> — если имя неизвестно.</summary>
    public static IImageFilter? Create(string id, FilterParams? parameters = null) =>
        Factories.TryGetValue(id, out var factory) ? factory(parameters ?? FilterParams.Empty) : null;
}
