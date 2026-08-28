using AI.DataStructs.Algebraic;
using AI.Fuzzy.Inference;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>fuzzy</c>: нечёткая логика и вывод по Мамдани.
/// </summary>
/// <remarks>
/// Терм задаётся вектором значений принадлежности на общей сетке (универсуме), а не объектом
/// с методами. Так правило вывода записывается одной строкой, а сетка остаётся одна на весь
/// вывод — что и требуется для дефаззификации центром тяжести.
/// </remarks>
[ScriptModule("fuzzy", "Нечёткая логика: функции принадлежности, операции, вывод по Мамдани", Version = "0.1")]
public static class FuzzyModule
{
    [ScriptFn("universe", "Сетка значений, на которой заданы термы", Example = "fuzzy.universe(0, 100, n: 101)")]
    public static Vector Universe(
        IScriptContext context,
        [ScriptParam("начало")] double from,
        [ScriptParam("конец")] double to,
        [ScriptParam("число точек")] int n = 101)
    {
        if (n < 2) throw new ScriptError(DiagnosticCodes.BadOperand, "fuzzy.universe: нужно не меньше двух точек");

        context.CountAllocation(n);

        var result = new Vector(n);
        double step = (to - from) / (n - 1);

        for (int i = 0; i < n; i++) result[i] = from + (step * i);

        return result;
    }

    [ScriptFn("triangle", "Треугольный терм на сетке", Example = "fuzzy.triangle(u, a: 0, b: 20, c: 40)")]
    public static Vector Triangle(
        [ScriptParam("сетка значений")] Vector universe,
        [ScriptParam("левое основание")] double a,
        [ScriptParam("вершина")] double b,
        [ScriptParam("правое основание")] double c)
        => Shape(universe, x => FuzzyMembershipShapes.Triangular(x, a, b, c));

    [ScriptFn("trapezoid", "Трапециевидный терм на сетке", Example = "fuzzy.trapezoid(u, a: 0, b: 10, c: 30, d: 40)")]
    public static Vector Trapezoid(
        [ScriptParam("сетка значений")] Vector universe,
        [ScriptParam("левое основание")] double a,
        [ScriptParam("левая вершина")] double b,
        [ScriptParam("правая вершина")] double c,
        [ScriptParam("правое основание")] double d)
        => Shape(universe, x => FuzzyMembershipShapes.Trapezoidal(x, a, b, c, d));

    [ScriptFn("gauss", "Гауссов терм на сетке", Example = "fuzzy.gauss(u, center: 20, width: 5)")]
    public static Vector Gauss(
        [ScriptParam("сетка значений")] Vector universe,
        [ScriptParam("центр")] double center,
        [ScriptParam("ширина")] double width)
    {
        if (width <= 0) throw new ScriptError(DiagnosticCodes.BadOperand, "fuzzy.gauss: ширина должна быть больше нуля");

        return Shape(universe, x => Math.Exp(-((x - center) * (x - center)) / (2 * width * width)));
    }

    [ScriptFn("and", "Пересечение термов (минимум)", Example = "fuzzy.and(a, b)")]
    public static Vector And(
        [ScriptParam("первый терм")] Vector a,
        [ScriptParam("второй терм")] Vector b)
        => Combine(a, b, Math.Min, "fuzzy.and");

    [ScriptFn("or", "Объединение термов (максимум)", Example = "fuzzy.or(a, b)")]
    public static Vector Or(
        [ScriptParam("первый терм")] Vector a,
        [ScriptParam("второй терм")] Vector b)
        => Combine(a, b, Math.Max, "fuzzy.or");

    [ScriptFn("not", "Дополнение терма", Example = "fuzzy.not(a)")]
    public static Vector Not([ScriptParam("терм")] Vector a) => a.Transform(value => 1 - value);

    /// <summary>
    /// Степень принадлежности значения терму.
    /// </summary>
    /// <remarks>
    /// Значение между узлами сетки интерполируется линейно: сетка — это дискретизация
    /// непрерывного терма, и возвращать ближайший узел значило бы терять точность там, где
    /// её нетрудно сохранить.
    /// </remarks>
    [ScriptFn("degree", "Степень принадлежности значения терму", Example = "fuzzy.degree(u, term: hot, at: 25)")]
    public static double Degree(
        [ScriptParam("сетка значений")] Vector universe,
        [ScriptParam("терм")] Vector term,
        [ScriptParam("значение")] double at)
    {
        RequireSameLength(universe, term, "fuzzy.degree");

        if (universe.Count == 0) return 0;
        if (at <= universe[0]) return term[0];
        if (at >= universe[^1]) return term[^1];

        for (int i = 1; i < universe.Count; i++)
        {
            if (at > universe[i]) continue;

            double span = universe[i] - universe[i - 1];

            if (span == 0) return term[i];

            double weight = (at - universe[i - 1]) / span;

            return term[i - 1] + (weight * (term[i] - term[i - 1]));
        }

        return term[^1];
    }

    [ScriptFn("defuzzify", "Чёткое значение терма методом центра тяжести",
        Example = "fuzzy.defuzzify(u, term: result)")]
    public static double Defuzzify(
        [ScriptParam("сетка значений")] Vector universe,
        [ScriptParam("терм")] Vector term)
    {
        RequireSameLength(universe, term, "fuzzy.defuzzify");

        return FuzzyMamdaniInference.DefuzzifyCentroid(universe, term);
    }

    /// <summary>
    /// Вывод по Мамдани: каждое правило со своим весом обрезает свой выходной терм.
    /// </summary>
    /// <remarks>
    /// Веса и термы передаются двумя списками одинаковой длины, а не списком пар: правило
    /// «если … то …» в скрипте естественно распадается на условие и следствие, и держать их
    /// вместе значило бы заводить ещё один тип ради одного вызова.
    /// </remarks>
    [ScriptFn("infer", "Вывод по Мамдани: чёткий результат по весам правил и выходным термам",
        Example = "fuzzy.infer(u, weights: <0.3, 0.8>, terms: [slow, fast])")]
    public static double Infer(
        [ScriptParam("сетка значений")] Vector universe,
        [ScriptParam("веса правил: степени выполнения условий")] Vector weights,
        [ScriptParam("выходные термы правил")] ScriptList terms)
    {
        if (weights.Count != terms.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"fuzzy.infer: {weights.Count} весов и {terms.Count} термов",
                "каждому правилу нужны свой вес и свой выходной терм");
        }

        if (weights.Count == 0) throw new ScriptError(DiagnosticCodes.SizeMismatch, "fuzzy.infer: правил нет");

        var samples = new List<Vector>(terms.Count);
        var ruleWeights = new List<double>(weights.Count);

        for (int i = 0; i < terms.Count; i++)
        {
            var term = (Vector)Marshaller.ToClr(terms[i], typeof(Vector), $"fuzzy.infer: терм {i}")!;

            RequireSameLength(universe, term, $"fuzzy.infer: терм {i}");

            samples.Add(term);
            ruleWeights.Add(weights[i]);
        }

        return FuzzyMamdaniInference.InferCentroid(ruleWeights, samples, universe);
    }

    [ScriptFn("aggregate", "Объединяет обрезанные выходные термы правил в один",
        Example = "fuzzy.aggregate(weights: <0.3, 0.8>, terms: [slow, fast])")]
    public static Vector Aggregate(
        [ScriptParam("веса правил")] Vector weights,
        [ScriptParam("выходные термы правил")] ScriptList terms)
    {
        if (weights.Count != terms.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"fuzzy.aggregate: {weights.Count} весов и {terms.Count} термов");
        }

        var samples = new List<Vector>(terms.Count);
        var ruleWeights = new List<double>(weights.Count);

        for (int i = 0; i < terms.Count; i++)
        {
            samples.Add((Vector)Marshaller.ToClr(terms[i], typeof(Vector), $"fuzzy.aggregate: терм {i}")!);
            ruleWeights.Add(weights[i]);
        }

        return FuzzyMamdaniInference.AggregateMaxMin(ruleWeights, samples);
    }

    private static Vector Shape(Vector universe, Func<double, double> membership)
    {
        var result = new Vector(universe.Count);

        for (int i = 0; i < universe.Count; i++) result[i] = membership(universe[i]);

        return result;
    }

    private static Vector Combine(Vector a, Vector b, Func<double, double, double> operation, string what)
    {
        RequireSameLength(a, b, what);

        var result = new Vector(a.Count);

        for (int i = 0; i < a.Count; i++) result[i] = operation(a[i], b[i]);

        return result;
    }

    private static void RequireSameLength(Vector a, Vector b, string what)
    {
        if (a.Count == b.Count) return;

        throw new ScriptError(
            DiagnosticCodes.SizeMismatch,
            $"{what}: длины {a.Count} и {b.Count} не совпадают",
            "термы задаются на одной сетке значений — той же, что создана fuzzy.universe");
    }
}
