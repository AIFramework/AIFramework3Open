using System.Globalization;
using AI.ClassicMath.Calculator;
using AI.ClassicMath.Calculator.Libs;
using AI.Solvers.Math.CAS;
using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Solvers;

namespace AI.Solvers.Math.Libs;

/// <summary>
/// Символьная математика для скриптового вычислителя: производные, интегралы, пределы, ряды,
/// преобразования и решение уравнений.
/// </summary>
/// <remarks>
/// Библиотека живёт ЗДЕСЬ, а не в самом вычислителе: решатели ссылаются на AI.ClassicMath, и
/// обратная ссылка замкнула бы круг. Поэтому состав функций собирает вызывающий —
/// <c>new Processor(new SymbolicLib())</c>, см. <see cref="AdvancedCalculator.Use"/>.
/// <para>
/// Выражения приходят и уходят СТРОКАМИ: у языка вычислителя нет символьных значений, а строки
/// он умеет и передавать, и печатать. Мост в числа — <c>evalf</c>: он вычисляет полученное
/// выражение в точке, и дальше с результатом работает обычная арифметика.
/// </para>
/// </remarks>
public sealed class SymbolicLib : IMathLib
{
    /// <summary>Области для справочника — общие у всех функций библиотеки.</summary>
    private static readonly List<string> Areas = ["Математика", "Символьные вычисления"];

    /// <inheritdoc />
    public string Name { get; set; } = "Символьная математика";

    /// <inheritdoc />
    public string Description { get; set; } =
        "Производные, интегралы, пределы, ряды Тейлора, преобразования Лапласа и Фурье, "
        + "решение уравнений. Выражения передаются строками, результат тоже строка; "
        + "evalf вычисляет выражение в точке и возвращает число.";

    /// <inheritdoc />
    public Dictionary<string, FunctionDefinition> GetFunctions() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["diff"] = Func("diff", -1,
                args => DerivativeSolver.FirstDerivative(Text(args, 0, "diff"), Var(args, 1)),
                "Вход: выражение [, переменная]. Выход: строка.",
                "Производная выражения по переменной (по умолчанию x).",
                """diff("x^2 + 3*x", "x")"""),

            ["diffn"] = Func("diffn", 3,
                args => DerivativeSolver.NthDerivative(
                    Text(args, 0, "diffn"), Var(args, 1), CastsVar.CastToInt32(args[2], "diffn")),
                "Вход: выражение, переменная, порядок. Выход: строка.",
                "Производная указанного порядка.",
                """diffn("x^4", "x", 2)"""),

            ["integrate"] = Func("integrate", -1,
                args => IntegralSolver.IndefiniteIntegral(Text(args, 0, "integrate"), Var(args, 1)),
                "Вход: выражение [, переменная]. Выход: строка.",
                "Неопределённый интеграл (в ответе есть постоянная C).",
                """integrate("x*exp(x)", "x")"""),

            ["defint"] = Func("defint", 4, Definite,
                "Вход: выражение, переменная, нижний предел, верхний предел. Выход: число.",
                "Определённый интеграл. Считается символьно, при неудаче — численно.",
                """defint("x^2", "x", 0, 1)"""),

            ["evalf"] = Func("evalf", 3, Value,
                "Вход: выражение, переменная, точка. Выход: число.",
                "Значение выражения в точке — мост из символьного ответа в обычную арифметику.",
                """evalf(diff("x^2", "x"), "x", 3)"""),

            ["limit"] = Func("limit", 3,
                args => AdvancedSolver.ComputeLimit(
                    Text(args, 0, "limit"), Var(args, 1), Point(args, 2, "limit")),
                "Вход: выражение, переменная, точка. Выход: строка.",
                "Предел выражения в точке.",
                """limit("sin(x)/x", "x", 0)"""),

            ["taylor"] = Func("taylor", -1,
                args => AdvancedSolver.TaylorSeries(
                    Text(args, 0, "taylor"), Var(args, 1), Point(args, 2, "taylor"), Terms(args, 3)),
                "Вход: выражение, переменная, точка [, число членов]. Выход: строка.",
                "Разложение в ряд Тейлора.",
                """taylor("exp(x)", "x", 0, 5)"""),

            ["solve"] = Func("solve", 1,
                args => AdvancedSolver.SolveEquation(Text(args, 0, "solve")),
                "Вход: уравнение со знаком =. Выход: строка с разбором и корнями.",
                "Решение уравнения: символьно до четвёртой степени, иначе численно.",
                """solve("x^2 - 4 = 0")"""),

            ["simplify"] = Func("simplify", 1,
                args => AlgebraicSimplifier
                    .Simplify(AdvancedMathExpression.Parse(Text(args, 0, "simplify")))
                    .ToString(),
                "Вход: выражение. Выход: строка.",
                "Алгебраическое упрощение выражения.",
                """simplify("x + x + 2*x")"""),

            ["laplace"] = Func("laplace", -1,
                args => AdvancedSolver.LaplaceTransform(Text(args, 0, "laplace"), Var(args, 1, "t")),
                "Вход: выражение [, переменная]. Выход: строка.",
                "Преобразование Лапласа (переменная по умолчанию t).",
                """laplace("exp(-2*t)", "t")"""),

            ["fourier"] = Func("fourier", -1,
                args => AdvancedSolver.FourierTransform(Text(args, 0, "fourier"), Var(args, 1)),
                "Вход: выражение [, переменная]. Выход: строка.",
                "Преобразование Фурье.",
                """fourier("exp(-x^2)", "x")"""),
        };

    // ── Разбор аргументов ───────────────────────────────────────────────────

    /// <summary>Обязательное выражение строкой.</summary>
    private static string Text(object[] args, int index, string funcName)
    {
        if (args.Length > index && args[index] is string text && text.Trim().Length > 0)
            return text;

        throw new ArgumentException(
            $$"""Функция '{{funcName}}' ожидает выражение строкой, например {{funcName}}("x^2", "x").""");
    }

    /// <summary>Необязательное имя переменной.</summary>
    private static string Var(object[] args, int index, string fallback = "x") =>
        args.Length > index && args[index] is string name && name.Trim().Length > 0
            ? name.Trim()
            : fallback;

    /// <summary>Точка: числом либо строкой (для inf и подобных).</summary>
    private static string Point(object[] args, int index, string funcName)
    {
        if (args.Length <= index)
            throw new ArgumentException($"Функция '{funcName}' ожидает точку третьим аргументом.");

        return args[index] is string text
            ? text.Trim()
            : CastsVar.CastToDouble(args[index], funcName).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Число членов ряда; по умолчанию столько же, сколько у решателя.</summary>
    private static int Terms(object[] args, int index) =>
        args.Length > index ? CastsVar.CastToInt32(args[index], "taylor") : 10;

    // ── Функции, которым мало одной строки ──────────────────────────────────

    /// <summary>
    /// Определённый интеграл числом.
    /// </summary>
    /// <remarks>
    /// Решатель отдаёт результат текстом и той же строкой сообщает о неудаче. Число мы обязаны
    /// вернуть числом (иначе с ним не посчитать дальше), а неудачу — отказом: текст «Ошибка…»,
    /// выданный за результат, модель примет за ответ и подставит в документ.
    /// </remarks>
    private static object Definite(object[] args)
    {
        var text = IntegralSolver.DefiniteIntegral(
            Text(args, 0, "defint"), Var(args, 1),
            CastsVar.CastToDouble(args[2], "defint"), CastsVar.CastToDouble(args[3], "defint"));

        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            return value;

        throw new ArgumentException($"Определённый интеграл не вычислен: {text}");
    }

    /// <summary>Значение выражения в точке.</summary>
    private static object Value(object[] args)
    {
        var expression = AdvancedMathExpression.Parse(Text(args, 0, "evalf"));
        var variables = new Dictionary<string, double>
        {
            [Var(args, 1)] = CastsVar.CastToDouble(args[2], "evalf"),
        };

        return ExpressionEvaluator.Evaluate(expression, variables);
    }

    // ── Сборка описания ─────────────────────────────────────────────────────

    /// <summary>
    /// Одна функция библиотеки.
    /// </summary>
    /// <remarks>
    /// Тело оборачивается инвариантной культурой: решатели печатают числа текущей культурой, и
    /// на русской локали ответ приезжает с запятой вместо точки. Такое число модель подставит в
    /// документ и передаст обратно в вычислитель, где оно уже не разберётся.
    /// </remarks>
    private static FunctionDefinition Func(
        string name, int argumentCount, Func<object[], object> body,
        string signature, string description, string example) =>
        new(argumentCount, args =>
        {
            var culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            try { return body(args); }
            finally { CultureInfo.CurrentCulture = culture; }
        })
        {
            Name = name,
            Description = new DescriptionFunction
            {
                Signature = signature,
                Description = description,
                AreaList = Areas,
                Example = example,
            },
        };
}
