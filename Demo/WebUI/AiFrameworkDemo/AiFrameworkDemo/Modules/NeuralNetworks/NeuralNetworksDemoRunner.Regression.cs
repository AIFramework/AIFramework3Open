using AI.Charts;
using AI.Charts.JS;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;
using SkiaSharp;
using V2T = AI.ML.NeuralNetworks.V2.Tensor;
using V2S = AI.ML.NeuralNetworks.V2.Shape;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.NeuralNetworks;

public static partial class NeuralNetworksDemoRunner
{
    #region Регрессия — MLP

    private static void RunRegressionCase(
        string key,
        IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut, ref PlotlyBuilder? plotly)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        switch (key)
        {
            case "mlp_reg_1d":
                RunRegression1D(N, p, cv, ref textOut);
                break;
            case "mlp_reg_2d":
                RunRegression2D(N, p, cv, ref plotly, ref textOut);
                break;
            case "mlp_reg_2d_3d":
                RunRegression2D3D(N, p, cv, ref plotly, ref textOut);
                break;
        }
    }

    private static void RunRegression1D(
        Func<string, double, double> N,
        IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut)
    {
        int hidden   = Math.Max(4,  (int)N("hidden", 20));
        int epochs   = Math.Max(20, (int)N("epochs", 200));
        float lr     = (float)Math.Clamp(N("lr", 0.01), 0.0001, 0.5);
        double noise = Math.Clamp(N("noise", 0.15), 0, 2);
        var rng      = new Random(42);

        var xVec  = Vector.Seq(-3, 0.12, 3);
        int m     = xVec.Count;
        var yTrue = xVec.Transform(xi => Math.Sin(1.5 * xi) + 0.5 * xi);
        var yNoisy = new Vector(m);
        var xData = new float[m]; var yData = new float[m];
        for (int i = 0; i < m; i++)
        {
            yNoisy[i] = yTrue[i] + (rng.NextDouble() - 0.5) * 2 * noise;
            xData[i]  = (float)xVec[i];
            yData[i]  = (float)yNoisy[i];
        }
        var net   = new Sequential(new Linear(1, hidden, true, rng), new ReLU(), new Linear(hidden, 1, true, rng));
        var xFull = V2T.From(xData, new V2S(m, 1));
        var yFull = V2T.From(yData, new V2S(m, 1));
        var optim = new Adam(net.Parameters(), lr: lr);
        for (int epoch = 0; epoch < epochs; epoch++) { optim.ZeroGrad(); RegressionLosses.MSE(net.Forward(xFull), yFull).Backward(); optim.Step(); }

        var xFine = Vector.Seq(-3, 0.03, 3);
        int mf    = xFine.Count;
        var xFineArr = new float[mf];
        for (int i = 0; i < mf; i++) xFineArr[i] = (float)xFine[i];
        float[] finePred, trainPred;
        { using var _ = TapeContext.NoGrad(); finePred = net.Forward(V2T.From(xFineArr, new V2S(mf, 1))).AsReadOnlySpan<float>().ToArray(); trainPred = net.Forward(xFull).AsReadOnlySpan<float>().ToArray(); }

        var yPred     = new Vector(mf); for (int i = 0; i < mf; i++) yPred[i] = finePred[i];
        var yFineTrue = xFine.Transform(xi => Math.Sin(1.5 * xi) + 0.5 * xi);
        double yMean = yNoisy.Mean(); double ssTot = 0, ssRes = 0;
        for (int i = 0; i < m; i++) { ssTot += (yNoisy[i] - yMean) * (yNoisy[i] - yMean); ssRes += (yNoisy[i] - trainPred[i]) * (yNoisy[i] - trainPred[i]); }
        double r2 = 1 - ssRes / Math.Max(1e-12, ssTot), mse = ssRes / m;
        cv.ChartName = $"Нейрорегрессия 1->{hidden}->1  —  R²={r2:F3}  MSE={mse:F3}  (epochs={epochs})";
        cv.LabelX = "x"; cv.LabelY = "y";
        cv.AddPlot(xFine, yFineTrue, "Истинная f(x)", Palette[2], width: 2);
        cv.AddScatterMark3(xVec, yNoisy, "Данные + шум", Palette[0]);
        cv.AddPlot(xFine, yPred, $"Сеть (h={hidden})", Palette[1], width: 3);
        textOut = $"n={m}  hidden={hidden}  epochs={epochs}  lr={N("lr", 0.01):F4}  R²={r2:F4}  MSE={mse:F5}";
    }

    private static void RunRegression2D(
        Func<string, double, double> N,
        IReadOnlyDictionary<string, double> p,
        ChartView cv, ref PlotlyBuilder? plotly, ref string? textOut)
    {
        int hidden = Math.Max(8,  (int)N("hidden", 24));
        int n      = Math.Max(50, (int)N("n", 160));
        int epochs = Math.Max(20, (int)N("epochs", 120));
        float lr   = (float)Math.Clamp(N("lr", 0.02), 0.0001, 0.5);
        var rng    = new Random(42);

        double[] xc = new double[n], yc = new double[n], zc = new double[n];
        var xData = new float[n * 2]; var zData = new float[n];
        for (int i = 0; i < n; i++) { xc[i] = (rng.NextDouble() - 0.5) * 6; yc[i] = (rng.NextDouble() - 0.5) * 6; zc[i] = Math.Sin(xc[i]) * Math.Cos(yc[i]); xData[i * 2] = (float)xc[i]; xData[i * 2 + 1] = (float)yc[i]; zData[i] = (float)zc[i]; }
        var xFull = V2T.From(xData, new V2S(n, 2)); var zFull = V2T.From(zData, new V2S(n, 1));
        var net   = new Sequential(new Linear(2, hidden, true, rng), new ReLU(), new Linear(hidden, 1, true, rng));
        var optim = new Adam(net.Parameters(), lr: lr);
        for (int epoch = 0; epoch < epochs; epoch++) { optim.ZeroGrad(); RegressionLosses.MSE(net.Forward(xFull), zFull).Backward(); optim.Step(); }

        float[] trainPred; { using var _ = TapeContext.NoGrad(); trainPred = net.Forward(xFull).AsReadOnlySpan<float>().ToArray(); }
        double mse = 0; for (int i = 0; i < n; i++) { double d = trainPred[i] - zc[i]; mse += d * d; } mse /= n;

        var bounds = (xMin: -3.2, xMax: 3.2, yMin: -3.2, yMax: 3.2);
        cv.SetBackgroundImage(RenderScalarFieldBatched(net, bounds, 300, 260, minVal: -1.0, maxVal: 1.0));
        {
            int gridRes = 60;
            double[] xGrid = new double[gridRes], yGrid = new double[gridRes];
            double dx = (bounds.xMax - bounds.xMin) / (gridRes - 1), dy = (bounds.yMax - bounds.yMin) / (gridRes - 1);
            for (int i = 0; i < gridRes; i++) { xGrid[i] = bounds.xMin + i * dx; yGrid[i] = bounds.yMin + i * dy; }
            var inputData = new float[gridRes * gridRes * 2];
            for (int j = 0; j < gridRes; j++) for (int i = 0; i < gridRes; i++) { int ii = (j * gridRes + i) * 2; inputData[ii] = (float)xGrid[i]; inputData[ii + 1] = (float)yGrid[j]; }
            var input = V2T.From(inputData, new V2S(gridRes * gridRes, 2)); V2T output;
            using (var _ = TapeContext.NoGrad()) { output = net.Forward(input); }
            var zGrid = new double[gridRes][]; var outSpan = output.AsReadOnlySpan<float>();
            for (int j = 0; j < gridRes; j++) { zGrid[j] = new double[gridRes]; for (int i = 0; i < gridRes; i++) zGrid[j][i] = outSpan[j * gridRes + i]; }
            plotly = new PlotlyBuilder { Title = $"Нейрорегрессия 2->{hidden}->1:  z = sin(x)·cos(y),  MSE={mse:F4}", AxisX = "x", AxisY = "y" };
            plotly.AddHeatmap(xGrid, yGrid, zGrid, "RdBu", opacity: 0.75, showScale: true, zMin: -1.0, zMax: 1.0);
            plotly.AddScatter2D(xc, yc, "Обучающие точки", "#ffffff", markerSize: 4);
        }
        var trainX = new Vector(n); var trainY = new Vector(n);
        for (int i = 0; i < n; i++) { trainX[i] = xc[i]; trainY[i] = yc[i]; }
        cv.ChartName = $"Нейрорегрессия 2->{hidden}->1:  z = sin(x)·cos(y),  MSE={mse:F4}";
        cv.LabelX = "x"; cv.LabelY = "y";
        cv.AddScatterMark3(trainX, trainY, "Обучающие точки", new SKColor(255, 255, 255, 140));
        cv.SetAxisRange(bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax);
        textOut = $"2-D тепловая карта z=sin(x)·cos(y)  n={n}  hidden={hidden}  epochs={epochs}  MSE={mse:F5}";
    }

    private static void RunRegression2D3D(
        Func<string, double, double> N,
        IReadOnlyDictionary<string, double> p,
        ChartView cv, ref PlotlyBuilder? plotly, ref string? textOut)
    {
        int hidden = Math.Max(8,  (int)N("hidden", 24));
        int n      = Math.Max(50, (int)N("n", 160));
        int epochs = Math.Max(20, (int)N("epochs", 120));
        float lr   = (float)Math.Clamp(N("lr", 0.02), 0.0001, 0.5);
        double azimuth   = N("azimuth", -35);
        double elevation = N("elevation", 25);
        var rng    = new Random(42);

        double[] xc = new double[n], yc = new double[n], zc = new double[n];
        var xData = new float[n * 2]; var zData = new float[n];
        for (int i = 0; i < n; i++) { xc[i] = (rng.NextDouble() - 0.5) * 6; yc[i] = (rng.NextDouble() - 0.5) * 6; zc[i] = Math.Sin(xc[i]) * Math.Cos(yc[i]); xData[i * 2] = (float)xc[i]; xData[i * 2 + 1] = (float)yc[i]; zData[i] = (float)zc[i]; }
        var xFull = V2T.From(xData, new V2S(n, 2)); var zFull = V2T.From(zData, new V2S(n, 1));
        var net   = new Sequential(new Linear(2, hidden, true, rng), new ReLU(), new Linear(hidden, 1, true, rng));
        var optim = new Adam(net.Parameters(), lr: lr);
        for (int epoch = 0; epoch < epochs; epoch++) { optim.ZeroGrad(); RegressionLosses.MSE(net.Forward(xFull), zFull).Backward(); optim.Step(); }

        const int G = 30;
        var xGrid = new Vector(G); var yGrid = new Vector(G);
        for (int i = 0; i < G; i++) { xGrid[i] = -3.0 + 6.0 * i / (G - 1); yGrid[i] = -3.0 + 6.0 * i / (G - 1); }
        var zSurf = new double[G, G];
        var batchIn = new float[G * G * 2];
        for (int ix = 0; ix < G; ix++) for (int iy = 0; iy < G; iy++) { int ii = ix * G + iy; batchIn[ii * 2] = (float)xGrid[ix]; batchIn[ii * 2 + 1] = (float)yGrid[iy]; }
        float[] predSurf;
        using (var _ = TapeContext.NoGrad()) predSurf = net.Forward(V2T.From(batchIn, new V2S(G * G, 2))).AsReadOnlySpan<float>().ToArray();
        for (int ix = 0; ix < G; ix++) for (int iy = 0; iy < G; iy++) zSurf[ix, iy] = predSurf[ix * G + iy];

        double mse = 0; float[] trainPred;
        using (var _ = TapeContext.NoGrad()) trainPred = net.Forward(xFull).AsReadOnlySpan<float>().ToArray();
        for (int i = 0; i < n; i++) { double d = trainPred[i] - zc[i]; mse += d * d; } mse /= n;

        cv.ChartName = $"3D нейрорегрессия 2->{hidden}->1:  z = sin(x)·cos(y),  MSE={mse:F4}";
        cv.LabelX = "x"; cv.LabelY = "y"; cv.LabelZ = "z";
        cv.Camera3D.Azimuth = azimuth; cv.Camera3D.Elevation = elevation;
        cv.AddSurface(xGrid, yGrid, zSurf, "Предсказание сети", ColormapKind.Viridis);
        var scX = new Vector(n); var scY = new Vector(n); var scZ = new Vector(n);
        for (int i = 0; i < n; i++) { scX[i] = xc[i]; scY[i] = yc[i]; scZ[i] = zc[i]; }
        cv.AddScatter3D(scX, scY, scZ, "Обучающие точки");
        plotly = new PlotlyBuilder { Title = cv.ChartName, AxisX = "x", AxisY = "y", AxisZ = "z" };
        plotly.AddSurface(ToArray(xGrid), ToArray(yGrid), zSurf, "Предсказание сети", "Viridis");
        plotly.AddScatter3D(xc, yc, zc, "Обучающие точки", "#ffffff", 2, colorByZ: true);
        textOut = $"3-D поверхность z=sin(x)·cos(y)  n={n}  hidden={hidden}  epochs={epochs}  MSE={mse:F5}";
    }

    #endregion

    #region Рендеринг скалярного поля

    private static SKImage RenderScalarFieldBatched(
        Module net,
        (double xMin, double xMax, double yMin, double yMax) b,
        int gridW, int gridH, double minVal, double maxVal)
    {
        int total = gridW * gridH;
        var grid = new float[total * 2]; int idx = 0;
        for (int py = 0; py < gridH; py++)
        {
            double yCoord = b.yMax - (b.yMax - b.yMin) * py / Math.Max(1, gridH - 1);
            for (int px = 0; px < gridW; px++) { double xCoord = b.xMin + (b.xMax - b.xMin) * px / Math.Max(1, gridW - 1); grid[idx++] = (float)xCoord; grid[idx++] = (float)yCoord; }
        }
        float[] preds;
        using (var _ = TapeContext.NoGrad()) preds = net.Forward(V2T.From(grid, new V2S(total, 2))).AsReadOnlySpan<float>().ToArray();
        var bmp = new SKBitmap(gridW, gridH); double range = Math.Max(1e-9, maxVal - minVal); int pi = 0;
        for (int py = 0; py < gridH; py++) for (int px = 0; px < gridW; px++) { double t = Math.Clamp((preds[pi++] - minVal) / range, 0, 1); bmp.SetPixel(px, py, ViridisColor(t, alpha: 160)); }
        return SKImage.FromBitmap(bmp);
    }

    private static SKColor ViridisColor(double t, byte alpha)
    {
        (double t, byte r, byte g, byte b)[] stops = [(0.00, 0x44, 0x01, 0x54), (0.25, 0x3B, 0x52, 0x8B), (0.50, 0x20, 0x90, 0x8C), (0.75, 0x5E, 0xC9, 0x62), (1.00, 0xFD, 0xE7, 0x25)];
        for (int i = 0; i < stops.Length - 1; i++)
        {
            if (t <= stops[i + 1].t) { double k = (t - stops[i].t) / (stops[i + 1].t - stops[i].t); return new SKColor((byte)(stops[i].r + k * (stops[i + 1].r - stops[i].r)), (byte)(stops[i].g + k * (stops[i + 1].g - stops[i].g)), (byte)(stops[i].b + k * (stops[i + 1].b - stops[i].b)), alpha); }
        }
        return new SKColor(stops[^1].r, stops[^1].g, stops[^1].b, alpha);
    }

    private static (double xMin, double xMax, double yMin, double yMax) GetBounds(
        Vector[] data, double padding = 0.5)
    {
        double xMin = double.MaxValue, xMax = double.MinValue, yMin = double.MaxValue, yMax = double.MinValue;
        foreach (var d in data) { xMin = Math.Min(xMin, d[0]); xMax = Math.Max(xMax, d[0]); yMin = Math.Min(yMin, d[1]); yMax = Math.Max(yMax, d[1]); }
        if (xMax - xMin < 1e-6) { xMin -= 1; xMax += 1; } if (yMax - yMin < 1e-6) { yMin -= 1; yMax += 1; }
        return (xMin - padding, xMax + padding, yMin - padding, yMax + padding);
    }

    #endregion
}
