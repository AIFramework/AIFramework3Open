using AI.DataStructs.Algebraic;
using AI.Statistics;
using AI.Statistics.Distributions;
using AI.Statistics.MixtureModeling;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Тесты закрывают ключевые исправления багов и проверяют корректность
/// алгоритмов смесей и EM.
/// </summary>
public class StatisticsTests
{
    // ----------------------------- Quantile -----------------------------

    [Fact]
    public void Quantile_DoesNotMutateInput()
    {
        // Регрессионный тест: старый конструктор сортировал пользовательские данные in-place.
        var input = new Vector(3.0, 1.0, 2.0, 5.0, 4.0);
        var snapshot = input.ToArray();

        var q = new Quantile(input);

        Assert.Equal(snapshot, input.ToArray());
        Assert.NotNull(q.SortVec);
        Assert.Equal(new double[] { 1, 2, 3, 4, 5 }, q.SortVec.ToArray());
    }

    [Fact]
    public void FastQuantile_EdgeValuesAreMinMax()
    {
        var input = new Vector(3.0, 1.0, 2.0, 5.0, 4.0);

        Assert.Equal(1.0, Quantile.FastQuantile(input, 0.0));
        Assert.Equal(5.0, Quantile.FastQuantile(input, 1.0));
    }

    // ------------------------ RandNorm(short) ---------------------------

    [Fact]
    public void RandNorm_Short_ProducesGaussianNotUniform()
    {
        // Старая реализация возвращала U(0,1); новая — N(0,1).
        var rng = RandomEngine.Create(seed: 42);
        var m = AI.Statistics.Statistic.RandNorm(500, 500, rng);

        double mean = 0, m2 = 0;
        int n = 0;
        for (int i = 0; i < m.Shape.Count; i++)
        {
            n++;
            double delta = m.Data[i] - mean;
            mean += delta / n;
            m2 += delta * (m.Data[i] - mean);
        }
        double variance = m2 / (n - 1);

        Assert.InRange(mean, -0.1, 0.1);        // N(0,1) -> среднее ~ 0
        Assert.InRange(variance, 0.85, 1.15);   // дисперсия ~ 1
    }

    // ----------------------------- Gauss2 -------------------------------

    [Fact]
    public void Gauss2_HasUnitVariance()
    {
        var rng = RandomEngine.Create(seed: 7);
        int n = 100_000;
        double sum = 0, sumSq = 0;
        for (int i = 0; i < n; i++)
        {
            double x = AI.Statistics.Statistic.Gauss2(rng, 12);
            sum += x;
            sumSq += x * x;
        }
        double mean = sum / n;
        double variance = (sumSq / n) - (mean * mean);

        Assert.InRange(mean, -0.03, 0.03);
        Assert.InRange(variance, 0.95, 1.05);
    }

    // ---------------------------- Histogram -----------------------------

    [Fact]
    public void Histogram_MaxValueFallsIntoLastBin()
    {
        // Регрессионный тест: при «двойном ≤» в сравнении max попадал
        // одновременно в два бина. Новая версия вводит полу-интервалы.
        var data = new Vector(0.0, 1.0, 2.0, 3.0, 4.0, 5.0);
        var stat = new Statistic(data);

        var hist = stat.Histogramm(5);

        // площадь гистограммы = 1
        double area = 0;
        double step = hist.X[1] - hist.X[0];
        for (int i = 0; i < hist.Y.Count; i++) area += hist.Y[i] * step;
        Assert.InRange(area, 0.999, 1.001);
    }

    // --------------------------- Hist JSON IO ---------------------------

    [Fact]
    public void Histogramm_JsonRoundTrip_PreservesData()
    {
        var hist = new Histogramm(3);
        hist.X[0] = 0; hist.X[1] = 1; hist.X[2] = 2;
        hist.Y[0] = 0.5; hist.Y[1] = 0.3; hist.Y[2] = 0.2;
        hist.Name = "tst";

        string path = Path.GetTempFileName();
        try
        {
            hist.Save(path);

            var loaded = new Histogramm();
            loaded.Open(path);

            Assert.Equal(hist.X.ToArray(), loaded.X.ToArray());
            Assert.Equal(hist.Y.ToArray(), loaded.Y.ToArray());
            Assert.Equal(hist.Name, loaded.Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ----------------------- RandomItemSelection ------------------------

    [Fact]
    public void RandomItemSelection_RespectsDistribution()
    {
        var rng = RandomEngine.Create(seed: 1);
        var probs = new Vector(0.1, 0.7, 0.2);
        int[] counts = new int[3];

        const int N = 100_000;
        for (int i = 0; i < N; i++)
            counts[RandomItemSelection.GetIndex(probs, rng)]++;

        Assert.InRange(counts[0] / (double)N, 0.085, 0.115);
        Assert.InRange(counts[1] / (double)N, 0.685, 0.715);
        Assert.InRange(counts[2] / (double)N, 0.185, 0.215);
    }

    // --------------------- MixtureModel + NCGaussian --------------------

    [Fact]
    public void MixtureModel_LogProb_IsNumericallyStable()
    {
        // Раньше CulcLogProb = Math.Log(CulcProb(x)): для x, далёкого от
        // средних, CulcProb = 0 -> log = -∞. Теперь должно быть конечное.
        var g = new NonCorrelatedGaussian();
        var w = new Vector(0.5, 0.5);
        var paramList = new List<Dictionary<string, double>>
        {
            new() { [NonCorrelatedGaussian.KeyMean] = -100.0, [NonCorrelatedGaussian.KeyStd] = 1.0 },
            new() { [NonCorrelatedGaussian.KeyMean] = +100.0, [NonCorrelatedGaussian.KeyStd] = 1.0 },
        };
        var mix = new MixtureModel(g, paramList, w);

        double logP = mix.CulcLogProb(200.0);
        Assert.False(double.IsNaN(logP));
        Assert.False(double.IsInfinity(logP));
    }

    [Fact]
    public void MixtureModel_Posterior_SumsToOne()
    {
        var g = new NonCorrelatedGaussian();
        var w = new Vector(0.3, 0.7);
        var paramList = new List<Dictionary<string, double>>
        {
            new() { [NonCorrelatedGaussian.KeyMean] = 0.0, [NonCorrelatedGaussian.KeyStd] = 1.0 },
            new() { [NonCorrelatedGaussian.KeyMean] = 5.0, [NonCorrelatedGaussian.KeyStd] = 1.0 },
        };
        var mix = new MixtureModel(g, paramList, w);

        var post = mix.Posterior(2.5);
        double sum = 0;
        for (int i = 0; i < post.Count; i++) sum += post[i];
        Assert.InRange(sum, 0.9999, 1.0001);
    }

    // ------------------------------ EM ----------------------------------

    [Fact]
    public void EM_Fit1D_RecoversWellSeparatedMeans()
    {
        // Генерируем две хорошо разделённые компоненты и смотрим, что
        // EM находит их средние с точностью до 0.3.
        var rng = RandomEngine.Create(seed: 123);
        var data = new List<double>();
        for (int i = 0; i < 500; i++)
            data.Add(NonCorrelatedGaussian.Sample(-5.0, 1.0, rng));
        for (int i = 0; i < 500; i++)
            data.Add(NonCorrelatedGaussian.Sample(+5.0, 1.0, rng));

        var mix = EM.Fit(data, numComponents: 2, seed: 42);

        double m0 = mix.Means[0][0];
        double m1 = mix.Means[1][0];
        double lo = Math.Min(m0, m1);
        double hi = Math.Max(m0, m1);

        Assert.InRange(lo, -5.5, -4.5);
        Assert.InRange(hi, 4.5, 5.5);
        Assert.False(double.IsNegativeInfinity(mix.LogLikelihood));
    }

    // --------------------------- Bayesian -------------------------------

    [Fact]
    public void Bayesian_LogArgmax_PrefersHigherLogPosterior()
    {
        var g1 = new NonCorrelatedGaussian();
        // две «компоненты» — 1D-обёртки
        var d1 = new BoundDistribution1D(g1, new Dictionary<string, double>
        {
            [NonCorrelatedGaussian.KeyMean] = 0.0, [NonCorrelatedGaussian.KeyStd] = 1.0
        });
        var d2 = new BoundDistribution1D(g1, new Dictionary<string, double>
        {
            [NonCorrelatedGaussian.KeyMean] = 10.0, [NonCorrelatedGaussian.KeyStd] = 1.0
        });

        var bayes = new Bayesian(new IDistributionWithoutParams[] { d1, d2 }, new Vector(0.5, 0.5));

        Assert.Equal(0, bayes.LogArgmax1D(-1.0));
        Assert.Equal(1, bayes.LogArgmax1D(9.5));
    }
}
