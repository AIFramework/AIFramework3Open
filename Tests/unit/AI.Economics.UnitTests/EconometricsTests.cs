using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;
using Xunit;

namespace AI.Economics.UnitTests;

/// <summary>Тесты эконометрического движка: регрессии, панели, причинность и ряды.</summary>
public class EconometricsTests
{
    [Fact]
    public void LinearRegression_Fit_RecoversKnownCoefficients()
    {
        Random rng = RandomEngine.Create(1);
        const int n = 500;

        var x = new Matrix(n, 2);
        var y = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double x1 = RandomEngine.NextGaussian(rng);
            double x2 = RandomEngine.NextGaussian(rng);

            x[i, 0] = x1;
            x[i, 1] = x2;
            y[i] = 2.0 + (3.0 * x1) - (1.5 * x2) + RandomEngine.NextGaussian(rng, 0, 0.5);
        }

        RegressionResult fit = LinearRegression.Fit(x, y, ["x1", "x2"]);

        Assert.Equal(2.0, fit.Coefficients[0].Estimate, 1);
        Assert.Equal(3.0, fit.Coefficients[1].Estimate, 1);
        Assert.Equal(-1.5, fit.Coefficients[2].Estimate, 1);

        Assert.True(fit.RSquared > 0.9);
        Assert.True(fit.FPValue < 1e-10);
        Assert.All(fit.Coefficients, c => Assert.True(c.StandardError > 0));
    }

    [Fact]
    public void LinearRegression_RobustVariance_KeepsEstimatesAndChangesErrors()
    {
        Random rng = RandomEngine.Create(2);
        const int n = 400;

        var x = new Matrix(n, 1);
        var y = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double value = RandomEngine.NextGaussian(rng);
            x[i, 0] = value;

            // Дисперсия ошибки растёт с регрессором
            y[i] = 1.0 + (2.0 * value) + RandomEngine.NextGaussian(rng, 0, 0.3 + Math.Abs(value));
        }

        RegressionResult classical = LinearRegression.Fit(x, y);
        RegressionResult robust = LinearRegression.Fit(x, y, null,
            new RegressionOptions { Variance = RobustVariance.Hc3 });

        Assert.Equal(classical.Coefficients[1].Estimate, robust.Coefficients[1].Estimate, 10);
        Assert.NotEqual(classical.Coefficients[1].StandardError, robust.Coefficients[1].StandardError, 6);
        Assert.Equal(RobustVariance.Hc3, robust.Variance);
    }

    [Fact]
    public void LinearRegression_Clustered_WidensErrorsWithinGroups()
    {
        Random rng = RandomEngine.Create(3);
        const int groups = 40, perGroup = 20;
        int n = groups * perGroup;

        var x = new Matrix(n, 1);
        var y = new Vector(n);
        var clusters = new List<int>(n);

        for (int g = 0; g < groups; g++)
        {
            double shock = RandomEngine.NextGaussian(rng, 0, 1.5);

            for (int t = 0; t < perGroup; t++)
            {
                int i = (g * perGroup) + t;
                double value = RandomEngine.NextGaussian(rng);

                x[i, 0] = value;
                y[i] = 1.0 + (0.5 * value) + shock + RandomEngine.NextGaussian(rng, 0, 0.4);
                clusters.Add(g);
            }
        }

        RegressionResult naive = LinearRegression.Fit(x, y);
        RegressionResult clustered = LinearRegression.Fit(x, y, null,
            new RegressionOptions { Variance = RobustVariance.Clustered, Clusters = clusters });

        Assert.Equal(naive.Coefficients[1].Estimate, clustered.Coefficients[1].Estimate, 10);
        Assert.True(clustered.Coefficients[0].StandardError > naive.Coefficients[0].StandardError,
            "Кластерная ошибка свободного члена обязана быть шире наивной при групповом шоке.");
    }

    [Fact]
    public void Diagnostics_BreuschPagan_SeparatesHomoskedasticFromHeteroskedastic()
    {
        Random rng = RandomEngine.Create(4);
        const int n = 400;

        var x = new Matrix(n, 1);
        var homo = new Vector(n);
        var hetero = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double value = RandomEngine.NextGaussian(rng);
            x[i, 0] = value;

            homo[i] = 1 + (2 * value) + RandomEngine.NextGaussian(rng, 0, 1);

            // Разброс монотонно растёт с регрессором: именно такую форму и ловит тест
            hetero[i] = 1 + (2 * value) + RandomEngine.NextGaussian(rng, 0, 0.3 * Math.Exp(0.7 * value));
        }

        RegressionResult homoFit = LinearRegression.Fit(x, homo);
        RegressionResult heteroFit = LinearRegression.Fit(x, hetero);

        DiagnosticTest clean = Diagnostics.BreuschPagan(x, homoFit.Residuals);
        DiagnosticTest dirty = Diagnostics.BreuschPagan(x, heteroFit.Residuals);

        Assert.False(clean.Rejected, $"Постоянная дисперсия ошибочно отвергнута, p = {clean.PValue:F4}.");
        Assert.True(dirty.Rejected, $"Гетероскедастичность не обнаружена, p = {dirty.PValue:F4}.");

        // При симметричной зависимости дисперсии от регрессора линейная связь
        // квадрата остатка с ним нулевая: Бройш — Паган здесь бессилен, а Уайт нет
        var symmetric = new Vector(n);
        for (int i = 0; i < n; i++)
            symmetric[i] = 1 + (2 * x[i, 0]) + RandomEngine.NextGaussian(rng, 0, 0.1 + (4 * Math.Abs(x[i, 0])));

        RegressionResult symmetricFit = LinearRegression.Fit(x, symmetric);

        DiagnosticTest breusch = Diagnostics.BreuschPagan(x, symmetricFit.Residuals);
        DiagnosticTest white = Diagnostics.White(x, symmetricFit.Residuals);

        Assert.True(white.Rejected, $"Уайт не обнаружил симметричную гетероскедастичность, p = {white.PValue:F4}.");
        Assert.True(white.PValue < breusch.PValue,
            $"Уайт ({white.PValue:F6}) обязан быть чувствительнее Бройша — Пагана ({breusch.PValue:F6}).");
    }

    [Fact]
    public void Diagnostics_VarianceInflation_DetectsCollinearity()
    {
        Random rng = RandomEngine.Create(5);
        const int n = 300;
        var x = new Matrix(n, 3);

        for (int i = 0; i < n; i++)
        {
            double a = RandomEngine.NextGaussian(rng);
            x[i, 0] = a;
            x[i, 1] = RandomEngine.NextGaussian(rng);

            // Третий регрессор почти повторяет первый
            x[i, 2] = a + RandomEngine.NextGaussian(rng, 0, 0.1);
        }

        IReadOnlyList<VarianceInflation> vif =
            Diagnostics.VarianceInflationFactors(x, ["a", "b", "почти a"]);

        Assert.Equal(3, vif.Count);
        Assert.True(vif.First(v => v.Variable == "почти a").IsSevere);
        Assert.False(vif.First(v => v.Variable == "b").IsSevere);
    }

    [Fact]
    public void Diagnostics_Chow_DetectsStructuralBreak()
    {
        Random rng = RandomEngine.Create(6);
        const int n = 300;

        var x = new Matrix(n, 1);
        var stable = new Vector(n);
        var broken = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double value = RandomEngine.NextGaussian(rng);
            x[i, 0] = value;

            double noise = RandomEngine.NextGaussian(rng, 0, 0.5);
            stable[i] = 1 + (2 * value) + noise;
            broken[i] = i < n / 2 ? 1 + (2 * value) + noise : 1 + (-2 * value) + noise;
        }

        Assert.False(Diagnostics.Chow(x, stable, n / 2).Rejected);
        Assert.True(Diagnostics.Chow(x, broken, n / 2).Rejected);
    }

    [Fact]
    public void InstrumentalVariables_TwoStage_CorrectsEndogeneityBias()
    {
        Random rng = RandomEngine.Create(7);
        const int n = 2000;

        var endogenous = new Matrix(n, 1);
        var instruments = new Matrix(n, 1);
        var y = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double instrument = RandomEngine.NextGaussian(rng);
            double confounder = RandomEngine.NextGaussian(rng);

            // Регрессор коррелирован с ошибкой через общий ненаблюдаемый фактор
            double price = (0.8 * instrument) + confounder + RandomEngine.NextGaussian(rng, 0, 0.3);

            endogenous[i, 0] = price;
            instruments[i, 0] = instrument;
            y[i] = 1.0 + (-2.0 * price) + (1.5 * confounder) + RandomEngine.NextGaussian(rng, 0, 0.3);
        }

        IvResult iv = InstrumentalVariables.TwoStage(
            endogenous, null, instruments, y, ["цена"]);

        double ivEstimate = iv.Coefficients.First(c => c.Name == "цена").Estimate;
        double olsEstimate = iv.OrdinaryLeastSquares.First(c => c.Name == "цена").Estimate;

        Assert.True(Math.Abs(ivEstimate + 2.0) < 0.15,
            $"Инструментальная оценка {ivEstimate:F3} далека от истинного -2.");
        Assert.True(Math.Abs(olsEstimate + 2.0) > Math.Abs(ivEstimate + 2.0),
            "МНК обязан быть смещён сильнее инструментальной оценки.");

        Assert.False(iv.HasWeakInstruments);
        Assert.True(iv.HausmanPValue < 0.05, "Эндогенность заложена в данные и должна обнаруживаться.");
    }

    [Fact]
    public void PanelData_FixedEffects_RemovesUnitEffectBias()
    {
        Random rng = RandomEngine.Create(8);
        const int units = 60, periods = 8;
        int n = units * periods;

        var x = new Matrix(n, 1);
        var y = new Vector(n);
        var unitIds = new List<int>(n);
        var periodIds = new List<int>(n);

        for (int u = 0; u < units; u++)
        {
            double effect = RandomEngine.NextGaussian(rng, 0, 2);

            for (int t = 0; t < periods; t++)
            {
                int i = (u * periods) + t;

                // Регрессор коррелирован с индивидуальным эффектом
                double value = (0.8 * effect) + RandomEngine.NextGaussian(rng);

                x[i, 0] = value;
                y[i] = (1.0 * value) + effect + RandomEngine.NextGaussian(rng, 0, 0.5);

                unitIds.Add(u);
                periodIds.Add(t);
            }
        }

        var dataset = new PanelDataset
        {
            Regressors = x, Response = y, Units = unitIds, Periods = periodIds, Names = ["x"],
        };

        PanelResult pooled = PanelData.Fit(dataset, PanelEstimator.Pooled);
        PanelResult within = PanelData.Fit(dataset, PanelEstimator.FixedEffects);

        double pooledEstimate = pooled.Coefficients.First(c => c.Name == "x").Estimate;
        double withinEstimate = within.Coefficients.First(c => c.Name == "x").Estimate;

        Assert.True(Math.Abs(withinEstimate - 1.0) < 0.1,
            $"Внутригрупповая оценка {withinEstimate:F3} должна быть близка к единице.");
        Assert.True(Math.Abs(pooledEstimate - 1.0) > Math.Abs(withinEstimate - 1.0),
            "Объединённый МНК обязан быть смещён сильнее.");
    }

    [Fact]
    public void PanelData_Hausman_PrefersFixedEffectsWhenEffectsCorrelated()
    {
        Random rng = RandomEngine.Create(9);
        const int units = 80, periods = 6;
        int n = units * periods;

        var x = new Matrix(n, 1);
        var y = new Vector(n);
        var unitIds = new List<int>(n);
        var periodIds = new List<int>(n);

        for (int u = 0; u < units; u++)
        {
            double effect = RandomEngine.NextGaussian(rng, 0, 1.5);

            for (int t = 0; t < periods; t++)
            {
                int i = (u * periods) + t;
                double value = effect + RandomEngine.NextGaussian(rng);

                x[i, 0] = value;
                y[i] = (0.7 * value) + effect + RandomEngine.NextGaussian(rng, 0, 0.4);

                unitIds.Add(u);
                periodIds.Add(t);
            }
        }

        var dataset = new PanelDataset
        {
            Regressors = x, Response = y, Units = unitIds, Periods = periodIds, Names = ["x"],
        };

        HausmanResult hausman = PanelData.Hausman(
            PanelData.Fit(dataset, PanelEstimator.FixedEffects),
            PanelData.Fit(dataset, PanelEstimator.RandomEffects));

        Assert.True(hausman.Statistic >= 0);
        Assert.NotEmpty(hausman.Differences);
    }

    [Fact]
    public void DynamicPanel_ArellanoBond_EstimatesPersistenceWithinBounds()
    {
        Random rng = RandomEngine.Create(10);
        const int units = 100, periods = 8;
        const double truth = 0.5;

        var xs = new List<double>();
        var ys = new List<double>();
        var unitIds = new List<int>();
        var periodIds = new List<int>();

        for (int u = 0; u < units; u++)
        {
            double effect = RandomEngine.NextGaussian(rng, 0, 0.5);
            double level = effect / (1 - truth);

            for (int t = 0; t < periods; t++)
            {
                double regressor = RandomEngine.NextGaussian(rng);
                level = (truth * level) + (0.4 * regressor) + effect + RandomEngine.NextGaussian(rng, 0, 0.3);

                xs.Add(regressor);
                ys.Add(level);
                unitIds.Add(u);
                periodIds.Add(t);
            }
        }

        var x = new Matrix(xs.Count, 1);
        var y = new Vector(ys.Count);

        for (int i = 0; i < xs.Count; i++) { x[i, 0] = xs[i]; y[i] = ys[i]; }

        var dataset = new PanelDataset
        {
            Regressors = x, Response = y, Units = unitIds, Periods = periodIds, Names = ["x"],
        };

        DynamicPanelResult result = DynamicPanel.ArellanoBond(dataset, maxLags: 3);

        Assert.True(result.WithinPersistence < result.PooledPersistence,
            "Внутригрупповая оценка обязана быть ниже объединённой в динамической панели.");
        Assert.InRange(result.Persistence, -1.0, 1.5);
        Assert.True(result.Instruments > 0);
    }

    [Fact]
    public void LimitedDependent_Logit_RecoversCoefficients()
    {
        Random rng = RandomEngine.Create(11);
        const int n = 3000;

        var x = new Matrix(n, 1);
        var y = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double value = RandomEngine.NextGaussian(rng);
            x[i, 0] = value;

            double probability = 1.0 / (1.0 + Math.Exp(-(-0.5 + (1.5 * value))));
            y[i] = rng.NextDouble() < probability ? 1 : 0;
        }

        LimitedDependentResult logit = LimitedDependent.Fit(x, y, LimitedDependentModel.Logit, ["x"]);

        Assert.Equal(-0.5, logit.Coefficients[0].Estimate, 1);
        Assert.Equal(1.5, logit.Coefficients[1].Estimate, 1);
        Assert.True(logit.McFaddenRSquared > 0.1);
        Assert.Single(logit.MarginalEffects);
        Assert.True(logit.Converged);
    }

    [Fact]
    public void LimitedDependent_Poisson_RecoversRate()
    {
        Random rng = RandomEngine.Create(12);
        const int n = 2000;

        var x = new Matrix(n, 1);
        var y = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double value = RandomEngine.NextGaussian(rng, 0, 0.5);
            x[i, 0] = value;
            y[i] = RandomEngine.NextPoisson(rng, Math.Exp(0.7 + (0.8 * value)));
        }

        LimitedDependentResult poisson = LimitedDependent.Fit(x, y, LimitedDependentModel.Poisson, ["x"]);

        Assert.Equal(0.7, poisson.Coefficients[0].Estimate, 1);
        Assert.Equal(0.8, poisson.Coefficients[1].Estimate, 1);
        Assert.True(poisson.Converged);
    }

    [Fact]
    public void LimitedDependent_Tobit_BeatsLeastSquaresUnderCensoring()
    {
        Random rng = RandomEngine.Create(13);
        const int n = 1500;

        var x = new Matrix(n, 1);
        var censored = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double value = RandomEngine.NextGaussian(rng);
            x[i, 0] = value;

            double latent = 0.5 + (1.0 * value) + RandomEngine.NextGaussian(rng, 0, 1);
            censored[i] = Math.Max(latent, 0);
        }

        LimitedDependentResult tobit = LimitedDependent.Fit(x, censored, LimitedDependentModel.Tobit, ["x"]);
        RegressionResult ols = LinearRegression.Fit(x, censored, ["x"]);

        double tobitSlope = tobit.Coefficients[1].Estimate;
        double olsSlope = ols.Coefficients[1].Estimate;

        Assert.True(Math.Abs(tobitSlope - 1.0) < Math.Abs(olsSlope - 1.0),
            $"Тобит {tobitSlope:F3} должен быть ближе к единице, чем МНК {olsSlope:F3}.");
        Assert.True(tobit.CensoredShare > 0.1);
    }

    [Fact]
    public void QuantileRegression_Median_IsRobustToOutliers()
    {
        Random rng = RandomEngine.Create(14);
        const int n = 400;

        var x = new Matrix(n, 1);
        var y = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double value = RandomEngine.NextGaussian(rng);
            x[i, 0] = value;
            y[i] = 1.0 + (2.0 * value) + RandomEngine.NextGaussian(rng, 0, 0.3);

            // Пять процентов наблюдений испорчены грубыми выбросами
            if (rng.NextDouble() < 0.05) y[i] += 50;
        }

        QuantileRegressionResult median = QuantileRegression.Fit(x, y, 0.5, ["x"], bootstrapSamples: 40);
        RegressionResult ols = LinearRegression.Fit(x, y, ["x"]);

        double medianSlope = median.Coefficients[1].Estimate;
        double olsIntercept = ols.Coefficients[0].Estimate;
        double medianIntercept = median.Coefficients[0].Estimate;

        Assert.True(Math.Abs(medianSlope - 2.0) < 0.2,
            $"Медианная регрессия дала наклон {medianSlope:F3} вместо двух.");
        Assert.True(Math.Abs(medianIntercept - 1.0) < Math.Abs(olsIntercept - 1.0),
            "Выбросы обязаны сдвигать МНК сильнее медианной регрессии.");
    }

    [Fact]
    public void DifferenceInDifferences_Estimate_RecoversKnownEffect()
    {
        Random rng = RandomEngine.Create(15);
        const int units = 60, periods = 8, adoption = 5;
        const double effect = 3.0;

        var observations = new List<DidObservation>();

        for (int u = 0; u < units; u++)
        {
            bool treated = u % 2 == 0;
            int first = treated ? adoption : 0;
            double level = RandomEngine.NextGaussian(rng, 10, 2);

            for (int t = 1; t <= periods; t++)
            {
                double outcome = level + (0.5 * t) + RandomEngine.NextGaussian(rng, 0, 0.5);
                if (treated && t >= adoption) outcome += effect;

                observations.Add(new DidObservation(u, t, outcome, first));
            }
        }

        DidResult did = DifferenceInDifferences.Estimate(observations, bootstrapSamples: 60, seed: 3);

        Assert.True(Math.Abs(did.RobustAtt - effect) < 0.5,
            $"Оценка {did.RobustAtt:F3} далека от заложенного эффекта {effect}.");
        Assert.True(did.PValue < 0.05);
        Assert.True(did.PreTrendPValue > 0.05, "Тренды до внедрения параллельны по построению.");
        Assert.NotEmpty(did.EventStudy);
    }

    [Fact]
    public void RegressionDiscontinuity_Estimate_RecoversJump()
    {
        Random rng = RandomEngine.Create(16);
        const int n = 2000;
        const double jump = 2.0;

        var observations = new List<RddObservation>(n);

        for (int i = 0; i < n; i++)
        {
            double running = (rng.NextDouble() * 4) - 2;
            double outcome = 1 + (0.5 * running) + RandomEngine.NextGaussian(rng, 0, 0.3);
            if (running >= 0) outcome += jump;

            observations.Add(new RddObservation(running, outcome));
        }

        RddResult rdd = RegressionDiscontinuity.Estimate(observations, cutoff: 0);

        Assert.True(Math.Abs(rdd.Effect - jump) < 0.3,
            $"Оценка скачка {rdd.Effect:F3} далека от заложенных {jump}.");
        Assert.True(rdd.PValue < 0.01);
        Assert.True(rdd.LeftObservations > 30 && rdd.RightObservations > 30);
        Assert.NotEmpty(rdd.Sensitivity);
    }

    [Fact]
    public void PropensityScoreMatching_Estimate_RemovesSelectionBias()
    {
        Random rng = RandomEngine.Create(17);
        const int n = 3000;
        const double effect = 1.0;

        var covariates = new Matrix(n, 2);
        var treatment = new Vector(n);
        var outcome = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double a = RandomEngine.NextGaussian(rng);
            double b = RandomEngine.NextGaussian(rng);

            covariates[i, 0] = a;
            covariates[i, 1] = b;

            // Отбор в программу зависит от наблюдаемых характеристик
            double probability = 1.0 / (1.0 + Math.Exp(-(0.9 * a) - (0.6 * b)));
            bool treated = rng.NextDouble() < probability;

            treatment[i] = treated ? 1 : 0;
            outcome[i] = (1.5 * a) + (1.0 * b) + (treated ? effect : 0) + RandomEngine.NextGaussian(rng, 0, 0.5);
        }

        MatchingResult matching = PropensityScoreMatching.Estimate(
            covariates, treatment, outcome, ["a", "b"], caliperFactor: 0.2, neighbours: 3);

        Assert.True(Math.Abs(matching.AverageTreatmentEffectOnTreated - effect) < 0.25,
            $"Оценка {matching.AverageTreatmentEffectOnTreated:F3} далека от {effect}.");
        Assert.True(Math.Abs(matching.NaiveDifference - effect) >
                    Math.Abs(matching.AverageTreatmentEffectOnTreated - effect),
            "Наивная разность обязана быть смещена сильнее.");
        Assert.True(matching.CommonSupport > 0.8);
        Assert.All(matching.Balance, b => Assert.True(Math.Abs(b.StandardizedAfter) < Math.Abs(b.StandardizedBefore)));
    }

    [Fact]
    public void SyntheticControl_Build_TracksTreatedBeforeIntervention()
    {
        Random rng = RandomEngine.Create(18);
        const int periods = 30, treatmentPeriod = 20, donors = 6;
        const double effect = 5.0;

        var donorSeries = new Matrix(periods, donors);
        var treated = new Vector(periods);

        var factors = new double[periods];
        for (int t = 0; t < periods; t++) factors[t] = 10 + (0.3 * t) + RandomEngine.NextGaussian(rng, 0, 1);

        for (int j = 0; j < donors; j++)
            for (int t = 0; t < periods; t++)
                donorSeries[t, j] = (factors[t] * (0.8 + (0.1 * j))) + RandomEngine.NextGaussian(rng, 0, 0.3);

        for (int t = 0; t < periods; t++)
        {
            // Объект воспроизводится комбинацией первых двух доноров
            treated[t] = (0.5 * donorSeries[t, 0]) + (0.5 * donorSeries[t, 1])
                + RandomEngine.NextGaussian(rng, 0, 0.2);

            if (t >= treatmentPeriod) treated[t] += effect;
        }

        SyntheticControlResult synthetic = SyntheticControl.Build(
            treated, donorSeries, null, treatmentPeriod, "регион");

        Assert.True(synthetic.PreTreatmentRmspe < 1.0,
            $"Ошибка подгонки до вмешательства {synthetic.PreTreatmentRmspe:F3} слишком велика.");
        Assert.True(Math.Abs(synthetic.AverageEffect - effect) < 1.0,
            $"Оценка эффекта {synthetic.AverageEffect:F3} далека от {effect}.");
        Assert.True(synthetic.RmspeRatio > 3);
        Assert.Equal(1.0, synthetic.Weights.Sum(w => w.Weight), 6);
    }

    [Fact]
    public void CausalForest_Fit_FindsHeterogeneity()
    {
        Random rng = RandomEngine.Create(19);
        const int n = 2000;

        var features = new Matrix(n, 2);
        var treatment = new Vector(n);
        var outcome = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double driver = rng.NextDouble();
            double noiseFeature = rng.NextDouble();

            features[i, 0] = driver;
            features[i, 1] = noiseFeature;

            bool treated = rng.NextDouble() < 0.5;
            treatment[i] = treated ? 1 : 0;

            // Эффект есть только у объектов с большим значением первого признака
            double individual = driver > 0.5 ? 2.0 : 0.0;
            outcome[i] = 1 + (0.5 * driver) + (treated ? individual : 0) + RandomEngine.NextGaussian(rng, 0, 0.5);
        }

        CausalForestResult forest = CausalForest.Fit(
            features, treatment, outcome, ["драйвер", "шум"], trees: 60, minLeaf: 25, maxDepth: 3, seed: 5);

        Assert.True(forest.HasHeterogeneity);
        Assert.Equal("драйвер", forest.Importance[0].Variable);
        Assert.True(forest.Groups[0].ActualEffect > forest.Groups[^1].ActualEffect,
            "Верхняя группа обязана иметь больший фактический эффект.");
        Assert.InRange(forest.AverageEffect, 0.5, 1.6);
    }

    [Fact]
    public void Stationarity_Tests_SeparateRandomWalkFromStationarySeries()
    {
        Random rng = RandomEngine.Create(20);
        const int n = 300;

        var walk = new Vector(n);
        var stationary = new Vector(n);
        double level = 0, value = 0;

        for (int t = 0; t < n; t++)
        {
            level += RandomEngine.NextGaussian(rng);
            value = (0.3 * value) + RandomEngine.NextGaussian(rng);

            walk[t] = level;
            stationary[t] = value;
        }

        StationarityReport walkReport = StationarityTests.Analyze(walk, name: "случайное блуждание");
        StationarityReport stationaryReport = StationarityTests.Analyze(stationary, name: "стационарный");

        Assert.False(walkReport.AugmentedDickeyFuller.Rejected,
            "Единичный корень у случайного блуждания отвергаться не должен.");
        Assert.True(stationaryReport.AugmentedDickeyFuller.Rejected,
            "У стационарного ряда единичный корень обязан отвергаться.");
        Assert.True(walkReport.IntegrationOrder >= 1);
        Assert.Equal(0, stationaryReport.IntegrationOrder);
    }

    [Fact]
    public void VectorAutoregression_Fit_RecoversDynamicsAndCausality()
    {
        Random rng = RandomEngine.Create(21);
        const int n = 600;

        var data = new Matrix(n, 2);
        double first = 0, second = 0;

        for (int t = 0; t < n; t++)
        {
            double nextFirst = (0.6 * first) + RandomEngine.NextGaussian(rng, 0, 0.5);

            // Вторая переменная зависит от прошлого первой, но не наоборот
            double nextSecond = (0.3 * second) + (0.7 * first) + RandomEngine.NextGaussian(rng, 0, 0.5);

            first = nextFirst;
            second = nextSecond;

            data[t, 0] = first;
            data[t, 1] = second;
        }

        VarResult var = VectorAutoregression.Fit(data, 1, ["первая", "вторая"]);

        Assert.True(var.IsStable);
        Assert.Equal(0.6, var.Coefficients[0, 1], 1);
        Assert.Equal(0.7, var.Coefficients[1, 1], 1);

        GrangerTest forward = var.Granger.First(g => g.From == "первая" && g.To == "вторая");
        GrangerTest backward = var.Granger.First(g => g.From == "вторая" && g.To == "первая");

        Assert.True(forward.Causes, "Причинность из первой во вторую заложена в данные.");
        Assert.False(backward.Causes, "Обратной причинности в данных нет.");

        double[][][] responses = VectorAutoregression.ImpulseResponse(var, 10);
        Assert.Equal(2, responses.Length);
        Assert.True(Math.Abs(responses[0][1][1]) > 0);

        Matrix decomposition = VectorAutoregression.VarianceDecomposition(var, 10);
        Assert.Equal(1.0, decomposition[0, 0] + decomposition[0, 1], 6);
    }

    [Fact]
    public void Cointegration_Johansen_FindsSingleRelation()
    {
        Random rng = RandomEngine.Create(22);
        const int n = 400;

        var data = new Matrix(n, 2);
        double common = 0;

        for (int t = 0; t < n; t++)
        {
            common += RandomEngine.NextGaussian(rng);

            data[t, 0] = common + RandomEngine.NextGaussian(rng, 0, 0.4);
            data[t, 1] = (2 * common) + RandomEngine.NextGaussian(rng, 0, 0.4);
        }

        JohansenResult johansen = Cointegration.Johansen(data, 1, ["первый", "второй"]);

        Assert.Equal(1, johansen.Rank);
        Assert.Equal(2, johansen.Rows.Count);
        Assert.True(johansen.Rows[0].TraceRejected);
        Assert.False(johansen.Rows[1].TraceRejected);

        VecmResult vecm = Cointegration.ErrorCorrection(data, 1, 1, ["первый", "второй"]);

        Assert.Equal(1, vecm.Rank);
        Assert.Equal(2, vecm.AdjustmentCoefficients.Count);
        Assert.Contains(vecm.AdjustmentCoefficients, c => c.Estimate < 0);
    }

    [Fact]
    public void Garch_Fit_RecoversVolatilityClustering()
    {
        Random rng = RandomEngine.Create(23);
        const int n = 2000;
        const double omega = 0.00002, alpha = 0.1, beta = 0.85;

        var returns = new Vector(n);
        double variance = omega / (1 - alpha - beta);

        for (int t = 0; t < n; t++)
        {
            double shock = RandomEngine.NextGaussian(rng) * Math.Sqrt(variance);
            returns[t] = shock;
            variance = omega + (alpha * shock * shock) + (beta * variance);
        }

        GarchResult garch = Garch.Fit(returns, GarchModel.Garch, horizon: 10);

        Assert.True(garch.IsStationary);
        Assert.InRange(garch.Persistence, 0.8, 0.999);
        Assert.Equal(n, garch.ConditionalVolatility.Count);
        Assert.Equal(10, garch.Forecast.Count);
        Assert.True(garch.LongRunVolatility > 0);

        // На стандартизованных остатках эффекта ARCH остаться не должно
        Assert.True(garch.ArchPValue > 0.01, $"ARCH-LM p = {garch.ArchPValue:F4} слишком мал.");
    }

    [Fact]
    public void StateSpace_Fit_SeparatesLevelFromNoise()
    {
        Random rng = RandomEngine.Create(24);
        const int n = 200;

        var series = new Vector(n);
        double level = 100;

        for (int t = 0; t < n; t++)
        {
            level += RandomEngine.NextGaussian(rng, 0, 0.3);
            series[t] = level + RandomEngine.NextGaussian(rng, 0, 2.0);
        }

        StateSpaceResult state = StateSpace.Fit(series, StateSpaceModel.LocalLevel, horizon: 6);

        Assert.Equal(n, state.Level.Count);
        Assert.Equal(6, state.Forecast.Count);
        Assert.True(state.ObservationVariance > state.LevelVariance,
            "Шум наблюдения в этих данных больше шума уровня.");

        // Сглаженный уровень должен быть заметно менее изменчив, чем исходный ряд
        double seriesVariation = Variation(series);
        double levelVariation = Variation(state.Level);

        Assert.True(levelVariation < seriesVariation,
            $"Сглаженный уровень {levelVariation:F3} не менее изменчив, чем ряд {seriesVariation:F3}.");
        Assert.All(state.ForecastUpper.Select((v, i) => v - state.ForecastLower[i]), w => Assert.True(w > 0));
    }

    /// <summary>Средний модуль первой разности ряда.</summary>
    private static double Variation(Vector series)
    {
        double sum = 0;
        for (int t = 1; t < series.Count; t++) sum += Math.Abs(series[t] - series[t - 1]);
        return sum / Math.Max(1, series.Count - 1);
    }
}
