using AI.ImageEditor.Pixels;

namespace AI.ImageEditor.Filters;

/// <summary>
/// Базовый поэлементный фильтр через таблицу подстановки (LUT) на 256 значений.
/// <para>
/// Аналог <c>FilterEE</c> из AI.ComputerVision, но без Matrix: таблица считается один
/// раз, дальше — один проход по байтам. Это самый быстрый класс фильтров, годится
/// для интерактивных ползунков.
/// </para>
/// </summary>
public abstract class LutFilter : IImageFilter
{
    private byte[]? _lut;

    /// <summary>Значение таблицы для входной яркости 0..255.</summary>
    protected abstract byte Map(int value);

    /// <inheritdoc />
    public void Apply(PixelBuffer buffer)
    {
        var lut = _lut ??= BuildLut();
        var data = buffer.Data;

        // Альфу (индекс +3) не трогаем — прозрачность слоя сохраняется.
        for (var i = 0; i < data.Length; i += PixelBuffer.Bpp)
        {
            data[i]     = lut[data[i]];
            data[i + 1] = lut[data[i + 1]];
            data[i + 2] = lut[data[i + 2]];
        }
    }

    private byte[] BuildLut()
    {
        var lut = new byte[256];
        for (var i = 0; i < 256; i++) lut[i] = Map(i);
        return lut;
    }

    /// <summary>Ограничение в диапазон байта.</summary>
    protected static byte Clamp(double v) => (byte)(v <= 0 ? 0 : v >= 255 ? 255 : v + 0.5);
}

/// <summary>Яркость и контрастность. <c>brightness</c> −100..100, <c>contrast</c> −100..100.</summary>
public sealed class BrightnessContrastFilter : LutFilter
{
    private readonly double _brightness;
    private readonly double _contrast;

    /// <summary>Создаёт фильтр яркости/контраста.</summary>
    public BrightnessContrastFilter(double brightness = 0, double contrast = 0)
    {
        _brightness = Math.Clamp(brightness, -100, 100) * 2.55;   // в единицы яркости
        // Классический коэффициент контраста: при contrast=100 наклон резко растёт.
        var c = Math.Clamp(contrast, -100, 100);
        _contrast = (259.0 * (c + 255.0)) / (255.0 * (259.0 - c));
    }

    /// <inheritdoc />
    protected override byte Map(int value) =>
        Clamp(_contrast * (value + _brightness - 128.0) + 128.0);
}

/// <summary>
/// Нелинейная (сигмоидальная) контрастность — портирован <c>SigmoidalFilter</c>.
/// Мягко тянет полутона, не выбивая тени и света в отсечку, как линейный контраст.
/// </summary>
public sealed class NonlinearContrastFilter : LutFilter
{
    private readonly double _offset;
    private readonly double _betta;

    /// <summary>Создаёт фильтр. <paramref name="offset"/> — смещение центра, <paramref name="betta"/> — крутизна.</summary>
    public NonlinearContrastFilter(double offset = -0.5, double betta = 10)
    {
        _offset = offset;
        _betta = Math.Max(0.01, betta);
    }

    /// <inheritdoc />
    protected override byte Map(int value)
    {
        var x = value / 255.0 + _offset;
        var sigmoid = 1.0 / (1.0 + Math.Exp(-_betta * x));
        return Clamp(sigmoid * 255.0);
    }
}

/// <summary>Гамма-коррекция. <c>gamma</c> &gt; 1 — светлее, &lt; 1 — темнее.</summary>
public sealed class GammaFilter : LutFilter
{
    private readonly double _inv;

    /// <summary>Создаёт гамма-фильтр.</summary>
    public GammaFilter(double gamma = 1.0) => _inv = 1.0 / Math.Clamp(gamma, 0.01, 10.0);

    /// <inheritdoc />
    protected override byte Map(int value) => Clamp(Math.Pow(value / 255.0, _inv) * 255.0);
}

/// <summary>Инверсия (негатив).</summary>
public sealed class InvertFilter : LutFilter
{
    /// <inheritdoc />
    protected override byte Map(int value) => (byte)(255 - value);
}

/// <summary>Бинаризация по порогу 0..255.</summary>
public sealed class ThresholdFilter : LutFilter
{
    private readonly int _threshold;

    /// <summary>Создаёт фильтр бинаризации.</summary>
    public ThresholdFilter(double threshold = 128) => _threshold = (int)Math.Clamp(threshold, 0, 255);

    /// <inheritdoc />
    protected override byte Map(int value) => value >= _threshold ? (byte)255 : (byte)0;
}

/// <summary>
/// Перевод в оттенки серого по яркостным весам Rec.601 — тот же принцип, что у
/// <c>BmpToMatr</c> в AI.ComputerVision (взвешивание каналов).
/// </summary>
public sealed class GrayscaleFilter : IImageFilter
{
    /// <inheritdoc />
    public void Apply(PixelBuffer buffer)
    {
        var data = buffer.Data;
        for (var i = 0; i < data.Length; i += PixelBuffer.Bpp)
        {
            // BGRA: B=i, G=i+1, R=i+2
            var y = (byte)((data[i + 2] * 299 + data[i + 1] * 587 + data[i] * 114) / 1000);
            data[i] = data[i + 1] = data[i + 2] = y;
        }
    }
}

/// <summary>Насыщенность: 0 — обесцветить, 1 — без изменений, &gt;1 — усилить.</summary>
public sealed class SaturationFilter : IImageFilter
{
    private readonly double _amount;

    /// <summary>Создаёт фильтр насыщенности.</summary>
    public SaturationFilter(double amount = 1.0) => _amount = Math.Clamp(amount, 0, 3);

    /// <inheritdoc />
    public void Apply(PixelBuffer buffer)
    {
        var data = buffer.Data;
        for (var i = 0; i < data.Length; i += PixelBuffer.Bpp)
        {
            double b = data[i], g = data[i + 1], r = data[i + 2];
            var y = (r * 299 + g * 587 + b * 114) / 1000.0;   // яркость как опорная точка

            data[i]     = ClampByte(y + (b - y) * _amount);
            data[i + 1] = ClampByte(y + (g - y) * _amount);
            data[i + 2] = ClampByte(y + (r - y) * _amount);
        }
    }

    private static byte ClampByte(double v) => (byte)(v <= 0 ? 0 : v >= 255 ? 255 : v + 0.5);
}
