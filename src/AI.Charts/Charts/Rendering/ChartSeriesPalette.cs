using SkiaSharp;

namespace AI.Charts.Rendering;

/// <summary>
/// Последовательные цвета серий (линии/графики), если цвет не задан явно.
/// </summary>
internal static class ChartSeriesPalette
{
    private static readonly SKColor[] Colors =
    {
        new SKColor(0, 120, 215),   // синий
        new SKColor(220, 53, 69),   // красный
        new SKColor(40, 167, 69),   // зелёный
        new SKColor(255, 152, 0),   // оранжевый
        new SKColor(123, 31, 162),  // фиолетовый
        new SKColor(0, 150, 136),   // бирюзовый
        new SKColor(121, 85, 72),    // коричневый
        new SKColor(63, 81, 181),    // индиго
    };

    public static SKColor Next(ref int index)
    {
        SKColor c = Colors[index % Colors.Length];
        index++;
        return c;
    }
}
