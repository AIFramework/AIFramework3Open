using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.Fuzzy.Inference;
using AiFrameworkDemo.Core;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Fuzzy;

public static partial class FuzzyDemoRunner
{
    // -- 1. Фаззификация -------------------------------------------------

    private static string DoMembership(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double temp    = N(p, "temp", 12);
        int    shape   = I(p, "shape", 0);
        double overlap = N(p, "overlap", 0.5);

        const int n = 241;
        var x = new Vector(n);
        for (int i = 0; i < n; i++) x[i] = TempMin + (TempMax - TempMin) * i / (n - 1.0);

        cv.ChartName = shape == 1
            ? "Трапециевидные термы входа"
            : "Треугольные термы входа";
        Axes(cv, "температура, °C", "степень принадлежности μ");

        var colors = new[] { ColdColor, NormColor, HotColor };
        var mus = new double[3];

        for (int t = 0; t < 3; t++)
        {
            var y = new Vector(n);
            for (int i = 0; i < n; i++) y[i] = InputMu(x[i], t, shape, overlap);
            cv.AddPlot(x, y, InputTerms[t], colors[t], 3);
            mus[t] = InputMu(temp, t, shape, overlap);
        }

        AddVerticalMarker(cv, temp, 1.0, $"вход {F2(temp)} °C", CrispColor);

        // -- Метрики --------------------------------------------------------
        double sum = mus.Sum();
        int    best = Array.IndexOf(mus, mus.Max());

        rep.Metric("Вход", temp, "°C", hint: "Значение, которое фаззифицируется")
           .Metric("Доминирующий терм", mus[best] > 0 ? InputTerms[best] : "нет",
                   hint: "Терм с наибольшей степенью принадлежности",
                   tone: mus[best] > 0 ? MetricTone.Good : MetricTone.Bad)
           .Metric("μ доминирующего", mus[best], format: "F3")
           .Metric("Сумма μ", sum, format: "F3",
                   hint: "Для нечёткого разбиения не обязана равняться единице",
                   tone: sum < 1e-9 ? MetricTone.Bad : MetricTone.Neutral)
           .Metric("Активных термов", mus.Count(m => m > 1e-9), "шт.")
           .Note("Перекрытие термов задаёт, сколько правил сработает одновременно. " +
                 "При нулевом перекрытии система становится кусочно-постоянной: " +
                 "в каждой точке активно ровно одно правило, и выход скачет между уровнями.");

        var t1 = rep.Table("Степени принадлежности входа",
            ["Терм", "Носитель, °C", "μ(вход)", "Активен"],
            numeric: [false, false, true, false]);

        var bounds = InputTermBounds(overlap);
        for (int t = 0; t < 3; t++)
            t1.Row(InputTerms[t],
                   $"[{F2(bounds[t].a)}; {F2(bounds[t].d)}]",
                   F(mus[t]),
                   mus[t] > 1e-9 ? "да" : "нет");

        var sb = new StringBuilder();
        sb.AppendLine($"Фаззификация входа {temp:F2} °C");
        sb.AppendLine($"Форма термов: {(shape == 1 ? "трапециевидная" : "треугольная")}, перекрытие {overlap:F2}");
        sb.AppendLine();
        for (int t = 0; t < 3; t++)
            sb.AppendLine($"  μ_{InputTerms[t],-9} = {mus[t]:F4}");
        sb.AppendLine();
        sb.AppendLine($"Сумма степеней принадлежности: {sum:F4}");
        return sb.ToString();
    }

    // -- 2. Мамдани и Ларсен ---------------------------------------------

    /// <summary>
    /// Обе схемы отличаются ровно одной операцией — импликацией:
    /// Мамдани срезает терм минимумом, Ларсен масштабирует произведением.
    /// Поэтому и код общий: разница в одном флаге.
    /// </summary>
    private static string DoMamdaniOrLarsen(IReadOnlyDictionary<string, double> p, ChartView cv,
        ReportBuilder rep, bool larsen)
    {
        double temp  = N(p, "temp", 12);
        int    shape = I(p, "shape", 0);
        int    grid  = Math.Max(21, I(p, "grid", 201));

        string method = larsen ? "Ларсен" : "Мамдани";
        double[] w = FiringStrengths(temp, shape, overlap: 0.5);

        Vector universe = PowerGrid(grid);
        var terms = new List<Vector>(3);
        for (int r = 0; r < 3; r++) terms.Add(OutputTermSamples(RuleMap[r], universe));

        Vector agg = larsen
            ? FuzzyLarsenInference.AggregateMaxProduct(w, terms)
            : FuzzyMamdaniInference.AggregateMaxMin(w, terms);

        double crisp = FuzzyMamdaniInference.DefuzzifyCentroid(universe, agg);

        // -- График: срезанные/масштабированные термы и результат агрегирования
        cv.ChartName = $"{method}: агрегирование на универсуме выхода";
        Axes(cv, "мощность нагревателя, %", "степень принадлежности μ");

        var colors = new[] { HotColor, NormColor, ColdColor };
        for (int r = 0; r < 3; r++)
        {
            if (w[r] < 1e-9) continue;   // не сработавшее правило только засоряет легенду

            var clipped = new Vector(grid);
            for (int i = 0; i < grid; i++)
                clipped[i] = larsen ? w[r] * terms[r][i] : Math.Min(w[r], terms[r][i]);

            cv.AddPlot(universe, clipped,
                $"R{r + 1}: {InputTerms[r]} → {OutputTerms[RuleMap[r]]} (w={w[r]:F2})",
                colors[r], 2);
        }

        cv.AddArea(universe, agg, "агрегированное μ", AggColor, 3);
        AddVerticalMarker(cv, crisp, 1.0, $"центр тяжести {crisp:F2} %", CrispColor);

        // -- Метрики --------------------------------------------------------
        int fired = w.Count(v => v > 1e-9);
        double area = agg.Sum() * (PowMax - PowMin) / (grid - 1.0);

        rep.Metric("Метод", method)
           .Metric("Вход", temp, "°C")
           .Metric("Выход (центр тяжести)", crisp, "%",
                   hint: "Дефаззификация методом COG", tone: MetricTone.Good, format: "F2")
           .Metric("Сработало правил", $"{fired} из 3",
                   tone: fired == 0 ? MetricTone.Bad : MetricTone.Neutral)
           .Metric("Максимум μ_agg", agg.Max(), format: "F3",
                   hint: "Высота агрегированного множества")
           .Note(larsen
               ? "Ларсен умножает терм следствия на степень срабатывания: форма терма сохраняется, " +
                 "меняется только высота. Мамдани в той же ситуации срезал бы верхушку горизонтально."
               : "Мамдани срезает терм следствия по уровню w (min): у результата появляется плоская вершина. " +
                 "Ларсен на тех же данных сохранил бы форму терма, лишь понизив её.");

        var t1 = rep.Table("Правила и их вклад",
            ["Правило", "Если", "То", "w (степень срабатывания)", "Вклад в агрегат"],
            numeric: [false, false, false, true, true],
            note: larsen
                ? "Вклад = w · max(μ_терм) — терм масштабируется."
                : "Вклад = min(w, max(μ_терм)) — терм срезается по уровню w.");

        for (int r = 0; r < 3; r++)
        {
            double peak = terms[r].Max();
            double contrib = larsen ? w[r] * peak : Math.Min(w[r], peak);
            t1.Row($"R{r + 1}", InputTerms[r], OutputTerms[RuleMap[r]], F(w[r]), F(contrib));
        }

        rep.Table("Дефаззификация", ["Показатель", "Значение"], numeric: [false, true])
           .Row("Узлов сетки", grid.ToString())
           .Row("Шаг сетки, %", F(( PowMax - PowMin) / (grid - 1.0)))
           .Row("Площадь под μ_agg", F(area))
           .Row("Центр тяжести, %", F(crisp));

        var sb = new StringBuilder();
        sb.AppendLine($"{method}: вход {temp:F2} °C, узлов сетки {grid}");
        sb.AppendLine();
        for (int r = 0; r < 3; r++)
            sb.AppendLine($"  R{r + 1}: если {InputTerms[r]} -> {OutputTerms[RuleMap[r]]}   w = {w[r]:F4}");
        sb.AppendLine();
        sb.AppendLine($"Максимум агрегированного множества: {agg.Max():F4}");
        sb.AppendLine($"Чёткий выход (центр тяжести): {crisp:F2} %");
        return sb.ToString();
    }

    // -- 3. Сугено -------------------------------------------------------

    private static string DoSugeno(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double temp  = N(p, "temp", 12);
        int    shape = I(p, "shape", 0);
        int    order = I(p, "order", 0);

        double[] w = FiringStrengths(temp, shape, overlap: 0.5);

        double crisp;
        double[] zi = new double[3];

        if (order == 0)
        {
            for (int r = 0; r < 3; r++) zi[r] = Singletons[RuleMap[r]];
            crisp = FuzzySugenoInference.WeightedAverageSingletons(w, zi);
        }
        else
        {
            // z_i = c_i + k_i * t: наклон задаёт, как правило реагирует на вход
            double[] slopes = [-1.5, -0.5, 0.5];
            var inputs = new Vector(1); inputs[0] = temp;

            var rules = new List<(double weight, Vector linearCoeffs, double constant)>(3);
            for (int r = 0; r < 3; r++)
            {
                var coeffs = new Vector(1); coeffs[0] = slopes[r];
                double c = Singletons[RuleMap[r]] - slopes[r] * 20;   // привязка к центру диапазона
                rules.Add((w[r], coeffs, c));
                zi[r] = c + slopes[r] * temp;
            }

            crisp = FuzzySugenoInference.TakagiSugenoOrder1(inputs, rules);
        }

        // -- График: синглтоны/линейные выходы правил и итог ------------------
        cv.ChartName = order == 0
            ? "Сугено 0-го порядка: синглтоны следствий"
            : "Сугено 1-го порядка: линейные следствия";
        Axes(cv, "мощность нагревателя, %", "вес правила w");

        var colors = new[] { HotColor, NormColor, ColdColor };
        for (int r = 0; r < 3; r++)
        {
            // Каждое правило — вертикальный «шип» высотой w в точке z_i:
            // именно так выглядит следствие-синглтон.
            var sx = new Vector(2); sx[0] = zi[r]; sx[1] = zi[r];
            var sy = new Vector(2); sy[0] = 0;     sy[1] = w[r];
            cv.AddPlot(sx, sy, $"R{r + 1}: {OutputTerms[RuleMap[r]]} z={zi[r]:F1} w={w[r]:F2}", colors[r], 3);
        }

        AddVerticalMarker(cv, crisp, Math.Max(1e-3, w.Max()), $"взвеш. среднее {crisp:F2} %", CrispColor);

        double wSum = w.Sum();
        rep.Metric("Метод", order == 0 ? "Сугено 0-го порядка" : "Сугено 1-го порядка")
           .Metric("Вход", temp, "°C")
           .Metric("Выход", crisp, "%", hint: "Взвешенное среднее следствий",
                   tone: MetricTone.Good, format: "F2")
           .Metric("Сумма весов", wSum, format: "F3",
                   tone: wSum < 1e-9 ? MetricTone.Bad : MetricTone.Neutral,
                   hint: "При нулевой сумме результат не определён и обнуляется")
           .Metric("Сработало правил", $"{w.Count(v => v > 1e-9)} из 3")
           .Note(order == 0
               ? "У Сугено нет дефаззификации как отдельного шага: следствия сразу чёткие, " +
                 "а выход — их взвешенное среднее. Поэтому метод дешевле Мамдани и удобен для управления."
               : "В 1-м порядке следствие каждого правила — линейная функция входа. " +
                 "Система становится кусочно-линейной: это классическая модель Такаги–Сугено.");

        var t1 = rep.Table("Правила Сугено",
            ["Правило", "Если", "Следствие zᵢ", "wᵢ", "wᵢ·zᵢ"],
            numeric: [false, false, true, true, true],
            note: "Итог = Σ wᵢzᵢ / Σ wᵢ.");

        for (int r = 0; r < 3; r++)
            t1.Row($"R{r + 1}", InputTerms[r], F2(zi[r]), F(w[r]), F(w[r] * zi[r]));

        var sb = new StringBuilder();
        sb.AppendLine($"Сугено {(order == 0 ? "0-го" : "1-го")} порядка: вход {temp:F2} °C");
        sb.AppendLine();
        for (int r = 0; r < 3; r++)
            sb.AppendLine($"  R{r + 1}: {InputTerms[r],-9} w={w[r]:F4}  z={zi[r]:F2}");
        sb.AppendLine();
        sb.AppendLine($"Σw = {wSum:F4}");
        sb.AppendLine($"Чёткий выход: {crisp:F2} %");
        return sb.ToString();
    }

    // -- 4. Цукамото -----------------------------------------------------

    private static string DoTsukamoto(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double temp  = N(p, "temp", 12);
        int    shape = I(p, "shape", 0);
        int    grid  = Math.Max(21, I(p, "grid", 201));

        double[] w = FiringStrengths(temp, shape, overlap: 0.5);

        // Цукамото требует монотонных следствий: берём сигмоидоподобные
        // нарастающие функции с разной крутизной вместо треугольных термов.
        var memberships = new List<Func<double, double>>(3);
        double[] centres = [20, 50, 80];
        for (int r = 0; r < 3; r++)
        {
            double centre = centres[RuleMap[r]];
            memberships.Add(z => 1.0 / (1.0 + Math.Exp(-(z - centre) / 8.0)));
        }

        double crisp = FuzzyTsukamotoInference.Infer(w, memberships, PowMin, PowMax,
            TsukamotoOutputMonotonicity.Increasing);

        var zi = new double[3];
        for (int r = 0; r < 3; r++)
            zi[r] = FuzzyTsukamotoInference.InverseMonotoneMembership(
                memberships[r], w[r], PowMin, PowMax, TsukamotoOutputMonotonicity.Increasing);

        // -- График: монотонные следствия и обратное отображение --------------
        cv.ChartName = "Цукамото: монотонные следствия и обратное отображение μ⁻¹(α)";
        Axes(cv, "мощность нагревателя, %", "степень принадлежности μ");

        Vector universe = PowerGrid(grid);
        var colors = new[] { HotColor, NormColor, ColdColor };

        for (int r = 0; r < 3; r++)
        {
            var y = new Vector(grid);
            for (int i = 0; i < grid; i++) y[i] = memberships[r](universe[i]);
            cv.AddPlot(universe, y, $"R{r + 1}: {OutputTerms[RuleMap[r]]}", colors[r], 2);

            if (w[r] > 1e-9)
            {
                // Точка (z_i, α_i) — то самое обратное отображение
                var px = new Vector(1); px[0] = zi[r];
                var py = new Vector(1); py[0] = w[r];
                cv.AddScatterMark6(px, py, $"z{r + 1} = {zi[r]:F1} при α={w[r]:F2}", SecondColor);
            }
        }

        AddVerticalMarker(cv, crisp, 1.0, $"взвеш. среднее {crisp:F2} %", CrispColor);

        double wSum = w.Sum();
        rep.Metric("Метод", "Цукамото")
           .Metric("Вход", temp, "°C")
           .Metric("Выход", crisp, "%", hint: "Взвешенное среднее по z_i = μ⁻¹(α_i)",
                   tone: MetricTone.Good, format: "F2")
           .Metric("Сумма весов", wSum, format: "F3",
                   tone: wSum < 1e-9 ? MetricTone.Bad : MetricTone.Neutral)
           .Metric("Сработало правил", $"{w.Count(v => v > 1e-9)} из 3")
           .Note("Следствия здесь — монотонные (сигмоидные) функции, а не треугольные термы: " +
                 "это обязательное требование метода, иначе обратная функция μ⁻¹ неоднозначна. " +
                 "Обратное значение ищется бисекцией за 48 итераций.");

        var t1 = rep.Table("Обратное отображение по правилам",
            ["Правило", "Если", "αᵢ (вес)", "zᵢ = μ⁻¹(αᵢ)", "αᵢ·zᵢ"],
            numeric: [false, false, true, true, true],
            note: "Итог = Σ αᵢzᵢ / Σ αᵢ. При α = 0 обратная функция упирается в левую границу универсума.");

        for (int r = 0; r < 3; r++)
            t1.Row($"R{r + 1}", InputTerms[r], F(w[r]), F2(zi[r]), F(w[r] * zi[r]));

        var sb = new StringBuilder();
        sb.AppendLine($"Цукамото: вход {temp:F2} °C");
        sb.AppendLine();
        for (int r = 0; r < 3; r++)
            sb.AppendLine($"  R{r + 1}: {InputTerms[r],-9} α={w[r]:F4}  z=μ⁻¹(α)={zi[r]:F2}");
        sb.AppendLine();
        sb.AppendLine($"Σα = {wSum:F4}");
        sb.AppendLine($"Чёткий выход: {crisp:F2} %");
        return sb.ToString();
    }

    // -- 5. Сравнение четырёх методов ------------------------------------

    private static string DoCompare(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
    {
        double temp      = N(p, "temp", 12);
        int    shape     = I(p, "shape", 0);
        int    sweep     = Math.Max(21, I(p, "sweep", 81));
        int    highlight = I(p, "highlight", 0);

        var x = new Vector(sweep);
        for (int i = 0; i < sweep; i++) x[i] = TempMin + (TempMax - TempMin) * i / (sweep - 1.0);

        var curves = new Vector[4];
        for (int m = 0; m < 4; m++) curves[m] = new Vector(sweep);

        for (int i = 0; i < sweep; i++)
        {
            var outs = InferAll(x[i], shape);
            for (int m = 0; m < 4; m++) curves[m][i] = outs[m];
        }

        string[] names = ["Мамдани", "Ларсен", "Сугено", "Цукамото"];
        var colors = new[] { AggColor, ColdColor, NormColor, SecondColor };

        cv.ChartName = "Характеристика управления: вход → выход для четырёх схем";
        Axes(cv, "температура, °C", "мощность нагревателя, %");
        for (int m = 0; m < 4; m++)
            cv.AddPlot(x, curves[m], names[m], colors[m], m == highlight ? 4 : 2);

        var atPoint = InferAll(temp, shape);
        AddVerticalMarker(cv, temp, PowMax, $"вход {F2(temp)} °C", CrispColor);

        // Максимальное расхождение между методами по всей развёртке
        double maxSpread = 0; double spreadAt = x[0];
        for (int i = 0; i < sweep; i++)
        {
            double lo = double.MaxValue, hi = double.MinValue;
            for (int m = 0; m < 4; m++)
            {
                lo = Math.Min(lo, curves[m][i]);
                hi = Math.Max(hi, curves[m][i]);
            }
            if (hi - lo > maxSpread) { maxSpread = hi - lo; spreadAt = x[i]; }
        }

        double spreadHere = atPoint.Max() - atPoint.Min();

        rep.Metric($"{names[highlight]} при {F2(temp)} °C", atPoint[highlight], "%",
                   tone: MetricTone.Good, format: "F2")
           .Metric("Разброс здесь", spreadHere, "%",
                   hint: "Разница между максимальным и минимальным выходом четырёх схем",
                   tone: spreadHere > 10 ? MetricTone.Warn : MetricTone.Good, format: "F2")
           .Metric("Максимальный разброс", maxSpread, "%", format: "F2",
                   hint: "По всей развёртке входа")
           .Metric("Достигается при", spreadAt, "°C", format: "F1")
           .Metric("Точек развёртки", sweep)
           .Note("Все четыре схемы работают на одной базе правил, поэтому расхождение вызвано " +
                 "исключительно способом импликации и дефаззификации. Мамдани и Ларсен обычно " +
                 "близки, Сугено даёт более гладкую характеристику, Цукамото — самую пологую " +
                 "из-за сигмоидных следствий.");

        var t1 = rep.Table($"Выход методов при входе {F2(temp)} °C",
            ["Метод", "Выход, %", "Отклонение от Мамдани, %"],
            numeric: [false, true, true]);
        for (int m = 0; m < 4; m++)
            t1.Row(names[m], F2(atPoint[m]), F2(atPoint[m] - atPoint[0]));

        var t2 = rep.Table("Характеристика по диапазону входа",
            ["Вход, °C", "Мамдани", "Ларсен", "Сугено", "Цукамото", "Разброс"],
            numeric: [true, true, true, true, true, true],
            note: "Показан каждый десятый узел развёртки, чтобы таблица оставалась читаемой.");

        for (int i = 0; i < sweep; i += Math.Max(1, sweep / 10))
        {
            double lo = double.MaxValue, hi = double.MinValue;
            for (int m = 0; m < 4; m++) { lo = Math.Min(lo, curves[m][i]); hi = Math.Max(hi, curves[m][i]); }
            t2.Row(F2(x[i]), F2(curves[0][i]), F2(curves[1][i]), F2(curves[2][i]), F2(curves[3][i]), F2(hi - lo));
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Сравнение схем вывода, вход {temp:F2} °C, точек развёртки {sweep}");
        sb.AppendLine();
        for (int m = 0; m < 4; m++)
            sb.AppendLine($"  {names[m],-10} = {atPoint[m]:F2} %");
        sb.AppendLine();
        sb.AppendLine($"Максимальный разброс по диапазону: {maxSpread:F2} % при {spreadAt:F1} °C");
        return sb.ToString();
    }

    /// <summary>Выход всех четырёх схем для одного значения входа.</summary>
    private static double[] InferAll(double temp, int shape)
    {
        const int grid = 201;
        double[] w = FiringStrengths(temp, shape, overlap: 0.5);

        Vector universe = PowerGrid(grid);
        var terms = new List<Vector>(3);
        for (int r = 0; r < 3; r++) terms.Add(OutputTermSamples(RuleMap[r], universe));

        double mamdani = FuzzyMamdaniInference.InferCentroid(w, terms, universe);
        double larsen  = FuzzyLarsenInference.InferCentroid(w, terms, universe);

        var singles = new double[3];
        for (int r = 0; r < 3; r++) singles[r] = Singletons[RuleMap[r]];
        double sugeno = FuzzySugenoInference.WeightedAverageSingletons(w, singles);

        var memberships = new List<Func<double, double>>(3);
        double[] centres = [20, 50, 80];
        for (int r = 0; r < 3; r++)
        {
            double centre = centres[RuleMap[r]];
            memberships.Add(z => 1.0 / (1.0 + Math.Exp(-(z - centre) / 8.0)));
        }
        double tsukamoto = FuzzyTsukamotoInference.Infer(w, memberships, PowMin, PowMax,
            TsukamotoOutputMonotonicity.Increasing);

        return [mamdani, larsen, sugeno, tsukamoto];
    }
}
