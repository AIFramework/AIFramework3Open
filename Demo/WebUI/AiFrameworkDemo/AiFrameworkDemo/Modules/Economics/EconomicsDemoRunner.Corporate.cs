using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Corporate;
using AI.Statistics;
using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Economics;

/// <summary>Демонстраторы категории «Оценка бизнеса и корпфинансы».</summary>
public static partial class EconomicsDemoRunner
{
    #region Стоимость капитала

    private static string DoWacc(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double debtShare = p.GetValueOrDefault("debt_share", 0.3);
        const double capital = 1_000_000_000;

        var input = new CostOfCapitalInput
        {
            Name = "Компания",
            RiskFreeRate = p.GetValueOrDefault("rf", 0.08),
            EquityRiskPremium = p.GetValueOrDefault("erp", 0.06),
            UnleveredBeta = p.GetValueOrDefault("beta", 1.0),
            CountryRiskPremium = p.GetValueOrDefault("crp", 0.02),
            SizePremium = p.GetValueOrDefault("size", 0.015),
            EquityValue = capital * Math.Max(1 - debtShare, 0.05),
            DebtValue = capital * debtShare,
            CostOfDebt = p.GetValueOrDefault("kd", 0.13),
            TaxRate = p.GetValueOrDefault("tax", 0.2),
        };

        CostOfCapitalResult result = CostOfCapital.Compute(input);

        IReadOnlyList<(double DebtShare, double Wacc)> curve = CostOfCapital.WaccCurve(
            input with { EquityValue = capital, DebtValue = 0 },
            distressSlope: p.GetValueOrDefault("distress", 0.2));

        cv.AddPlot(Vec(curve.Select(c => c.DebtShare)), Vec(curve.Select(c => c.Wacc)),
            "Стоимость капитала", C(0), 3);
        Segment(cv, debtShare, curve.Min(c => c.Wacc), debtShare, curve.Max(c => c.Wacc),
            C(3), $"Текущая доля долга: {Pct(debtShare, 0)}", 2);
        cv.ChartName = $"WACC = {Pct(result.Wacc, 2)} при доле долга {Pct(debtShare, 0)}";
        cv.LabelX = "Доля долга в капитале";
        cv.LabelY = "Средневзвешенная ставка";

        var buildUp = rep.Table("Разложение стоимости капитала",
            ["Слагаемое", "Значение"], [false, true]);
        foreach ((string component, double value) in result.EquityBuildUp)
            buildUp.Row(component, Pct(value, 2));

        var curveTable = rep.Table("Кривая по доле долга",
            ["Доля долга", "WACC"], [true, true]);
        foreach ((double share, double wacc) in curve)
            curveTable.Row(Pct(share, 0), Pct(wacc, 3));

        (double optimal, double minimum) = curve.OrderBy(c => c.Wacc).First();

        string log =
            $"Стоимость собственного капитала {Pct(result.CostOfEquity, 2)} при бете " +
            $"{Num(result.LeveredBeta, 2)}.\n" +
            $"Стоимость долга после налога {Pct(result.AfterTaxCostOfDebt, 2)}.\n" +
            $"Средневзвешенная ставка {Pct(result.Wacc, 2)}.\n" +
            $"Минимум кривой {Pct(minimum, 3)} при доле долга {Pct(optimal, 0)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Дисконтированные потоки

    private static string DoDcf(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        DcfInput input = BuildDcf(p);
        DcfResult result = DiscountedCashFlow.Value(input);

        var years = Axis(result.CashFlows.Count, 1);
        cv.AddPlot(years, result.CashFlows, "Свободный поток", C(0), 3);
        cv.AddPlot(years, result.DiscountedCashFlows, "Дисконтированный поток", C(1), 2);
        cv.ChartName = $"Стоимость бизнеса {Money(result.EnterpriseValue)}, " +
                       $"продлённая стоимость {Pct(result.TerminalShare, 0)}";
        cv.LabelX = "Год прогноза";
        cv.LabelY = "Денежный поток, руб.";

        var flows = rep.Table("Прогноз потоков",
            ["Год", "Выручка", "Прибыль до налога", "Свободный поток", "Дисконтированный"],
            [true, true, true, true, true]);

        for (int t = 0; t < input.Forecast.Count; t++)
        {
            ForecastYear year = input.Forecast[t];
            flows.Row($"{t + 1}", Money(year.Revenue), Money(year.Ebit),
                Money(year.FreeCashFlow), Money(result.DiscountedCashFlows[t]));
        }

        var tornado = rep.Table("Чувствительность оценки",
            ["Фактор", "Нижняя оценка", "Верхняя оценка", "Размах"], [false, true, true, true]);

        foreach (SensitivityBar bar in result.Tornado)
            tornado.Row(bar.Factor, Money(bar.LowValue), Money(bar.HighValue), Money(bar.Swing));

        DcfResult byMultiple = DiscountedCashFlow.Value(input with
        {
            TerminalMethod = TerminalValueMethod.ExitMultiple,
            ExitMultiple = result.ImpliedExitMultiple,
        });

        string log =
            $"Стоимость бизнеса {Money(result.EnterpriseValue)}, собственного капитала " +
            $"{Money(result.EquityValue)}.\n" +
            $"Продлённая стоимость {Money(result.TerminalValue)} — {Pct(result.TerminalShare, 0)} оценки.\n" +
            $"Неявный мультипликатор выхода {Num(result.ImpliedExitMultiple, 1)}, " +
            $"неявный темп роста {Pct(result.ImpliedGrowth, 2)}.\n" +
            $"Проверка через мультипликатор выхода: {Money(byMultiple.EnterpriseValue)}.";

        return Explain(rep, result, log);
    }

    private static string DoDcfMonteCarlo(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        DcfInput input = BuildDcf(p);

        DcfSimulationResult result = DiscountedCashFlow.Simulate(
            input,
            p.GetValueOrDefault("rev_vol", 0.12),
            p.GetValueOrDefault("margin_vol", 0.02),
            p.GetValueOrDefault("rate_vol", 0.015),
            (int)p.GetValueOrDefault("sims", 4000),
            (int)p.GetValueOrDefault("seed", 42));

        // Эмпирическая функция распределения оценки
        var quantiles = new Vector(41);
        var values = new Vector(41);

        for (int i = 0; i <= 40; i++)
        {
            double q = i / 40.0;
            quantiles[i] = q;
            values[i] = result.Distribution.Count > 0
                ? result.Distribution[Math.Clamp((int)(q * (result.Distribution.Count - 1)), 0,
                    result.Distribution.Count - 1)]
                : 0;
        }

        cv.AddPlot(values, quantiles, "Накопленная вероятность", C(0), 3);
        Segment(cv, result.BaseCase, 0, result.BaseCase, 1, C(3), "Базовый расчёт", 2);
        Segment(cv, result.MedianEquityValue, 0, result.MedianEquityValue, 1, C(1), "Медиана", 2);
        cv.ChartName = $"Распределение стоимости капитала по {result.Simulations} симуляциям";
        cv.LabelX = "Стоимость капитала, руб.";
        cv.LabelY = "Накопленная вероятность";

        var table = rep.Table("Процентили оценки", ["Процентиль", "Стоимость капитала"], [true, true]);
        foreach (double q in new[] { 0.05, 0.1, 0.25, 0.5, 0.75, 0.9, 0.95 })
        {
            int index = Math.Clamp((int)(q * (result.Distribution.Count - 1)), 0,
                Math.Max(0, result.Distribution.Count - 1));

            table.Row(Pct(q, 0), result.Distribution.Count > 0 ? Money(result.Distribution[index]) : "—");
        }

        string log =
            $"Медианная оценка {Money(result.MedianEquityValue)} против базовой {Money(result.BaseCase)}.\n" +
            $"Интервал 10-90: {Money(result.LowerPercentile)} — {Money(result.UpperPercentile)}.\n" +
            $"Стандартное отклонение {Money(result.StandardDeviation)}.\n" +
            $"Вероятность отрицательной стоимости {Pct(result.ProbabilityOfLoss, 1)}.";

        return Explain(rep, result, log);
    }

    /// <summary>Собирает прогноз для оценки дисконтированных потоков из параметров демо.</summary>
    private static DcfInput BuildDcf(IReadOnlyDictionary<string, double> p)
    {
        double revenue = p.GetValueOrDefault("revenue", 1_000_000_000);
        double growth = p.GetValueOrDefault("growth", 0.1);
        double margin = p.GetValueOrDefault("margin", 0.2);
        double tax = p.GetValueOrDefault("tax", 0.2);
        double capex = p.GetValueOrDefault("capex", 0.06);
        double depreciation = p.GetValueOrDefault("depreciation", 0.05);
        double workingCapital = p.GetValueOrDefault("working_capital", 0.02);
        int years = (int)p.GetValueOrDefault("years", 5);

        var forecast = new List<ForecastYear>(years);
        double current = revenue;

        for (int t = 0; t < years; t++)
        {
            double previous = current;
            if (t > 0) current *= 1 + growth;

            // Оборотный капитал растёт вместе с выручкой: в поток попадает только прирост
            double workingCapitalChange = t == 0 ? 0 : (current - previous) * workingCapital;

            forecast.Add(new ForecastYear(
                current, margin, tax,
                current * depreciation,
                current * capex,
                workingCapitalChange));
        }

        return new DcfInput
        {
            Name = "Компания",
            Forecast = forecast,
            DiscountRate = p.GetValueOrDefault("wacc", 0.16),
            TerminalGrowth = Math.Min(p.GetValueOrDefault("terminal_growth", 0.03),
                p.GetValueOrDefault("wacc", 0.16) - 0.01),
            NetDebt = p.GetValueOrDefault("net_debt", 400_000_000),
            MidYearConvention = p.GetValueOrDefault("mid_year", 1) > 0.5,
        };
    }

    #endregion

    #region Мультипликаторы

    private static string DoComparables(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int peerCount = (int)p.GetValueOrDefault("peers", 20);
        int selected = (int)p.GetValueOrDefault("selected", 8);
        double revenue = p.GetValueOrDefault("revenue", 1_000_000_000);
        double margin = p.GetValueOrDefault("margin", 0.2);
        double growth = p.GetValueOrDefault("growth", 0.15);
        double dispersion = p.GetValueOrDefault("dispersion", 0.35);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 11));

        var peers = new List<Peer>(peerCount);

        for (int i = 0; i < peerCount; i++)
        {
            double peerRevenue = revenue * Math.Exp(RandomEngine.NextGaussian(rng, 0, 0.6));
            double peerGrowth = Math.Max(-0.2, growth + RandomEngine.NextGaussian(rng, 0, 0.08));
            double peerMargin = Math.Clamp(margin + RandomEngine.NextGaussian(rng, 0, 0.05), 0.02, 0.6);
            double ebitda = peerRevenue * peerMargin;

            // Мультипликатор растёт с темпом роста и рентабельностью плюс шум
            double multiple = 5 + (20 * peerGrowth) + (8 * peerMargin)
                + (dispersion * 5 * RandomEngine.NextGaussian(rng));

            multiple = Math.Max(multiple, 1.5);

            peers.Add(new Peer
            {
                Name = $"Аналог {i + 1}",
                Revenue = peerRevenue,
                Ebitda = ebitda,
                NetIncome = ebitda * 0.5,
                EnterpriseValue = ebitda * multiple,
                MarketCapitalization = ebitda * multiple * 0.75,
                Growth = peerGrowth,
            });
        }

        var target = new Peer
        {
            Name = "Компания", Revenue = revenue, Ebitda = revenue * margin,
            NetIncome = revenue * margin * 0.5, Growth = growth,
        };

        ComparablesResult result = Comparables.Value(target, peers, selected);

        cv.AddPlot(Vec(peers.Select(x => x.Growth)), Vec(peers.Select(x => x.EvToEbitda)),
            "Аналоги: мультипликатор против роста", C(1), 0);
        Segment(cv, growth, 0, growth, peers.Max(x => x.EvToEbitda), C(3), "Оцениваемая компания", 2);
        cv.ChartName = $"Оценка {Money(result.MedianValue)} по {result.SelectedPeers.Count} аналогам";
        cv.LabelX = "Темп роста выручки";
        cv.LabelY = "Стоимость бизнеса к прибыли";

        var multiples = rep.Table("Мультипликаторы группы",
            ["Мультипликатор", "Медиана", "Квартили", "Оценка"], [false, true, false, true]);

        foreach (MultipleStatistic multiple in result.Multiples)
        {
            multiples.Row(multiple.Name, Num(multiple.Median, 2),
                $"{Num(multiple.LowerQuartile, 1)}–{Num(multiple.UpperQuartile, 1)}",
                double.IsFinite(multiple.ImpliedValue) ? Money(multiple.ImpliedValue) : "—");
        }

        var selectedTable = rep.Table("Отобранные аналоги",
            ["Аналог", "Расстояние", "Рост", "Рентабельность", "EV/EBITDA"], [false, true, true, true, true]);

        foreach ((string name, double distance) in result.SelectedPeers.Take(12))
        {
            Peer peer = peers.First(x => x.Name == name);
            selectedTable.Row(name, Num(distance, 2), Pct(peer.Growth, 1),
                Pct(peer.Margin, 1), Num(peer.EvToEbitda, 1));
        }

        string log =
            $"Средняя оценка по мультипликаторам {Money(result.MedianValue)}, разброс " +
            $"{Money(result.ValuationSpread)}.\n" +
            (result.Regression is not null
                ? $"Регрессия мультипликатора на рост и рентабельность: R² = " +
                  $"{Num(result.Regression.RSquared, 2)}, оценка {Money(result.RegressionValue)}.\n"
                : "Аналогов не хватило для регрессии мультипликатора.\n") +
            $"Отобрано {result.SelectedPeers.Count} аналогов из {peerCount}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Выкуп за счёт долга

    private static string DoLbo(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double ebitda = p.GetValueOrDefault("ebitda", 1_000_000_000);
        double senior = p.GetValueOrDefault("senior", 3.0) * ebitda;
        double mezzanine = p.GetValueOrDefault("mezz", 1.0) * ebitda;

        var input = new LboInput
        {
            Name = "Сделка",
            EntryEbitda = ebitda,
            EntryMultiple = p.GetValueOrDefault("entry", 7),
            ExitMultiple = p.GetValueOrDefault("exit", 7),
            HoldingPeriod = (int)p.GetValueOrDefault("years", 5),
            EbitdaGrowth = p.GetValueOrDefault("growth", 0.08),
            CashConversionDrag = p.GetValueOrDefault("drag", 0.25),
            MaxLeverage = p.GetValueOrDefault("max_lev", 5.0),
            Tranches =
            [
                new DebtTranche("Старший долг", senior, p.GetValueOrDefault("senior_rate", 0.13), 0.1, 1),
                new DebtTranche("Мезонин", mezzanine, p.GetValueOrDefault("senior_rate", 0.13) + 0.04, 0, 2),
            ],
        };

        LboResult result = LeveragedBuyout.Run(input);

        var years = Axis(result.Schedule.Count, 1);
        cv.AddPlot(years, Vec(result.Schedule.Select(s => s.Leverage)), "Долг к прибыли", C(0), 3);
        cv.AddPlot(years, Vec(result.Schedule.Select(s => s.InterestCoverage)), "Покрытие процентов", C(1), 2);
        Segment(cv, 1, input.MaxLeverage, result.Schedule.Count, input.MaxLeverage, C(3), "Ковенант", 2);
        cv.ChartName = $"Доходность {Pct(result.Irr, 1)} ({Num(result.MoneyMultiple, 2)}x) " +
                       $"за {result.Schedule.Count} лет";
        cv.LabelX = "Год владения";
        cv.LabelY = "Кратность прибыли";

        var schedule = rep.Table("График сделки",
            ["Год", "Прибыль", "Проценты", "Погашение", "Долг", "Нагрузка", "Покрытие"],
            [true, true, true, true, true, true, true]);

        foreach (LboYear year in result.Schedule)
        {
            schedule.Row($"{year.Year}", Money(year.Ebitda), Money(year.Interest),
                Money(year.Amortization + year.CashSweep), Money(year.ClosingDebt),
                Num(year.Leverage, 2), Num(year.InterestCoverage, 1));
        }

        var sources = rep.Table("Источники доходности",
            ["Источник", "Вклад"], [false, true]);
        sources.Row("Рост прибыли", Money(result.GrowthContribution));
        sources.Row("Погашение долга", Money(result.DeleveragingContribution));
        sources.Row("Изменение мультипликатора", Money(result.MultipleContribution));

        double maximum = LeveragedBuyout.MaximumEntryMultiple(input, 0.2);

        string log =
            $"Вход {Money(result.EntryValue)}, собственные средства {Money(result.EquityInvested)}.\n" +
            $"Выход {Money(result.ExitValue)}, поступление {Money(result.EquityProceeds)}.\n" +
            $"Доходность {Pct(result.Irr, 2)}, мультипликатор возврата {Num(result.MoneyMultiple, 2)}x.\n" +
            $"Максимальный мультипликатор входа при цели 20% годовых: {Num(maximum, 2)}x.\n" +
            $"Нарушений ковенантов: {result.Breaches.Count}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Экономическая добавленная стоимость

    private static string DoEva(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int count = (int)p.GetValueOrDefault("units", 4);
        double capital = p.GetValueOrDefault("capital", 5_000_000_000);
        double roic = p.GetValueOrDefault("roic", 0.16);
        double gap = p.GetValueOrDefault("spread_gap", 0.12);
        double wacc = p.GetValueOrDefault("wacc", 0.15);
        double tax = p.GetValueOrDefault("tax", 0.2);
        Random rng = RandomEngine.Create((int)p.GetValueOrDefault("seed", 5));

        var units = new List<BusinessUnit>(count);

        for (int i = 0; i < count; i++)
        {
            double share = 1.0 / count * (0.6 + (0.8 * rng.NextDouble()));
            double unitCapital = capital * share;

            // Рентабельность равномерно распределена вокруг средней с заданным размахом
            double unitRoic = roic - (gap / 2) + (gap * i / Math.Max(1, count - 1));
            double revenue = unitCapital * 1.5;

            units.Add(new BusinessUnit
            {
                Name = $"Направление {i + 1}",
                Revenue = revenue,
                OperatingProfit = unitRoic * unitCapital / Math.Max(1 - tax, 1e-9),
                InvestedCapital = unitCapital,
                TaxRate = tax,
            });
        }

        EconomicProfitResult result = EconomicValueAdded.Compute("Компания", units, wacc);

        var index = Axis(result.Units.Count, 1);
        cv.AddPlot(index, Vec(result.Units.Select(u => u.Roic)), "Рентабельность капитала", C(0), 3);
        Segment(cv, 1, wacc, result.Units.Count, wacc, C(3), "Стоимость капитала", 2);
        cv.ChartName = $"Экономическая прибыль {Money(result.TotalEconomicProfit)}, " +
                       $"спред {Pct(result.Spread, 2)}";
        cv.LabelX = "Подразделение";
        cv.LabelY = "Рентабельность инвестированного капитала";

        var table = rep.Table("Подразделения",
            ["Направление", "Капитал", "Прибыль после налога", "ROIC", "Спред", "EVA"],
            [false, true, true, true, true, true]);

        foreach (UnitEconomicProfit unit in result.Units.OrderByDescending(u => u.EconomicProfit))
        {
            table.Row(unit.Name, Money(unit.InvestedCapital), Money(unit.Nopat),
                Pct(unit.Roic, 1), Pct(unit.Spread, 1), Money(unit.EconomicProfit));
        }

        double marketValueAdded = EconomicValueAdded.MarketValueAdded(
            result.TotalEconomicProfit, 0.03, wacc);

        string log =
            $"Рентабельность капитала {Pct(result.Roic, 2)} против стоимости {Pct(wacc, 2)}.\n" +
            $"Экономическая прибыль {Money(result.TotalEconomicProfit)} при капитале " +
            $"{Money(result.TotalCapital)}.\n" +
            $"В убыточных направлениях заперто {Money(result.CapitalDestroying)}.\n" +
            $"Потенциал перераспределения {Money(result.ReallocationUpside)}.\n" +
            $"Приведённая стоимость будущей экономической прибыли: {Money(marketValueAdded)}.";

        return Explain(rep, result, log);
    }

    #endregion

    #region Реальные опционы

    private static string DoRealOptionsLsm(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var input = new ProjectOptionInput
        {
            Name = "Проект",
            Option = (ProjectOption)(int)p.GetValueOrDefault("option", 0),
            ProjectValue = p.GetValueOrDefault("value", 1_000_000_000),
            InvestmentCost = p.GetValueOrDefault("cost", 1_050_000_000),
            SalvageValue = p.GetValueOrDefault("salvage", 600_000_000),
            ExpansionFactor = p.GetValueOrDefault("expansion", 0.5),
            Horizon = p.GetValueOrDefault("horizon", 3),
            Volatility = p.GetValueOrDefault("vol", 0.4),
            RiskFreeRate = p.GetValueOrDefault("rate", 0.08),
            Steps = (int)p.GetValueOrDefault("steps", 12),
            Paths = (int)p.GetValueOrDefault("paths", 8000),
            Seed = (int)p.GetValueOrDefault("seed", 7),
        };

        ProjectOptionResult result = LongstaffSchwartz.Value(input);

        // Зависимость стоимости опциона от волатильности
        var volatilities = new Vector(9);
        var optionValues = new Vector(9);

        for (int i = 0; i < 9; i++)
        {
            double vol = 0.1 + (i * 0.1);
            volatilities[i] = vol;
            optionValues[i] = LongstaffSchwartz.Value(input with { Volatility = vol, Paths = 3000 }).OptionValue;
        }

        cv.AddPlot(volatilities, optionValues, "Стоимость гибкости", C(0), 3);
        Segment(cv, input.Volatility, 0, input.Volatility, optionValues.Max(), C(3),
            $"Текущая волатильность: {Pct(input.Volatility, 0)}", 2);
        cv.ChartName = $"Гибкость стоит {Money(result.OptionValue)} при статической оценке " +
                       $"{Money(result.StaticNpv)}";
        cv.LabelX = "Волатильность проекта";
        cv.LabelY = "Стоимость опциона, руб.";

        var sensitivity = rep.Table("Стоимость опциона по волатильности",
            ["Волатильность", "Стоимость гибкости"], [true, true]);
        for (int i = 0; i < 9; i++) sensitivity.Row(Pct(volatilities[i], 0), Money(optionValues[i]));

        var boundary = rep.Table("Граница исполнения",
            ["Шаг", "Стоимость проекта"], [true, true]);

        for (int i = 0; i < result.ExerciseBoundary.Count; i++)
        {
            if (!double.IsFinite(result.ExerciseBoundary[i])) continue;
            boundary.Row($"{i + 1}", Money(result.ExerciseBoundary[i]));
        }

        string log =
            $"Стоимость с гибкостью {Money(result.TotalValue)}, без неё {Money(result.StaticNpv)}.\n" +
            $"Сам опцион стоит {Money(result.OptionValue)}.\n" +
            $"Исполняется на {Pct(result.ExerciseProbability, 0)} траекторий, в среднем через " +
            $"{Num(result.ExpectedExerciseTime, 2)} года.\n" +
            $"Стандартная ошибка оценки {Money(result.StandardError)} по {result.Paths} траекториям.";

        return Explain(rep, result, log);
    }

    #endregion
}
