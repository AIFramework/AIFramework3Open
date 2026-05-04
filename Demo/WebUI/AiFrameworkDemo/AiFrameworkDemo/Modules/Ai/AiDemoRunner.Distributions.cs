using AI;
using AI.Charts;
using AI.Charts.JS;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using AI.Statistics;
using AI.Statistics.Distributions;
using AI.Statistics.MixtureModeling;
using AI.Statistics.MonteCarlo;
using System.Text;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.Ai;

public static partial class AiDemoRunner
{
    #region Распределения — случаи

    private static void RunDistributionsCase(
        string key, IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut, ref PlotlyBuilder? plotly)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        switch (key)
        {
            case "uniform_normal":
            {
                int n = Math.Max(100, (int)N("n", 1200));
                int bins = Math.Max(4, (int)N("bins", 28));
                int seed = (int)N("seed", 42);
                var uni = Statistic.UniformDistribution(n, new Random(seed));
                var nor = Statistic.RandNorm(n, new Random(seed + 1));
                var hU = new Statistic(uni).Histogramm(bins);
                var hN = new Statistic(nor).Histogramm(bins);
                cv.ChartName = $"U(0, 1) и N(0, 1) (n={n} на каждое)";
                cv.LabelX = "x"; cv.LabelY = "плотность";
                cv.AddBar(hU.X, hU.Y, "U(0, 1)", WithAlpha(Palette[0], 180));
                cv.AddBar(hN.X, hN.Y, "N(0, 1)", WithAlpha(Palette[1], 180));
                var stU = new Statistic(uni); var stN = new Statistic(nor);
                var sb = new StringBuilder();
                sb.AppendLine("> U(0,1) vs N(0,1)");
                sb.AppendLine();
                sb.AppendLine("                U(0,1)        N(0,1)       теор. U     теор. N");
                sb.AppendLine($"  среднее:   {stU.Expected,10:F4}  {stN.Expected,10:F4}    0.5000    0.0000");
                sb.AppendLine($"  дисперсия: {stU.Variance,10:F4}  {stN.Variance,10:F4}    0.0833    1.0000");
                sb.AppendLine($"  СКО:       {stU.STD,10:F4}  {stN.STD,10:F4}    0.2887    1.0000");
                sb.AppendLine($"  асимм.:    {stU.Asymmetry(),10:F4}  {stN.Asymmetry(),10:F4}    0.0000    0.0000");
                sb.AppendLine($"  эксцесс:   {stU.Excess(),10:F4}  {stN.Excess(),10:F4}   -1.2000    0.0000");
                textOut = sb.ToString();
                break;
            }
            case "clt":
            {
                int k = Math.Clamp((int)N("k", 12), 1, 50);
                int n = Math.Max(200, (int)N("n", 2000));
                int bins = Math.Max(4, (int)N("bins", 30));
                var rng = new Random((int)N("seed", 42));
                var means = new Vector(n);
                for (int i = 0; i < n; i++)
                {
                    double sum = 0;
                    for (int j = 0; j < k; j++) sum += rng.NextDouble();
                    means[i] = (sum - 0.5 * k) / Math.Sqrt(k / 12.0);
                }
                var stat = new Statistic(means);
                var h = stat.Histogramm(bins);
                double xMin = stat.MinValue - 0.3, xMax = stat.MaxValue + 0.3;
                var xGrid = Vector.Seq(xMin, (xMax - xMin) / 200.0, xMax);
                cv.ChartName = $"ЦПТ: (sum of {k} × U − k/2) / √(k/12),  n = {n} выборок";
                cv.LabelX = "Z"; cv.LabelY = "плотность";
                cv.AddBar(h.X, h.Y, $"Эмпирическая (k={k})", Palette[0]);
                cv.AddPlot(xGrid, xGrid.Transform(x => NormalPdf(x, 0, 1)), "N(0, 1) — предел", Palette[1], width: 3);
                break;
            }
            case "mle":
            {
                int n = Math.Max(30, (int)N("n", 400));
                double muT = N("muT", 1.5), sigT = Math.Max(0.05, N("sigT", 1.0));
                var rng = new Random((int)N("seed", 42));
                var data = new double[n];
                for (int i = 0; i < n; i++) data[i] = NonCorrelatedGaussian.Sample(muT, sigT, rng);
                var fit = NonCorrelatedGaussian.FitMaximumLikelihood(data);
                double muEst = fit[NonCorrelatedGaussian.KeyMean], sigEst = fit[NonCorrelatedGaussian.KeyStd];
                var sample = new Vector(data);
                var stat = new Statistic(sample);
                var h = stat.Histogramm(Math.Min(40, Math.Max(12, n / 20)));
                double xMin = stat.MinValue - 0.5, xMax = stat.MaxValue + 0.5;
                var xGrid = Vector.Seq(xMin, (xMax - xMin) / 200.0, xMax);
                cv.ChartName = $"ML-оценка: μ̂={muEst:F3} (ист. {muT:F3}), σ̂={sigEst:F3} (ист. {sigT:F3})";
                cv.LabelX = "x"; cv.LabelY = "p(x)";
                cv.AddBar(h.X, h.Y, "Данные", Palette[0]);
                cv.AddPlot(xGrid, xGrid.Transform(x => NormalPdf(x, muT, sigT)), "Истинная PDF", Palette[2], width: 3);
                cv.AddPlot(xGrid, xGrid.Transform(x => NormalPdf(x, muEst, sigEst)), "MLE PDF", Palette[1], width: 2);
                var sb = new StringBuilder();
                sb.AppendLine("> Метод максимального правдоподобия");
                sb.AppendLine();
                sb.AppendLine($"  Объём выборки:  n = {n}");
                sb.AppendLine();
                sb.AppendLine("                 Истина     Оценка      Ошибка");
                sb.AppendLine($"  μ:          {muT,10:F4} {muEst,10:F4} {muEst - muT,10:F4}");
                sb.AppendLine($"  σ:          {sigT,10:F4} {sigEst,10:F4} {sigEst - sigT,10:F4}");
                sb.AppendLine();
                sb.AppendLine($"  Теор. ст. ошибка μ̂ = σ/√n = {sigT / Math.Sqrt(n):F4}");
                sb.AppendLine($"  Теор. ст. ошибка σ̂ ≈ σ/√(2n) = {sigT / Math.Sqrt(2.0 * n):F4}");
                textOut = sb.ToString();
                break;
            }
            case "exponential":
            {
                int n = Math.Max(100, (int)N("n", 1500));
                double rate = Math.Max(0.05, N("rate", 1.0));
                int bins = Math.Max(8, (int)N("bins", 40));
                var rng = new Random((int)N("seed", 42));
                var sample = new Vector(n);
                for (int i = 0; i < n; i++) sample[i] = RandomEngine.NextExponential(rng, rate);
                var stat = new Statistic(sample);
                var h = stat.Histogramm(bins);
                double xMax = stat.MaxValue;
                var xGrid = Vector.Seq(0, xMax / 200.0, xMax);
                cv.ChartName = $"Exp(λ={rate:F2}): средн. {stat.Expected:F3} (теор. {1.0 / rate:F3}), σ {stat.STD:F3} (теор. {1.0 / rate:F3})";
                cv.LabelX = "x"; cv.LabelY = "p(x)";
                cv.AddBar(h.X, h.Y, "Эмпирическая", Palette[0]);
                cv.AddPlot(xGrid, xGrid.Transform(x => rate * Math.Exp(-rate * x)), "λ·e^(−λx)", Palette[1], width: 3);
                break;
            }
            case "gamma_beta":
            {
                int n = Math.Max(100, (int)N("n", 2000));
                double shape = Math.Max(0.1, N("shape", 2.0));
                double scale = Math.Max(0.1, N("scale", 1.0));
                double alpha = Math.Max(0.1, N("alpha", 2.0));
                double beta = Math.Max(0.1, N("beta", 5.0));
                int bins = Math.Max(8, (int)N("bins", 40));
                var rng = new Random((int)N("seed", 42));

                var gamSample = new Vector(n);
                var betaSample = new Vector(n);
                for (int i = 0; i < n; i++)
                {
                    gamSample[i] = RandomEngine.NextGamma(rng, shape, scale);
                    betaSample[i] = RandomEngine.NextBeta(rng, alpha, beta);
                }

                var stG = new Statistic(gamSample);
                var hG = stG.Histogramm(bins);
                double xMax = stG.MaxValue * 1.1;
                var xGrid = Vector.Seq(0.01, xMax / 250.0, xMax);

                cv.ChartName = $"Gamma({shape:F1},{scale:F1}) и Beta({alpha:F1},{beta:F1})";
                cv.LabelX = "x"; cv.LabelY = "плотность";
                cv.AddBar(hG.X, hG.Y, "Gamma — выборка", WithAlpha(Palette[0], 140));
                cv.AddPlot(xGrid, xGrid.Transform(x => GammaPdf(x, shape, scale)),
                    $"Gamma PDF", Palette[0], width: 3);

                var xBeta = Vector.Seq(0.01, 0.98 / 200.0, 0.99);
                cv.AddPlot(xBeta, xBeta.Transform(x => BetaPdf(x, alpha, beta)),
                    $"Beta PDF", Palette[1], width: 3);

                var stB = new Statistic(betaSample);
                double gamMeanT = shape * scale, gamVarT = shape * scale * scale;
                double betaMeanT = alpha / (alpha + beta);
                double betaVarT = alpha * beta / ((alpha + beta) * (alpha + beta) * (alpha + beta + 1));
                var sb = new StringBuilder();
                sb.AppendLine($"> Gamma(shape={shape:F1}, scale={scale:F1})");
                sb.AppendLine($"  среднее:  {stG.Expected:F4}  (теор. {gamMeanT:F4})");
                sb.AppendLine($"  дисп.:    {stG.Variance:F4}  (теор. {gamVarT:F4})");
                sb.AppendLine();
                sb.AppendLine($"> Beta(α={alpha:F1}, β={beta:F1})");
                sb.AppendLine($"  среднее:  {stB.Expected:F4}  (теор. {betaMeanT:F4})");
                sb.AppendLine($"  дисп.:    {stB.Variance:F4}  (теор. {betaVarT:F4})");
                textOut = sb.ToString();
                break;
            }
            case "cauchy_laplace":
            {
                int n = Math.Max(100, (int)N("n", 2000));
                double loc = N("loc", 0);
                double scaleCauchy = Math.Max(0.1, N("scaleCauchy", 1.0));
                double bLaplace = Math.Max(0.1, N("bLaplace", 1.0));
                int bins = Math.Max(8, (int)N("bins", 50));
                var rng = new Random((int)N("seed", 42));

                var cauchySample = new Vector(n);
                var laplaceSample = new Vector(n);
                for (int i = 0; i < n; i++)
                {
                    cauchySample[i] = RandomEngine.NextCauchy(rng, loc, scaleCauchy);
                    laplaceSample[i] = RandomEngine.NextLaplace(rng, loc, bLaplace);
                }

                // Гистограмма Лапласа (хорошо визуализируется)
                var stL = new Statistic(laplaceSample);
                var hL = stL.Histogramm(bins);

                // Общая сетка X для теоретических PDF
                double range = Math.Max(4 * scaleCauchy, 4 * bLaplace);
                var xGrid = Vector.Seq(loc - range, 2 * range / 300.0, loc + range);

                cv.ChartName = $"Cauchy(loc={loc:F1},γ={scaleCauchy:F1}) и Laplace(μ={loc:F1},b={bLaplace:F1})";
                cv.LabelX = "x"; cv.LabelY = "плотность";
                cv.AddBar(hL.X, hL.Y, "Laplace — выборка", WithAlpha(Palette[2], 140));
                cv.AddPlot(xGrid, xGrid.Transform(x => LaplacePdf(x, loc, bLaplace)),
                    "Laplace PDF", Palette[2], width: 3);
                cv.AddPlot(xGrid, xGrid.Transform(x => CauchyPdf(x, loc, scaleCauchy)),
                    "Cauchy PDF", Palette[3], width: 3);

                var sb = new StringBuilder();
                sb.AppendLine($"> Cauchy: среднее/дисперсия не определены. Медиана ≈ {loc:F2}");
                sb.AppendLine($"  Выборочная медиана: {Quantile.FastQuantile(cauchySample, 0.5):F4}");
                sb.AppendLine();
                sb.AppendLine($"> Laplace(μ={loc:F1}, b={bLaplace:F1})");
                sb.AppendLine($"  среднее:  {stL.Expected:F4}  (теор. {loc:F4})");
                sb.AppendLine($"  дисп.:    {stL.Variance:F4}  (теор. {2 * bLaplace * bLaplace:F4})");
                textOut = sb.ToString();
                break;
            }
            case "weibull_poisson":
            {
                int n = Math.Max(100, (int)N("n", 2000));
                double wShape = Math.Max(0.2, N("wShape", 1.5));
                double wScale = Math.Max(0.1, N("wScale", 2.0));
                double lambda = Math.Max(0.1, N("lambda", 5.0));
                int bins = Math.Max(8, (int)N("bins", 30));
                var rng = new Random((int)N("seed", 42));

                var wSample = new Vector(n);
                var pSample = new Vector(n);
                for (int i = 0; i < n; i++)
                {
                    wSample[i] = RandomEngine.NextWeibull(rng, wShape, wScale);
                    pSample[i] = RandomEngine.NextPoisson(rng, lambda);
                }

                // Гистограмма Вейбулла + теоретическая PDF
                var stW = new Statistic(wSample);
                var hW = stW.Histogramm(bins);
                double xMax = stW.MaxValue * 1.1;
                var xGrid = Vector.Seq(0.01, xMax / 250.0, xMax);

                cv.ChartName = $"Weibull(k={wShape:F1},λ={wScale:F1}) и Poisson(λ={lambda:F1})";
                cv.LabelX = "x"; cv.LabelY = "плотность";
                cv.AddBar(hW.X, hW.Y, "Weibull — выборка", WithAlpha(Palette[0], 140));
                cv.AddPlot(xGrid, xGrid.Transform(x => WeibullPdf(x, wShape, wScale)),
                    "Weibull PDF", Palette[0], width: 3);

                // PMF Пуассона как отдельные точки-столбцы
                int pMax = (int)Math.Min(lambda + 4 * Math.Sqrt(lambda) + 2, 50);
                var pX = new Vector(pMax + 1);
                var pY = new Vector(pMax + 1);
                for (int k = 0; k <= pMax; k++)
                {
                    pX[k] = k;
                    pY[k] = PoissonPmf(k, lambda);
                }
                cv.AddBar(pX, pY, $"Poisson PMF (λ={lambda:F1})", WithAlpha(Palette[4], 200));

                var stP = new Statistic(pSample);
                var sb = new StringBuilder();
                sb.AppendLine($"> Weibull(shape={wShape:F1}, scale={wScale:F1})");
                sb.AppendLine($"  среднее:  {stW.Expected:F4}");
                sb.AppendLine($"  СКО:      {stW.STD:F4}");
                sb.AppendLine();
                sb.AppendLine($"> Poisson(λ={lambda:F1})");
                sb.AppendLine($"  среднее:  {stP.Expected:F4}  (теор. {lambda:F4})");
                sb.AppendLine($"  дисп.:    {stP.Variance:F4}  (теор. {lambda:F4})");
                textOut = sb.ToString();
                break;
            }
            case "mixture_em":
            {
                int n = Math.Max(100, (int)N("n", 1000));
                int k = Math.Clamp((int)N("k", 3), 2, 6);
                int bins = Math.Max(10, (int)N("bins", 50));
                var rng = new Random((int)N("seed", 42));

                // Генерируем синтетические данные из k гауссовых компонент
                double[] trueMeans = new double[k];
                double[] trueStds = new double[k];
                double spread = 3.0;
                for (int i = 0; i < k; i++)
                {
                    trueMeans[i] = -spread + 2.0 * spread * i / (k - 1);
                    trueStds[i] = 0.4 + 0.3 * rng.NextDouble();
                }
                var data = new double[n];
                for (int i = 0; i < n; i++)
                {
                    int comp = rng.Next(k);
                    data[i] = RandomEngine.NextGaussian(rng, trueMeans[comp], trueStds[comp]);
                }

                var gmm = EM.Fit(data, k, seed: (int)N("seed", 42));
                var stat = new Statistic(new Vector(data));
                var h = stat.Histogramm(bins);

                double xMin = stat.MinValue - 1, xMax = stat.MaxValue + 1;
                var xGrid = Vector.Seq(xMin, (xMax - xMin) / 300.0, xMax);

                cv.ChartName = $"GMM (K={k}): EM-фит, BIC={gmm.Bic(n):F1}";
                cv.LabelX = "x"; cv.LabelY = "p(x)";
                cv.AddBar(h.X, h.Y, "Данные", WithAlpha(Palette[0], 140));

                // Суммарная плотность смеси
                cv.AddPlot(xGrid, xGrid.Transform(x => gmm.CulcProb(x)),
                    "GMM PDF", Palette[1], width: 3);

                // Отдельные компоненты
                for (int c = 0; c < k; c++)
                {
                    int ci = c;
                    double w = gmm.Weights[ci];
                    double mu = gmm.Means[ci][0], sig = Math.Max(gmm.Stds[ci][0], 1e-6);
                    cv.AddPlot(xGrid, xGrid.Transform(x => w * NormalPdf(x, mu, sig)),
                        $"w={w:F2} μ={mu:F2} σ={sig:F2}", WithAlpha(Palette[2 + ci % 6], 200), width: 1);
                }

                var sb = new StringBuilder();
                sb.AppendLine($"> EM: K={k}, n={n}, log L = {gmm.LogLikelihood:F2}, BIC = {gmm.Bic(n):F1}");
                sb.AppendLine();
                sb.AppendLine("  Компонента   Вес      μ        σ");
                for (int c = 0; c < k; c++)
                    sb.AppendLine($"  {c + 1,5}     {gmm.Weights[c]:F4}  {gmm.Means[c][0],8:F3}  {gmm.Stds[c][0],8:F3}");
                textOut = sb.ToString();
                break;
            }
            case "monte_carlo":
            {
                int nMax = Math.Max(200, (int)N("nMax", 20000));
                double a = N("a", 1), b = N("b", 10);
                if (b <= a) b = a + 1;
                int seed = (int)N("seed", 42);
                double F(double x) => Math.Sin(x) / Math.Max(1e-9, x);
                int pts = 40;
                var nx = new Vector(pts); var est = new Vector(pts); var refV = new Vector(pts);
                double analytic = TrapzRef(F, a, b, 20000);
                for (int i = 0; i < pts; i++)
                {
                    int n = (int)(100 * Math.Pow(nMax / 100.0, (i + 1.0) / pts));
                    est[i]  = Integration.CalcIntegral1D(F, a, b, Math.Max(50, n), iter: 4, seed: seed + i);
                    nx[i]   = n;
                    refV[i] = analytic;
                }
                cv.ChartName = $"Монте-Карло ∫sin(x)/x dx на [{a:F1};{b:F1}]  —  ref ≈ {analytic:F5}";
                cv.LabelX = "N (точек)"; cv.LabelY = "оценка";
                cv.AddPlot(nx, refV, "Эталон (трапеции)", Palette[2], width: 2);
                cv.AddPlot(nx, est,  "Монте-Карло",       Palette[0], width: 3);
                double lastErr = Math.Abs(est[pts - 1] - analytic);
                textOut =
                    $"Эталон (трапеции, 20 000 узлов): {analytic:F6}\n" +
                    $"Оценка МК при N={(int)nx[pts - 1]}:  {est[pts - 1]:F6}\n" +
                    $"Абсолютная ошибка:                {lastErr:F6}\n" +
                    $"Относительная ошибка:             {100 * lastErr / Math.Max(1e-12, Math.Abs(analytic)):F3}%";
                break;
            }
            case "monte_carlo_nd":
            {
                int dim = Math.Clamp((int)N("dim", 3), 2, 8);
                int nMax = Math.Max(500, (int)N("nMax", 50000));
                int seed = (int)N("seed", 42);

                // ∫...∫ exp(−|x|²) dx по гиперкубу [−2, 2]^dim
                Vector lower = new Vector(dim); lower = lower.Transform(_ => -2.0);
                Vector upper = new Vector(dim); upper = upper.Transform(_ => 2.0);
                double F(Vector x) { double s = 0; for (int i = 0; i < x.Count; i++) s += x[i] * x[i]; return Math.Exp(-s); }

                // Аналитический результат: (√π · erf(2))^dim
                double erf2 = Erf(2.0);
                double analyticPerDim = Math.Sqrt(Math.PI) * erf2;
                double analytic = Math.Pow(analyticPerDim, dim);

                int pts = 30;
                var nx = new Vector(pts); var est = new Vector(pts); var refV = new Vector(pts);
                for (int i = 0; i < pts; i++)
                {
                    int n = (int)(200 * Math.Pow(nMax / 200.0, (i + 1.0) / pts));
                    est[i] = Integration.CalcIntegralND(F, lower, upper, Math.Max(100, n), iter: 4, seed: seed + i);
                    nx[i] = n;
                    refV[i] = analytic;
                }

                cv.ChartName = $"MC-ND: ∫exp(−|x|²)dx,  dim={dim},  [−2,2]^{dim}";
                cv.LabelX = "N (точек)"; cv.LabelY = "оценка";
                cv.AddPlot(nx, refV, $"Аналитический = {analytic:F5}", Palette[2], width: 2);
                cv.AddPlot(nx, est, "Монте-Карло ND", Palette[0], width: 3);

                double lastErr = Math.Abs(est[pts - 1] - analytic);
                textOut =
                    $"Размерность: {dim}\n" +
                    $"Аналитическое значение: (√π·erf(2))^{dim} = {analytic:F6}\n" +
                    $"Оценка МК при N={(int)nx[pts - 1]}:  {est[pts - 1]:F6}\n" +
                    $"Абс. ошибка: {lastErr:F6}   Отн. ошибка: {100 * lastErr / Math.Max(1e-12, analytic):F3}%";
                break;
            }
            case "gauss2d":
            {
                double mu1 = N("mu1", 0), mu2 = N("mu2", 0);
                double sig1 = Math.Max(0.1, N("sig1", 1.0));
                double sig2 = Math.Max(0.1, N("sig2", 0.6));
                double rho = Math.Clamp(N("rho", 0.5), -0.99, 0.99);

                const int G = 50;
                double range = 3.5;
                var xGrid = new double[G]; var yGrid = new double[G];
                for (int i = 0; i < G; i++)
                {
                    xGrid[i] = mu1 - range * sig1 + 2 * range * sig1 * i / (G - 1);
                    yGrid[i] = mu2 - range * sig2 + 2 * range * sig2 * i / (G - 1);
                }
                var z = new double[G, G];
                double det = sig1 * sig1 * sig2 * sig2 * (1 - rho * rho);
                double norm = 1.0 / (2 * Math.PI * Math.Sqrt(det));
                for (int ix = 0; ix < G; ix++)
                    for (int iy = 0; iy < G; iy++)
                    {
                        double dx = xGrid[ix] - mu1, dy = yGrid[iy] - mu2;
                        double q = (1.0 / (1 - rho * rho)) *
                            (dx * dx / (sig1 * sig1) - 2 * rho * dx * dy / (sig1 * sig2) + dy * dy / (sig2 * sig2));
                        z[ix, iy] = norm * Math.Exp(-0.5 * q);
                    }

                cv.ChartName = $"N₂(μ=[{mu1:F1},{mu2:F1}], σ=[{sig1:F1},{sig2:F1}], ρ={rho:F2})";
                cv.LabelX = "x₁"; cv.LabelY = "x₂"; cv.LabelZ = "p(x)";
                cv.Camera3D.Azimuth = N("azimuth", -30); cv.Camera3D.Elevation = N("elevation", 30);
                cv.AddSurface(new Vector(xGrid), new Vector(yGrid), z, "PDF", ColormapKind.Jet);

                plotly = new PlotlyBuilder { Title = cv.ChartName, AxisX = "x₁", AxisY = "x₂", AxisZ = "p(x)" };
                plotly.CameraEyeX = 1.6; plotly.CameraEyeY = 1.6; plotly.CameraEyeZ = 1.0;
                plotly.AddSurface(xGrid, yGrid, z, "PDF", "Jet", 0.95);

                textOut =
                    $"Двумерная гауссиана N₂\n" +
                    $"  μ = [{mu1:F2}, {mu2:F2}]\n" +
                    $"  σ = [{sig1:F2}, {sig2:F2}]\n" +
                    $"  ρ = {rho:F3}\n" +
                    $"  det(Σ) = {det:F4}\n" +
                    $"  Максимум PDF = {norm:F4}";
                break;
            }
            case "mixture2d":
            {
                int k = Math.Clamp((int)N("k", 3), 2, 5);
                int seed = (int)N("seed", 42);
                var rng = new Random(seed);

                // Генерируем параметры компонент
                double[] wx = new double[k], mx = new double[k], my = new double[k];
                double[] sx = new double[k], sy = new double[k];
                double totalW = 0;
                for (int c = 0; c < k; c++)
                {
                    wx[c] = 0.5 + rng.NextDouble();
                    totalW += wx[c];
                    mx[c] = (rng.NextDouble() - 0.5) * 4;
                    my[c] = (rng.NextDouble() - 0.5) * 4;
                    sx[c] = 0.3 + rng.NextDouble() * 0.7;
                    sy[c] = 0.3 + rng.NextDouble() * 0.7;
                }
                for (int c = 0; c < k; c++) wx[c] /= totalW;

                const int G = 50;
                double lo = -4, hi = 4;
                var xGrid = new double[G]; var yGrid = new double[G];
                for (int i = 0; i < G; i++)
                {
                    xGrid[i] = lo + (hi - lo) * i / (G - 1);
                    yGrid[i] = lo + (hi - lo) * i / (G - 1);
                }
                var z = new double[G, G];
                for (int ix = 0; ix < G; ix++)
                    for (int iy = 0; iy < G; iy++)
                    {
                        double pdf = 0;
                        for (int c = 0; c < k; c++)
                        {
                            double zx = (xGrid[ix] - mx[c]) / sx[c];
                            double zy = (yGrid[iy] - my[c]) / sy[c];
                            pdf += wx[c] * Math.Exp(-0.5 * (zx * zx + zy * zy))
                                   / (2 * Math.PI * sx[c] * sy[c]);
                        }
                        z[ix, iy] = pdf;
                    }

                cv.ChartName = $"Смесь {k} гауссиан 2D (seed={seed})";
                cv.LabelX = "x₁"; cv.LabelY = "x₂"; cv.LabelZ = "p(x)";
                cv.Camera3D.Azimuth = N("azimuth", -30); cv.Camera3D.Elevation = N("elevation", 30);
                cv.AddSurface(new Vector(xGrid), new Vector(yGrid), z, "GMM PDF", ColormapKind.Jet);

                plotly = new PlotlyBuilder { Title = cv.ChartName, AxisX = "x₁", AxisY = "x₂", AxisZ = "p(x)" };
                plotly.CameraEyeX = 1.6; plotly.CameraEyeY = 1.6; plotly.CameraEyeZ = 1.0;
                plotly.AddSurface(xGrid, yGrid, z, "GMM PDF", "Jet", 0.95);

                var sb = new StringBuilder();
                sb.AppendLine($"> Смесь из {k} компонент (диагональная ковариация)");
                sb.AppendLine();
                sb.AppendLine("  К  Вес    μx      μy      σx     σy");
                for (int c = 0; c < k; c++)
                    sb.AppendLine($"  {c + 1}  {wx[c]:F3}  {mx[c],6:F2}  {my[c],6:F2}  {sx[c]:F2}   {sy[c]:F2}");
                textOut = sb.ToString();
                break;
            }
            case "heterogeneous_mixture":
            {
                int n = Math.Max(500, (int)N("n", 3000));
                int compKind = Math.Clamp((int)N("kind", 0), 0, 2);
                bool fitEM = (int)N("fit", 0) == 1;
                var rng = new Random((int)N("seed", 42));

                AI.Statistics.Distributions.SimpleDist1DBase[] components;
                Vector weights;
                string mixLabel;

                switch (compKind)
                {
                    case 1:
                        components = [
                            new AI.Statistics.Distributions.GaussianDist1D(-2, 0.6),
                            new AI.Statistics.Distributions.LaplaceDist1D(1.5, 0.5),
                            new AI.Statistics.Distributions.RayleighDist1D(1.2)
                        ];
                        weights = new Vector(new double[] { 0.4, 0.35, 0.25 });
                        mixLabel = "0.4·N(−2,0.6) + 0.35·Laplace(1.5,0.5) + 0.25·Rayleigh(1.2)";
                        break;
                    case 2:
                        components = [
                            new AI.Statistics.Distributions.UniformDist1D(-3, 0),
                            new AI.Statistics.Distributions.ExponentialDist1D(1.5, 0),
                            new AI.Statistics.Distributions.GaussianDist1D(3, 0.5)
                        ];
                        weights = new Vector(new double[] { 0.3, 0.4, 0.3 });
                        mixLabel = "0.3·U(−3,0) + 0.4·Exp(1.5) + 0.3·N(3,0.5)";
                        break;
                    default:
                        components = [
                            new AI.Statistics.Distributions.GaussianDist1D(0, 1),
                            new AI.Statistics.Distributions.ExponentialDist1D(0.8, 2)
                        ];
                        weights = new Vector(new double[] { 0.6, 0.4 });
                        mixLabel = "0.6·N(0,1) + 0.4·Exp(0.8, shift=2)";
                        break;
                }

                var mixture = new AI.Statistics.MixtureModeling.MixtureModel(components, weights);

                var samples = new Vector(n);
                for (int i = 0; i < n; i++)
                    samples[i] = mixture.Sample1D(rng);

                var stat = new Statistic(samples);
                var hist = stat.Histogramm(50);

                double xMin = stat.MinValue - 0.3 * stat.STD;
                double xMax = stat.MaxValue + 0.3 * stat.STD;
                int gridPts = 300;
                var xGrid = new Vector(gridPts);
                for (int i = 0; i < gridPts; i++) xGrid[i] = xMin + (xMax - xMin) * i / (gridPts - 1);

                cv.LabelX = "x"; cv.LabelY = "p(x)";
                cv.AddBar(hist.X, hist.Y, "Выборка", WithAlpha(Palette[0], 120));

                var sbm = new StringBuilder();

                if (fitEM)
                {
                    // Classification EM: начальное приближение с «размазанными» параметрами
                    var initComps = new AI.Statistics.Distributions.SimpleDist1DBase[components.Length];
                    for (int c = 0; c < components.Length; c++)
                        initComps[c] = components[c]; // стартуем от истинных (в реальности — рандомных)

                    var dataArr = new double[n];
                    for (int i = 0; i < n; i++) dataArr[i] = samples[i];

                    var result = AI.Statistics.MixtureModeling.ClassificationEM.Fit(dataArr, initComps);
                    var fitted = result.ToMixtureModel();
                    var pdfFit = xGrid.Transform(x => fitted.CulcProb(x));

                    cv.ChartName = $"Classification EM ({result.Iterations} ит., LL={result.LogLikelihood:F1})";
                    cv.AddPlot(xGrid, pdfFit, "EM PDF (подгонка)", Palette[1], width: 3);

                    for (int c = 0; c < result.Components.Length; c++)
                    {
                        double w = result.Weights[c];
                        int ci = (c + 2) % Palette.Length;
                        var comp = result.Components[c];
                        var pdfC = xGrid.Transform(x => w * comp.CulcProb(x));
                        cv.AddPlot(xGrid, pdfC, $"EM комп. {c + 1} (w={w:F2})", Palette[ci], width: 2);
                    }

                    sbm.AppendLine($"> Classification EM — гетерогенная смесь");
                    sbm.AppendLine($"  Итераций: {result.Iterations},  LogL = {result.LogLikelihood:F2}");
                    sbm.AppendLine();
                    sbm.AppendLine("  Истинные компоненты:");
                    for (int c = 0; c < components.Length; c++)
                        sbm.AppendLine($"    {c + 1}. {components[c].GetType().Name} (w={weights[c]:F3})");
                    sbm.AppendLine();
                    sbm.AppendLine("  Подогнанные компоненты:");
                    for (int c = 0; c < result.Components.Length; c++)
                        sbm.AppendLine($"    {c + 1}. {result.Components[c].GetType().Name} (w={result.Weights[c]:F3})");
                }
                else
                {
                    var pdfMix = xGrid.Transform(x => mixture.CulcProb(x));
                    cv.ChartName = $"Гетерогенная смесь: {mixLabel}";
                    cv.AddPlot(xGrid, pdfMix, "Смесь PDF", Palette[1], width: 3);

                    for (int c = 0; c < components.Length; c++)
                    {
                        double w = weights[c];
                        int ci = (c + 2) % Palette.Length;
                        var pdfC = xGrid.Transform(x => w * ((IDistributionWithoutParams)components[c]).CulcProb(x));
                        cv.AddPlot(xGrid, pdfC, $"Комп. {c + 1} (w={w:F2})", Palette[ci], width: 2);
                    }

                    sbm.AppendLine($"> Гетерогенная смесь распределений");
                    sbm.AppendLine($"  {mixLabel}");
                    sbm.AppendLine();
                    sbm.AppendLine($"  n = {n}, μ̂ = {stat.Expected:F4}, σ̂ = {stat.STD:F4}");
                    sbm.AppendLine();
                    sbm.AppendLine("  Компоненты:");
                    for (int c = 0; c < components.Length; c++)
                        sbm.AppendLine($"    {c + 1}. {components[c].GetType().Name} (вес = {weights[c]:F3})");
                }

                textOut = sbm.ToString();
                break;
            }
            case "heterogeneous_mixture_nd":
            {
                int n = Math.Max(300, (int)N("n", 2000));
                int compKind = Math.Clamp((int)N("kind", 0), 0, 1);
                bool fitEM = (int)N("fit", 0) == 1;
                var rng = new Random((int)N("seed", 42));

                AI.Statistics.Distributions.SimpleDistNDBase[] components;
                double[] wArr;
                string mixLabel;

                if (compKind == 1)
                {
                    // Полная ковариация: два Гауссиана с корреляцией + один диагональный
                    var cov1 = new double[,] { { 1.0, 0.6 }, { 0.6, 0.8 } };
                    var cov2 = new double[,] { { 0.5, -0.3 }, { -0.3, 1.2 } };
                    components = [
                        new AI.Statistics.Distributions.GaussianDistFullCov(new Vector(new[] { -2.0, 1.0 }), cov1),
                        new AI.Statistics.Distributions.GaussianDistFullCov(new Vector(new[] { 2.0, -1.0 }), cov2),
                        new AI.Statistics.Distributions.GaussianDistND(
                            new Vector(new[] { 0.0, 0.0 }),
                            new Vector(new[] { 0.4, 0.4 }))
                    ];
                    wArr = [0.4, 0.35, 0.25];
                    mixLabel = "0.4·N([-2,1],Σ₁) + 0.35·N([2,-1],Σ₂) + 0.25·N([0,0],diag)";
                }
                else
                {
                    // Два диагональных гауссиана
                    components = [
                        new AI.Statistics.Distributions.GaussianDistND(
                            new Vector(new[] { -1.5, 1.0 }),
                            new Vector(new[] { 0.8, 0.5 })),
                        new AI.Statistics.Distributions.GaussianDistND(
                            new Vector(new[] { 1.5, -0.5 }),
                            new Vector(new[] { 0.5, 1.0 }))
                    ];
                    wArr = [0.55, 0.45];
                    mixLabel = "0.55·N([-1.5,1],diag) + 0.45·N([1.5,-0.5],diag)";
                }

                var weights = new Vector(wArr);
                var ndMixture = new AI.Statistics.MixtureModeling.MixtureModel(components, weights);

                // Генерация выборки
                var samples = new Vector[n];
                for (int i = 0; i < n; i++)
                    samples[i] = ndMixture.SampleND(rng);

                // Визуализация: 3D поверхность PDF
                const int G = 50;
                double lo = -5, hi = 5;
                var xGrid = new double[G]; var yGrid = new double[G];
                for (int i = 0; i < G; i++)
                {
                    xGrid[i] = lo + (hi - lo) * i / (G - 1);
                    yGrid[i] = lo + (hi - lo) * i / (G - 1);
                }
                var z = new double[G, G];
                for (int ix = 0; ix < G; ix++)
                    for (int iy = 0; iy < G; iy++)
                        z[ix, iy] = ndMixture.CulcProb(new Vector(new[] { xGrid[ix], yGrid[iy] }));

                cv.ChartName = fitEM ? "ND Classification EM" : $"Гетерогенная ND-смесь";
                cv.LabelX = "x₁"; cv.LabelY = "x₂"; cv.LabelZ = "p(x)";
                cv.Camera3D.Azimuth = N("azimuth", -30); cv.Camera3D.Elevation = N("elevation", 30);

                var sbnd = new StringBuilder();

                if (fitEM)
                {
                    var result = AI.Statistics.MixtureModeling.ClassificationEM.FitND(
                        samples, components);
                    var fittedMix = result.ToMixtureModel();

                    var zFit = new double[G, G];
                    for (int ix = 0; ix < G; ix++)
                        for (int iy = 0; iy < G; iy++)
                            zFit[ix, iy] = fittedMix.CulcProb(new Vector(new[] { xGrid[ix], yGrid[iy] }));

                    cv.AddSurface(new Vector(xGrid), new Vector(yGrid), zFit,
                        "EM PDF (подгонка)", ColormapKind.Jet);

                    plotly = new PlotlyBuilder { Title = $"ND Classification EM ({result.Iterations} ит.)", AxisX = "x₁", AxisY = "x₂", AxisZ = "p(x)" };
                    plotly.CameraEyeX = 1.6; plotly.CameraEyeY = 1.6; plotly.CameraEyeZ = 1.0;
                    plotly.AddSurface(xGrid, yGrid, zFit, "EM PDF", "Jet", 0.95);

                    sbnd.AppendLine($"> Classification EM (ND) — {result.Iterations} ит., LL = {result.LogLikelihood:F2}");
                    sbnd.AppendLine($"  Компонент: {result.Components.Length}");
                    sbnd.AppendLine();
                    for (int c = 0; c < result.Components.Length; c++)
                        sbnd.AppendLine($"  {c + 1}. {result.Components[c].GetType().Name} (w={result.Weights[c]:F3})");
                }
                else
                {
                    cv.AddSurface(new Vector(xGrid), new Vector(yGrid), z,
                        "Смесь PDF", ColormapKind.Jet);

                    plotly = new PlotlyBuilder { Title = $"ND Heterogeneous: {mixLabel}", AxisX = "x₁", AxisY = "x₂", AxisZ = "p(x)" };
                    plotly.CameraEyeX = 1.6; plotly.CameraEyeY = 1.6; plotly.CameraEyeZ = 1.0;
                    plotly.AddSurface(xGrid, yGrid, z, "Mixture PDF", "Jet", 0.95);

                    sbnd.AppendLine($"> Гетерогенная ND-смесь распределений");
                    sbnd.AppendLine($"  {mixLabel}");
                    sbnd.AppendLine();
                    sbnd.AppendLine($"  n = {n}");
                    sbnd.AppendLine();
                    sbnd.AppendLine("  Компоненты:");
                    for (int c = 0; c < components.Length; c++)
                        sbnd.AppendLine($"    {c + 1}. {components[c].GetType().Name} (вес = {wArr[c]:F3})");
                }

                textOut = sbnd.ToString();
                break;
            }
            case "rayleigh_rice":
            {
                int n = Math.Max(200, (int)N("n", 2000));
                double sigma = Math.Max(0.1, N("sigma", 1.0));
                double nu = Math.Max(0, N("nu", 2.0));
                var rng = new Random((int)N("seed", 42));

                var rayleighSample = new Vector(n);
                var riceSample = new Vector(n);
                for (int i = 0; i < n; i++)
                {
                    rayleighSample[i] = RandomEngine.NextRayleigh(rng, sigma);
                    riceSample[i] = RandomEngine.NextRice(rng, nu, sigma);
                }

                var statR = new Statistic(rayleighSample);
                var statRice = new Statistic(riceSample);
                int bins = 40;
                var histR = statR.Histogramm(bins);

                double xMax = Math.Max(statR.MaxValue, statRice.MaxValue) * 1.05;
                int gridPts = 200;
                var xGrid = new Vector(gridPts);
                for (int i = 0; i < gridPts; i++) xGrid[i] = xMax * (i + 1) / gridPts;

                var rayleighPdf = xGrid.Transform(x => RayleighPdf(x, sigma));
                var ricePdf = xGrid.Transform(x => RicePdf(x, nu, sigma));

                cv.ChartName = $"Релей σ={sigma:F2} и Райс ν={nu:F2}, σ={sigma:F2}";
                cv.LabelX = "x"; cv.LabelY = "p(x)";
                cv.AddBar(histR.X, histR.Y, "Rayleigh — выборка", WithAlpha(Palette[0], 120));
                cv.AddPlot(xGrid, rayleighPdf, $"Rayleigh PDF (σ={sigma:F2})", Palette[0], width: 3);
                cv.AddPlot(xGrid, ricePdf, $"Rice PDF (ν={nu:F2}, σ={sigma:F2})", Palette[1], width: 3);

                var sbr = new StringBuilder();
                sbr.AppendLine($"> Распределения Релея и Райса");
                sbr.AppendLine();
                sbr.AppendLine($"  Параметры: σ = {sigma:F2}, ν = {nu:F2}");
                sbr.AppendLine();
                sbr.AppendLine($"  Rayleigh(σ): E[X] = σ√(π/2) = {sigma * Math.Sqrt(Math.PI / 2):F4}");
                sbr.AppendLine($"     выборочное среднее = {statR.Expected:F4}");
                sbr.AppendLine($"     Var = (4−π)/2 · σ² = {(4 - Math.PI) / 2 * sigma * sigma:F4}");
                sbr.AppendLine($"     выборочная дисперсия = {statR.Variance:F4}");
                sbr.AppendLine();
                sbr.AppendLine($"  Rice(ν,σ): при ν=0 → Rayleigh, при ν≫σ → ≈ N(ν, σ²)");
                sbr.AppendLine($"     выборочное среднее = {statRice.Expected:F4}");
                sbr.AppendLine($"     выборочная дисперсия = {statRice.Variance:F4}");
                textOut = sbr.ToString();
                break;
            }
        }
    }

    private static double Erf(double x) => AI.Statistics.StatInference.Erf(x);

    private static double GammaPdf(double x, double shape, double scale)
    {
        if (x <= 0) return 0;
        return Math.Exp((shape - 1) * Math.Log(x) - x / scale - LogGamma(shape) - shape * Math.Log(scale));
    }

    private static double BetaPdf(double x, double a, double b)
    {
        if (x <= 0 || x >= 1) return 0;
        return Math.Exp((a - 1) * Math.Log(x) + (b - 1) * Math.Log(1 - x) - LogBeta(a, b));
    }

    private static double CauchyPdf(double x, double loc, double gamma)
    {
        double z = (x - loc) / gamma;
        return 1.0 / (Math.PI * gamma * (1.0 + z * z));
    }

    private static double LaplacePdf(double x, double mu, double b)
        => Math.Exp(-Math.Abs(x - mu) / b) / (2.0 * b);

    private static double WeibullPdf(double x, double k, double lam)
    {
        if (x <= 0) return 0;
        double ratio = x / lam;
        return (k / lam) * Math.Pow(ratio, k - 1) * Math.Exp(-Math.Pow(ratio, k));
    }

    private static double PoissonPmf(int k, double lam)
        => Math.Exp(k * Math.Log(lam) - lam - LogGamma(k + 1));

    private static double LogGamma(double x) => AI.Statistics.StatInference.LogGamma(x);

    private static double LogBeta(double a, double b)
        => LogGamma(a) + LogGamma(b) - LogGamma(a + b);

    private static double RayleighPdf(double x, double sigma)
    {
        if (x <= 0) return 0;
        double s2 = sigma * sigma;
        return x / s2 * Math.Exp(-x * x / (2 * s2));
    }

    private static double RicePdf(double x, double nu, double sigma)
    {
        if (x <= 0) return 0;
        double s2 = sigma * sigma;
        double arg = x * nu / s2;
        return x / s2 * Math.Exp(-(x * x + nu * nu) / (2 * s2)) * BesselI0(arg);
    }

    private static double BesselI0(double x)
    {
        double ax = Math.Abs(x);
        if (ax < 3.75)
        {
            double t = x / 3.75; t *= t;
            return 1.0 + t * (3.5156229 + t * (3.0899424 + t * (1.2067492
                + t * (0.2659732 + t * (0.0360768 + t * 0.0045813)))));
        }
        else
        {
            double t = 3.75 / ax;
            return Math.Exp(ax) / Math.Sqrt(ax) *
                (0.39894228 + t * (0.01328592 + t * (0.00225319 + t * (-0.00157565
                + t * (0.00916281 + t * (-0.02057706 + t * (0.02635537
                + t * (-0.01647633 + t * 0.00392377))))))));
        }
    }

    #endregion
}
