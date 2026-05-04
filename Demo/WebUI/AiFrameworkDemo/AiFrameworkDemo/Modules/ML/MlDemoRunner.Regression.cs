using AI.Charts;
using AI.Charts.JS;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using AI.ML.Genetic.GeneticCore;
using AI.ML.Regression;
using SkiaSharp;
using Vector = AI.DataStructs.Algebraic.Vector;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.ML;

public static partial class MlDemoRunner
{
    #region Регрессия / PCA / AR / ГА — случаи

    private static void RunRegressionCase(
        string key, IReadOnlyDictionary<string, double> p,
        ChartView cv, ref PlotlyBuilder? plotly)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        switch (key)
        {
            case "lin_reg":
            {
                double k = N("k", 2.0), b = N("b", 1.0), noise = N("noise", 0.8);
                var rng = new Random(42);
                var xArr = Vector.Seq(-3, 0.1, 3);
                var yArr = xArr.Transform(xi => k * xi + b + (rng.NextDouble() - 0.5) * 2 * noise);
                var reg = new LinearRegression(xArr, yArr);
                var yPred = reg.Predict(xArr);
                double yMean = yArr.Mean(), ssTot = 0, ssRes = 0;
                for (int i = 0; i < yArr.Count; i++)
                {
                    ssTot += (yArr[i] - yMean) * (yArr[i] - yMean);
                    ssRes += (yArr[i] - yPred[i]) * (yArr[i] - yPred[i]);
                }
                double r2 = 1 - ssRes / Math.Max(1e-12, ssTot);
                cv.ChartName = $"Линейная регрессия  —  y = {reg.Lrm.Slope:F3}·x + {reg.Lrm.Intercept:F3}   R²={r2:F3}";
                cv.LabelX = "x"; cv.LabelY = "y";
                var bandColor = new SKColor(Palette[1].Red, Palette[1].Green, Palette[1].Blue, 130);
                cv.AddPlot(xArr, yPred + noise, "+σ", bandColor, width: 1);
                cv.AddPlot(xArr, yPred - noise, "−σ", bandColor, width: 1);
                cv.AddScatterMark3(xArr, yArr, "Данные", Palette[0]);
                cv.AddPlot(xArr, yPred, "Регрессия", Palette[1], width: 3);
                break;
            }
            case "poly_reg":
            {
                int deg = Math.Clamp((int)N("deg", 3), 1, 6);
                double noise = N("noise", 0.4);
                var rng = new Random(42);
                var xArr = Vector.Seq(-2, 0.12, 2);
                var yTrue = xArr.Transform(xi => Math.Sin(1.5 * xi) + 0.5 * xi);
                var yArr = new Vector(xArr.Count);
                for (int i = 0; i < xArr.Count; i++) yArr[i] = yTrue[i] + (rng.NextDouble() - 0.5) * 2 * noise;
                var poly = new PolynomialRegression(xArr, yArr, deg);
                var xFine = Vector.Seq(-2, 0.04, 2);
                var yFit = poly.Predict(xFine);
                double yMean = yArr.Mean(), ssTot = 0, ssRes = 0;
                var yPredAtData = poly.Predict(xArr);
                for (int i = 0; i < yArr.Count; i++)
                {
                    ssTot += (yArr[i] - yMean) * (yArr[i] - yMean);
                    ssRes += (yArr[i] - yPredAtData[i]) * (yArr[i] - yPredAtData[i]);
                }
                double r2 = 1 - ssRes / Math.Max(1e-12, ssTot);
                double mse = ssRes / yArr.Count;
                cv.ChartName = $"Полиномиальная регрессия степени {deg}  —  R²={r2:F3}  MSE={mse:F3}";
                cv.LabelX = "x"; cv.LabelY = "y";
                cv.AddPlot(xFine, xFine.Transform(xi => Math.Sin(1.5 * xi) + 0.5 * xi), "Истинная f(x)", Palette[2], width: 2);
                cv.AddScatterMark3(xArr, yArr, "Данные + шум", Palette[0]);
                cv.AddPlot(xFine, yFit, $"Полином {deg}-й степени", Palette[1], width: 3);
                break;
            }
            case "multiple_reg":
            {
                int n = Math.Max(30, (int)N("n", 80));
                double noise = N("noise", 1.5);
                var rng = new Random(42);
                var X = new Vector[n]; var targets = new Vector(n);
                for (int i = 0; i < n; i++)
                {
                    double x1 = (rng.NextDouble() - 0.5) * 4;
                    double x2 = (rng.NextDouble() - 0.5) * 4;
                    double x3 = (rng.NextDouble() - 0.5) * 4;
                    X[i] = new Vector(new[] { x1, x2, x3 });
                    targets[i] = 3 * x1 + 2 * x2 - 1 * x3 + (rng.NextDouble() - 0.5) * 2 * noise;
                }
                var reg = new MultipleRegression(isScale: true);
                reg.Train(X, targets);
                var pred = reg.Predict(X);
                double yMean = targets.Mean(), ssTot = 0, ssRes = 0;
                for (int i = 0; i < n; i++)
                {
                    ssTot += (targets[i] - yMean) * (targets[i] - yMean);
                    ssRes += (targets[i] - pred[i]) * (targets[i] - pred[i]);
                }
                double r2 = 1 - ssRes / Math.Max(1e-12, ssTot);
                cv.ChartName = $"Множественная регрессия  —  y=3x₁+2x₂−x₃   R²={r2:F3}  MSE={ssRes / n:F3}";
                cv.LabelX = "y (истинное)"; cv.LabelY = "ŷ (предсказанное)";
                cv.AddPlot(targets, targets, "Идеал y = ŷ", Palette[2], width: 2);
                cv.AddScatterMark3(targets, pred, "Предсказание", Palette[0]);
                break;
            }
            case "pca_2d":
            {
                int n = Math.Max(30, (int)N("n", 120));
                double angle = N("angle", 35) * Math.PI / 180.0;
                var rng = new Random(42);
                var xs = new Vector(n); var ys = new Vector(n);
                for (int i = 0; i < n; i++)
                {
                    double u = (rng.NextDouble() - 0.5) * 4, v = (rng.NextDouble() - 0.5) * 0.8;
                    xs[i] = u * Math.Cos(angle) - v * Math.Sin(angle);
                    ys[i] = u * Math.Sin(angle) + v * Math.Cos(angle);
                }
                double mx = xs.Mean(), my = ys.Mean(), cxx = 0, cxy = 0, cyy = 0;
                for (int i = 0; i < n; i++)
                {
                    double dx = xs[i] - mx, dy = ys[i] - my;
                    cxx += dx * dx; cxy += dx * dy; cyy += dy * dy;
                }
                cxx /= n; cxy /= n; cyy /= n;
                double vx = 1, vy = 0;
                for (int iter = 0; iter < 200; iter++)
                {
                    double nx = cxx * vx + cxy * vy, ny = cxy * vx + cyy * vy;
                    double nm = Math.Sqrt(nx * nx + ny * ny);
                    if (nm < 1e-12) break;
                    vx = nx / nm; vy = ny / nm;
                }
                double lam1 = cxx * vx * vx + 2 * cxy * vx * vy + cyy * vy * vy;
                double wx = -vy, wy = vx;
                double lam2 = cxx * wx * wx + 2 * cxy * wx * wy + cyy * wy * wy;
                double totalVar = cxx + cyy;
                double ep1 = totalVar > 1e-12 ? 100.0 * lam1 / totalVar : 0;
                double ep2 = totalVar > 1e-12 ? 100.0 * lam2 / totalVar : 0;
                double scl1 = 2.5 * Math.Sqrt(Math.Max(0, lam1));
                double scl2 = 2.5 * Math.Sqrt(Math.Max(0, lam2));
                cv.ChartName = $"PCA  —  PC1: {ep1:F1}%   PC2: {ep2:F1}%";
                cv.LabelX = "x₁"; cv.LabelY = "x₂";
                cv.AddScatterMark3(xs, ys, "Данные", Palette[0]);
                cv.AddPlot(new Vector(new[] { mx - vx * scl1, mx + vx * scl1 }), new Vector(new[] { my - vy * scl1, my + vy * scl1 }), $"PC1 ({ep1:F1}%)", Palette[1], width: 3);
                cv.AddPlot(new Vector(new[] { mx - wx * scl2, mx + wx * scl2 }), new Vector(new[] { my - wy * scl2, my + wy * scl2 }), $"PC2 ({ep2:F1}%)", Palette[2], width: 2);
                break;
            }
            case "ar_predict":
            {
                int window   = Math.Max(2, (int)N("window", 5));
                int trainLen = Math.Max(window + 10, (int)N("trainLen", 80));
                int predLen  = Math.Max(1, (int)N("predLen", 30));
                double freq  = Math.Clamp(N("freq", 0.15), 0.01, 0.49);
                var rng = new Random(42);
                double[] y = new double[trainLen];
                for (int i = 0; i < trainLen; i++)
                    y[i] = Math.Sin(2 * Math.PI * freq * i)
                         + 0.25 * Math.Sin(2 * Math.PI * freq * 3 * i)
                         + (rng.NextDouble() - 0.5) * 0.2;
                double[] coeffs = FitAR(y, window);
                double[] ext = new double[trainLen + predLen];
                Array.Copy(y, ext, trainLen);
                for (int i = trainLen; i < ext.Length; i++)
                {
                    ext[i] = coeffs[0];
                    for (int j = 0; j < window; j++) ext[i] += coeffs[j + 1] * ext[i - 1 - j];
                }
                var trainT = new Vector(trainLen); var trainY = new Vector(trainLen);
                var predT  = new Vector(predLen);  var predY  = new Vector(predLen);
                var trueT  = new Vector(predLen);  var trueY  = new Vector(predLen);
                for (int i = 0; i < trainLen; i++) { trainT[i] = i; trainY[i] = y[i]; }
                double mse = 0;
                for (int i = 0; i < predLen; i++)
                {
                    predT[i] = trueT[i] = trainLen + i;
                    predY[i] = ext[trainLen + i];
                    trueY[i] = Math.Sin(2 * Math.PI * freq * (trainLen + i))
                             + 0.25 * Math.Sin(2 * Math.PI * freq * 3 * (trainLen + i));
                    double d = predY[i] - trueY[i]; mse += d * d;
                }
                mse /= predLen;
                cv.ChartName = $"AR-прогноз  —  окно {window}, горизонт {predLen}, MSE={mse:F4}";
                cv.LabelX = "Время"; cv.LabelY = "Значение";
                cv.AddPlot(trainT, trainY, "Обучение", Palette[0], width: 2);
                cv.AddPlot(trueT,  trueY,  "Истина",   Palette[2], width: 2);
                cv.AddPlot(predT,  predY,  "Прогноз",  Palette[1], width: 3);
                break;
            }
            case "genetic":
            {
                int popSize  = Math.Max(10, (int)N("popSize", 30));
                int epochs   = Math.Max(5,  (int)N("epochs", 60));
                double mutProb = Math.Clamp(N("mutProb", 0.25), 0.01, 0.9);
                const double trueA = 2.0, trueB = -1.0, trueC = 0.5;
                var xArr = Vector.Seq(-2, 0.2, 2);
                var inpVectors    = new Vector[xArr.Count];
                var targetVectors = new Vector[xArr.Count];
                for (int i = 0; i < xArr.Count; i++)
                {
                    inpVectors[i]    = new Vector(new[] { xArr[i] });
                    targetVectors[i] = new Vector(new[] { trueA * xArr[i] * xArr[i] + trueB * xArr[i] + trueC });
                }
                Func<Vector, Vector, Vector> model = (x, pv) =>
                    new Vector(new[] { pv[0] * x[0] * x[0] + pv[1] * x[0] + pv[2] });
                var pop = new Population(popSize, 3, model, inpVectors, targetVectors, -6, 6)
                {
                    mutProb = mutProb, LiderCount = Math.Max(2, popSize / 5)
                };
                pop.SortCells();
                var bestHist = new double[epochs]; var avgHist = new double[epochs];
                for (int e = 0; e < epochs; e++)
                {
                    pop.Epoch(popSize);
                    double bestMse = double.MaxValue, sumMse = 0; int cnt = 0;
                    foreach (var cell in pop)
                    {
                        double mse2 = 0;
                        for (int i = 0; i < xArr.Count; i++)
                        {
                            double yP = cell.Parametrs[0] * xArr[i] * xArr[i] + cell.Parametrs[1] * xArr[i] + cell.Parametrs[2];
                            double d  = yP - targetVectors[i][0]; mse2 += d * d;
                        }
                        mse2 /= xArr.Count;
                        if (mse2 < bestMse) bestMse = mse2;
                        sumMse += mse2; cnt++;
                    }
                    bestHist[e] = bestMse; avgHist[e] = cnt > 0 ? sumMse / cnt : bestMse;
                }
                var epochAxis = Vector.Seq(1, 1.0, epochs);
                cv.ChartName = $"Генетический алгоритм  —  MSE={bestHist[^1]:F4}, эпох={epochs}";
                cv.LabelX = "Эпоха"; cv.LabelY = "MSE (log)";
                cv.AddPlot(epochAxis, new Vector(avgHist .Select(x => Math.Log10(x + 1e-10)).ToArray()), "Средний MSE (log10)", Palette[3], width: 2);
                cv.AddPlot(epochAxis, new Vector(bestHist.Select(x => Math.Log10(x + 1e-10)).ToArray()), "Лучший MSE (log10)",  Palette[1], width: 3);
                break;
            }
            case "genetic_fit":
            {
                int popSize = Math.Max(10, (int)N("popSize", 30));
                int epochs  = Math.Max(5,  (int)N("epochs", 80));
                const double trueA = 2.0, trueB = -1.0, trueC = 0.5;
                var xArr = Vector.Seq(-2, 0.2, 2);
                var inpVectors    = new Vector[xArr.Count];
                var targetVectors = new Vector[xArr.Count];
                for (int i = 0; i < xArr.Count; i++)
                {
                    inpVectors[i]    = new Vector(new[] { xArr[i] });
                    targetVectors[i] = new Vector(new[] { trueA * xArr[i] * xArr[i] + trueB * xArr[i] + trueC });
                }
                Func<Vector, Vector, Vector> model = (x, pv) =>
                    new Vector(new[] { pv[0] * x[0] * x[0] + pv[1] * x[0] + pv[2] });
                var pop = new Population(popSize, 3, model, inpVectors, targetVectors, -6, 6)
                {
                    LiderCount = Math.Max(2, popSize / 5)
                };
                pop.SortCells();
                var initParams = pop[0].Parametrs;
                for (int e = 0; e < epochs; e++) pop.Epoch(popSize);
                Cell? bestCell = null; double bestMseFinal = double.MaxValue;
                foreach (var cell in pop)
                {
                    double mse = 0;
                    for (int i = 0; i < xArr.Count; i++)
                    {
                        double yP = cell.Parametrs[0] * xArr[i] * xArr[i] + cell.Parametrs[1] * xArr[i] + cell.Parametrs[2];
                        double d  = yP - targetVectors[i][0]; mse += d * d;
                    }
                    mse /= xArr.Count;
                    if (mse < bestMseFinal) { bestMseFinal = mse; bestCell = cell; }
                }
                var xFine = Vector.Seq(-2, 0.05, 2);
                var yTarget = xFine.Transform(xi => trueA * xi * xi + trueB * xi + trueC);
                var yInit   = xFine.Transform(xi => initParams[0] * xi * xi + initParams[1] * xi + initParams[2]);
                Vector yBest = bestCell is null
                    ? xFine.Transform(_ => 0.0)
                    : xFine.Transform(xi => bestCell.Parametrs[0] * xi * xi + bestCell.Parametrs[1] * xi + bestCell.Parametrs[2]);
                cv.ChartName = $"ГА подбор параметров  —  MSE={bestMseFinal:F4}";
                cv.LabelX = "x"; cv.LabelY = "y";
                cv.AddPlot(xFine, yTarget, "Цель: 2x²−x+0.5",       Palette[2], width: 3);
                cv.AddPlot(xFine, yInit,   "Начальное приближение", new SKColor(160, 160, 160, 180), width: 2);
                cv.AddPlot(xFine, yBest,   "Лучший результат",      Palette[1], width: 3);
                break;
            }
            case "multiple_reg_3d":
            {
                int n = Math.Max(30, (int)N("n", 80));
                double noise = N("noise", 1.5);
                var rng = new Random(42);
                var X = new Vector[n]; var targets = new Vector(n);
                for (int i = 0; i < n; i++)
                {
                    double x1 = (rng.NextDouble() - 0.5) * 4, x2 = (rng.NextDouble() - 0.5) * 4;
                    X[i] = new Vector(new[] { x1, x2, 0.0 });
                    targets[i] = 3 * x1 + 2 * x2 + (rng.NextDouble() - 0.5) * 2 * noise;
                }
                var reg = new MultipleRegression(isScale: true);
                reg.Train(X, targets);
                const int G = 30;
                var xGrid = new Vector(G); var yGrid = new Vector(G);
                for (int i = 0; i < G; i++) { xGrid[i] = -2.0 + 4.0 * i / (G - 1); yGrid[i] = -2.0 + 4.0 * i / (G - 1); }
                var zSurf = new double[G, G];
                for (int ix = 0; ix < G; ix++)
                    for (int iy = 0; iy < G; iy++)
                        zSurf[ix, iy] = reg.Predict(new Vector(new[] { xGrid[ix], yGrid[iy], 0.0 }));
                var scX = new Vector(n); var scY = new Vector(n); var scZ = new Vector(n);
                for (int i = 0; i < n; i++) { scX[i] = X[i][0]; scY[i] = X[i][1]; scZ[i] = targets[i]; }
                cv.ChartName = "3D множественная регрессия  y = 3x₁ + 2x₂  (x₃=0)";
                cv.LabelX = "x₁"; cv.LabelY = "x₂"; cv.LabelZ = "y";
                cv.Camera3D.Azimuth = N("azimuth", -35);
                cv.Camera3D.Elevation = N("elevation", 25);
                cv.AddSurface(xGrid, yGrid, zSurf, "Регрессия", ColormapKind.Viridis, showEdges: false);
                cv.AddScatter3D(scX, scY, scZ, "Обучающие точки");
                plotly = new PlotlyBuilder { Title = cv.ChartName, AxisX = "x₁", AxisY = "x₂", AxisZ = "y" };
                plotly.AddSurface(ToArray(xGrid), ToArray(yGrid), zSurf, "Регрессия", "Viridis", 0.85, false);
                plotly.AddScatter3D(ToArray(scX), ToArray(scY), ToArray(scZ), "Обучающие точки", colorByZ: true);
                break;
            }
            case "pca_3d":
            {
                int n = Math.Max(30, (int)N("n", 120));
                var rng = new Random(42);
                var xs = new Vector(n); var ys = new Vector(n); var zs = new Vector(n);
                double ax = 0.6, ay = 0.3, az = 0.7;
                double norm = Math.Sqrt(ax * ax + ay * ay + az * az);
                ax /= norm; ay /= norm; az /= norm;
                for (int i = 0; i < n; i++)
                {
                    double u = (rng.NextDouble() - 0.5) * 6;
                    double v1 = (rng.NextDouble() - 0.5) * 1.2, v2 = (rng.NextDouble() - 0.5) * 0.6;
                    xs[i] = ax * u + v1 * 0.4 + v2 * 0.1;
                    ys[i] = ay * u + v1 * 0.6 + v2 * 0.3;
                    zs[i] = az * u + v1 * 0.2 + v2 * 0.9;
                }
                double mx = xs.Mean(), my = ys.Mean(), mz = zs.Mean();
                double[,] cov = new double[3, 3];
                for (int i = 0; i < n; i++)
                {
                    double dx = xs[i] - mx, dy = ys[i] - my, dz = zs[i] - mz;
                    cov[0, 0] += dx * dx; cov[0, 1] += dx * dy; cov[0, 2] += dx * dz;
                    cov[1, 0] += dy * dx; cov[1, 1] += dy * dy; cov[1, 2] += dy * dz;
                    cov[2, 0] += dz * dx; cov[2, 1] += dz * dy; cov[2, 2] += dz * dz;
                }
                for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) cov[r, c] /= n;
                var pc = PowerIteration3D(cov);
                double scale = 3.0;
                var pcLineX = new Vector(2) { [0] = mx - pc[0] * scale, [1] = mx + pc[0] * scale };
                var pcLineY = new Vector(2) { [0] = my - pc[1] * scale, [1] = my + pc[1] * scale };
                var pcLineZ = new Vector(2) { [0] = mz - pc[2] * scale, [1] = mz + pc[2] * scale };
                cv.ChartName = "PCA в 3D пространстве";
                cv.LabelX = "x"; cv.LabelY = "y"; cv.LabelZ = "z";
                cv.Camera3D.Azimuth = N("azimuth", -35);
                cv.Camera3D.Elevation = N("elevation", 25);
                cv.AddScatter3D(xs, ys, zs, "Данные");
                cv.AddScatter3D(pcLineX, pcLineY, pcLineZ, "PC1");
                plotly = new PlotlyBuilder { Title = cv.ChartName, AxisX = "x", AxisY = "y", AxisZ = "z" };
                plotly.AddScatter3D(ToArray(xs), ToArray(ys), ToArray(zs), "Данные", "#6366f1", 3);
                plotly.AddScatter3D(ToArray(pcLineX), ToArray(pcLineY), ToArray(pcLineZ), "PC1", "#ef4444", 5);
                break;
            }
            case "genetic_landscape":
            {
                const double trueA = 2.0, trueB = -1.0, trueC = 0.5;
                var xArr = Vector.Seq(-2, 0.2, 2);
                var targetVectors = new Vector[xArr.Count];
                for (int i = 0; i < xArr.Count; i++)
                    targetVectors[i] = new Vector(new[] { trueA * xArr[i] * xArr[i] + trueB * xArr[i] + trueC });
                const int G = 40;
                var aGrid = new Vector(G); var bGrid = new Vector(G);
                for (int i = 0; i < G; i++) { aGrid[i] = -1.0 + 6.0 * i / (G - 1); bGrid[i] = -4.0 + 6.0 * i / (G - 1); }
                var zSurf = new double[G, G];
                for (int ia = 0; ia < G; ia++)
                    for (int ib = 0; ib < G; ib++)
                    {
                        double a = aGrid[ia], b = bGrid[ib], mse = 0;
                        for (int ix = 0; ix < xArr.Count; ix++)
                        {
                            double d = a * xArr[ix] * xArr[ix] + b * xArr[ix] + trueC - targetVectors[ix][0];
                            mse += d * d;
                        }
                        zSurf[ia, ib] = Math.Log10(mse / xArr.Count + 1e-10);
                    }
                cv.ChartName = "Ландшафт потерь GA: log₁₀(MSE) по (a, b), c = 0.5";
                cv.LabelX = "a"; cv.LabelY = "b"; cv.LabelZ = "log₁₀(MSE)";
                cv.Camera3D.Azimuth = N("azimuth", -35);
                cv.Camera3D.Elevation = N("elevation", 25);
                cv.AddSurface(aGrid, bGrid, zSurf, "MSE(a, b)", ColormapKind.Thermal);
                cv.AddScatter3D(new Vector(1) { [0] = trueA }, new Vector(1) { [0] = trueB }, new Vector(1) { [0] = Math.Log10(1e-10) }, "Истина (2, −1)");
                plotly = new PlotlyBuilder { Title = cv.ChartName, AxisX = "a", AxisY = "b", AxisZ = "log₁₀(MSE)" };
                plotly.AddSurface(ToArray(aGrid), ToArray(bGrid), zSurf, "MSE(a, b)", "Hot");
                plotly.AddScatter3D(new[] { trueA }, new[] { trueB }, new[] { Math.Log10(1e-10) }, "Истина (2, −1)", "#00ff00", 6);
                break;
            }
        }
    }

    #endregion

    #region Математические утилиты регрессии

    /// <summary>AR(p) методом наименьших квадратов через нормальные уравнения.</summary>
    private static double[] FitAR(double[] y, int p)
    {
        int m = p + 1;
        double[,] XtX = new double[m, m];
        double[]  XtY = new double[m];
        Span<double> row = stackalloc double[m];
        for (int i = p; i < y.Length; i++)
        {
            row[0] = 1.0;
            for (int j = 0; j < p; j++) row[j + 1] = y[i - 1 - j];
            for (int a = 0; a < m; a++)
            {
                XtY[a] += row[a] * y[i];
                for (int b = 0; b < m; b++) XtX[a, b] += row[a] * row[b];
            }
        }
        return SolveGauss(XtX, XtY, m);
    }

    private static double[] SolveGauss(double[,] A, double[] b, int n)
    {
        double[,] a = (double[,])A.Clone();
        double[]  x = (double[])b.Clone();
        for (int i = 0; i < n; i++)
        {
            int pivot = i;
            for (int k = i + 1; k < n; k++)
                if (Math.Abs(a[k, i]) > Math.Abs(a[pivot, i])) pivot = k;
            for (int j = 0; j < n; j++) (a[i, j], a[pivot, j]) = (a[pivot, j], a[i, j]);
            (x[i], x[pivot]) = (x[pivot], x[i]);
            double diag = a[i, i];
            if (Math.Abs(diag) < 1e-12) continue;
            for (int k = i + 1; k < n; k++)
            {
                double f = a[k, i] / diag;
                x[k] -= f * x[i];
                for (int j = i; j < n; j++) a[k, j] -= f * a[i, j];
            }
        }
        double[] result = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            result[i] = x[i];
            for (int j = i + 1; j < n; j++) result[i] -= a[i, j] * result[j];
            if (Math.Abs(a[i, i]) > 1e-12) result[i] /= a[i, i];
        }
        return result;
    }

    private static double[] PowerIteration3D(double[,] m, int iters = 200)
    {
        var v = new double[] { 1, 0, 0 };
        for (int it = 0; it < iters; it++)
        {
            var nv = new double[3];
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++) nv[r] += m[r, c] * v[c];
            double nm = Math.Sqrt(nv[0] * nv[0] + nv[1] * nv[1] + nv[2] * nv[2]);
            if (nm < 1e-12) break;
            v = new double[] { nv[0] / nm, nv[1] / nm, nv[2] / nm };
        }
        return v;
    }

    #endregion
}
