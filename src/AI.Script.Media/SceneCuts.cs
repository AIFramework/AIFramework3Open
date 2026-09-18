using AI.DataStructs.Algebraic;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Vision;
using SkiaSharp;

namespace AI.Script.Media;

/// <summary>Сцена ролика: номер с единицы и границы в секундах.</summary>
public sealed record Scene(int Number, double Start, double End);

/// <summary>Поиск смен сцен по разности соседних кадров.</summary>
public static class SceneCuts
{
    /// <summary>До какой стороны уменьшается кадр перед сравнением.</summary>
    private const int Side = 32;

    /// <summary>Сцены по кадрам: новая начинается там, где разность с прошлым кадром выше порога.</summary>
    public static IReadOnlyList<Scene> Find(IReadOnlyList<ScriptVideoFrame> frames, double threshold)
    {
        if (frames.Count == 0) return [];

        var scenes = new List<Scene>();
        double start = frames[0].At.TotalSeconds;
        double[] previous = Thumbnail(Decode(frames[0]));

        for (int i = 1; i < frames.Count; i++)
        {
            double[] current = Thumbnail(Decode(frames[i]));

            if (Difference(previous, current) > threshold)
            {
                scenes.Add(new Scene(scenes.Count + 1, start, frames[i].At.TotalSeconds));
                start = frames[i].At.TotalSeconds;
            }

            previous = current;
        }

        scenes.Add(new Scene(scenes.Count + 1, start, frames[^1].At.TotalSeconds));

        return scenes;
    }

    internal static ScriptTable Table(IReadOnlyList<Scene> scenes) => ScriptTable.Create(
    [
        ScriptColumn.Own("scene", [.. scenes.Select(scene => ScriptValue.Num(scene.Number))]),
        ScriptColumn.Own("start", [.. scenes.Select(scene => ScriptValue.Num(scene.Start))]),
        ScriptColumn.Own("end", [.. scenes.Select(scene => ScriptValue.Num(scene.End))]),
    ]);

    internal static ColorImage Decode(ScriptVideoFrame frame)
    {
        using SKBitmap? bitmap = SKBitmap.Decode(frame.Image);

        return bitmap is null
            ? throw new ScriptError(DiagnosticCodes.BadFileFormat, $"video: кадр на {frame.At} не декодируется как изображение")
            : ColorImage.From(bitmap);
    }

    /// <summary>Серый кадр, усредненный в сетку не больше <see cref="Side"/> на <see cref="Side"/>.</summary>
    private static double[] Thumbnail(ColorImage image)
    {
        Matrix gray = image.Gray();
        int rows = Math.Min(Side, image.Height);
        int columns = Math.Min(Side, image.Width);
        var cells = new double[rows * columns];
        var counts = new int[rows * columns];

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int cell = (y * rows / image.Height * columns) + (x * columns / image.Width);

                cells[cell] += gray[y, x];
                counts[cell]++;
            }
        }

        for (int i = 0; i < cells.Length; i++) cells[i] /= Math.Max(1, counts[i]);

        return cells;
    }

    /// <summary>Средняя разность яркости в долях полной шкалы; кадры разного размера это полная смена.</summary>
    private static double Difference(double[] a, double[] b)
    {
        if (a.Length != b.Length) return 1;

        double sum = 0;

        for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i] - b[i]);

        return sum / a.Length / 255.0;
    }
}
