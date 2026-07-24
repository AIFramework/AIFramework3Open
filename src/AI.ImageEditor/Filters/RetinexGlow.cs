using AI.ImageEditor.Pixels;

namespace AI.ImageEditor.Filters;

/// <summary>
/// Одномасштабный ретинекс (SSR): <c>R = log(I) − log(Gauss(I))</c>.
/// <para>
/// Вытягивает детали из теней и убирает неравномерность освещения. Считается по
/// <b>яркости</b>, а цветность восстанавливается масштабированием каналов — иначе
/// поканальный ретинекс уводит цвета. Быстрый: одно сепарабельное размытие + два
/// линейных прохода.
/// </para>
/// <remarks>
/// В AI.ComputerVision ретинекса не было — добавлен здесь как самостоятельный фильтр.
/// </remarks>
/// </summary>
public sealed class RetinexFilter : IImageFilter
{
    private readonly double _sigma;
    private readonly double _strength;

    /// <summary>Создаёт ретинекс.</summary>
    /// <param name="sigma">Масштаб освещённости (больше — мягче выравнивание).</param>
    /// <param name="strength">Доля эффекта 0..1 (смешивание с оригиналом).</param>
    public RetinexFilter(double sigma = 24, double strength = 0.8)
    {
        _sigma = Math.Clamp(sigma, 1, 100);
        _strength = Math.Clamp(strength, 0, 1);
    }

    /// <inheritdoc />
    public void Apply(PixelBuffer buffer)
    {
        var data = buffer.Data;
        var pixels = buffer.Width * buffer.Height;

        // Освещённость = сильно размытая копия.
        var illum = buffer.Clone();
        Convolution.GaussianBlur(illum, _sigma);

        // 1-й проход: логарифмическая разность по яркости + границы для нормировки.
        var refl = new float[pixels];
        double min = double.MaxValue, max = double.MinValue;

        for (int p = 0, i = 0; p < pixels; p++, i += PixelBuffer.Bpp)
        {
            var lum = Luminance(data, i);
            var lumIll = Luminance(illum.Data, i);

            // +1 защищает от log(0); разность логарифмов = локальный контраст.
            var r = (float)(Math.Log(lum + 1.0) - Math.Log(lumIll + 1.0));
            refl[p] = r;
            if (r < min) min = r;
            if (r > max) max = r;
        }

        var range = max - min;
        if (range < 1e-6) return;   // равномерная картинка — делать нечего

        // 2-й проход: нормировка в 0..255 и восстановление цвета через коэффициент яркости.
        for (int p = 0, i = 0; p < pixels; p++, i += PixelBuffer.Bpp)
        {
            var lum = Luminance(data, i);
            var target = (refl[p] - min) / range * 255.0;
            var blended = lum + (target - lum) * _strength;

            // Масштабируем каналы, сохраняя их соотношение (цветовой тон не плывёт).
            var gain = lum < 1.0 ? 1.0 : blended / lum;
            data[i]     = Convolution.ToByte(data[i] * gain);
            data[i + 1] = Convolution.ToByte(data[i + 1] * gain);
            data[i + 2] = Convolution.ToByte(data[i + 2] * gain);
        }
    }

    /// <summary>Яркость Rec.601 по BGRA-пикселю.</summary>
    private static double Luminance(byte[] d, int i) =>
        (d[i + 2] * 299 + d[i + 1] * 587 + d[i] * 114) / 1000.0;
}

/// <summary>
/// Свечение (bloom): яркие области размываются и подмешиваются обратно в режиме
/// «экран». Даёт мягкий ореол вокруг источников света.
/// <remarks>В AI.ComputerVision свечения не было — добавлено здесь.</remarks>
/// </summary>
public sealed class GlowFilter : IImageFilter
{
    private readonly int _threshold;
    private readonly double _sigma;
    private readonly double _intensity;

    /// <summary>Создаёт фильтр свечения.</summary>
    /// <param name="threshold">Порог яркости, с которого начинается свечение (0..255).</param>
    /// <param name="radius">Радиус ореола.</param>
    /// <param name="intensity">Сила подмешивания 0..2.</param>
    public GlowFilter(double threshold = 180, double radius = 8, double intensity = 0.8)
    {
        _threshold = (int)Math.Clamp(threshold, 0, 255);
        _sigma = Math.Clamp(radius, 1, 60);
        _intensity = Math.Clamp(intensity, 0, 2);
    }

    /// <inheritdoc />
    public void Apply(PixelBuffer buffer)
    {
        var data = buffer.Data;

        // 1. Маска ярких областей (всё темнее порога — в ноль).
        var bright = buffer.Clone();
        var bd = bright.Data;
        for (var i = 0; i < bd.Length; i += PixelBuffer.Bpp)
        {
            var lum = (bd[i + 2] * 299 + bd[i + 1] * 587 + bd[i] * 114) / 1000;
            if (lum < _threshold)
            {
                bd[i] = bd[i + 1] = bd[i + 2] = 0;
            }
            else
            {
                // Плавный набор силы от порога к белому — без резкой ступеньки.
                var k = (lum - _threshold) / (255.0 - _threshold + 1e-6);
                bd[i] = Convolution.ToByte(bd[i] * k);
                bd[i + 1] = Convolution.ToByte(bd[i + 1] * k);
                bd[i + 2] = Convolution.ToByte(bd[i + 2] * k);
            }
        }

        // 2. Размываем ореол.
        Convolution.GaussianBlur(bright, _sigma);

        // 3. Подмешиваем режимом «экран»: 255 − (255−a)(255−b)/255.
        for (var i = 0; i < data.Length; i += PixelBuffer.Bpp)
        {
            for (var c = 0; c < 3; c++)
            {
                double a = data[i + c];
                var b = bd[i + c] * _intensity;
                var screen = 255.0 - (255.0 - a) * (255.0 - Math.Min(b, 255.0)) / 255.0;
                data[i + c] = Convolution.ToByte(screen);
            }
        }
    }
}
