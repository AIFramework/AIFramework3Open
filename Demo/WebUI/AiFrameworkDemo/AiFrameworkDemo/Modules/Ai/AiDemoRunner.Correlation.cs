using AI;
using AI.Charts;
using AI.Charts.JS;
using AI.DataStructs.Algebraic;
using AI.Statistics;
using SkiaSharp;
using System.Text;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.Ai;

public static partial class AiDemoRunner
{
    #region Корреляция — случаи

    private static void RunCorrelationCase(
        string key, IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut, ref PlotlyBuilder? plotly)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        switch (key)
        {
            case "pearson":
            {
                int n = Math.Max(50, (int)N("n", 300));
                double a = N("alpha", 1), noise = Math.Max(0, N("noise", 0.6));
                var rng = new Random((int)N("seed", 42));
                var x = Statistic.RandNorm(n, rng);
                var eps = Statistic.RandNorm(n, new Random((int)N("seed", 42) + 1)) * noise;
                var y = x * a + eps;
                double r = Statistic.CorrelationCoefficient(x, y);
                double cv2 = Statistic.Cov(x, y);
                double xMin = x.Min() - 0.3, xMax = x.Max() + 0.3;
                var xLine = Vector.Seq(xMin, (xMax - xMin) / 100, xMax);
                cv.ChartName = $"r = {r:F4}   cov = {cv2:F4}   (α={a:F2}, шум={noise:F2})";
                cv.LabelX = "x"; cv.LabelY = "y";
                cv.AddScatterMark3(x, y, "Точки", Palette[0]);
                cv.AddPlot(xLine, xLine * a, $"y = {a:F2}·x", Palette[1], width: 2);
                break;
            }
            case "corr_matrix":
            {
                int n = Math.Max(50, (int)N("n", 300));
                int k = Math.Clamp((int)N("k", 5), 2, 8);
                double mix = Math.Clamp(N("mix", 0.5), 0, 1);
                int seed = (int)N("seed", 42);
                var rng = new Random(seed);
                var common = Statistic.RandNorm(n, rng);
                var series = new Vector[k];
                for (int i = 0; i < k; i++)
                {
                    var ind = Statistic.RandNorm(n, new Random(seed + 100 + i));
                    double sign = (i % 2 == 0) ? 1 : -1;
                    series[i] = common * (sign * mix) + ind * (1 - mix);
                }
                var M = Matrix.GetCorrelationMatrixNorm(series);
                cv.SetBackgroundImage(RenderMatrixHeatmap(M, 320, 320, -1, 1));
                int rows = M.Height, cols = M.Width;
                var xArr = new double[cols]; var yArr = new double[rows]; var zArr = new double[rows][];
                for (int i = 0; i < cols; i++) xArr[i] = i;
                for (int i = 0; i < rows; i++) yArr[i] = i;
                for (int i = 0; i < rows; i++) { zArr[i] = new double[cols]; for (int j = 0; j < cols; j++) zArr[i][j] = M[i, j]; }
                plotly = new PlotlyBuilder { Title = $"Матрица корреляций {k}×{k}  (зависимость {mix:F2})", AxisX = "j", AxisY = "i" };
                plotly.AddHeatmap(xArr, yArr, zArr, "RdBu", showScale: true, zMin: -1, zMax: 1);
                cv.ChartName = $"Матрица корреляций {k}×{k}  (зависимость {mix:F2})";
                cv.LabelX = "j"; cv.LabelY = "i";
                cv.SetAxisRange(-0.5, k - 0.5, -0.5, k - 0.5);
                var sb = new StringBuilder();
                sb.AppendLine("> Матрица корреляций");
                sb.AppendLine();
                sb.Append("        ");
                for (int j = 0; j < k; j++) sb.Append($"  x{j,-4}");
                sb.AppendLine();
                for (int i = 0; i < k; i++)
                {
                    sb.Append($"  x{i}:   ");
                    for (int j = 0; j < k; j++) sb.Append($" {M[i, j],6:F2}");
                    sb.AppendLine();
                }
                textOut = sb.ToString();
                break;
            }
            case "autocorr":
            {
                int n = Math.Max(64, (int)N("n", 256));
                int kind = Math.Clamp((int)N("kind", 0), 0, 2);
                double freq = Math.Clamp(N("freq", 0.08), 0.005, 0.499);
                double phi = Math.Clamp(N("phi", 0.7), -0.95, 0.95);
                var rng = new Random((int)N("seed", 42));
                var signal = new Vector(n);
                string name = kind switch { 1 => "Белый шум", 2 => $"AR(1), φ={phi:F2}", _ => $"sin(2π·{freq:F3}·t)" };
                switch (kind)
                {
                    case 1: for (int i = 0; i < n; i++) signal[i] = RandomEngine.NextGaussian(rng); break;
                    case 2:
                        signal[0] = RandomEngine.NextGaussian(rng);
                        for (int i = 1; i < n; i++) signal[i] = phi * signal[i - 1] + RandomEngine.NextGaussian(rng);
                        break;
                    default: for (int i = 0; i < n; i++) signal[i] = Math.Sin(2 * Math.PI * freq * i); break;
                }
                var acf = Correlation.AutoCorrelation(signal);
                var tAcf = new Vector(acf.Count); for (int i = 0; i < acf.Count; i++) tAcf[i] = i - acf.Count / 2;
                cv.ChartName = $"АКФ: {name}";
                cv.LabelX = "отставание τ"; cv.LabelY = "R(τ)";
                cv.AddPlot(tAcf, acf, "R(τ)", Palette[0], width: 2);
                var statS = new Statistic(signal);
                textOut = $"Сигнал:      {name}\nДлина N:     {n}\nСреднее:     {statS.Expected:F4}\nДисперсия:   {statS.Variance:F4}\nMax |R(τ)|:  {acf.Transform(Math.Abs).Max():F4}";
                break;
            }
            case "crosscorr":
            {
                int n = Math.Max(64, (int)N("n", 256));
                int lag = (int)N("lag", 18);
                double noise = Math.Max(0, N("noise", 0.3));
                var rng = new Random((int)N("seed", 42));
                var a = new Vector(n); var b = new Vector(n);
                for (int i = 0; i < n; i++)
                {
                    double v = Math.Sin(2 * Math.PI * 0.05 * i) + 0.5 * Math.Sin(2 * Math.PI * 0.11 * i);
                    a[i] = v + RandomEngine.NextGaussian(rng) * noise;
                    int j = i - lag;
                    double w = (j >= 0 && j < n) ? Math.Sin(2 * Math.PI * 0.05 * j) + 0.5 * Math.Sin(2 * Math.PI * 0.11 * j) : 0;
                    b[i] = w + RandomEngine.NextGaussian(rng) * noise;
                }
                var xc = Correlation.CrossCorrelation(a, b);
                int argmax = 0; double mx = xc[0];
                for (int i = 1; i < xc.Count; i++) if (xc[i] > mx) { mx = xc[i]; argmax = i; }
                int estLag = argmax - xc.Count / 2;
                var tx = new Vector(xc.Count); for (int i = 0; i < xc.Count; i++) tx[i] = i - xc.Count / 2;
                cv.ChartName = $"ВКФ: истинная задержка = {lag}, оценка = {estLag}  (ошибка {Math.Abs(estLag - lag)})";
                cv.LabelX = "τ"; cv.LabelY = "R_xy(τ)";
                cv.AddPlot(tx, xc, "R_xy", Palette[0], width: 2);
                DrawVLine(cv, lag,    xc.Max(), "истинное", Palette[2], width: 3);
                DrawVLine(cv, estLag, xc.Max(), "оценка",   Palette[1], width: 2);
                break;
            }
            case "convolution":
            {
                int n = Math.Max(32, (int)N("n", 128));
                int kw = Math.Max(3, (int)N("kw", 15));
                if (kw % 2 == 0) kw++;
                double sigma = Math.Max(0.1, N("sigma", 3.0));
                var sig = new Vector(n);
                for (int i = 0; i < n; i++) sig[i] = (i > n / 3 && i < 2 * n / 3) ? 1.0 : 0.0;
                for (int i = n / 2 - 5; i < n / 2 + 5; i++) if (i >= 0 && i < n) sig[i] = 1.5;
                var ker = new Vector(kw);
                double half = (kw - 1) / 2.0, ks = 0;
                for (int i = 0; i < kw; i++) { double x = i - half; ker[i] = Math.Exp(-x * x / (2 * sigma * sigma)); ks += ker[i]; }
                ker /= ks;
                var res = Convolution.DirectConvolution(sig, ker);
                int outLen = res.Count, offset = (outLen - n) / 2;
                var tSig = new Vector(n); for (int i = 0; i < n; i++) tSig[i] = i;
                var tRes = new Vector(outLen); for (int i = 0; i < outLen; i++) tRes[i] = i - offset;
                var tKer = new Vector(kw); for (int i = 0; i < kw; i++) tKer[i] = i - (kw - 1) / 2.0;
                cv.ChartName = $"Свёртка с Гауссовым ядром (σ={sigma:F1}, ширина {kw})";
                cv.LabelX = "t"; cv.LabelY = "значение";
                cv.AddPlot(tSig, sig, "Сигнал", Palette[0], width: 2);
                cv.AddPlot(tRes, res, "Свёртка", Palette[1], width: 3);
                cv.AddPlot(tKer, ker * (sig.Max() * 0.8 / Math.Max(1e-9, ker.Max())), $"Ядро ×{sig.Max() * 0.8 / Math.Max(1e-9, ker.Max()):F1}", Palette[2], width: 2);
                break;
            }
        }
    }

    #endregion

    #region Рендеринг матрицы корреляций

    private static SKImage RenderMatrixHeatmap(Matrix M, int w, int h, double minV, double maxV)
    {
        int rows = M.Height, cols = M.Width;
        var bmp = new SKBitmap(w, h);
        double range = Math.Max(1e-9, maxV - minV);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Transparent);
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
            {
                double t = Math.Clamp((M[i, j] - minV) / range, 0, 1);
                var paint = new SKPaint { Color = DivergingColor(t, 220) };
                int x0 = j * w / cols, x1 = (j + 1) * w / cols;
                int y0 = i * h / rows, y1 = (i + 1) * h / rows;
                canvas.DrawRect(x0, y0, x1 - x0, y1 - y0, paint);
                paint.Color = new SKColor(20, 20, 20, 220);
                paint.TextSize = Math.Min(14, Math.Min((x1 - x0) / 3f, (y1 - y0) / 2f));
                paint.IsAntialias = true;
                canvas.DrawText(M[i, j].ToString("F2"), (x0 + x1) / 2f - paint.TextSize * 1.1f, (y0 + y1) / 2f + paint.TextSize / 3f, paint);
            }
        return SKImage.FromBitmap(bmp);
    }

    private static SKColor DivergingColor(double t, byte alpha)
    {
        (double t, byte r, byte g, byte b)[] stops =
        {
            (0.00, 0x21, 0x66, 0xAC),
            (0.25, 0x67, 0xA9, 0xCF),
            (0.50, 0xF7, 0xF7, 0xF7),
            (0.75, 0xEF, 0x8A, 0x62),
            (1.00, 0xB2, 0x18, 0x2B),
        };
        for (int i = 0; i < stops.Length - 1; i++)
        {
            if (t <= stops[i + 1].t)
            {
                double k = (t - stops[i].t) / Math.Max(1e-9, stops[i + 1].t - stops[i].t);
                return new SKColor(
                    (byte)(stops[i].r + k * (stops[i + 1].r - stops[i].r)),
                    (byte)(stops[i].g + k * (stops[i + 1].g - stops[i].g)),
                    (byte)(stops[i].b + k * (stops[i + 1].b - stops[i].b)),
                    alpha);
            }
        }
        return new SKColor(stops[^1].r, stops[^1].g, stops[^1].b, alpha);
    }

    #endregion
}
