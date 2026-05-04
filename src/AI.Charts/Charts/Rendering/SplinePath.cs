using AI.DataStructs.Algebraic;
using SkiaSharp;

namespace AI.Charts.Rendering;

internal static class SplinePath
{
    public static void AppendLineOrSpline(SKPath path, Vector x, Vector y, ChartViewport vp, bool spline)
    {
        int n = x.Count;
        if (n == 0)
        {
            return;
        }

        if (n == 1)
        {
            path.MoveTo(vp.XToPx(x[0]), vp.YToPx(y[0]));
            return;
        }

        if (!spline)
        {
            path.MoveTo(vp.XToPx(x[0]), vp.YToPx(y[0]));
            for (int i = 1; i < n; i++)
            {
                path.LineTo(vp.XToPx(x[i]), vp.YToPx(y[i]));
            }

            return;
        }

        path.MoveTo(vp.XToPx(x[0]), vp.YToPx(y[0]));
        for (int i = 0; i < n - 1; i++)
        {
            double x0 = i == 0 ? x[0] : x[i - 1];
            double y0 = i == 0 ? y[0] : y[i - 1];
            double x1 = x[i];
            double y1 = y[i];
            double x2 = x[i + 1];
            double y2 = y[i + 1];
            double x3 = i + 2 < n ? x[i + 2] : x2 * 2 - x1;
            double y3 = i + 2 < n ? y[i + 2] : y2 * 2 - y1;

            for (int s = 1; s <= 8; s++)
            {
                double t = s / 8.0;
                double t2 = t * t;
                double t3 = t2 * t;
                double px = 0.5 * ((2 * x1) + (-x0 + x2) * t + (2 * x0 - 5 * x1 + 4 * x2 - x3) * t2 + (-x0 + 3 * x1 - 3 * x2 + x3) * t3);
                double py = 0.5 * ((2 * y1) + (-y0 + y2) * t + (2 * y0 - 5 * y1 + 4 * y2 - y3) * t2 + (-y0 + 3 * y1 - 3 * y2 + y3) * t3);
                path.LineTo(vp.XToPx(px), vp.YToPx(py));
            }
        }
    }
}
