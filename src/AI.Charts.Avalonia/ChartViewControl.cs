using System;
using System.IO;
using AI.Charts;
using AI.Charts.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace AI.Charts.Avalonia;

/// <summary>
/// Кроссплатформенный контрол Avalonia: отображает <see cref="AI.Charts.ChartView"/> через SkiaSharp.
/// Supports interactive 3D rotation via mouse drag and zoom via scroll wheel.
/// </summary>
public class ChartViewControl : Control
{
    public static readonly StyledProperty<ChartView?> ChartProperty =
        AvaloniaProperty.Register<ChartViewControl, ChartView?>(nameof(Chart));

    public ChartView? Chart
    {
        get => GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    private bool _dragging;
    private Point _lastPos;

    static ChartViewControl()
    {
        AffectsRender<ChartViewControl>(ChartProperty);
    }

    public ChartViewControl()
    {
        Focusable = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        ChartView? chart = Chart;
        if (chart == null)
        {
            return;
        }

        double w = Math.Max(1, Bounds.Width);
        double h = Math.Max(1, Bounds.Height);
        using SKBitmap sk = chart.ToBitmap((int)w, (int)h);
        using SKImage img = SKImage.FromBitmap(sk);
        using SKData data = img.Encode(SKEncodedImageFormat.Png, 100);
        if (data == null)
        {
            return;
        }

        using var ms = new MemoryStream();
        data.SaveTo(ms);
        ms.Position = 0;
        var bitmap = new Bitmap(ms);
        var rect = new Rect(Bounds.Size);
        context.DrawImage(bitmap, rect);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        ChartView? chart = Chart;
        if (chart == null || chart.CurrentLayout != ChartLayoutKind.ThreeD) return;

        _dragging = true;
        _lastPos = e.GetPosition(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;

        ChartView? chart = Chart;
        if (chart == null) return;

        Point pos = e.GetPosition(this);
        double dx = pos.X - _lastPos.X;
        double dy = pos.Y - _lastPos.Y;
        _lastPos = pos;

        Camera3D cam = chart.Camera3D;
        cam.Azimuth += dx * 0.5;
        cam.Elevation += dy * 0.5;

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ChartView? chart = Chart;
        if (chart == null || chart.CurrentLayout != ChartLayoutKind.ThreeD) return;

        Camera3D cam = chart.Camera3D;
        cam.Distance -= e.Delta.Y * 0.15;
        InvalidateVisual();
        e.Handled = true;
    }
}
