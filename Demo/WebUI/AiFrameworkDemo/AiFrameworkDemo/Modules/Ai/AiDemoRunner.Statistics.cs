using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Statistics;
using System.Text;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.Ai;

public static partial class AiDemoRunner
{
    #region Описательная статистика — случаи

    private static void RunStatisticsCase(
        string key, IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        switch (key)
        {
            case "descriptive":
            {
                int n = Math.Max(50, (int)N("n", 400));
                int dist = Math.Clamp((int)N("dist", 0), 0, 3);
                double mu = N("mu", 0), sig = Math.Max(0.01, N("sigma", 1));
                bool showCi = (int)N("ci", 0) == 1;
                var rng = new Random((int)N("seed", 42));
                var sample = GenerateSample(dist, n, mu, sig, rng);
                var stat = new Statistic(sample);
                var q = new Quantile(sample);
                double q1 = q.GetQuantile(0.25), med = q.GetQuantile(0.50), q3 = q.GetQuantile(0.75);
                var hist = stat.Histogramm(36);
                cv.ChartName = $"Сводные статистики (n={n}, μ={stat.Expected:F3}, σ={stat.STD:F3})";
                cv.LabelX = "Значение"; cv.LabelY = "Плотность";
                cv.AddBar(hist.X, hist.Y, "Гистограмма", Palette[0]);
                double yMax = hist.Y.Max();
                DrawVLine(cv, stat.Expected, yMax, "μ",       Palette[1], width: 3);
                DrawVLine(cv, med,           yMax, "медиана", Palette[2], width: 3);
                DrawVLine(cv, q1,            yMax, "Q1",      Palette[3], width: 2);
                DrawVLine(cv, q3,            yMax, "Q3",      Palette[3], width: 2);

                if (showCi)
                {
                    var ciMean = StatInference.ConfidenceIntervalT(stat.Expected, stat.STD, n);
                    DrawVLine(cv, ciMean.Lower, yMax * 0.7, "CI μ нижн.", Palette[5], 2);
                    DrawVLine(cv, ciMean.Upper, yMax * 0.7, "CI μ верхн.", Palette[5], 2);
                }

                textOut = BuildDescriptiveReport(stat, q1, med, q3, stat.Asymmetry(), stat.Excess(), Statistic.RMS(sample), sample, dist, showCi, n);
                break;
            }
            case "histogram_pdf":
            {
                int n = Math.Max(50, (int)N("n", 800));
                int bins = Math.Max(4, (int)N("bins", 30));
                double muT = N("mu", 0), sigT = Math.Max(0.05, N("sigma", 1));
                bool showCi = (int)N("ci", 0) == 1;
                var rng = new Random((int)N("seed", 42));
                var sample = Statistic.RandNorm(n, rng) * sigT + muT;
                var stat = new Statistic(sample);
                var hist = stat.Histogramm(bins);
                double xMin = stat.MinValue - 0.5 * stat.STD, xMax = stat.MaxValue + 0.5 * stat.STD;
                var xGrid = Vector.Seq(xMin, (xMax - xMin) / 200.0, xMax);
                cv.ChartName = $"Гистограмма vs теоретическая PDF (n={n}, bins={bins})";
                cv.LabelX = "x"; cv.LabelY = "p(x)";
                cv.AddBar(hist.X, hist.Y, "Эмпирическая", Palette[0]);
                cv.AddPlot(xGrid, xGrid.Transform(x => NormalPdf(x, muT, sigT)), $"N({muT:F2}, {sigT:F2}) — истинная", Palette[1], width: 3);
                cv.AddPlot(xGrid, xGrid.Transform(x => NormalPdf(x, stat.Expected, stat.STD)), $"N({stat.Expected:F2}, {stat.STD:F2}) — оценка", Palette[2], width: 2);

                if (showCi)
                {
                    var ciMean = StatInference.ConfidenceIntervalT(stat.Expected, stat.STD, n);
                    double yMax = hist.Y.Max();
                    DrawVLine(cv, ciMean.Lower, yMax * 0.6, "CI μ̂ нижн.", Palette[5], 2);
                    DrawVLine(cv, ciMean.Upper, yMax * 0.6, "CI μ̂ верхн.", Palette[5], 2);
                }

                textOut = BuildPdfFitReport(stat, muT, sigT, n, showCi);
                break;
            }
            case "quantiles":
            {
                int n = Math.Max(50, (int)N("n", 400));
                int dist = Math.Clamp((int)N("dist", 0), 0, 2);
                var rng = new Random((int)N("seed", 42));
                Vector sample = dist switch
                {
                    1 => Statistic.UniformDistribution(n, rng),
                    2 => GenerateExp(n, 1.0, rng),
                    _ => Statistic.RandNorm(n, rng)
                };
                var q = new Quantile(sample);
                var sorted = q.SortVec;
                var ecdfY = new Vector(n);
                for (int i = 0; i < n; i++) ecdfY[i] = (i + 1.0) / n;
                double q1 = q.GetQuantile(0.25), q2 = q.GetQuantile(0.50), q3 = q.GetQuantile(0.75);
                double q05 = q.GetQuantile(0.05), q95 = q.GetQuantile(0.95);
                cv.ChartName = $"ECDF — {DistName(dist)}, n={n}";
                cv.LabelX = "x"; cv.LabelY = "F(x)";
                cv.AddPlot(sorted, ecdfY, "ECDF", Palette[0], width: 2);
                DrawVLine(cv, q1, 1.0, "Q1 (25%)", Palette[2], width: 2);
                DrawVLine(cv, q2, 1.0, "медиана",  Palette[1], width: 3);
                DrawVLine(cv, q3, 1.0, "Q3 (75%)", Palette[2], width: 2);
                DrawVLine(cv, q05, 1.0, "5%",  Palette[3], width: 1);
                DrawVLine(cv, q95, 1.0, "95%", Palette[3], width: 1);
                var sb = new StringBuilder();
                sb.AppendLine("> Квантили выборки (Nearest-Rank)");
                sb.AppendLine();
                sb.AppendLine($"  Распределение:  {DistName(dist)}");
                sb.AppendLine($"  Объём выборки:  n = {n}");
                sb.AppendLine();
                sb.AppendLine("  +---------+------------+");
                sb.AppendLine("  | Уровень |  Квантиль  |");
                sb.AppendLine("  |---------+------------|");
                sb.AppendLine($"  |   5%   | {q05,10:F4} |");
                sb.AppendLine($"  |  25%   | {q1,10:F4} |  Q1");
                sb.AppendLine($"  |  50%   | {q2,10:F4} |  медиана");
                sb.AppendLine($"  |  75%   | {q3,10:F4} |  Q3");
                sb.AppendLine($"  |  95%   | {q95,10:F4} |");
                sb.AppendLine("  +---------+------------+");
                sb.AppendLine();
                sb.AppendLine($"  IQR (Q3 − Q1):  {(q3 - q1):F4}");
                sb.AppendLine($"  FastQuantile(0.5) = {Quantile.FastQuantile(sample, 0.5):F4}");
                textOut = sb.ToString();
                break;
            }
            case "moments_scan":
            {
                int nMax = Math.Max(100, (int)N("nMax", 1500));
                double mu = N("mu", 0), sig = Math.Max(0.05, N("sigma", 1));
                var rng = new Random((int)N("seed", 42));
                var big = Statistic.RandNorm(nMax, rng) * sig + mu;
                int pts = 80;
                var xs = new Vector(pts); var meanE = new Vector(pts); var stdE = new Vector(pts);
                for (int i = 0; i < pts; i++)
                {
                    int k = Math.Max(10, (int)(10 + (nMax - 10.0) * (i + 1) / pts));
                    var sub = new Vector(k);
                    for (int j = 0; j < k; j++) sub[j] = big[j];
                    var st = new Statistic(sub);
                    xs[i] = k; meanE[i] = st.Expected; stdE[i] = st.STD;
                }
                var muLine  = new Vector(pts); var sigLine = new Vector(pts);
                for (int i = 0; i < pts; i++) { muLine[i] = mu; sigLine[i] = sig; }
                cv.ChartName = $"Сходимость моментов к μ={mu:F2}, σ={sig:F2}";
                cv.LabelX = "n"; cv.LabelY = "оценка";
                cv.AddPlot(xs, muLine,  $"истинное μ = {mu:F2}", Palette[2], width: 2);
                cv.AddPlot(xs, sigLine, $"истинное σ = {sig:F2}", Palette[3], width: 2);
                cv.AddPlot(xs, meanE,   "оценка μ(n)", Palette[0], width: 3);
                cv.AddPlot(xs, stdE,    "оценка σ(n)", Palette[1], width: 3);
                break;
            }
            case "confidence_interval":
            {
                int n = Math.Max(10, (int)N("n", 100));
                double mu = N("mu", 2.0), sig = Math.Max(0.1, N("sigma", 1.0));
                double conf = Math.Clamp(N("conf", 0.95), 0.8, 0.999);
                var rng = new Random((int)N("seed", 42));
                var sample = Statistic.RandNorm(n, rng) * sig + mu;
                var stat = new Statistic(sample);
                double xBar = stat.Expected, s = stat.STD;
                var ciZ = StatInference.ConfidenceIntervalZ(xBar, sig, n, conf);
                var ciT = StatInference.ConfidenceIntervalT(xBar, s, n, conf);

                var hist = stat.Histogramm(Math.Max(12, n / 10));
                cv.ChartName = $"CI ({conf * 100:F0}%): z=[{ciZ.Lower:F3};{ciZ.Upper:F3}], t=[{ciT.Lower:F3};{ciT.Upper:F3}]";
                cv.LabelX = "x"; cv.LabelY = "плотность";
                cv.AddBar(hist.X, hist.Y, "Выборка", WithAlpha(Palette[0], 140));
                double yMax = hist.Y.Max();
                DrawVLine(cv, mu, yMax, $"μ ист. = {mu:F2}", Palette[2], 2);
                DrawVLine(cv, xBar, yMax, $"x̄ = {xBar:F3}", Palette[1], 3);
                DrawVLine(cv, ciT.Lower, yMax * 0.8, "CI нижн.", Palette[3], 2);
                DrawVLine(cv, ciT.Upper, yMax * 0.8, "CI верхн.", Palette[3], 2);

                var sb = new StringBuilder();
                sb.AppendLine($"> Доверительные интервалы для μ ({conf * 100:F0}%)");
                sb.AppendLine();
                sb.AppendLine($"  n = {n},  x̄ = {xBar:F4},  s = {s:F4},  σ ист. = {sig:F4}");
                sb.AppendLine();
                sb.AppendLine("  Метод          Нижняя      Верхняя     Ширина     μ внутри?");
                sb.AppendLine($"  z-интервал:  {ciZ.Lower,10:F4} {ciZ.Upper,10:F4} {(ciZ.Upper - ciZ.Lower),9:F4}    {(mu >= ciZ.Lower && mu <= ciZ.Upper ? "Да" : "Нет")}");
                sb.AppendLine($"  t-интервал:  {ciT.Lower,10:F4} {ciT.Upper,10:F4} {(ciT.Upper - ciT.Lower),9:F4}    {(mu >= ciT.Lower && mu <= ciT.Upper ? "Да" : "Нет")}");
                sb.AppendLine();
                sb.AppendLine($"  t-интервал шире z на {(ciT.Upper - ciT.Lower) - (ciZ.Upper - ciZ.Lower):F4}");
                sb.AppendLine($"  (учитывает неопределённость в оценке σ)");
                textOut = sb.ToString();
                break;
            }
            case "hypothesis_test":
            {
                int n = Math.Max(10, (int)N("n", 50));
                double mu0 = N("mu0", 0);
                double muTrue = N("muTrue", 0.5);
                double sig = Math.Max(0.1, N("sigma", 1.0));
                double alpha = Math.Clamp(N("alpha", 0.05), 0.001, 0.5);
                var rng = new Random((int)N("seed", 42));
                var sample = Statistic.RandNorm(n, rng) * sig + muTrue;
                var stat = new Statistic(sample);
                double xBar = stat.Expected, s = stat.STD;

                var zRes = StatInference.ZTest(xBar, sig, n, mu0, alpha);
                var tRes = StatInference.TTest(xBar, s, n, mu0, alpha);

                int df = n - 1;
                var xGrid = new Vector(300);
                for (int i = 0; i < 300; i++) xGrid[i] = -4.0 + 8.0 * i / 299.0;
                var pdfH0 = xGrid.Transform(x => StatInference.TPdf(x, df));

                cv.ChartName = $"t-тест: t={tRes.Statistic:F3}, p={tRes.PValue:F4}, H₀ {(tRes.Reject ? "отвергнута" : "не отвергнута")}";
                cv.LabelX = "t"; cv.LabelY = "p(t | H₀)";
                cv.AddPlot(xGrid, pdfH0, $"t({df}) под H₀", Palette[0], width: 2);

                double yMaxPdf = pdfH0.Max();
                DrawVLine(cv, tRes.CriticalLower, yMaxPdf, $"-t_crit = {tRes.CriticalLower:F3}", Palette[7], 2);
                DrawVLine(cv, tRes.CriticalUpper, yMaxPdf, $"+t_crit = {tRes.CriticalUpper:F3}", Palette[7], 2);
                DrawVLine(cv, tRes.Statistic, yMaxPdf, $"t_obs = {tRes.Statistic:F3}", Palette[1], 3);

                var sb = new StringBuilder();
                sb.AppendLine($"> Тест гипотезы H₀: μ = {mu0:F2}  vs  H₁: μ ≠ {mu0:F2}");
                sb.AppendLine($"  (истинное μ = {muTrue:F2}, α = {alpha})");
                sb.AppendLine();
                sb.AppendLine($"  n = {n},  x̄ = {xBar:F4},  s = {s:F4}");
                sb.AppendLine();
                sb.AppendLine("  Тест        Статистика    p-value      Решение");
                sb.AppendLine($"  z-тест:     {zRes.Statistic,10:F4}  {zRes.PValue,10:F4}    {(zRes.Reject ? "ОТВЕРГНУТЬ H₀" : "не отвергать")}");
                sb.AppendLine($"  t-тест:     {tRes.Statistic,10:F4}  {tRes.PValue,10:F4}    {(tRes.Reject ? "ОТВЕРГНУТЬ H₀" : "не отвергать")}");
                sb.AppendLine();
                sb.AppendLine($"  Критическая область: |t| > {tRes.CriticalUpper:F3}");
                sb.AppendLine($"  Мощность: при Δμ = {muTrue - mu0:F2}, n = {n}:");
                double noncentrality = (muTrue - mu0) / (sig / Math.Sqrt(n));
                sb.AppendLine($"    нецентральность = {noncentrality:F3}");
                textOut = sb.ToString();
                break;
            }
            case "normality_test":
            {
                int n = Math.Max(20, (int)N("n", 200));
                int dist = Math.Clamp((int)N("dist", 0), 0, 3);
                double mu = N("mu", 0), sig = Math.Max(0.1, N("sigma", 1));
                var rng = new Random((int)N("seed", 42));
                var sample = GenerateSample(dist, n, mu, sig, rng);
                var stat = new Statistic(sample);
                double skew = stat.Asymmetry(), kurt = stat.Excess();

                var jb = StatInference.JarqueBeraTest(skew, kurt, n);

                // Sorted data for Anderson-Darling
                var sorted = new double[n];
                for (int i = 0; i < n; i++) sorted[i] = sample[i];
                Array.Sort(sorted);
                var ad = StatInference.AndersonDarlingTest(sorted, n, stat.Expected, stat.STD);

                // Q-Q plot: theoretical quantiles vs sorted data
                var theoretical = new Vector(n);
                var empirical = new Vector(n);
                for (int i = 0; i < n; i++)
                {
                    double pi = (i + 0.5) / n;
                    theoretical[i] = StatInference.NormalQuantile(pi) * stat.STD + stat.Expected;
                    empirical[i] = sorted[i];
                }

                cv.ChartName = $"Q-Q plot ({DistName(dist)}, n={n}) — JB p={FormatPValue(jb.PValue)}";
                cv.LabelX = "Теоретические квантили N"; cv.LabelY = "Выборочные квантили";
                cv.AddPlot(theoretical, empirical, "Q-Q", Palette[0], width: 2);
                // Reference line y = x
                double lo = Math.Min(theoretical[0], empirical[0]);
                double hi = Math.Max(theoretical[n - 1], empirical[n - 1]);
                var refX = new Vector(2); refX[0] = lo; refX[1] = hi;
                var refY = new Vector(2); refY[0] = lo; refY[1] = hi;
                cv.AddPlot(refX, refY, "y = x (норм.)", Palette[2], width: 2);

                var sb = new StringBuilder();
                sb.AppendLine($"> Тесты на нормальность — {DistName(dist)}, n={n}");
                sb.AppendLine();
                sb.AppendLine($"  Асимметрия γ₁ = {skew:F4},  Эксцесс γ₂ = {kurt:F4}");
                sb.AppendLine();
                sb.AppendLine("  Тест               Статистика   p-value      Решение (α=0.05)");
                sb.AppendLine($"  Жарк-Бера:       {jb.Statistic,10:F4}   {FormatPValue(jb.PValue),8}   {(jb.Reject ? "ОТВЕРГНУТЬ H₀" : "не отвергать")}");
                sb.AppendLine($"  Андерсон-Дарлинг: {ad.Statistic,10:F4}   {FormatPValue(ad.PValue),8}   {(ad.Reject ? "ОТВЕРГНУТЬ H₀" : "не отвергать")}");
                sb.AppendLine();
                sb.AppendLine("  H₀: выборка из нормального распределения");
                sb.AppendLine($"  Интерпретация: {(jb.Reject || ad.Reject ? "данные НЕ нормальны" : "нет оснований отвергать нормальность")}");
                textOut = sb.ToString();
                break;
            }
        }
    }

    #endregion

    #region Генерация данных

    private static Vector GenerateSample(int kind, int n, double mu, double sig, Random rng) => kind switch
    {
        1 => Statistic.UniformDistribution(n, rng) * (2 * sig * Math.Sqrt(3)) + (mu - sig * Math.Sqrt(3)),
        2 => GenerateExp(n, 1.0 / Math.Max(1e-6, sig), rng) + (mu - sig),
        3 => MixtureSample(n, mu, sig, rng),
        _ => Statistic.RandNorm(n, rng) * sig + mu
    };

    private static Vector GenerateExp(int n, double rate, Random rng)
    {
        var v = new Vector(n);
        for (int i = 0; i < n; i++) v[i] = RandomEngine.NextExponential(rng, rate);
        return v;
    }

    private static Vector MixtureSample(int n, double mu, double sig, Random rng)
    {
        var v = new Vector(n);
        for (int i = 0; i < n; i++)
        {
            v[i] = rng.NextDouble() < 0.5
                ? RandomEngine.NextGaussian(rng) * (sig * 0.6) + (mu - sig)
                : RandomEngine.NextGaussian(rng) * (sig * 0.6) + (mu + sig);
        }
        return v;
    }

    private static string DistName(int kind) => kind switch
    {
        1 => "U(a,b)",
        2 => "Exp(1)",
        3 => "смесь 0.5·N(μ−σ, 0.6σ) + 0.5·N(μ+σ, 0.6σ)",
        _ => "N(μ, σ)"
    };

    #endregion

    #region Текстовые отчёты статистики

    private static string BuildDescriptiveReport(Statistic stat, double q1, double med, double q3,
        double skew, double kurt, double rms, Vector sample, int dist, bool showCi = false, int n = 0)
    {
        if (n == 0) n = sample.Count;
        var sb = new StringBuilder();
        sb.AppendLine("> Описательная статистика");
        sb.AppendLine();
        sb.AppendLine($"  Распределение:  {DistName(dist)}");
        sb.AppendLine($"  Объём n:        {sample.Count}");
        sb.AppendLine();
        sb.AppendLine($"  - Центр");
        sb.AppendLine($"    Среднее μ̂:        {stat.Expected,10:F4}");
        sb.AppendLine($"    Медиана Q₂:       {med,10:F4}");
        try { sb.AppendLine($"    Геом. среднее:    {Statistic.MeanGeom(sample),10:F4}"); } catch { }
        try { sb.AppendLine($"    Гарм. среднее:    {Statistic.MeanGarmonic(sample),10:F4}"); } catch { }
        sb.AppendLine($"    RMS:              {rms,10:F4}");
        sb.AppendLine();
        sb.AppendLine($"  - Разброс");
        sb.AppendLine($"    Дисперсия σ̂²:    {stat.Variance,10:F4}");
        sb.AppendLine($"    СКО σ̂:           {stat.STD,10:F4}");
        sb.AppendLine($"    min/max:          {stat.MinValue,10:F4} / {stat.MaxValue,10:F4}");
        sb.AppendLine($"    Размах R:        {(stat.MaxValue - stat.MinValue),10:F4}");
        sb.AppendLine($"    IQR (Q₃−Q₁):     {(q3 - q1),10:F4}");
        sb.AppendLine();
        sb.AppendLine($"  - Форма");
        sb.AppendLine($"    Асимметрия γ₁:    {skew,10:F4}   (0 — симметрично)");
        sb.AppendLine($"    Эксцесс γ₂:       {kurt,10:F4}   (0 — как у N; >0 «острее»)");

        if (showCi)
        {
            sb.AppendLine();
            sb.AppendLine("  ── Доверительные интервалы (95%) ──");
            double se = stat.STD / Math.Sqrt(n);
            var ciMean = StatInference.ConfidenceIntervalT(stat.Expected, stat.STD, n);
            var ciVar = StatInference.ConfidenceIntervalVariance(stat.Variance, n);
            var ciStd = StatInference.ConfidenceIntervalStd(stat.STD, n);
            sb.AppendLine($"    μ̂:   [{ciMean.Lower:F4}; {ciMean.Upper:F4}]   SE = {se:F4}");
            sb.AppendLine($"    σ̂²:  [{ciVar.Lower:F4}; {ciVar.Upper:F4}]   (χ²-интервал)");
            sb.AppendLine($"    σ̂:   [{ciStd.Lower:F4}; {ciStd.Upper:F4}]");
            sb.AppendLine();
            sb.AppendLine("  ── Тесты значимости ──");
            var tTest = StatInference.TTest(stat.Expected, stat.STD, n, 0);
            var chi2Test = StatInference.ChiSquaredVarianceTest(stat.Variance, n, 1.0);
            sb.AppendLine($"    H₀: μ = 0     t = {tTest.Statistic:F3},  p = {FormatPValue(tTest.PValue)}  → {(tTest.Reject ? "ОТВЕРГНУТЬ" : "не отвергать")}");
            sb.AppendLine($"    H₀: σ² = 1    χ² = {chi2Test.Statistic:F2},  p = {FormatPValue(chi2Test.PValue)}  → {(chi2Test.Reject ? "ОТВЕРГНУТЬ" : "не отвергать")}");
        }

        return sb.ToString();
    }

    private static string FormatPValue(double p)
    {
        if (p < 0.001) return "< 0.001";
        return p.ToString("F4");
    }

    private static string BuildPdfFitReport(Statistic stat, double muT, double sigT, int n, bool showCi = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("> Подгонка нормальной плотности к выборке");
        sb.AppendLine();
        sb.AppendLine($"  n = {n}");
        sb.AppendLine($"  μ̂ (выборка) = {stat.Expected:F4}   истина {muT:F4}   Δ = {stat.Expected - muT:F4}");
        sb.AppendLine($"  σ̂ (выборка) = {stat.STD:F4}   истина {sigT:F4}   Δ = {stat.STD - sigT:F4}");
        sb.AppendLine();
        sb.AppendLine($"  Ст. ошибка μ̂ ≈ σ/√n = {sigT / Math.Sqrt(n):F4}");
        sb.AppendLine($"  Ст. ошибка σ̂ ≈ σ/√(2n) = {sigT / Math.Sqrt(2.0 * n):F4}");

        if (showCi)
        {
            sb.AppendLine();
            sb.AppendLine("  ── Доверительные интервалы (95%) ──");
            var ciMean = StatInference.ConfidenceIntervalT(stat.Expected, stat.STD, n);
            var ciStd = StatInference.ConfidenceIntervalStd(stat.STD, n);
            sb.AppendLine($"    μ̂:  [{ciMean.Lower:F4}; {ciMean.Upper:F4}]   SE = {stat.STD / Math.Sqrt(n):F4}");
            sb.AppendLine($"    σ̂:  [{ciStd.Lower:F4}; {ciStd.Upper:F4}]");
            sb.AppendLine();
            sb.AppendLine("  ── Тест: μ̂ = μ_ист? ──");
            var tTest = StatInference.TTest(stat.Expected, stat.STD, n, muT);
            sb.AppendLine($"    H₀: μ = {muT:F2}   t = {tTest.Statistic:F3},  p = {FormatPValue(tTest.PValue)}  → {(tTest.Reject ? "ОТВЕРГНУТЬ" : "не отвергать")}");
            var chi2 = StatInference.ChiSquaredVarianceTest(stat.Variance, n, sigT * sigT);
            sb.AppendLine($"    H₀: σ² = {sigT * sigT:F2}   χ² = {chi2.Statistic:F2},  p = {FormatPValue(chi2.PValue)}  → {(chi2.Reject ? "ОТВЕРГНУТЬ" : "не отвергать")}");
        }

        return sb.ToString();
    }

    #endregion
}
