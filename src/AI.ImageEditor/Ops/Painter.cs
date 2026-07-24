using AI.ImageEditor.Model;
using SkiaSharp;

namespace AI.ImageEditor.Ops;

/// <summary>Точка мазка в координатах документа.</summary>
/// <param name="X">Координата X.</param>
/// <param name="Y">Координата Y.</param>
public readonly record struct StrokePoint(float X, float Y);

/// <summary>
/// Параметры мазка. Один тип и для кисти, и для ластика — отличается только
/// <see cref="Erase"/>, поэтому плодить два почти одинаковых класса незачем.
/// </summary>
/// <param name="Radius">Радиус в пикселях документа.</param>
/// <param name="Color">Цвет (для ластика игнорируется).</param>
/// <param name="Hardness">Жёсткость края 0..1 (1 — резкий край).</param>
/// <param name="Opacity">Непрозрачность мазка 0..1.</param>
/// <param name="Erase">true — стирать вместо рисования.</param>
public readonly record struct BrushSettings(
    float Radius,
    SKColor Color,
    float Hardness = 1f,
    float Opacity = 1f,
    bool Erase = false);

/// <summary>
/// Рисование по слою: кисть и ластик.
/// <para>
/// Растеризацию делает Skia (<see cref="SKPath"/> + <see cref="SKPaint"/>) — это даёт
/// сглаженные круглые мазки, мягкий край через размытие маски и корректное стирание
/// в прозрачность (<see cref="SKBlendMode.Clear"/>) без собственного растеризатора.
/// </para>
/// </summary>
public static class Painter
{
    /// <summary>
    /// Наносит мазок по точкам на указанный слой. Точки соединяются линией с
    /// круглыми стыками — так штрих остаётся непрерывным даже при редких сэмплах
    /// (важно на медленном канале, где точки приходят реже).
    /// </summary>
    public static void Stroke(Layer layer, IReadOnlyList<StrokePoint> points, BrushSettings brush)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (points is null || points.Count == 0 || brush.Radius <= 0) return;

        using var canvas = new SKCanvas(layer.Bitmap);
        using var paint = BuildPaint(brush);

        if (points.Count == 1)
        {
            // Одиночный тап — точка.
            canvas.DrawCircle(points[0].X, points[0].Y, brush.Radius, PointPaint(paint, brush));
            return;
        }

        using var path = new SKPath();
        path.MoveTo(points[0].X, points[0].Y);
        for (var i = 1; i < points.Count; i++)
            path.LineTo(points[i].X, points[i].Y);

        canvas.DrawPath(path, paint);
    }

    /// <summary>Кисть для линии мазка.</summary>
    private static SKPaint BuildPaint(BrushSettings brush)
    {
        var alpha = (byte)Math.Clamp(brush.Opacity * 255f, 0, 255);

        var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = brush.Radius * 2f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = brush.Erase ? SKColors.Black : brush.Color.WithAlpha(alpha),
            // Ластик стирает в прозрачность, а не рисует белым.
            BlendMode = brush.Erase ? SKBlendMode.Clear : SKBlendMode.SrcOver
        };

        // Мягкий край: размываем маску мазка. hardness=1 — край резкий.
        var hardness = Math.Clamp(brush.Hardness, 0f, 1f);
        if (hardness < 0.99f)
        {
            var sigma = brush.Radius * (1f - hardness) * 0.5f;
            if (sigma > 0.1f)
                paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma);
        }

        return paint;
    }

    /// <summary>Та же кисть, но заливкой — для одиночной точки.</summary>
    private static SKPaint PointPaint(SKPaint stroke, BrushSettings brush)
    {
        stroke.Style = SKPaintStyle.Fill;
        return stroke;
    }
}
