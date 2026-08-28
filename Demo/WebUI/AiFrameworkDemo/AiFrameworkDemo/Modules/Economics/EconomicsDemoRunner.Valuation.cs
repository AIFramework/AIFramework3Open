using System.Text;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Market;
using AI.Economics.Valuation;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Economics;

public static partial class EconomicsDemoRunner
{
    private static string DoStartupValuation(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double investment = N(p, "investment", 50_000_000);
        double exitRevenue = N(p, "exit_revenue", 1_500_000_000);
        double multiple = N(p, "multiple", 4);
        double years = N(p, "years", 5);
        double irr = N(p, "irr", 0.5);
        double dilution = N(p, "dilution", 0.4);
        double marketAvg = N(p, "market_avg", 120_000_000);
        double team = N(p, "team", 1.3);
        double pSuccess = N(p, "p_success", 0.1);

        VcMethodResult vc = StartupValuation.VcMethod(new VcMethodInput
        {
            Investment = investment,
            ExitRevenue = exitRevenue,
            ExitMultiple = multiple,
            YearsToExit = years,
            TargetIrr = irr,
            ExpectedFutureDilution = dilution,
        });

        double berkusMax = marketAvg / 5.0;
        IReadOnlyList<BerkusFactor> berkusFactors =
            StartupValuation.BerkusDefaults(berkusMax, 0.9, 0.7, Math.Clamp(team / 2.0, 0, 1), 0.5, 0.3);
        double berkus = StartupValuation.Berkus(berkusFactors);

        IReadOnlyList<ScorecardFactor> scorecardFactors =
            StartupValuation.ScorecardDefaults(team, 1.2, 1.0, 0.9, 0.8, 1.0, 1.0);
        double scorecard = StartupValuation.Scorecard(marketAvg, scorecardFactors);

        double exitValue = exitRevenue * multiple;
        double discount = Math.Pow(1.0 + irr, years);

        ScenarioValuationResult chicago = StartupValuation.FirstChicago(
        [
            new ValuationScenario("Прорыв", pSuccess, exitValue / discount),
            new ValuationScenario("База", 0.35, exitValue / discount * 0.25),
            new ValuationScenario("Провал", Math.Max(1.0 - pSuccess - 0.35, 0.05), 0),
        ]);

        // ── График: четыре метода на одних данных ────────────────────────
        (string Name, double Value)[] methods =
        [
            ("Метод VC", vc.PreMoneyValuation),
            ("Беркус", berkus),
            ("Scorecard", scorecard),
            ("First Chicago", chicago.ExpectedValuation),
        ];

        cv.AddBar(Axis(methods.Length, 1), Vec(methods.Select(m => m.Value)), "Оценка до денег, ₽", C(0));

        double mean = methods.Average(m => m.Value);
        Segment(cv, 0.5, mean, methods.Length + 0.5, mean, C(3), "Среднее по методам", 2);

        cv.ChartName = "Оценка: " + string.Join(" · ", methods.Select((m, i) => $"{i + 1}. {m.Name}"));
        cv.LabelX = "Метод";
        cv.LabelY = "Оценка до денег, ₽";

        double spread = methods.Max(m => m.Value) / Math.Max(methods.Min(m => m.Value), 1);

        rep.Metric("Разброс оценок", Num(spread) + "×", null,
               "Отношение максимальной оценки к минимальной",
               spread < 2 ? MetricTone.Good : spread < 4 ? MetricTone.Warn : MetricTone.Bad)
           .Metric("Метод VC", Money(vc.PreMoneyValuation), "₽",
               $"Требуемая доля инвестора сегодня: {Pct(vc.OwnershipNow)}")
           .Metric("Scorecard", Money(scorecard), "₽", "Рынок, скорректированный на качество команды")
           .Metric("First Chicago", Money(chicago.ExpectedValuation), "₽",
               $"Из них {Pct(chicago.BestCaseShare)} даёт один сценарий",
               chicago.BestCaseShare > 0.7 ? MetricTone.Warn : MetricTone.Neutral)
           .Metric("Беркус", Money(berkus), "₽", "Пять качественных факторов с денежным потолком");

        var table = rep.Table("Четыре метода на одних входных данных",
            ["Метод", "Оценка до денег", "Что решает результат"], [false, true, false]);

        table.Row("Метод венчурного капитала", Money(vc.PreMoneyValuation),
                $"Требуемая доходность {Pct(irr, 0)} и разводнение {Pct(dilution, 0)}")
             .Row("Беркус", Money(berkus), "Субъективные баллы по пяти факторам риска")
             .Row("Scorecard", Money(scorecard), $"Средняя оценка рынка {Money(marketAvg)} ₽ и вес команды")
             .Row("First Chicago", Money(chicago.ExpectedValuation),
                $"Вероятность прорыва {Pct(pSuccess, 0)}");

        var vcTable = rep.Table("Разбор метода венчурного капитала",
            ["Показатель", "Значение"], [false, true]);

        vcTable.Row("Стоимость при выходе", Money(vc.ExitValue) + " ₽")
               .Row("Множитель на вложенное", Num(vc.MoneyMultiple) + "×")
               .Row("Доля инвестора при выходе", Pct(vc.OwnershipAtExit))
               .Row("Доля инвестора сегодня", Pct(vc.OwnershipNow))
               .Row("Оценка после денег", Money(vc.PostMoneyValuation) + " ₽")
               .Row("Оценка до денег", Money(vc.PreMoneyValuation) + " ₽");

        var scenarios = rep.Table("Сценарии First Chicago",
            ["Сценарий", "Вероятность", "Оценка", "Вклад"], [false, true, true, true]);

        foreach ((string name, double probability, double valuation, double contribution) in chicago.Breakdown)
            scenarios.Row(name, Pct(probability), Money(valuation), Money(contribution));

        rep.Note("Смысл считать четырьмя методами не в том, чтобы усреднить, а в том, чтобы увидеть, " +
                 "какое допущение управляет ценой. Если оценки расходятся втрое, спорить надо " +
                 "не о цифре, а о том допущении, которое их разводит.");

        var log = new StringBuilder();
        foreach ((string name, double value) in methods)
            log.AppendLine($"{name,-24} {Money(value),14} ₽");
        log.AppendLine();
        log.AppendLine($"Разброс: {Num(spread)}×");
        log.AppendLine($"Стандартное отклонение сценариев First Chicago: {Money(chicago.StandardDeviation)} ₽");

        return Narrate(rep, vc, log.ToString());
    }

    private static string DoRealOptions(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var input = new RealOptionInput
        {
            ProjectValue = N(p, "value", 160_000_000),
            InvestmentCost = N(p, "cost", 200_000_000),
            YearsToDecision = N(p, "years", 3),
            Volatility = N(p, "vol", 0.6),
            RiskFreeRate = N(p, "rate", 0.08),
            ValueLeakage = N(p, "leak", 0.05),
            Steps = 200,
        };

        RealOptionResult result = RealOptionValuation.Evaluate(input);

        // ── График: стоимость опциона против статического NPV ────────────
        const int grid = 50;
        double maxValue = input.InvestmentCost * 2.5;
        var values = new Vector(grid);
        var optionValues = new Vector(grid);
        var npvValues = new Vector(grid);

        for (int i = 0; i < grid; i++)
        {
            double s = maxValue * (i + 1) / grid;
            values[i] = s;
            optionValues[i] = RealOptionValuation.Evaluate(input with { ProjectValue = s, Steps = 80 }).BinomialValue;
            npvValues[i] = Math.Max(s - input.InvestmentCost, 0);
        }

        cv.AddPlot(values, optionValues, "Стоимость с правом подождать", C(0), 3);
        cv.AddPlot(values, npvValues, "Статический NPV («сейчас или никогда»)", C(3), 2);
        Segment(cv, input.ProjectValue, 0, input.ProjectValue, optionValues.Max(), C(2), "Текущая оценка проекта", 2);

        cv.ChartName = "Премия за гибкость: разрыв между кривыми";
        cv.LabelX = "Приведённая стоимость проекта, ₽";
        cv.LabelY = "Стоимость проекта с учётом опциона, ₽";

        rep.Metric("Статический NPV", Money(result.StaticNpv), "₽",
               "Классический ответ «делать или не делать сейчас»",
               result.StaticNpv > 0 ? MetricTone.Good : MetricTone.Bad)
           .Metric("Стоимость опциона", Money(result.BinomialValue), "₽",
               "Биномиальное дерево с правом досрочного запуска", MetricTone.Good)
           .Metric("Премия за гибкость", Money(result.FlexibilityPremium), "₽",
               "Столько стоит право подождать и решить позже")
           .Metric("Порог запуска", Money(result.ImmediateExerciseThreshold), "₽",
               "Начиная с этой стоимости проекта ждать уже не выгодно")
           .Metric("Вероятность запуска", Pct(result.ExerciseProbability), null,
               "Риск-нейтральная вероятность того, что проект стартует");

        rep.Table("Сравнение методов", ["Метод", "Значение", "Комментарий"], [false, true, false])
           .Row("Статический NPV", Money(result.StaticNpv) + " ₽", "Игнорирует, что решение можно отложить")
           .Row("Блэк — Шоулз", Money(result.BlackScholesValue) + " ₽", "Европейский опцион: решение только в конце срока")
           .Row("Биномиальное дерево", Money(result.BinomialValue) + " ₽", "Американский опцион: запуск в любой момент")
           .Row("Дельта", Num(result.Delta, 3), "Чувствительность к стоимости проекта");

        rep.Table("Исходные допущения", ["Параметр", "Значение"], [false, true])
           .Row("Стоимость проекта", Money(input.ProjectValue) + " ₽")
           .Row("Стоимость запуска", Money(input.InvestmentCost) + " ₽")
           .Row("Срок решения", Num(input.YearsToDecision, 1) + " лет")
           .Row("Волатильность", Pct(input.Volatility, 0))
           .Row("Безрисковая ставка", Pct(input.RiskFreeRate, 1))
           .Row("Утечка стоимости", Pct(input.ValueLeakage, 1) + " в год");

        rep.Note("Волатильность непубличного проекта не наблюдаема и берётся из аналогов — " +
                 "это главное ограничение метода. Двигая соответствующий ползунок, видно, " +
                 "насколько результат зависит от допущения, которое нечем проверить.");

        var log = new StringBuilder();
        log.AppendLine($"Статический NPV:      {Money(result.StaticNpv)} ₽");
        log.AppendLine($"Блэк — Шоулз:         {Money(result.BlackScholesValue)} ₽");
        log.AppendLine($"Биномиальное дерево:  {Money(result.BinomialValue)} ₽");
        log.AppendLine($"Премия за гибкость:   {Money(result.FlexibilityPremium)} ₽");
        log.AppendLine($"Порог немедленного запуска: {Money(result.ImmediateExerciseThreshold)} ₽");

        return Narrate(rep, result, log.ToString());
    }

    private static string DoMarketSizing(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        var topDown = new TopDownInput
        {
            TotalMarketValue = N(p, "total", 800_000_000_000),
            GeographyShare = N(p, "geo", 0.08),
            SegmentShare = N(p, "segment", 0.25),
            AddressableShare = N(p, "addressable", 0.5),
            AchievableShare = N(p, "achievable", 0.04),
        };

        var bottomUp = new BottomUpInput
        {
            TargetAccounts = N(p, "accounts", 12_000),
            QualifiedShare = N(p, "qualified", 0.6),
            AnnualRevenuePerAccount = N(p, "arpa", 1_200_000),
            ReachableShare = N(p, "reachable", 0.5),
            WinRate = N(p, "winrate", 0.06),
        };

        MarketSizingResult result = MarketSizing.Estimate(topDown, bottomUp);

        // ── График: TAM/SAM/SOM двумя способами ──────────────────────────
        Vector axis = Axis(3, 1);
        cv.AddBar(axis, new Vector(result.TamTopDown, result.SamTopDown, result.SomTopDown),
            "Сверху вниз", C(0));
        cv.AddBar(axis, new Vector(result.TamBottomUp, result.SamBottomUp, result.SomBottomUp),
            "Снизу вверх", C(1));
        cv.AddBar(axis, new Vector(result.ReconciledTam, result.ReconciledSam, result.ReconciledSom),
            "Согласованная оценка", C(2));

        cv.ChartName = "1. TAM · 2. SAM · 3. SOM";
        cv.LabelX = "Уровень рынка";
        cv.LabelY = "Объём, ₽";

        rep.Metric("Согласованный TAM", Money(result.ReconciledTam), "₽", "Среднее геометрическое двух оценок")
           .Metric("Согласованный SOM", Money(result.ReconciledSom), "₽", "То, что реально можно захватить")
           .Metric("Расхождение TAM", Num(result.TamDivergence) + "×", null,
               "Во сколько раз оценки отличаются друг от друга",
               result.TamDivergence <= 1.5 ? MetricTone.Good
                   : result.TamDivergence <= 3 ? MetricTone.Warn : MetricTone.Bad)
           .Metric("Подразумеваемая доля", Pct(result.ImpliedMarketShare), null,
               "SOM делить на TAM — проверка на здравый смысл")
           .Metric("Вердикт", result.TamDivergence <= 1.5 ? "согласованы" : "требует проверки", null,
               result.Verdict);

        rep.Table("Две независимые оценки",
            ["Уровень", "Сверху вниз", "Снизу вверх", "Расхождение", "Согласовано"],
            [false, true, true, true, true])
           .Row("TAM", Money(result.TamTopDown), Money(result.TamBottomUp),
               Num(result.TamDivergence) + "×", Money(result.ReconciledTam))
           .Row("SAM", Money(result.SamTopDown), Money(result.SamBottomUp),
               Num(Math.Max(result.SamTopDown, result.SamBottomUp)
                   / Math.Max(Math.Min(result.SamTopDown, result.SamBottomUp), 1)) + "×",
               Money(result.ReconciledSam))
           .Row("SOM", Money(result.SomTopDown), Money(result.SomBottomUp),
               Num(result.SomDivergence) + "×", Money(result.ReconciledSom));

        rep.Table("Как получена каждая оценка", ["Способ", "Расчёт"], [false, false])
           .Row("Сверху вниз",
               $"{Money(topDown.TotalMarketValue)} × {Pct(topDown.GeographyShare, 0)} × " +
               $"{Pct(topDown.SegmentShare, 0)} × {Pct(topDown.AddressableShare, 0)} × {Pct(topDown.AchievableShare, 1)}")
           .Row("Снизу вверх",
               $"{Int(bottomUp.TargetAccounts)} клиентов × {Money(bottomUp.AnnualRevenuePerAccount)} ₽ × " +
               $"{Pct(bottomUp.QualifiedShare, 0)} × {Pct(bottomUp.ReachableShare, 0)} × {Pct(bottomUp.WinRate, 0)}");

        rep.Note(result.Verdict + " Ценность метода не в числе, а в расхождении: совпадение " +
                 "оценок означает непротиворечивую модель рынка, разрыв в разы — ошибку в одной из них.");

        var log = new StringBuilder();
        log.AppendLine($"TAM: {Money(result.TamTopDown)} (сверху) / {Money(result.TamBottomUp)} (снизу) → {Money(result.ReconciledTam)} ₽");
        log.AppendLine($"SAM: {Money(result.SamTopDown)} / {Money(result.SamBottomUp)} → {Money(result.ReconciledSam)} ₽");
        log.AppendLine($"SOM: {Money(result.SomTopDown)} / {Money(result.SomBottomUp)} → {Money(result.ReconciledSom)} ₽");
        log.AppendLine();
        log.AppendLine(result.Verdict);

        return Narrate(rep, result, log.ToString());
    }

    private static string DoBassDiffusion(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double m = N(p, "m", 200_000);
        double pCoefficient = N(p, "p", 0.02);
        double q = N(p, "q", 0.4);
        int observed = I(p, "observed", 14);
        int horizon = I(p, "horizon", 48);
        double noise = N(p, "noise", 0.03);
        var rng = new Random(I(p, "seed", 3));

        var truth = new BassDiffusion();
        truth.SetParameters(m, pCoefficient, q);
        Vector trueCumulative = truth.Cumulative(horizon);

        // Наблюдения доступны только до «сегодня» и содержат погрешность учёта
        var noisy = new Vector(observed);
        for (int i = 0; i < observed; i++)
            noisy[i] = Math.Max(trueCumulative[i] * (1.0 + ((rng.NextDouble() - 0.5) * 2 * noise)),
                i > 0 ? noisy[i - 1] : 0);

        var fitted = new BassDiffusion();
        fitted.Fit(noisy);

        Vector forecast = fitted.Cumulative(horizon);
        Vector adopters = fitted.Adopters(horizon);

        // ── График ───────────────────────────────────────────────────────
        Vector axis = Axis(horizon, 1);
        cv.AddPlot(axis, trueCumulative, "Истинная кривая проникновения", C(4), 1);
        cv.AddPlot(axis, forecast, "Подогнанный прогноз", C(0), 3);
        cv.AddScatter(Axis(observed, 1), noisy, "Наблюдения", C(3));
        cv.AddPlot(axis, adopters, "Новых клиентов в месяц", C(1), 2);
        Segment(cv, observed, 0, observed, fitted.MarketPotential, C(6), "Конец наблюдений", 1);

        if (fitted.PeakTime > 0)
            Segment(cv, fitted.PeakTime, 0, fitted.PeakTime, fitted.MarketPotential, C(2), "Пик продаж", 2);

        cv.ChartName = "Диффузия Басса: подгонка по " + observed + " мес. и прогноз до " + horizon;
        cv.LabelX = "Месяц";
        cv.LabelY = "Клиентов";

        double capturedAtObserved = noisy[observed - 1] / fitted.MarketPotential;

        rep.Metric("Потенциал рынка m", Int(fitted.MarketPotential), "клиентов",
               $"Истинное значение: {Int(m)}")
           .Metric("Инновация p", Num(fitted.Innovation, 4), "", $"Истинное значение: {Num(pCoefficient, 4)}")
           .Metric("Имитация q", Num(fitted.Imitation, 4), "", $"Истинное значение: {Num(q, 4)}")
           .Metric("Пик продаж", Num(fitted.PeakTime, 1), "мес.",
               "После него число новых клиентов падает само, без изменения маркетинга",
               fitted.PeakTime > observed ? MetricTone.Good : MetricTone.Warn)
           .Metric("R²", Num(fitted.RSquared, 4), "", "Качество подгонки по накопленным принявшим",
               fitted.RSquared > 0.99 ? MetricTone.Good : MetricTone.Warn);

        rep.Table("Подогнанные параметры",
            ["Параметр", "Оценка", "Истина", "Ошибка"], [false, true, true, true])
           .Row("m — потенциал рынка", Int(fitted.MarketPotential), Int(m),
               Pct((fitted.MarketPotential - m) / m))
           .Row("p — инновация", Num(fitted.Innovation, 4), Num(pCoefficient, 4),
               Pct((fitted.Innovation - pCoefficient) / pCoefficient))
           .Row("q — имитация", Num(fitted.Imitation, 4), Num(q, 4), Pct((fitted.Imitation - q) / q));

        var plan = rep.Table("Прогноз новых клиентов",
            ["Месяц", "Новых", "Накопленно", "Проникновение"], [true, true, true, true]);

        int step = Math.Max(1, horizon / 12);
        for (int i = 0; i < horizon; i += step)
            plan.Row((i + 1).ToString(), Int(adopters[i]), Int(forecast[i]),
                Pct(forecast[i] / fitted.MarketPotential));

        rep.Note($"На момент конца наблюдений рынок выбран на {Pct(capturedAtObserved)}. " +
                 "Пик продаж — важнейший вывод для планирования: после него падение выручки " +
                 "не означает, что маркетинг стал работать хуже.");

        var log = new StringBuilder();
        log.AppendLine($"Подгонка по {observed} месяцам, шум {Pct(noise, 1)}");
        log.AppendLine($"m = {Int(fitted.MarketPotential)} (истина {Int(m)})");
        log.AppendLine($"p = {Num(fitted.Innovation, 5)} (истина {Num(pCoefficient, 5)})");
        log.AppendLine($"q = {Num(fitted.Imitation, 5)} (истина {Num(q, 5)})");
        log.AppendLine($"R² = {Num(fitted.RSquared, 5)}");
        log.AppendLine($"Пик продаж: месяц {Num(fitted.PeakTime, 1)}, {Int(fitted.PeakAdopters)} клиентов");

        return Narrate(rep, fitted, log.ToString());
    }
}
