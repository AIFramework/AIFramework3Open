using System.Text;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Clv;
using AI.Statistics;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Economics;

public static partial class EconomicsDemoRunner
{
    /// <summary>
    /// Синтетический портфель покупок: часть клиентов ушла в неизвестный момент,
    /// остальные продолжают покупать пуассоновским потоком.
    /// </summary>
    private static List<CustomerSummary> GeneratePortfolio(
        int customers, double activeShare, double rate, double window,
        double meanValue, double dispersion, Random rng)
    {
        var list = new List<CustomerSummary>(customers);
        double cv2 = Math.Max(dispersion * dispersion, 1e-4);

        for (int i = 0; i < customers; i++)
        {
            bool active = rng.NextDouble() < activeShare;
            double lifetime = active ? window : window * rng.NextDouble();

            // Индивидуальный чек: разброс между клиентами задан гаммой с CV 0,5
            double personalValue = meanValue * RandomEngine.NextGamma(rng, 4.0, 0.25);

            double t = 0, lastPurchase = 0;
            int count = 0;
            double valueSum = 0;

            while (true)
            {
                t += RandomEngine.NextExponential(rng, Math.Max(rate, 1e-6));
                if (t > lifetime) break;

                count++;
                lastPurchase = t;
                valueSum += personalValue * RandomEngine.NextGamma(rng, 1.0 / cv2, cv2);
            }

            list.Add(new CustomerSummary
            {
                Id = $"c{i + 1:D4}",
                Frequency = count,
                Recency = lastPurchase,
                Age = window,
                MonetaryValue = count > 0 ? valueSum / count : 0,
            });
        }

        return list;
    }

    private static string DoBgNbd(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int choice = I(p, "model", 0);
        int customers = I(p, "customers", 800);
        double active = N(p, "active", 0.6);
        double rate = N(p, "rate", 1.0);
        double window = N(p, "window", 18);
        double horizon = N(p, "horizon", 12);
        var rng = new Random(I(p, "seed", 17));

        List<CustomerSummary> portfolio =
            GeneratePortfolio(customers, active, rate, window, 5000, 0.6, rng);

        var bg = new BgNbdModel();
        bg.Fit(portfolio);

        ParetoNbdModel? pareto = null;
        if (choice >= 1)
        {
            pareto = new ParetoNbdModel();
            pareto.Fit(portfolio);
        }

        ITransactionModel primary = choice == 1 && pareto is not null ? pareto : bg;

        // ── График: вероятность активности от давности последней покупки ──
        const int grid = 60;
        Vector recency = Axis(grid, 0, window / (grid - 1));
        int[] frequencies = [1, 3, 10, 25];

        for (int f = 0; f < frequencies.Length; f++)
        {
            var curve = new Vector(grid);
            for (int i = 0; i < grid; i++)
                curve[i] = bg.ProbabilityAlive(new CustomerSummary
                {
                    Frequency = frequencies[f],
                    Recency = recency[i],
                    Age = window,
                });

            cv.AddPlot(recency, curve, $"BG/NBD, покупок: {frequencies[f]}", C(f), 2);
        }

        if (pareto is not null)
        {
            var curve = new Vector(grid);
            for (int i = 0; i < grid; i++)
                curve[i] = pareto.ProbabilityAlive(new CustomerSummary
                {
                    Frequency = 10,
                    Recency = recency[i],
                    Age = window,
                });

            cv.AddPlot(recency, curve, "Pareto/NBD, покупок: 10", C(5), 3);
        }

        cv.ChartName = "Вероятность того, что клиент ещё жив";
        cv.LabelX = "Момент последней покупки, мес. от первой";
        cv.LabelY = "P(активен)";

        double totalForecast = portfolio.Sum(c => primary.ExpectedTransactions(c, horizon));
        double meanAlive = portfolio.Average(c => primary.ProbabilityAlive(c));
        double repeatShare = portfolio.Count(c => c.Frequency > 0) / (double)portfolio.Count;

        rep.Metric("Клиентов", customers, "шт.", "Размер синтетического портфеля")
           .Metric("Средняя P(активен)", Pct(meanAlive), null, "По всему портфелю",
               meanAlive > 0.5 ? MetricTone.Good : MetricTone.Warn)
           .Metric($"Покупок за {Num(horizon, 0)} мес.", Int(totalForecast), "шт.",
               "Сумма условных ожиданий по всем клиентам")
           .Metric("Доля с повторной покупкой", Pct(repeatShare), null, "Остальные купили один раз");

        rep.Table("Оценённые параметры", ["Параметр", "Значение", "Смысл"], [false, true, false])
           .Row("r", Num(bg.R, 4), "Форма гаммы интенсивности покупок")
           .Row("alpha", Num(bg.Alpha, 4), "Масштаб гаммы интенсивности покупок")
           .Row("a", Num(bg.A, 4), "Первый параметр беты вероятности ухода")
           .Row("b", Num(bg.B, 4), "Второй параметр беты вероятности ухода")
           .Row("lnL", Num(bg.LogLikelihood, 1), "Логарифм правдоподобия");

        var top = rep.Table("Топ-10 клиентов по прогнозу покупок",
            ["Клиент", "Покупок было", "Давность", "P(активен)", $"Прогноз на {Num(horizon, 0)} мес."],
            [false, true, true, true, true]);

        foreach (CustomerSummary c in portfolio
                     .OrderByDescending(c => primary.ExpectedTransactions(c, horizon))
                     .Take(10))
        {
            top.Row(c.Id, Int(c.Frequency), Num(c.Recency, 1),
                Pct(primary.ProbabilityAlive(c)), Num(primary.ExpectedTransactions(c, horizon), 2));
        }

        rep.Note("Кривые расходятся: у клиента с 25 покупками месяц молчания почти гарантирует уход, " +
                 "у клиента с одной покупкой то же молчание не значит ничего. Средний отток такого " +
                 "различия не видит.");

        var log = new StringBuilder();
        log.AppendLine($"BG/NBD:      r={Num(bg.R, 4)}  alpha={Num(bg.Alpha, 4)}  a={Num(bg.A, 4)}  b={Num(bg.B, 4)}  lnL={Num(bg.LogLikelihood, 1)}");
        if (pareto is not null)
            log.AppendLine($"Pareto/NBD:  r={Num(pareto.R, 4)}  alpha={Num(pareto.Alpha, 4)}  s={Num(pareto.S, 4)}  beta={Num(pareto.Beta, 4)}  lnL={Num(pareto.LogLikelihood, 1)}");
        log.AppendLine();
        log.AppendLine($"Ожидаемое число покупок портфеля за {Num(horizon, 0)} мес.: {Int(totalForecast)}");

        return Narrate(rep, bg, log.ToString());
    }

    private static string DoGammaGamma(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int customers = I(p, "customers", 800);
        double meanValue = N(p, "mean_value", 5000);
        double dispersion = N(p, "dispersion", 0.6);
        double rate = N(p, "rate", 1.0);
        double window = N(p, "window", 18);
        var rng = new Random(I(p, "seed", 23));

        List<CustomerSummary> portfolio =
            GeneratePortfolio(customers, 0.75, rate, window, meanValue, dispersion, rng);

        var model = new GammaGammaModel();
        model.Fit(portfolio);

        List<CustomerSummary> withPurchases = [.. portfolio.Where(c => c.Frequency > 0)];

        // ── График: наблюдённый чек против ожидаемого ────────────────────
        Vector observed = Vec(withPurchases.Select(c => c.MonetaryValue));
        Vector expected = Vec(withPurchases.Select(model.ConditionalExpectedValue));

        cv.AddScatter(observed, expected, "Клиенты", C(1));

        double maxValue = Math.Max(observed.Max(), expected.Max());
        Segment(cv, 0, 0, maxValue, maxValue, C(3), "Если верить наблюдённому чеку", 2);
        Segment(cv, 0, model.PopulationMean, maxValue, model.PopulationMean, C(0),
            "Средний чек популяции", 2);

        cv.ChartName = "Регрессия к среднему: чем меньше покупок, тем сильнее сдвиг";
        cv.LabelX = "Наблюдённый средний чек, ₽";
        cv.LabelY = "Ожидаемый средний чек, ₽";

        double meanShift = withPurchases.Average(c =>
            Math.Abs(model.ConditionalExpectedValue(c) - c.MonetaryValue) / Math.Max(c.MonetaryValue, 1));

        rep.Metric("Средний чек популяции", Money(model.PopulationMean), "₽", "p·gamma/(q−1)")
           .Metric("p", Num(model.P, 4), "", "Форма распределения чека внутри клиента")
           .Metric("q", Num(model.Q, 4), "", "Форма распределения масштаба по популяции")
           .Metric("Средний сдвиг оценки", Pct(meanShift), null,
               "Насколько модель поправляет наблюдённый чек")
           .Metric("Клиентов в подгонке", model.SampleSize, "шт.", "Только с повторными покупками");

        var table = rep.Table("Сильнее всего поправлены редкие покупатели",
            ["Клиент", "Покупок", "Наблюдённый чек", "Ожидаемый чек", "Поправка"],
            [false, true, true, true, true]);

        foreach (CustomerSummary c in withPurchases
                     .OrderByDescending(c => Math.Abs(model.ConditionalExpectedValue(c) - c.MonetaryValue))
                     .Take(10))
        {
            double e = model.ConditionalExpectedValue(c);
            table.Row(c.Id, Int(c.Frequency), Money(c.MonetaryValue), Money(e),
                Pct((e - c.MonetaryValue) / Math.Max(c.MonetaryValue, 1)));
        }

        rep.Note("Точки у диагонали — клиенты с длинной историей, им модель верит. " +
                 "Точки, притянутые к горизонтали, — те, у кого покупок мало: их «средний чек» " +
                 "почти целиком шум.");

        var log = new StringBuilder();
        log.AppendLine($"p = {Num(model.P, 4)}   q = {Num(model.Q, 4)}   gamma = {Num(model.Gamma, 2)}");
        log.AppendLine($"Средний чек популяции: {Money(model.PopulationMean)} ₽");
        log.AppendLine($"lnL = {Num(model.LogLikelihood, 1)}");

        return Narrate(rep, model, log.ToString());
    }

    private static string DoClvPortfolio(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int customers = I(p, "customers", 800);
        double active = N(p, "active", 0.6);
        double meanValue = N(p, "mean_value", 5000);
        double margin = N(p, "margin", 0.4);
        double horizon = N(p, "horizon", 12);
        double discount = N(p, "discount", 0.01);
        var rng = new Random(I(p, "seed", 31));

        List<CustomerSummary> portfolio =
            GeneratePortfolio(customers, active, 1.0, 18, meanValue, 0.6, rng);

        var frequency = new BgNbdModel();
        frequency.Fit(portfolio);
        var monetary = new GammaGammaModel();
        monetary.Fit(portfolio);

        ClvPortfolio result = ClvCalculator.Compute(
            frequency, monetary, portfolio, horizon, (int)Math.Max(horizon, 1), discount, margin);

        ClvPortfolio undiscounted = ClvCalculator.Compute(
            frequency, monetary, portfolio, horizon, (int)Math.Max(horizon, 1), 0, margin);

        // ── График: кривая Лоренца по ценности клиентов ──────────────────
        double[] sorted = [.. result.Customers.Select(c => c.Clv).OrderByDescending(v => v)];
        var share = new Vector(sorted.Length);
        var index = new Vector(sorted.Length);
        double cumulative = 0;

        for (int i = 0; i < sorted.Length; i++)
        {
            cumulative += sorted[i];
            share[i] = result.TotalClv > 0 ? cumulative / result.TotalClv : 0;
            index[i] = (i + 1.0) / sorted.Length;
        }

        cv.AddPlot(index, share, "Накопленная доля ценности", C(0), 3);
        Segment(cv, 0, 0, 1, 1, C(3), "Если бы все клиенты были одинаковы", 2);
        Segment(cv, 0.1, 0, 0.1, 1, C(2), "Верхние 10 % клиентов", 1);

        cv.ChartName = $"Концентрация ценности: верхние 10 % дают {Pct(result.Top10PercentShare)}";
        cv.LabelX = "Доля клиентов (по убыванию CLV)";
        cv.LabelY = "Доля суммарного CLV";

        rep.Metric("CLV портфеля", Money(result.TotalClv), "₽",
               $"Дисконтированный по марже на горизонте {Num(horizon, 0)} мес.")
           .Metric("Средний CLV", Money(result.MeanClv), "₽", "На одного клиента")
           .Metric("Верхние 10 %", Pct(result.Top10PercentShare), null,
               "Доля суммарной ценности у лучшего дециля", MetricTone.Neutral)
           .Metric("Цена дисконтирования", Pct(1 - (result.TotalClv / Math.Max(undiscounted.TotalClv, 1e-9))),
               null, "Насколько дисконт уменьшает ценность портфеля")
           .Metric("Средняя P(активен)", Pct(result.MeanProbabilityAlive), null, "По портфелю");

        var top = rep.Table("Топ-10 клиентов по CLV",
            ["Клиент", "P(активен)", "Прогноз покупок", "Ожидаемый чек", "CLV"],
            [false, true, true, true, true]);

        foreach (CustomerClv c in result.Customers.Take(10))
            top.Row(c.Id, Pct(c.ProbabilityAlive), Num(c.ExpectedTransactions, 2),
                Money(c.ExpectedValue), Money(c.Clv));

        int zeroValue = result.Customers.Count(c => c.Clv < result.MeanClv * 0.05);
        rep.Table("Распределение ценности", ["Группа", "Клиентов", "Доля CLV"], [false, true, true])
           .Row("Верхние 10 %", Int(Math.Max(1, customers / 10)), Pct(result.Top10PercentShare))
           .Row("Практически без ценности", Int(zeroValue), Pct(zeroValue / (double)customers));

        rep.Note("Кривая далеко от диагонали означает, что бюджет удержания надо тратить адресно: " +
                 "равномерная скидка всем клиентам оплачивается прибылью верхнего дециля.");

        var log = new StringBuilder();
        log.AppendLine($"CLV портфеля (дисконт):    {Money(result.TotalClv)} ₽");
        log.AppendLine($"CLV портфеля (без дисконта): {Money(undiscounted.TotalClv)} ₽");
        log.AppendLine($"Средний CLV:               {Money(result.MeanClv)} ₽");
        log.AppendLine($"Доля верхних 10 %:         {Pct(result.Top10PercentShare)}");

        return Narrate(rep, result, log.ToString());
    }
}
