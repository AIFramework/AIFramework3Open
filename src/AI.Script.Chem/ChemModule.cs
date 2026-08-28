using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Solvers.Chem.Kinetics;
using AI.Solvers.Chem.Metrology;
using AI.Solvers.Chem.Models;
using AI.Solvers.Chem.Production;
using AI.Solvers.Chem.Safety;
using AI.Solvers.Chem.Signals;

namespace AI.Script.Chem;

/// <summary>
/// Пространство <c>chem</c>: химические расчёты, метрология, обработка сигналов приборов
/// и калькуляция рецептур.
/// </summary>
/// <remarks>
/// Модуль даёт агенту не «ответ про химию», а проверяемый расчёт: баланс уравнения
/// подтверждается сохранением атомов и заряда, молярная масса берётся из таблицы
/// элементов, концентрация - из градуировки с доверительным интервалом. Там, где
/// языковая модель склонна выдумывать, функция либо считает, либо честно отказывает.
/// </remarks>
[ScriptModule("chem", "Химия: формулы, уравнения, метрология, хроматография, рецептуры", Version = "0.1")]
public static class ChemModule
{
    #region Формулы и уравнения

    [ScriptFn("mass", "Молярная масса по формуле, г/моль", Example = "chem.mass(\"CuSO4·5H2O\")")]
    public static double Mass([ScriptParam("химическая формула")] string formula)
    {
        var parsed = Parse(formula);

        if (!parsed.TryCalculateMolarMass(ChemContext.Database, out double mass, out string error))
            throw new ScriptError(DiagnosticCodes.BadOperand, $"chem.mass: {error}");

        return mass;
    }

    [ScriptFn("formula", "Разбор формулы: состав, масса, заряд", Example = "chem.formula(\"Ca(OH)2\").mass")]
    public static ScriptRecord Formula([ScriptParam("химическая формула")] string formula)
    {
        var parsed = Parse(formula);
        parsed.TryCalculateMolarMass(ChemContext.Database, out double mass, out string error);

        var symbols = parsed.Elements.Keys.ToArray();
        var counts = new Vector(symbols.Length);

        for (int i = 0; i < symbols.Length; i++)
            counts[i] = parsed.Elements[symbols[i]];

        var composition = ScriptTable.Create(
        [
            ScriptColumn.Own("element", symbols.Select(ScriptValue.Str).ToArray()),
            ScriptColumn.FromVector("count", counts),
        ]);

        return Record(
            ("formula", ScriptValue.Str(parsed.CoreFormula)),
            ("mass", ScriptValue.Num(mass)),
            ("charge", ScriptValue.Num(parsed.Charge)),
            ("state", ScriptValue.Str(parsed.State ?? string.Empty)),
            ("known", ScriptValue.Bool(error == null)),
            ("composition", ScriptValue.Table(composition)));
    }

    [ScriptFn("balance", "Сбалансированное уравнение реакции", Example = "chem.balance(\"Fe + O2 = Fe2O3\")")]
    public static string Balance([ScriptParam("уравнение реакции")] string equation)
    {
        var result = ChemContext.Engine.Execute($"balance {equation}");

        if (!result.Success)
            throw new ScriptError(DiagnosticCodes.BadOperand, $"chem.balance: {result.ErrorMessage}");

        return result.Result.Trim();
    }

    [ScriptFn("check", "Проверка: сбалансировано ли уравнение как записано",
        Example = "chem.check(\"2H2 + O2 = 2H2O\").balanced")]
    public static ScriptRecord Check([ScriptParam("уравнение реакции")] string equation)
    {
        if (!MolecularFormula.TrySplitEquation(equation, out string left, out string right))
        {
            return Record(
                ("balanced", ScriptValue.Bool(false)),
                ("reason", ScriptValue.Str("в уравнении нет стрелки или знака равенства")),
                ("balanced_equation", ScriptValue.Str(string.Empty)));
        }

        List<MolecularFormula> reactants, products;

        try
        {
            reactants = MolecularFormula.ParseSide(left);
            products = MolecularFormula.ParseSide(right);
        }
        catch (FormatException ex)
        {
            return Record(
                ("balanced", ScriptValue.Bool(false)),
                ("reason", ScriptValue.Str(ex.Message)),
                ("balanced_equation", ScriptValue.Str(string.Empty)));
        }

        string mismatch = FindMismatch(reactants, products);
        var suggestion = ChemContext.Engine.Execute($"balance {equation}");

        return Record(
            ("balanced", ScriptValue.Bool(mismatch == null)),
            ("reason", ScriptValue.Str(mismatch ?? "атомы и заряд сохраняются")),
            ("balanced_equation", ScriptValue.Str(suggestion.Success ? suggestion.Result.Trim() : string.Empty)));
    }

    [ScriptFn("oxidation", "Степени окисления элементов в соединении",
        Example = "chem.oxidation(\"KMnO4\")")]
    public static ScriptTable Oxidation([ScriptParam("химическая формула")] string formula)
    {
        var result = ChemContext.Engine.Execute($"oxidation states of {formula}");

        if (!result.Success || !result.Data.TryGetValue("oxidation_states", out object states)
            || states is not Dictionary<string, double> map)
        {
            throw new ScriptError(DiagnosticCodes.BadOperand,
                $"chem.oxidation: {(result.Success ? "степени окисления не определены" : result.ErrorMessage)}");
        }

        var symbols = map.Keys.ToArray();
        var values = new Vector(symbols.Length);

        for (int i = 0; i < symbols.Length; i++)
            values[i] = map[symbols[i]];

        return ScriptTable.Create(
        [
            ScriptColumn.Own("element", symbols.Select(ScriptValue.Str).ToArray()),
            ScriptColumn.FromVector("state", values),
        ]);
    }

    [ScriptFn("solve", "Выполнить произвольную команду химического движка",
        Example = "chem.solve(\"pH of 0.01M HCl\").result")]
    public static ScriptRecord Solve([ScriptParam("команда движка")] string command)
    {
        var result = ChemContext.Engine.Execute(command);

        return Record(
            ("ok", ScriptValue.Bool(result.Success)),
            ("result", ScriptValue.Str(result.Success ? result.Result?.Trim() ?? string.Empty : string.Empty)),
            ("error", ScriptValue.Str(result.ErrorMessage ?? string.Empty)),
            ("steps", ScriptValue.List(ScriptList.From(result.Steps.Select(ScriptValue.Str)))));
    }

    #endregion

    #region Метрология

    [ScriptFn("calibrate", "Градуировка: наклон, R², Sy/x, пределы обнаружения и определения",
        Example = "chem.calibrate(conc, signal).lod")]
    public static ScriptRecord Calibrate(
        [ScriptParam("концентрации стандартов")] Vector concentrations,
        [ScriptParam("отклики прибора")] Vector signals,
        [ScriptParam("взвешивание: none, 1/x, 1/x2, 1/y, 1/y2")] string weighting = "none",
        [ScriptParam("доверительная вероятность")] double confidence = 0.95)
    {
        var calibration = BuildCalibration(concentrations, signals, weighting);
        var fit = calibration.Fit;
        var (slopeLow, slopeHigh) = fit.SlopeInterval(confidence);

        return Record(
            ("slope", ScriptValue.Num(fit.Slope)),
            ("intercept", ScriptValue.Num(fit.Intercept)),
            ("r2", ScriptValue.Num(fit.R2)),
            ("sy_x", ScriptValue.Num(fit.ResidualStd)),
            ("slope_se", ScriptValue.Num(fit.SlopeStdError)),
            ("slope_low", ScriptValue.Num(slopeLow)),
            ("slope_high", ScriptValue.Num(slopeHigh)),
            ("intercept_significant", ScriptValue.Bool(fit.InterceptIsSignificant(confidence))),
            ("lod", ScriptValue.Num(calibration.DetectionLimit)),
            ("loq", ScriptValue.Num(calibration.QuantitationLimit)),
            ("report", ScriptValue.Str(calibration.Report(confidence))));
    }

    [ScriptFn("concentration", "Концентрация пробы по градуировке с доверительным интервалом",
        Example = "chem.concentration(conc, signal, response: 0.42)")]
    public static ScriptRecord Concentration(
        [ScriptParam("концентрации стандартов")] Vector concentrations,
        [ScriptParam("отклики стандартов")] Vector signals,
        [ScriptParam("отклик пробы")] double response,
        [ScriptParam("число повторных измерений пробы")] int replicates = 1,
        [ScriptParam("взвешивание: none, 1/x, 1/x2, 1/y, 1/y2")] string weighting = "none",
        [ScriptParam("доверительная вероятность")] double confidence = 0.95)
    {
        var calibration = BuildCalibration(concentrations, signals, weighting);
        var estimate = calibration.Concentration(response, replicates, confidence);

        return Record(
            ("value", ScriptValue.Num(estimate.Value)),
            ("uncertainty", ScriptValue.Num(estimate.StandardUncertainty)),
            ("low", ScriptValue.Num(estimate.Lower)),
            ("high", ScriptValue.Num(estimate.Upper)),
            ("rsd_percent", ScriptValue.Num(estimate.RelativeUncertaintyPercent)),
            ("in_range", ScriptValue.Bool(estimate.WithinRange)));
    }

    [ScriptFn("addition", "Метод добавок: концентрация в исходной пробе",
        Example = "chem.addition(added, signal).value")]
    public static ScriptRecord StandardAddition(
        [ScriptParam("концентрации добавок")] Vector added,
        [ScriptParam("отклики после добавок")] Vector signals,
        [ScriptParam("доверительная вероятность")] double confidence = 0.95)
    {
        var estimate = Quantification.StandardAddition(added.ToArray(), signals.ToArray(), confidence);

        return Record(
            ("value", ScriptValue.Num(estimate.Value)),
            ("uncertainty", ScriptValue.Num(estimate.StandardUncertainty)),
            ("low", ScriptValue.Num(estimate.Lower)),
            ("high", ScriptValue.Num(estimate.Upper)));
    }

    [ScriptFn("outlier", "Проверка серии на грубый промах (критерий Граббса)",
        Example = "chem.outlier(<10.1, 10.2, 10.0, 12.4>).is_outlier")]
    public static ScriptRecord Outlier(
        [ScriptParam("серия результатов")] Vector values,
        [ScriptParam("уровень значимости")] double alpha = 0.05)
    {
        var result = OutlierTests.Grubbs(values.ToArray(), alpha);

        return Record(
            ("is_outlier", ScriptValue.Bool(result.IsOutlier)),
            ("value", ScriptValue.Num(result.Value)),
            ("index", ScriptValue.Num(result.Index)),
            ("statistic", ScriptValue.Num(result.Statistic)),
            ("critical", ScriptValue.Num(result.CriticalValue)));
    }

    [ScriptFn("precision", "Прецизионность по сериям: повторяемость и промежуточная",
        Example = "chem.precision([day1, day2, day3]).rsd_r")]
    public static ScriptRecord Precision([ScriptParam("список серий измерений")] ScriptList series)
    {
        var groups = new List<double[]>(series.Count);

        for (int i = 0; i < series.Count; i++)
            groups.Add(series[i].AsVector($"chem.precision: серия {i + 1}").ToArray());

        var result = MethodValidation.Precision(groups.ToArray());

        return Record(
            ("mean", ScriptValue.Num(result.GrandMean)),
            ("sr", ScriptValue.Num(result.RepeatabilityStd)),
            ("sl", ScriptValue.Num(result.BetweenGroupStd)),
            ("sr_intermediate", ScriptValue.Num(result.IntermediateStd)),
            ("rsd_r", ScriptValue.Num(result.RepeatabilityRsdPercent)),
            ("rsd_intermediate", ScriptValue.Num(result.IntermediateRsdPercent)),
            ("report", ScriptValue.Str(MethodValidation.Report(result))));
    }

    [ScriptFn("recovery", "Правильность по методу добавок: степень извлечения",
        Example = "chem.recovery(found, added).mean")]
    public static ScriptRecord Recovery(
        [ScriptParam("найденные количества")] Vector found,
        [ScriptParam("введённые количества")] Vector added,
        [ScriptParam("доверительная вероятность")] double confidence = 0.95)
    {
        var result = MethodValidation.Recovery(found.ToArray(), added.ToArray(), confidence);

        return Record(
            ("mean", ScriptValue.Num(result.MeanRecoveryPercent)),
            ("std", ScriptValue.Num(result.StdPercent)),
            ("low", ScriptValue.Num(result.Lower)),
            ("high", ScriptValue.Num(result.Upper)),
            ("bias_significant", ScriptValue.Bool(result.BiasSignificant)));
    }

    [ScriptFn("control_chart", "Контрольная карта Шухарта: границы и нарушения",
        Example = "chem.control_chart(qc).in_control")]
    public static ScriptRecord ControlChartFn(
        [ScriptParam("результаты контрольных измерений")] Vector values,
        [ScriptParam("аттестованное значение; 0 - среднее по серии")] double center = 0)
    {
        var chart = center == 0
            ? new ControlChart(values.ToArray())
            : new ControlChart(values.ToArray(), center);

        var violations = chart.Violations();

        return Record(
            ("center", ScriptValue.Num(chart.CenterLine)),
            ("sigma", ScriptValue.Num(chart.Sigma)),
            ("ucl", ScriptValue.Num(chart.UpperControlLimit)),
            ("lcl", ScriptValue.Num(chart.LowerControlLimit)),
            ("in_control", ScriptValue.Bool(violations.Count == 0)),
            ("violations", ScriptValue.Num(violations.Count)),
            ("report", ScriptValue.Str(chart.Report())));
    }

    [ScriptFn("uncertainty", "Суммарная и расширенная неопределённость по составляющим",
        Example = "chem.uncertainty(<0.01, 0.005, 0.02>)")]
    public static ScriptRecord Uncertainty(
        [ScriptParam("стандартные неопределённости составляющих")] Vector components,
        [ScriptParam("значение измеряемой величины")] double value = 0,
        [ScriptParam("доверительная вероятность")] double confidence = 0.95)
    {
        var budget = new UncertaintyBudget("результат", value);

        for (int i = 0; i < components.Count; i++)
        {
            budget.Add(new UncertaintyComponent
            {
                Name = $"составляющая {i + 1}",
                Value = components[i],
                Distribution = DistributionKind.Normal
            });
        }

        return Record(
            ("combined", ScriptValue.Num(budget.CombinedStandardUncertainty)),
            ("k", ScriptValue.Num(budget.CoverageFactor(confidence))),
            ("expanded", ScriptValue.Num(budget.ExpandedUncertainty(confidence))),
            ("relative_percent", ScriptValue.Num(budget.RelativeExpandedPercent(confidence))),
            ("report", ScriptValue.Str(budget.Report(confidence))));
    }

    #endregion

    #region Сигналы приборов

    [ScriptFn("smooth", "Сглаживание Савицкого-Голея", Example = "chem.smooth(signal, window: 11)")]
    public static Vector Smooth(
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("окно, нечётное число точек")] int window = 9,
        [ScriptParam("порядок полинома")] int order = 2)
        => new(SavitzkyGolay.Apply(signal.ToArray(), window, order));

    [ScriptFn("baseline", "Базовая линия сигнала (асимметричный МНК)",
        Example = "chem.baseline(signal, smoothness: 1e5)")]
    public static Vector Baseline(
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("штраф за кривизну линии")] double smoothness = 1e6,
        [ScriptParam("вес точек выше линии")] double asymmetry = 0.01)
        => new(BaselineCorrection.AsymmetricLeastSquares(signal.ToArray(), smoothness, asymmetry));

    [ScriptFn("noise", "Оценка шума базовой линии", Example = "chem.noise(signal)")]
    public static double Noise([ScriptParam("сигнал")] Vector signal)
        => BaselineCorrection.EstimateNoise(signal.ToArray());

    [ScriptFn("peaks", "Поиск и интегрирование пиков хроматограммы",
        Example = "show chem.peaks(time, signal)")]
    public static ScriptTable Peaks(
        [ScriptParam("ось времени")] Vector time,
        [ScriptParam("сигнал детектора")] Vector signal,
        [ScriptParam("окно сглаживания")] int window = 9,
        [ScriptParam("минимальное отношение сигнал/шум")] double snr = 3.0,
        [ScriptParam("вычитать базовую линию")] bool baseline = false)
    {
        var options = new PeakDetectionOptions
        {
            SmoothingWindow = window,
            SignalToNoise = snr,
            Baseline = baseline ? BaselineMode.Polynomial : BaselineMode.None
        };

        var peaks = PeakDetector.Detect(time.ToArray(), signal.ToArray(), options);

        var retention = new Vector(peaks.Count);
        var area = new Vector(peaks.Count);
        var height = new Vector(peaks.Count);
        var width = new Vector(peaks.Count);
        var percent = new Vector(peaks.Count);
        var plates = new Vector(peaks.Count);
        var asymmetry = new Vector(peaks.Count);

        for (int i = 0; i < peaks.Count; i++)
        {
            retention[i] = peaks[i].RetentionTime;
            area[i] = peaks[i].Area;
            height[i] = peaks[i].Height;
            width[i] = peaks[i].WidthAtHalfHeight;
            percent[i] = peaks[i].AreaPercent;
            plates[i] = peaks[i].TheoreticalPlates;
            asymmetry[i] = peaks[i].AsymmetryFactor;
        }

        return ScriptTable.Create(
        [
            ScriptColumn.FromVector("rt", retention),
            ScriptColumn.FromVector("area", area),
            ScriptColumn.FromVector("height", height),
            ScriptColumn.FromVector("width_half", width),
            ScriptColumn.FromVector("area_percent", percent),
            ScriptColumn.FromVector("plates", plates),
            ScriptColumn.FromVector("asymmetry", asymmetry),
        ]);
    }

    [ScriptFn("resolution", "Разрешение соседних пиков по хроматограмме",
        Example = "chem.resolution(time, signal)")]
    public static Vector Resolution(
        [ScriptParam("ось времени")] Vector time,
        [ScriptParam("сигнал детектора")] Vector signal,
        [ScriptParam("окно сглаживания")] int window = 9)
    {
        var peaks = PeakDetector.Detect(time.ToArray(), signal.ToArray(),
            new PeakDetectionOptions { SmoothingWindow = window });

        var result = new Vector(Math.Max(0, peaks.Count - 1));

        for (int i = 1; i < peaks.Count; i++)
            result[i - 1] = Peak.Resolution(peaks[i - 1], peaks[i]);

        return result;
    }

    #endregion

    #region Рецептура и себестоимость

    [ScriptFn("batch", "Потребность в сырье на партию по уравнению реакции",
        Example = "show chem.batch(\"CaCO3 = CaO + CO2\", product: \"CaO\", mass: 100, yield: 0.9)")]
    public static ScriptTable Batch(
        [ScriptParam("уравнение реакции")] string equation,
        [ScriptParam("формула целевого продукта")] string product,
        [ScriptParam("плановый выпуск, кг")] double mass,
        [ScriptParam("выход по реакции, доля от 0 до 1")] double yield = 1.0)
    {
        ReactionDemand demand;

        try
        {
            demand = MaterialBalance.FromReaction(equation, product, mass, ChemContext.Database, yield);
        }
        catch (ArgumentException ex)
        {
            throw new ScriptError(DiagnosticCodes.BadOperand, $"chem.batch: {ex.Message}");
        }

        var names = demand.Reagents.Select(r => ScriptValue.Str(r.Formula)).ToArray();
        var molar = new Vector(demand.Reagents.Count);
        var kilomoles = new Vector(demand.Reagents.Count);
        var masses = new Vector(demand.Reagents.Count);

        for (int i = 0; i < demand.Reagents.Count; i++)
        {
            molar[i] = demand.Reagents[i].MolarMass;
            kilomoles[i] = demand.Reagents[i].Kilomoles;
            masses[i] = demand.Reagents[i].MassWithExcess;
        }

        return ScriptTable.Create(
        [
            ScriptColumn.Own("reagent", names),
            ScriptColumn.FromVector("molar_mass", molar),
            ScriptColumn.FromVector("kmol", kilomoles),
            ScriptColumn.FromVector("mass_kg", masses),
        ]);
    }

    [ScriptFn("balance_report", "Материальный баланс партии текстом",
        Example = "chem.balance_report(\"CaCO3 = CaO + CO2\", product: \"CaO\", mass: 100, yield: 0.9)")]
    public static string BalanceReport(
        [ScriptParam("уравнение реакции")] string equation,
        [ScriptParam("формула целевого продукта")] string product,
        [ScriptParam("плановый выпуск, кг")] double mass,
        [ScriptParam("выход по реакции")] double yield = 1.0)
    {
        try
        {
            return MaterialBalance.FromReaction(equation, product, mass, ChemContext.Database, yield).Report();
        }
        catch (ArgumentException ex)
        {
            throw new ScriptError(DiagnosticCodes.BadOperand, $"chem.balance_report: {ex.Message}");
        }
    }

    [ScriptFn("cost", "Себестоимость партии по рецептуре",
        Example = "chem.cost([\"сырьё A\", \"сырьё B\"], <120, 80>, <35, 12>, batch: 100)")]
    public static ScriptRecord Cost(
        [ScriptParam("названия компонентов")] ScriptList names,
        [ScriptParam("потребность на партию, кг")] Vector quantities,
        [ScriptParam("цены, ден.ед./кг")] Vector prices,
        [ScriptParam("выпуск за партию, кг")] double batch,
        [ScriptParam("чистота сырья, доля основного вещества")] double purity = 1.0,
        [ScriptParam("трудозатраты, ч")] double labor_hours = 0,
        [ScriptParam("ставка, ден.ед./ч")] double labor_rate = 0,
        [ScriptParam("энергия на партию, ден.ед.")] double energy = 0,
        [ScriptParam("накладные, % от прямых затрат")] double overhead_percent = 0,
        [ScriptParam("цена продажи, ден.ед./кг")] double price = 0)
    {
        if (names.Count != quantities.Count || names.Count != prices.Count)
            throw new ScriptError(DiagnosticCodes.SizeMismatch, "chem.cost: списки названий, количеств и цен разной длины");

        var recipe = new Recipe("партия", batch)
        {
            LaborHours = labor_hours,
            LaborRatePerHour = labor_rate,
            EnergyCost = energy,
            OverheadPercent = overhead_percent
        };

        for (int i = 0; i < names.Count; i++)
        {
            recipe.Add(
                names[i].AsString($"chem.cost: название {i + 1}"),
                quantities[i],
                prices[i],
                purity > 0 ? purity : 1.0);
        }

        var cost = recipe.Cost();
        var drivers = recipe.Sensitivity();

        return Record(
            ("total", ScriptValue.Num(cost.TotalCost)),
            ("per_kg", ScriptValue.Num(cost.CostPerKg)),
            ("material", ScriptValue.Num(cost.MaterialCost)),
            ("material_share", ScriptValue.Num(cost.MaterialSharePercent)),
            ("overhead", ScriptValue.Num(cost.OverheadCost)),
            ("margin_percent", ScriptValue.Num(price > 0 ? cost.MarginPercent(price) : double.NaN)),
            ("break_even_price", ScriptValue.Num(cost.BreakEvenPrice)),
            ("top_driver", ScriptValue.Str(drivers.Count > 0 ? drivers[0].Name : string.Empty)),
            ("report", ScriptValue.Str(cost.Report(price))));
    }

    #endregion

    #region Кинетика

    [ScriptFn("fit_order", "Порядок реакции и константа скорости по кривой расходования",
        Example = "chem.fit_order(t, c).order")]
    public static ScriptRecord FitOrder(
        [ScriptParam("моменты времени")] Vector times,
        [ScriptParam("концентрации реагента")] Vector concentrations)
    {
        if (times.Count != concentrations.Count)
            throw new ScriptError(DiagnosticCodes.SizeMismatch, "chem.fit_order: разное число точек времени и концентраций");

        try
        {
            ReactionOrderResult result = KineticFit.DetermineOrder(times.ToArray(), concentrations.ToArray());

            return Record(
                ("order", ScriptValue.Num(result.Order)),
                ("k", ScriptValue.Num(result.RateConstant)),
                ("rss", ScriptValue.Num(result.ResidualSumOfSquares)),
                ("r2", ScriptValue.Num(result.R2)));
        }
        catch (ArgumentException ex)
        {
            throw new ScriptError(DiagnosticCodes.BadOperand, $"chem.fit_order: {ex.Message}");
        }
    }

    [ScriptFn("arrhenius", "Энергия активации по зависимости константы от температуры",
        Example = "chem.arrhenius(<300, 310, 320>, <0.01, 0.02, 0.05>).ea")]
    public static ScriptRecord Arrhenius(
        [ScriptParam("температуры, K")] Vector temperatures,
        [ScriptParam("константы скорости")] Vector rateConstants,
        [ScriptParam("доверительная вероятность")] double confidence = 0.95)
    {
        try
        {
            ArrheniusResult result = ArrheniusAnalysis.Fit(temperatures.ToArray(), rateConstants.ToArray(), confidence);

            return Record(
                ("ea", ScriptValue.Num(result.ActivationEnergy)),
                ("ea_error", ScriptValue.Num(result.ActivationEnergyError)),
                ("ea_low", ScriptValue.Num(result.ActivationEnergyInterval.Lower)),
                ("ea_high", ScriptValue.Num(result.ActivationEnergyInterval.Upper)),
                ("a", ScriptValue.Num(result.PreExponentialFactor)),
                ("r2", ScriptValue.Num(result.R2)),
                ("report", ScriptValue.Str(result.Report())));
        }
        catch (ArgumentException ex)
        {
            throw new ScriptError(DiagnosticCodes.BadOperand, $"chem.arrhenius: {ex.Message}");
        }
    }

    [ScriptFn("runaway", "Оценка адиабатического теплового разгона",
        Example = "chem.runaway(heat: 800000, cp: 1800, ea: 120, a: 1e13, t0: 350).tmr_hours")]
    public static ScriptRecord Runaway(
        [ScriptParam("удельная теплота реакции, Дж/кг")] double heat,
        [ScriptParam("теплоёмкость, Дж/(кг·K)")] double cp,
        [ScriptParam("энергия активации, кДж/моль")] double ea,
        [ScriptParam("предэкспоненциальный множитель, 1/с")] double a,
        [ScriptParam("начальная температура, K")] double t0,
        [ScriptParam("окно наблюдения, с")] double duration = 86400)
    {
        var parameters = new RunawayParameters
        {
            ReactionHeat = heat,
            HeatCapacity = cp,
            ActivationEnergy = ea,
            PreExponentialFactor = a,
            InitialTemperature = t0
        };

        RunawayResult result = ThermalRunaway.Simulate(parameters, duration);

        return Record(
            ("dt_adiabatic", ScriptValue.Num(result.AdiabaticTemperatureRise)),
            ("t_max", ScriptValue.Num(result.MaximumTemperature)),
            ("tmr_hours", ScriptValue.Num(ThermalRunaway.TimeToMaximumRateEstimate(parameters) / 3600)),
            ("tmr_simulated_hours", ScriptValue.Num(result.TimeToMaximumRateHours)),
            ("runaway", ScriptValue.Bool(result.RunawayWithinWindow)),
            ("t_d24", ScriptValue.Num(ThermalRunaway.TemperatureForTimeToMaximumRate(parameters))),
            ("report", ScriptValue.Str(result.Report())));
    }

    #endregion

    #region Классификация опасности и паспорт безопасности

    [ScriptFn("classify", "Классификация смеси по СГС: пиктограммы, сигнальное слово, H-фразы",
        Example = "chem.classify([\"ацетон\"], <60>, [\"Flam. Liq. 2; Eye Irrit. 2\"]).signal")]
    public static ScriptRecord Classify(
        [ScriptParam("названия компонентов")] ScriptList names,
        [ScriptParam("содержание компонентов, %")] Vector contents,
        [ScriptParam("классификации компонентов через точку с запятой")] ScriptList hazards)
    {
        Mixture mixture = BuildMixture("смесь", names, contents, hazards);
        MixtureClassification classification = mixture.Classify();

        return Record(
            ("hazardous", ScriptValue.Bool(classification.IsHazardous)),
            ("signal", ScriptValue.Str(HazardCatalog.Text(classification.Signal))),
            ("pictograms", ScriptValue.List(ScriptList.From(
                classification.Pictograms.Select(p => ScriptValue.Str(HazardCatalog.Code(p)))))),
            ("h_codes", ScriptValue.List(ScriptList.From(
                classification.HazardStatements.Select(ScriptValue.Str)))),
            ("p_codes", ScriptValue.List(ScriptList.From(
                classification.Precautions.Select(ScriptValue.Str)))),
            ("report", ScriptValue.Str(classification.Report())));
    }

    [ScriptFn("sds", "Паспорт безопасности из 16 разделов по составу смеси",
        Example = "chem.sds(\"Растворитель 646\", names, contents, hazards)")]
    public static string SafetyDataSheetText(
        [ScriptParam("наименование продукции")] string product,
        [ScriptParam("названия компонентов")] ScriptList names,
        [ScriptParam("содержание компонентов, %")] Vector contents,
        [ScriptParam("классификации компонентов через точку с запятой")] ScriptList hazards)
        => BuildMixture(product, names, contents, hazards).CreateSafetyDataSheet().Render();

    #endregion

    #region Вспомогательное

    private static Mixture BuildMixture(string product, ScriptList names, Vector contents, ScriptList hazards)
    {
        if (names.Count != contents.Count || names.Count != hazards.Count)
            throw new ScriptError(DiagnosticCodes.SizeMismatch,
                "классификация: списки названий, содержаний и классификаций разной длины");

        var mixture = new Mixture { Name = product };

        for (int i = 0; i < names.Count; i++)
        {
            string spec = hazards[i].AsString($"классификация компонента {i + 1}");
            var classifications = new List<HazardCategory>();

            foreach (string part in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                if (HazardCatalog.TryParse(part, out HazardCategory category))
                    classifications.Add(category);
                else if (!string.IsNullOrWhiteSpace(part))
                    throw new ScriptError(DiagnosticCodes.BadOperand, $"неизвестная классификация '{part.Trim()}'");
            }

            mixture.Add(new MixtureComponent
            {
                Name = names[i].AsString($"название компонента {i + 1}"),
                ContentPercent = contents[i],
                Classifications = classifications
            });
        }

        return mixture;
    }

    private static MolecularFormula Parse(string formula)
    {
        if (!MolecularFormula.TryParse(formula, out var parsed, out string error))
            throw new ScriptError(DiagnosticCodes.BadOperand, $"неразобранная формула '{formula}': {error}");

        return parsed;
    }

    private static AnalyticalCalibration BuildCalibration(Vector concentrations, Vector signals, string weighting)
    {
        if (concentrations.Count != signals.Count)
            throw new ScriptError(DiagnosticCodes.SizeMismatch, "градуировка: разное число концентраций и откликов");

        var scheme = weighting?.Trim().ToLowerInvariant() switch
        {
            null or "" or "none" or "нет" => WeightingScheme.None,
            "1/x" or "x" => WeightingScheme.InverseX,
            "1/x2" or "1/x^2" or "x2" => WeightingScheme.InverseX2,
            "1/y" or "y" => WeightingScheme.InverseY,
            "1/y2" or "1/y^2" or "y2" => WeightingScheme.InverseY2,
            _ => throw new ScriptError(DiagnosticCodes.BadOperand, $"неизвестное взвешивание '{weighting}'")
        };

        try
        {
            return new AnalyticalCalibration(concentrations.ToArray(), signals.ToArray(), scheme);
        }
        catch (ArgumentException ex)
        {
            throw new ScriptError(DiagnosticCodes.BadOperand, $"градуировка: {ex.Message}");
        }
    }

    // Проверка баланса «как записано»: коэффициенты берутся из самих формул
    private static string FindMismatch(List<MolecularFormula> reactants, List<MolecularFormula> products)
    {
        var elements = reactants.Concat(products)
            .SelectMany(f => f.Elements.Keys)
            .Distinct()
            .OrderBy(e => e, StringComparer.Ordinal);

        foreach (string element in elements)
        {
            long left = reactants.Sum(f => (long)f.Coefficient * f.GetCount(element));
            long right = products.Sum(f => (long)f.Coefficient * f.GetCount(element));

            if (left != right)
                return $"{element}: слева {left}, справа {right}";
        }

        long chargeLeft = reactants.Sum(f => (long)f.Coefficient * f.Charge);
        long chargeRight = products.Sum(f => (long)f.Coefficient * f.Charge);

        return chargeLeft != chargeRight
            ? $"заряд: слева {chargeLeft:+#;-#;0}, справа {chargeRight:+#;-#;0}"
            : null;
    }

    private static ScriptRecord Record(params (string Name, ScriptValue Value)[] fields)
        => ScriptRecord.From(fields.Select(f => new KeyValuePair<string, ScriptValue>(f.Name, f.Value)));

    #endregion
}
