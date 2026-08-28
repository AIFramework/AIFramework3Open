using System.Text;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Economics.Cohorts;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Economics;

public static partial class EconomicsDemoRunner
{
    private static string DoRetentionFit(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int modelChoice = I(p, "model", 0);
        int cohort = I(p, "cohort", 2000);
        int observed = I(p, "observed", 6);
        int horizon = I(p, "horizon", 36);
        double churn1 = N(p, "churn1", 0.4);
        double spread = N(p, "spread", 0.8);
        int boot = I(p, "boot", 120);
        int seed = I(p, "seed", 42);

        (double alpha, double beta) = SbgFromChurn(churn1, spread);
        double[] trueCurve = SbgSurvival(alpha, beta, horizon);
        var rng = new Random(seed);

        // Наблюдения обрываются на «сегодня»: дальше начинается экстраполяция
        double[] sampled = SampleSurvivors(SbgSurvival(alpha, beta, observed), cohort, rng);
        Vector observedCurve = Vec(sampled.Select(c => c / Math.Max(sampled[0], 1)));

        RetentionModel[] models = modelChoice == 0
            ? [RetentionModel.Exponential, RetentionModel.PowerLaw,
               RetentionModel.Weibull, RetentionModel.ShiftedBetaGeometric]
            : [(RetentionModel)(modelChoice - 1)];

        var fits = models
            .Select(m => RetentionFitter.Fit(observedCurve, cohort, m, horizon, 0.9, boot, seed))
            .OrderBy(f => f.Aic)
            .ToList();

        RetentionFitResult best = fits[0];

        // ── График ───────────────────────────────────────────────────────
        Vector axis = Axis(horizon + 1);
        cv.AddScatter(Axis(observed + 1), observedCurve, "Наблюдения", C(3));

        if (boot > 0)
        {
            cv.AddPlot(axis, best.SurvivalUpper, "Верхняя граница 90 %", C(5), 1);
            cv.AddPlot(axis, best.SurvivalLower, "Нижняя граница 90 %", C(5), 1);
        }

        for (int i = 0; i < fits.Count; i++)
            cv.AddPlot(axis, fits[i].Survival, ModelName(fits[i].Model), C(i), i == 0 ? 3 : 2);

        cv.AddPlot(axis, Vec(trueCurve), "Истинная кривая", C(4), 1);
        Segment(cv, observed, 0, observed, 1, C(6), "Конец наблюдений", 1);

        cv.ChartName = $"Удержание: {observed} мес. данных, экстраполяция до {horizon} мес.";
        cv.LabelX = "Месяц жизни";
        cv.LabelY = "Доля доживших";

        // ── Отчёт ────────────────────────────────────────────────────────
        double naiveLifetime = churn1 > 0 ? 1.0 / churn1 : double.PositiveInfinity;

        rep.Metric("Лучшая модель", ModelName(best.Model), null, "Выбрана по минимуму AIC")
           .Metric("Срок жизни", Num(best.ExpectedLifetime, 1), "мес.",
               $"Интервал 90 %: {Num(best.ExpectedLifetimeLower, 1)}–{Num(best.ExpectedLifetimeUpper, 1)}")
           .Metric("«Средний отток» дал бы", Num(naiveLifetime, 1), "мес.",
               "Оценка 1/отток по первому месяцу — типичная ошибка",
               MetricTone.Warn)
           .Metric("S(" + horizon + ")", Pct(best.Survival[horizon]), null,
               $"Интервал: {Pct(best.SurvivalLower[horizon])} – {Pct(best.SurvivalUpper[horizon])}")
           .Metric("RMSE подгонки", Num(best.Rmse, 4), "", "Отклонение от наблюдений");

        var table = rep.Table("Сравнение моделей",
            ["Модель", "AIC", "RMSE", "Параметры", $"S({horizon})", "Срок жизни, мес."],
            [false, true, true, false, true, true]);

        foreach (RetentionFitResult f in fits)
        {
            string parameters = string.Join(", ",
                f.ParameterNames.Zip(f.Parameters, (nm, v) => $"{nm}={Num(v, 3)}"));

            table.Row(ModelName(f.Model), Num(f.Aic, 1), Num(f.Rmse, 4),
                parameters, Pct(f.Survival[horizon]), Num(f.ExpectedLifetime, 1));
        }

        var rates = rep.Table("Мгновенное удержание растёт, а не постоянно",
            ["Месяц", "r(t) = S(t)/S(t−1)"], [true, true],
            note: "Именно поэтому единая «месячная ставка оттока» не работает.");

        foreach (int t in new[] { 1, 2, 3, 6, 12, 24 }.Where(t => t <= horizon))
            rates.Row(t.ToString(), Pct(best.RetentionRates[t]));

        rep.Note($"Доверительный интервал построен параметрическим бутстрапом по {boot} повторам " +
                 $"на когорте {Int(cohort)} клиентов. Чем дальше за границу наблюдений, тем он шире — " +
                 "это и есть честная цена экстраполяции.");

        var log = new StringBuilder();
        log.AppendLine($"Генерирующая модель sBG: alpha={Num(alpha, 3)}, beta={Num(beta, 3)}");
        log.AppendLine($"Наблюдений: {observed} мес., когорта {cohort} клиентов");
        log.AppendLine();
        foreach (RetentionFitResult f in fits)
            log.AppendLine($"{ModelName(f.Model),-28} AIC={Num(f.Aic, 1),12}  RMSE={Num(f.Rmse, 4)}  " +
                           $"LT={Num(f.ExpectedLifetime, 1)} мес.");

        return Narrate(rep, best, log.ToString());
    }

    /// <summary>Человекочитаемое имя семейства кривых.</summary>
    private static string ModelName(RetentionModel model) => model switch
    {
        RetentionModel.Exponential => "Экспоненциальная",
        RetentionModel.PowerLaw => "Степенная",
        RetentionModel.Weibull => "Вейбулла",
        RetentionModel.ShiftedBetaGeometric => "sBG",
        _ => model.ToString(),
    };

    private static string DoCohortMatrix(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        int cohorts = I(p, "cohorts", 9);
        double size = N(p, "size", 600);
        double growth = N(p, "growth", 0.08);
        double churn1 = N(p, "churn1", 0.4);
        double spread = N(p, "spread", 0.8);
        double drift = N(p, "drift", 0.0);
        var rng = new Random(I(p, "seed", 7));

        var raw = new Matrix(cohorts, cohorts);
        for (int c = 0; c < cohorts; c++)
        {
            int cohortSize = (int)Math.Round(size * Math.Pow(1.0 + growth, c));
            (double alpha, double beta) = SbgFromChurn(Math.Clamp(churn1 + (drift * c), 0.05, 0.9), spread);
            double[] counts = SampleSurvivors(SbgSurvival(alpha, beta, cohorts - 1), cohortSize, rng);

            for (int t = 0; t < cohorts; t++) raw[c, t] = counts[t];
        }

        CohortMatrix matrix = CohortMatrix.Triangular(raw);
        Vector pooled = matrix.PooledRetention();
        Vector observationBase = matrix.ObservationBase();

        // ── График: кривые когорт и сводная ──────────────────────────────
        for (int c = 0; c < cohorts; c++)
        {
            Vector curve = matrix.RetentionOf(c);
            if (curve.Count < 2) continue;
            cv.AddPlot(Axis(curve.Count), curve, c == 0 ? "Отдельные когорты" : "", C(1), 1);
        }

        cv.AddPlot(Axis(pooled.Count), pooled, "Сводная кривая (без смещения)", C(0), 3);

        // Ошибочный вариант: непронаблюдённые ячейки приняты за нули
        var biased = new Vector(cohorts);
        for (int t = 0; t < cohorts; t++)
        {
            double alive = 0, total = 0;
            for (int c = 0; c < cohorts; c++)
            {
                total += raw[c, 0];
                if (matrix.IsObserved(c, t)) alive += raw[c, t];
            }
            biased[t] = total > 0 ? alive / total : 0;
        }
        cv.AddPlot(Axis(cohorts), biased, "Если считать пустые ячейки нулями", C(3), 2);

        cv.ChartName = $"Когортная матрица: {cohorts} когорт, треугольник наблюдений";
        cv.LabelX = "Возраст когорты, мес.";
        cv.LabelY = "Доля доживших";

        rep.Metric("Когорт", cohorts, "шт.", "Строк в треугольнике")
           .Metric("Всего клиентов", Int(matrix.CohortSizes().Sum()), "шт.", "Сумма размеров когорт")
           .Metric("Сводное S(" + (cohorts - 1) + ")", Pct(pooled[cohorts - 1]), null,
               "Считано только по когортам, дожившим до этого возраста")
           .Metric("Наивная оценка", Pct(biased[cohorts - 1]), null,
               "Непронаблюдённые ячейки приняты за отток", MetricTone.Bad)
           .Metric("База последнего возраста", Int(observationBase[observationBase.Count - 1]), "клиентов",
               "Столько данных стоит за самой правой точкой кривой");

        var triangle = rep.Table("Треугольник удержания, % от размера когорты",
            ["Когорта", .. Enumerable.Range(0, cohorts).Select(t => $"M{t}")],
            [false, .. Enumerable.Repeat(true, cohorts)],
            note: "Пустые ячейки — будущее, которое ещё не наступило.");

        Matrix retention = matrix.RetentionMatrix();
        for (int c = 0; c < cohorts; c++)
        {
            var cells = new List<string> { $"#{c + 1} ({Int(raw[c, 0])})" };
            for (int t = 0; t < cohorts; t++)
                cells.Add(double.IsNaN(retention[c, t]) ? "" : Pct(retention[c, t], 0));
            triangle.Row([.. cells]);
        }

        rep.Note("Красная кривая — самая частая ошибка когортного отчёта: непронаблюдённые ячейки " +
                 "молча суммируются как нули, и удержание «обваливается» просто из-за нехватки данных.");

        var log = new StringBuilder();
        log.AppendLine("Возраст  Сводное удержание  База, клиентов");
        for (int t = 0; t < pooled.Count; t++)
            log.AppendLine($"{t,6}   {Pct(pooled[t]),16}   {Int(observationBase[t]),12}");

        return Narrate(rep, matrix, log.ToString());
    }
}
