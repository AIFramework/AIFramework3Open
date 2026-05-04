using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.DSP.Analyse;
using AI.DSP.DSPCore;
using AI.DSP.FIR;
using AI.Charts.JS;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;
using SkiaSharp;
using Vector = AI.DataStructs.Algebraic.Vector;
using AIFunctions = AI.Functions;

namespace AiFrameworkDemo.Modules.DSP;

public static class DspDemoRunner
{
    public static (string png, string? plotlyJson, ChartView cv) Render(string key, IReadOnlyDictionary<string, double> p, DemoSettings s)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;
        int fd = (int)N("fd", 2000);
        double dur = N("dur", 1.0);
        int n = Math.Max(16, (int)(fd * dur));

        // Время всегда ровно n элементов — без плавающей точки в Seq
        var t = MakeTime(n, fd);

        var cv = MakeView(s.Width, s.Height, s.DarkTheme);

        switch (key)
        {
            // -- СПЕКТРАЛЬНЫЙ АНАЛИЗ ------------------------------------------
            case "fft_spectrum":
            {
                double f1 = N("f1", 100), f2 = N("f2", 350), noise = N("noise", 0.3);
                var sig = Sin(t, 1, f1) + Sin(t, 0.6, f2) + WhiteNoise(n, noise);
                var padded = sig.CutAndZero(AIFunctions.NextPow2(n));
                var spec   = FFT.CalcFFT(padded).MagnitudeVector;
                int half   = spec.Count / 2;
                var freqs  = Signal.Frequency(spec.Count, fd).CutAndZero(half);
                var amp    = spec.CutAndZero(half) * (2.0 / spec.Count);

                cv.ChartName = $"FFT-спектр: {f1} Гц + {f2} Гц + шум";
                cv.LabelX = "Частота, Гц"; cv.LabelY = "Амплитуда";
                cv.AddPlot(freqs, amp, "Спектр", new SKColor(99, 200, 255));
                break;
            }
            case "welch_psd":
            {
                double f1 = N("f1", 100), f2 = N("f2", 350), noise = N("noise", 0.4);
                int win = Clamp((int)N("win", 256), 32, n);
                var sig    = Sin(t, 1, f1) + Sin(t, 0.6, f2) + WhiteNoise(n, noise);
                var window = WindowForFFT.HannWindow(win);
                var psd    = Welch.WelchRun(sig, win, 0.5, window);
                int half   = psd.Count / 2;
                var freqs  = Signal.Frequency(psd.Count, fd).CutAndZero(half);

                cv.ChartName = $"Welch PSD: окно {win}, перекрытие 50%";
                cv.LabelX = "Частота, Гц"; cv.LabelY = "СПМ";
                cv.AddPlot(freqs, psd.CutAndZero(half), "Welch PSD", new SKColor(160, 255, 140));
                break;
            }
            case "cepstrum":
            {
                double f0 = N("f0", 120), nHarm = N("nHarm", 5), noise = N("noise", 0.2);
                var sig = new Vector(n);
                for (int h = 1; h <= (int)nHarm; h++)
                    sig += Sin(t, 1.0 / h, f0 * h);
                sig += WhiteNoise(n, noise);
                var cep   = Cepstrum.FKT(sig.CutAndZero(AIFunctions.NextPow2(n)));
                int half  = cep.Count / 2;
                var qFreq = Signal.Frequency(cep.Count, fd).CutAndZero(half);

                cv.ChartName = $"Кепстр: F0={f0} Гц, {(int)nHarm} гармоник";
                cv.LabelX = "Кепстральная частота, Гц"; cv.LabelY = "|Кепстр|";
                cv.AddPlot(qFreq, cep.CutAndZero(half).Transform(Math.Abs), "Кепстр",
                    new SKColor(255, 200, 80));
                break;
            }

            // -- ФИЛЬТРАЦИЯ ---------------------------------------------------
            case "flt_lowpass":
            {
                double fSig = N("fSig", 100), fInterf = N("fInterf", 600),
                       fCut = N("fCut", 300), noise = N("noise", 0.1);
                var raw      = Sin(t, 1, fSig) + Sin(t, 0.7, fInterf) + WhiteNoise(n, noise);
                var filtered = Filters.FilterLowButterworthAFH(raw, fCut, fd, order: 4);
                TrimToMatch(ref t, ref filtered);

                cv.ChartName = $"ФНЧ Баттерворта (fср={fCut} Гц)";
                cv.LabelX = "Время, с"; cv.LabelY = "Амплитуда";
                PlotFirst(cv, t, raw,      "Вход",        new SKColor(100, 140, 255, 180));
                PlotFirst(cv, t, filtered, "Отфильтр.",   new SKColor(100, 255, 160));
                break;
            }
            case "flt_bandpass":
            {
                double fLow = N("fLow", 80), fHigh = N("fHigh", 200), noise = N("noise", 0.3);
                double fOut1 = N("fOut1", 30), fOut2 = N("fOut2", 400);
                var raw      = Sin(t, 1, fLow + (fHigh - fLow) / 2)
                             + Sin(t, 0.8, fOut1) + Sin(t, 0.8, fOut2)
                             + WhiteNoise(n, noise);
                var filtered = Filters.FilterBand(raw, fLow, fHigh, fd);
                TrimToMatch(ref t, ref filtered);

                cv.ChartName = $"Полосовой фильтр [{fLow}–{fHigh} Гц]";
                cv.LabelX = "Время, с"; cv.LabelY = "Амплитуда";
                PlotFirst(cv, t, raw,      "Вход",        new SKColor(100, 140, 255, 180));
                PlotFirst(cv, t, filtered, "Отфильтр.",   new SKColor(255, 160, 80));
                break;
            }
            case "flt_notch":
            {
                double fMain = N("fMain", 200), fNotch = N("fNotch", 50), bw = N("bw", 10);
                var raw      = Sin(t, 1, fMain) + Sin(t, 0.8, fNotch);
                var filtered = Filters.FilterRezector(raw, fNotch - bw / 2, fNotch + bw / 2, fd);
                TrimToMatch(ref t, ref filtered);

                cv.ChartName = $"Режекторный фильтр: подавить {fNotch} Гц ±{bw/2} Гц";
                cv.LabelX = "Время, с"; cv.LabelY = "Амплитуда";
                PlotFirst(cv, t, raw,      "Вход (сигн. + помеха)", new SKColor(100, 140, 255, 180));
                PlotFirst(cv, t, filtered, "После режектора",       new SKColor(100, 255, 160));
                break;
            }
            case "flt_fir":
            {
                double fSig = N("fSig", 100), fInterf = N("fInterf", 500), fCut = N("fCut", 250);
                int order   = Clamp((int)N("firOrder", 64), 4, n / 4);
                var raw     = Sin(t, 1, fSig) + Sin(t, 0.8, fInterf) + WhiteNoise(n, 0.1);
                var ht      = SincLowpass(order, fCut, fd);
                var fir     = new FIRFilter(ht, fd);
                var filtered = fir.FilterOutp(raw);
                TrimToMatch(ref t, ref filtered);

                cv.ChartName = $"КИХ-ФНЧ (sinc, порядок {order}, fср={fCut} Гц)";
                cv.LabelX = "Время, с"; cv.LabelY = "Амплитуда";
                PlotFirst(cv, t, raw,      "Вход",        new SKColor(100, 140, 255, 180));
                PlotFirst(cv, t, filtered, "КИХ-фильтр", new SKColor(255, 200, 80));
                break;
            }

            // -- ГЕНЕРАЦИЯ СИГНАЛОВ -------------------------------------------
            case "sig_sin_noise":
            {
                double f1 = N("f1", 100), f2 = N("f2", 250), noise = N("noise", 0.5);
                var clean = Sin(t, 1, f1) + Sin(t, 0.5, f2);
                var noisy = clean + WhiteNoise(n, noise);

                cv.ChartName = $"Сигнал: {f1} Гц + {f2} Гц + шум (σ={noise})";
                cv.LabelX = "Время, с"; cv.LabelY = "Амплитуда";
                PlotFirst(cv, t, noisy, "С шумом", new SKColor(100, 140, 255, 150));
                PlotFirst(cv, t, clean, "Чистый",  new SKColor(255, 140, 80));
                break;
            }
            case "sig_lfm":
            {
                double f0 = N("f0", 50), f1 = N("f1", 500);
                double mu  = (f1 - f0) / dur;
                var lfm    = t.Transform(ti => Math.Sin(2 * Math.PI * (f0 * ti + 0.5 * mu * ti * ti)));

                cv.ChartName = $"ЛЧМ-сигнал: {f0}->{f1} Гц за {dur} с";
                cv.LabelX = "Время, с"; cv.LabelY = "Амплитуда";
                PlotFirst(cv, t, lfm, "ЛЧМ", new SKColor(200, 160, 255));
                break;
            }
            case "sig_damped":
            {
                double fDamp = N("fDamp", 80), kDamp = N("kDamp", 2.0), nComp = N("nComp", 3);
                var sig = Signal.DampedOscillations(t, fDamp, -kDamp, 1.0, 0);
                for (int h = 2; h <= (int)nComp; h++)
                    sig += Signal.DampedOscillations(t, fDamp * h, -kDamp * h, 0.5 / h, 0);

                cv.ChartName = $"Затухающие колебания: f={fDamp} Гц, k={kDamp}";
                cv.LabelX = "Время, с"; cv.LabelY = "Амплитуда";
                PlotFirst(cv, t, sig, "Сигнал", new SKColor(255, 200, 80));
                break;
            }
            case "sig_ammod":
            {
                double fCarrier = N("fCarrier", 500), fMod = N("fMod", 30), mIdx = N("mIdx", 0.8);
                var mod      = Sin(t, mIdx, fMod);
                var am       = (1 + mod) * Sin(t, 1, fCarrier);
                var envelope = FastHilbert.Envelope(am).CutAndZero(n);

                cv.ChartName = $"АМ-сигнал: несущая {fCarrier} Гц, модуляция {fMod} Гц";
                cv.LabelX = "Время, с"; cv.LabelY = "Амплитуда";
                PlotFirst(cv, t, am,       "АМ-сигнал", new SKColor(100, 160, 255, 170));
                PlotFirst(cv, t, envelope, "Огибающая", new SKColor(255, 100, 100));
                break;
            }

            // -- АНАЛИТИЧЕСКИЙ СИГНАЛ (ГИЛЬБЕРТ) -----------------------------
            case "hilbert_envelope":
            {
                double fSig = N("fSig", 100), kDamp = N("kDamp", 2.0), noise = N("noise", 0.2);
                var sig          = Signal.DampedOscillations(t, fSig, -kDamp, 1.0, 0) + WhiteNoise(n, noise);
                var envelope     = FastHilbert.Envelope(sig).CutAndZero(n);
                var trueEnvelope = t.Transform(ti => Math.Exp(-kDamp * ti));

                cv.ChartName = "Огибающая через преобразование Гильберта";
                cv.LabelX = "Время, с"; cv.LabelY = "Амплитуда";
                PlotFirst(cv, t, sig,           "Сигнал",              new SKColor(100, 140, 255, 130));
                PlotFirst(cv, t, envelope,      "Гильберт-огибающая",  new SKColor(255, 160, 60));
                PlotFirst(cv, t, trueEnvelope,  "Истинная e^(-kt)",    new SKColor(100, 255, 120));
                break;
            }
            case "hilbert_instfreq":
            {
                double f0 = N("f0", 50), f1 = N("f1", 300);
                double mu  = (f1 - f0) / dur;
                var lfm    = t.Transform(ti => Math.Sin(2 * Math.PI * (f0 * ti + 0.5 * mu * ti * ti)));

                // Мгновенная частота — производная фазы × fd/(2π), длина n-1
                var instFreq = FastHilbert.Frequency(lfm) * (fd / (2 * Math.PI));
                var trueFreq = t.Transform(ti => f0 + mu * ti);

                int m = Math.Min(instFreq.Count, n - 1);
                var tTrim  = t.CutAndZero(m);
                var iF     = instFreq.CutAndZero(m);
                var tF     = trueFreq.CutAndZero(m);

                cv.ChartName = "Мгновенная частота ЛЧМ (Гильберт)";
                cv.LabelX = "Время, с"; cv.LabelY = "Частота, Гц";
                PlotFirst(cv, tTrim, tF, "Истинная f(t)",  new SKColor(100, 255, 120));
                PlotFirst(cv, tTrim, iF, "Гильберт f(t)", new SKColor(255, 160, 60));
                break;
            }

            // -- СВЁРТКА И КОРРЕЛЯЦИЯ -----------------------------------------
            case "conv_fast":
            {
                double fSig = N("fSig", 100), fCut = N("fCut", 200);
                int order   = Clamp((int)N("firOrder", 32), 4, n / 4);
                var sig     = Sin(t, 1, fSig) + Sin(t, 0.7, fSig * 4) + WhiteNoise(n, 0.15);
                var ht      = SincLowpass(order, fCut, fd);
                var result  = FastConv.FastConvolution(sig, ht).CutAndZero(n);

                cv.ChartName = $"Быстрая свёртка: КИХ-ФНЧ {fCut} Гц, {order} кап.";
                cv.LabelX = "Время, с"; cv.LabelY = "Амплитуда";
                PlotFirst(cv, t, sig,    "Вход",  new SKColor(100, 140, 255, 150));
                PlotFirst(cv, t, result, "Выход", new SKColor(255, 200, 80));
                break;
            }
            case "conv_autocorr":
            {
                double fSig = N("fSig", 100), noise = N("noise", 0.5);
                var sig      = Sin(t, 1, fSig) + WhiteNoise(n, noise);
                var autocorr = FastConv.FastCorrelation(sig, sig);
                int half     = autocorr.Count / 2;
                int len      = half + 1;
                var acfPos   = new Vector(len);
                var lags     = new Vector(len);
                double dt    = 1.0 / fd;
                for (int i = 0; i < len; i++)
                {
                    acfPos[i] = autocorr[half + i];
                    lags[i]   = i * dt;
                }

                cv.ChartName = $"Автокорреляция: f={fSig} Гц + шум σ={noise}";
                cv.LabelX = "Лаг, с"; cv.LabelY = "R(τ)";
                cv.AddPlot(lags, acfPos, "Автокорреляция",
                    new SKColor(200, 120, 255));
                break;
            }

            default:
                cv.ChartName = "Неизвестный ключ: " + key;
                break;
        }

        return (RenderPng(cv, s.Width, s.Height), PlotlyChartRenderer.ToPlotlyJson(cv), cv);
    }

    #region Вспомогательные методы

    /// <summary>Время всегда ровно n элементов — без плавающей точки</summary>
    private static Vector MakeTime(int n, int fd)
    {
        var t = new Vector(n);
        double dt = 1.0 / fd;
        for (int i = 0; i < n; i++) t[i] = i * dt;
        return t;
    }

    /// <summary>Синус через явный цикл — гарантированно n элементов</summary>
    private static Vector Sin(Vector t, double A, double f)
    {
        var v = new Vector(t.Count);
        for (int i = 0; i < t.Count; i++)
            v[i] = A * Math.Sin(2 * Math.PI * f * t[i]);
        return v;
    }

    /// <summary>Выравнивает длины t и y до минимума из двух</summary>
    private static void TrimToMatch(ref Vector t, ref Vector y)
    {
        int m = Math.Min(t.Count, y.Count);
        if (t.Count != m) t = t.CutAndZero(m);
        if (y.Count != m) y = y.CutAndZero(m);
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

    private static ChartView MakeView(int w, int h, bool dark)
        => DemoRunnerBase.MakeView(w, h, dark);

    private static string RenderPng(ChartView cv, int w, int h)
        => DemoRunnerBase.RenderPng(cv, w, h);

    /// <summary>Рисует не более maxPts точек (скорость рендера)</summary>
    private static void PlotFirst(ChartView cv, Vector t, Vector y, string label,
        SKColor color, int maxPts = 2000)
    {
        int cnt = Math.Min(t.Count, y.Count);
        if (cnt <= maxPts)
        {
            var tOut = t.CutAndZero(cnt);
            var yOut = y.CutAndZero(cnt);
            cv.AddPlot(tOut, yOut, label, color);
            return;
        }
        int step = cnt / maxPts;
        var ts = new Vector(maxPts);
        var ys = new Vector(maxPts);
        for (int i = 0; i < maxPts; i++) { ts[i] = t[i * step]; ys[i] = y[i * step]; }
        cv.AddPlot(ts, ys, label, color);
    }

    /// <summary>Белый гауссов шум (Box-Muller)</summary>
    private static Vector WhiteNoise(int n, double sigma, int seed = 42)
    {
        var rng = new Random(seed);
        var v   = new Vector(n);
        for (int i = 0; i < n; i++)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            v[i] = sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2 * Math.PI * u2);
        }
        return v;
    }

    /// <summary>sinc-КИХ идеального ФНЧ с окном Хэнна</summary>
    private static Vector SincLowpass(int order, double fCut, int fd)
    {
        int half    = order / 2;
        double wc   = 2 * Math.PI * fCut / fd;
        var ht      = new Vector(order + 1);
        var window  = WindowForFFT.HannWindow(order + 1);
        for (int i = 0; i <= order; i++)
        {
            int m  = i - half;
            ht[i]  = m == 0 ? wc / Math.PI : Math.Sin(wc * m) / (Math.PI * m);
            ht[i] *= window[i];
        }
        return ht;
    }
    #endregion Вспомогательные методы

}