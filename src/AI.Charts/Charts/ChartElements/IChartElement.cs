using AI.Charts.Data;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using SkiaSharp;

namespace AI.Charts.ChartElements;

internal interface IChartElement
{
    string Name { get; }
    IData Data { get; }
    SKColor ElementColor { get; }
    int BorderWidth { get; }
    ChartLayoutKind LayoutKind { get; }

    void SetColor(SKColor color);
    void LoadData(Vector x, Vector y);
    void LoadData(IData data);
    void Recalc(double min, double max);

    double GetXMin();
    double GetXMax();
    double GetYMin();
    double GetYMax();

    void Draw(SKCanvas canvas, ChartViewport vp);
}
