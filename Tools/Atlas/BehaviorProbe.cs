using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using System.Collections;
using System.Globalization;
using Complex = System.Numerics.Complex;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AiFramework.Tools.Atlas;

/// <summary>Строка таблицы расхождений.</summary>
/// <param name="Input">Вход.</param>
/// <param name="A">Что вернул первый метод.</param>
/// <param name="B">Что вернул второй метод.</param>
/// <param name="Edge">Крайний случай.</param>
public sealed record BehaviorRow(string Input, string A, string B, bool Edge);

/// <summary>Итог исполнения пары.</summary>
/// <param name="Verdict">Один из вердиктов <see cref="BehaviorProbe"/>.</param>
/// <param name="Relation">Связь выходов на обычных входах, если найдена.</param>
/// <param name="Mapping">Как аргументы первого метода переданы второму.</param>
/// <param name="Cases">Сколько входов исполнено.</param>
/// <param name="Compared">На скольких обычных входах оба метода вернули значение.</param>
/// <param name="Deviation">Наибольшее относительное отклонение выходов при связи «равны».</param>
/// <param name="Rows">Входы, на которых методы расходятся.</param>
/// <param name="Note">Почему не проверено или оговорка.</param>
public sealed record BehaviorResult(
    string Verdict, string Relation, string Mapping, int Cases, int Compared, double Deviation,
    IReadOnlyList<BehaviorRow> Rows, string Note)
{
    /// <summary>Пара не исполнена.</summary>
    public static BehaviorResult Unchecked(string why) => new(BehaviorProbe.NotChecked, "", "", 0, 0, 0, [], why);
}

/// <summary>
/// Сравнение двух методов исполнением: одни и те же входы, выходы сравниваются с допуском,
/// между выходами ищется связь.
/// </summary>
/// <remarks>
/// <para>
/// Аргументы второго метода берутся из аргументов первого того же рода (ряд, таблица,
/// число…); перестановки перебираются, необязательные параметры получают значения по
/// умолчанию. Из переходников выбирается тот, при котором выходы согласуются лучше.
/// </para>
/// <para>
/// Экземплярный метод вызывается на объекте из конструктора без параметров, а если его нет —
/// на объекте без конструктора: закрытые помощники часто не трогают полей. Не вышло —
/// исключение, и пара остаётся непроверенной.
/// </para>
/// <para>
/// Исполняются только методы над данными: параметры и результат — числа, ряды, таблицы,
/// строки. Имена вроде <c>Save</c>, <c>Load</c>, <c>Send</c> и проекты графики, сети и
/// нейросетей на GPU не исполняются вовсе. Открытый экземплярный метод работает с
/// состоянием объекта, и на объекте без конструктора вернёт мусор, а не исключение: такие
/// не исполняются, как и у детектора, функцией считается статический или закрытый метод.
/// </para>
/// </remarks>
public static class BehaviorProbe
{
    /// <summary>Выходы совпадают на всех входах.</summary>
    public const string Duplicate = "поведенческий дубль";

    /// <summary>Выходы совпадают на обычных входах, крайние случаи расходятся.</summary>
    public const string DuplicateExceptEdges = "дубль, кроме крайних случаев";

    /// <summary>Выходы связаны преобразованием: знак, множитель, сдвиг, нормировка.</summary>
    public const string Related = "выходы связаны";

    /// <summary>На обычных входах выходы различаются и не связаны.</summary>
    public const string Different = "выходы различаются";

    /// <summary>Не исполнено.</summary>
    public const string NotChecked = "не проверено";

    /// <summary>Переходник пары методов без аргументов.</summary>
    public const string NoArguments = "без аргументов";

    private const int MaxMappings = 24;
    private const int MaxRows = 8;
    private const int MinCompared = 3;

    private static readonly string[] DeniedWords =
        ["Save", "Load", "Write", "Read", "Delete", "Download", "Upload", "Send", "Show", "Open", "Plot", "Print",
         "Export", "Import", "Start", "Execute", "Launch", "Connect", "Draw", "Play", "Record", "Sleep", "Wait", "File", "Path", "Url", "Http"];

    private static readonly string[] DeniedProjects = ["Charts", "LLM", "Llm", "Media", "Gpu", "WinForms", "Avalonia", "ImageEditor", "Onnx"];

    /// <summary>Исполняет оба метода на одних входах и выносит вердикт.</summary>
    public static BehaviorResult Compare(MethodInfo a, MethodInfo b, int seed = 1, int count = 40)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (Refusal(a) is string whyA) return BehaviorResult.Unchecked("A: " + whyA);
        if (Refusal(b) is string whyB) return BehaviorResult.Unchecked("B: " + whyB);

        ParameterInfo[] pa = a.GetParameters(), pb = b.GetParameters();
        List<int[]> mappings = Mappings(pa, pb);
        if (mappings.Count == 0) return BehaviorResult.Unchecked("аргументы B не получить из аргументов A: разные роды входов");

        IReadOnlyList<InputCase> cases = BehaviorInputs.Cases([.. pa.Select(p => BehaviorInputs.KindOf(p.ParameterType))], seed, count);
        double tolerance = UsesFloat(a) || UsesFloat(b) ? OutputRelation.FloatRelTol : OutputRelation.RelTol;
        var outcomesA = new Dictionary<string, Outcome[]>();

        BehaviorResult? best = null;
        foreach (int[] map in mappings)
        {
            // Параметр A, который не дошёл до B, у обоих получает значение по умолчанию.
            bool[] passed = [.. pa.Select((p, i) => map.Contains(i))];
            string key = string.Concat(passed.Select(flag => flag ? '1' : '0'));
            if (!outcomesA.TryGetValue(key, out Outcome[]? outA))
                outcomesA[key] = outA = Settle([.. cases.Select(input => Invoke(a, [.. input.Values.Select((v, i) => passed[i] ? v : Default(pa[i]))]))]);

            Outcome[] outB = Settle([.. cases.Select(input => Invoke(b, [.. map.Select((source, j) => source < 0 ? Default(pb[j]) : input.Values[source])]))]);
            BehaviorResult result = Judge(cases, outA, outB, Describe(map, pa, pb), tolerance);
            if (best is null || Rank(result) < Rank(best)) best = result;
            if (best.Verdict == Duplicate) break;
        }

        return best!;
    }

    /// <summary>Почему метод не исполняется; <c>null</c> — исполняется.</summary>
    public static string? Refusal(MethodInfo method)
    {
        Type type = method.DeclaringType!;

        if (method.ContainsGenericParameters || type.ContainsGenericParameters) return "обобщённый метод";
        if (method.IsAbstract || (!method.IsStatic && (type.IsAbstract || type.IsInterface))) return "абстрактный метод";
        if (!method.IsStatic && !method.IsPrivate) return "открытый экземплярный метод зависит от состояния объекта";
        if (RefusedAssembly(type.Assembly.GetName().Name!) is string why) return why;
        if (DeniedWords.Any(word => method.Name.Contains(word, StringComparison.Ordinal))) return $"имя {method.Name} похоже на действие с внешним эффектом";
        if (method.GetParameters().FirstOrDefault(p => p.ParameterType.IsByRef || BehaviorInputs.KindOf(p.ParameterType) == ValueKind.None) is { } parameter)
            return $"параметр {parameter.Name} типа {parameter.ParameterType.Name} не генерируется";
        if (method.ReturnType != typeof(void) && !Comparable(method.ReturnType)) return $"результат типа {method.ReturnType.Name} не сравнивается";

        return null;
    }

    /// <summary>Почему методы сборки не исполняются, не загружая её; <c>null</c> — исполняются.</summary>
    public static string? RefusedAssembly(string name) =>
        DeniedProjects.Any(word => name.Contains(word, StringComparison.Ordinal)) ? "проект с внешними эффектами" : null;

    private static BehaviorResult Judge(IReadOnlyList<InputCase> cases, Outcome[] outA, Outcome[] outB, string mapping, double tolerance)
    {
        int[] regular = [.. Enumerable.Range(0, cases.Count).Where(i => !cases[i].Edge)];
        int[] compared = [.. regular.Where(i => outA[i].Value && outB[i].Value)];

        if (compared.Length < MinCompared)
        {
            string why = regular.All(i => !outA[i].Value) ? $"A не исполняется на сгенерированных входах: {Failure(outA, regular)}"
                : regular.All(i => !outB[i].Value) ? $"B не исполняется на сгенерированных входах: {Failure(outB, regular)}"
                : "оба метода вернули значение меньше чем на трёх входах";
            return new BehaviorResult(NotChecked, "", mapping, cases.Count, compared.Length, 0, [], why);
        }

        Relation? relation = compared.All(i => outA[i].Text == null && outB[i].Text == null)
            ? OutputRelation.Fit([.. compared.Select(i => Pair(cases[i], outA[i], outB[i]))], tolerance)
            : compared.All(i => outA[i].Text == outB[i].Text) ? OutputRelation.Identity(tolerance) : null;

        // Связи нет — в таблицу идут входы, где выходы просто не равны.
        Relation check = relation ?? OutputRelation.Identity(tolerance);
        bool Holds(int i) => (!outA[i].Value && !outB[i].Value)
            || (outA[i].Value && outB[i].Value
                && (outA[i].Text != null || outB[i].Text != null ? outA[i].Text == outB[i].Text : check.Holds(Pair(cases[i], outA[i], outB[i]))));

        int[] broken = [.. Enumerable.Range(0, cases.Count).Where(i => !Holds(i))];
        bool regularHolds = relation != null && broken.All(i => cases[i].Edge);

        string verdict = !regularHolds ? Different
            : !relation!.IsIdentity ? Related
            : broken.Length == 0 ? Duplicate
            : DuplicateExceptEdges;

        double deviation = relation?.IsIdentity == true
            ? compared.Where(i => outA[i].Text == null).Select(i => OutputRelation.Deviation(Pair(cases[i], outA[i], outB[i]))).DefaultIfEmpty(0).Max()
            : 0;

        BehaviorRow[] rows = [.. broken.OrderBy(i => cases[i].Edge).Take(MaxRows)
            .Select(i => new BehaviorRow(BehaviorInputs.Describe(cases[i]), outA[i].Show(outB[i]), outB[i].Show(outA[i]), cases[i].Edge))];

        return new BehaviorResult(verdict, relation?.Name ?? "", mapping, cases.Count, compared.Length, deviation, rows, "");
    }

    private static int Rank(BehaviorResult result) => result.Verdict switch
    {
        Duplicate => 0,
        DuplicateExceptEdges => 1,
        Related => 2,
        Different => 3,
        _ => 4,
    } * 1000 + result.Rows.Count - result.Compared;

    private static OutputPair Pair(InputCase input, Outcome a, Outcome b) => new(a.Numbers!, b.Numbers!, input.Size);

    private static string Failure(Outcome[] outcomes, int[] indices) =>
        string.Join(", ", indices.Select(i => outcomes[i].Error).Distinct().Take(3));

    /// <summary>
    /// Переходники: для каждого параметра B — параметр A того же рода или значение по
    /// умолчанию; каждый обязательный параметр A должен дойти до B.
    /// </summary>
    private static List<int[]> Mappings(ParameterInfo[] pa, ParameterInfo[] pb)
    {
        var result = new List<int[]>();
        var map = new int[pb.Length];
        var used = new bool[pa.Length];

        void Assign(int j)
        {
            if (result.Count >= MaxMappings) return;
            if (j == pb.Length)
            {
                if (pa.Where((p, i) => !used[i]).All(p => p.HasDefaultValue)) result.Add((int[])map.Clone());
                return;
            }

            ValueKind kind = BehaviorInputs.KindOf(pb[j].ParameterType);
            for (int i = 0; i < pa.Length; i++)
            {
                if (used[i] || BehaviorInputs.KindOf(pa[i].ParameterType) != kind) continue;
                used[i] = true;
                map[j] = i;
                Assign(j + 1);
                used[i] = false;
            }

            if (pb[j].HasDefaultValue)
            {
                map[j] = -1;
                Assign(j + 1);
            }
        }

        Assign(0);
        return result;
    }

    private static string Describe(int[] map, ParameterInfo[] pa, ParameterInfo[] pb) =>
        pb.Length == 0 ? NoArguments
            : string.Join(", ", pb.Select((p, j) => map[j] < 0 ? $"{p.Name} = по умолчанию" : $"{p.Name} ← {pa[map[j]].Name}"));

    private static object? Default(ParameterInfo parameter) =>
        parameter.DefaultValue is null or DBNull && parameter.ParameterType.IsValueType
            ? Activator.CreateInstance(parameter.ParameterType)
            : parameter.DefaultValue;

    private static Outcome Invoke(MethodInfo method, object?[] values)
    {
        ParameterInfo[] parameters = method.GetParameters();
        object?[] arguments = [.. values.Select((value, i) => value is null || i >= parameters.Length ? value : BehaviorInputs.Convert(value, parameters[i].ParameterType))];

        try
        {
            object? target = method.IsStatic ? null : Target(method.DeclaringType!);
            object?[] mutable = [.. arguments.Where(argument => argument is Array or IList)];
            double[]? before = method.ReturnType == typeof(void) ? Outcome.Of(mutable).Numbers : null;

            object? result = method.Invoke(target, arguments);
            if (before is null) return Outcome.Of(result);

            // Метод без результата сравнивается по входам после вызова.
            Outcome after = Outcome.Of(mutable);
            return after with { Untouched = after.Numbers is { } numbers && numbers.SequenceEqual(before) };
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            return Outcome.Failed("исключение " + error.InnerException.GetType().Name);
        }
        catch (Exception error) when (error is ArgumentException or MemberAccessException or NotSupportedException or InvalidOperationException)
        {
            return Outcome.Failed("исключение " + error.GetType().Name);
        }
    }

    /// <summary>
    /// Метод без результата, который ни на одном входе не изменил входы, меняет только своё
    /// состояние: сравнивать у него нечего, иначе любые два таких метода «совпадут».
    /// </summary>
    private static Outcome[] Settle(Outcome[] outcomes) =>
        outcomes.Any(o => o.Value) && outcomes.Where(o => o.Value).All(o => o.Untouched)
            ? [.. outcomes.Select(o => o.Value ? Outcome.Failed("ничего не вернул и не изменил входы") : o)]
            : outcomes;

    private static bool UsesFloat(MethodInfo method) =>
        method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType).Any(type => type == typeof(float) || type == typeof(float[]));

    private static object Target(Type type) =>
        type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes) != null || type.IsValueType
            ? Activator.CreateInstance(type, nonPublic: true)!
            : RuntimeHelpers.GetUninitializedObject(type);

    private static bool Comparable(Type type) =>
        type.IsPrimitive || type == typeof(decimal) || type == typeof(string) || type == typeof(Complex)
        || (type.IsArray && type.GetElementType() != typeof(string) && Comparable(type.GetElementType()!))
        || typeof(IAlgebraicStructure<double>).IsAssignableFrom(type) || typeof(IComplexStructure).IsAssignableFrom(type)
        || typeof(IEnumerable<double>).IsAssignableFrom(type) || typeof(IEnumerable<int>).IsAssignableFrom(type)
        || (type.IsGenericType && type.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal) && type.GetGenericArguments().All(Comparable));

    /// <summary>Выход вызова: числа (развёрнутые), текст или исключение.</summary>
    private sealed record Outcome(double[]? Numbers, string? Text, string? Error, bool Untouched = false)
    {
        public bool Value => Error == null;

        public static Outcome Failed(string error) => new(null, null, error);

        public static Outcome Of(object? value)
        {
            if (value is string text) return new Outcome([], text, null);

            var numbers = new List<double>();
            return Flatten(value, numbers) ? new Outcome([.. numbers], null, null) : Failed("результат не сравнивается");
        }

        /// <summary>Запись для таблицы; у двух текстов — окно вокруг первого различия и длина.</summary>
        public string Show(Outcome other) => Error ?? (Text != null ? Window(Text, other.Text)
            : Numbers!.Length == 1 ? Number(Numbers[0])
            : $"[{string.Join(", ", Numbers.Take(4).Select(Number))}{(Numbers.Length > 4 ? ", …" : "")}] ({Numbers.Length})");

        private static string Number(double value) => value.ToString("G6", CultureInfo.InvariantCulture);

        private static string Window(string text, string? other)
        {
            int at = 0;
            if (other != null) while (at < text.Length && at < other.Length && text[at] == other[at]) at++;

            int start = Math.Max(0, Math.Min(at, text.Length) - 15);
            string part = text.Substring(start, Math.Min(40, text.Length - start));
            return (start > 0 ? "«…" : "«") + part + (start + part.Length < text.Length ? "…»" : "»") + $" (длина {text.Length})";
        }

        private static bool Flatten(object? value, List<double> numbers)
        {
            switch (value)
            {
                case null: return true;
                case bool flag: numbers.Add(flag ? 1 : 0); return true;
                case char symbol: numbers.Add(symbol); return true;
                case string: return false;
                case Complex z: numbers.Add(z.Real); numbers.Add(z.Imaginary); return true;
                case IConvertible number when value.GetType().IsPrimitive || value is decimal: numbers.Add(number.ToDouble(CultureInfo.InvariantCulture)); return true;
                case IAlgebraicStructure<double> structure: numbers.AddRange(structure.Data); return true;
                case IComplexStructure complex: return complex.Data.All(z => Flatten(z, numbers));
                case ITuple tuple: return Enumerable.Range(0, tuple.Length).All(i => Flatten(tuple[i], numbers));
                case IEnumerable items: return items.Cast<object?>().All(item => Flatten(item, numbers));
                default: return false;
            }
        }
    }
}
