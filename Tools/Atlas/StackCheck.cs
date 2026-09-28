using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Text;

namespace AiFramework.Tools.Atlas;

/// <summary>Итог проверки стека.</summary>
/// <param name="Kind"><c>C#</c> или <c>AIScript</c>.</param>
/// <param name="Passed">Прошла ли; <c>null</c> — проверить не удалось (обобщённый метод, не функция языка).</param>
/// <param name="Details">Код, который проверялся, и что сказал компилятор или проверка.</param>
public sealed record CheckOutcome(string Kind, bool? Passed, string Details);

/// <summary>
/// Проверка стека без запуска: собирается ли цепочка вызовов, а не только найдены ли шаги.
/// </summary>
/// <remarks>
/// <para>
/// C#: из шагов строится каркас «результат шага идёт во вход следующего», остальные
/// аргументы — <c>default</c>. Каркас компилируется Roslyn в памяти против собранных сборок.
/// Ошибка компиляции — это несостыковка типов, которую иначе нашли бы, только написав код.
/// </para>
/// <para>
/// AIScript: если все шаги — функции языка, из них собирается скрипт и проходит
/// <see cref="ScriptHost.Check(string, string, IReadOnlyCollection{string}?)"/>: те же имена,
/// аргументы и типы, что проверяются перед запуском настоящего скрипта.
/// </para>
/// </remarks>
public static class StackCheck
{
    /// <summary>Компилирует каркас C# из шагов стека.</summary>
    public static CheckOutcome CompileCSharp(StackPlan plan, TypeGraph graph, string root)
    {
        var code = new StringBuilder();
        string? previous = null;
        string? previousType = null;
        int i = 0;

        foreach (CodeUnit unit in plan.Steps.Where(step => step.Hit != null).Select(step => step.Hit!.Unit))
        {
            i++;
            string name = ApiSearch.QualifiedName(unit.Id);
            string type = GoldSet.TypeOf(unit);
            string member = name[(name.LastIndexOf('.') + 1)..];

            if (unit.Id.Contains('`') || unit.Kind == "operator")
                return new CheckOutcome("C#", null, $"шаг {i}: {UnitReport.ShortId(unit.Id)} — обобщённый метод или оператор, каркас не строится");

            string arguments = Arguments(unit, graph, previous, previousType);
            string receiver = previous != null && previousType != null && graph.Link(previousType, type).Distance == 0 ? previous : $"default({type})!";

            string call = unit.Kind == "ctor" ? $"new {type}({arguments})"
                : unit.IsStatic ? $"{type}.{member}({arguments})"
                : $"{receiver}.{member}({arguments})";

            if (unit.Returns == "void")
            {
                code.AppendLine($"        var r{i} = {receiver};");
                code.AppendLine($"        {call.Replace(receiver + ".", $"r{i}.", StringComparison.Ordinal)};");
                previous = $"r{i}";
                previousType = type;
            }
            else
            {
                code.AppendLine($"        var s{i} = {call};");
                previous = $"s{i}";
                previousType = unit.Returns;
            }
        }

        if (i == 0) return new CheckOutcome("C#", null, "в стеке нет ни одного найденного шага");

        string source = $$"""
            namespace AtlasCheck;

            internal static class Stack
            {
                internal static void Run()
                {
            {{code}}    }
            }
            """;

        CSharpCompilation compilation = CSharpCompilation.Create(
            "AtlasStack",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            References(root, plan.Steps.Where(step => step.Hit != null).Select(step => step.Hit!.Unit.Project)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable));

        Microsoft.CodeAnalysis.Diagnostic[] errors = [.. compilation.GetDiagnostics().Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)];

        return errors.Length == 0
            ? new CheckOutcome("C#", true, code.ToString().TrimEnd())
            : new CheckOutcome("C#", false, code.ToString().TrimEnd() + "\n" + string.Join('\n', errors.Take(3).Select(e => "  " + e.GetMessage())));
    }

    /// <summary>Собирает скрипт AIScript из шагов-функций языка и проверяет его.</summary>
    public static CheckOutcome CheckScript(StackPlan plan, ScriptHost host)
    {
        ScriptFunction[] functions = [.. plan.Steps
            .Where(step => step.Hit?.Unit.Script != null)
            .Select(step => host.Registry.Find(step.Hit!.Unit.Script!))
            .OfType<ScriptFunction>()];

        if (functions.Length == 0 || functions.Length != plan.Steps.Count(step => step.Hit != null))
            return new CheckOutcome("AIScript", null, "не все шаги — функции AIScript");

        ScriptType inputType = functions[0].Parameters.FirstOrDefault()?.Type ?? ScriptType.Any;
        var script = new StringBuilder().AppendLine($"let x0 = {Sample(inputType, "данные")}");
        string previous = "x0";
        ScriptType previousType = inputType;

        for (int i = 0; i < functions.Length; i++)
        {
            ScriptFunction function = functions[i];
            bool fed = false;
            var arguments = new List<string>();

            foreach (ScriptParameter parameter in function.Parameters.Where(p => !p.IsOptional && !p.IsVariadic))
            {
                if (!fed && TypeRules.Accepts(parameter.Type, previousType)) arguments.Add(previous);
                else if (!fed && TypeRules.Accepts(parameter.Type, inputType)) arguments.Add("x0");
                else
                {
                    arguments.Add(Sample(parameter.Type, parameter.Name));
                    continue;
                }

                fed = true;
            }

            script.AppendLine($"let x{i + 1} = {function.FullName}({string.Join(", ", arguments)})");
            previous = $"x{i + 1}";
            previousType = function.ReturnType;
        }

        script.Append($"emit результат = {previous}");

        CheckResult check = host.Check(script.ToString());
        return new CheckOutcome("AIScript", check.Success, script + (check.Success ? "" : "\n" + check.Render()));
    }

    /// <summary>Пример значения нужного типа; для составных — объявленный вход.</summary>
    private static string Sample(ScriptType type, string name) => type switch
    {
        ScriptType.Num => "2",
        ScriptType.Bool => "true",
        ScriptType.Str => "\"мама мыла раму\"",
        ScriptType.Vec => "<3, 1, 4, 1, 5, 9, 2, 6>",
        ScriptType.List => "[3, 1, 4]",
        ScriptType.Fn => "x => x",
        ScriptType.Table => $"input(\"{name}\", kind: \"table\")",
        _ => $"input(\"{name}\")",
    };

    /// <summary>
    /// Аргументы вызова: вход-данные получает результат предыдущего шага (с приведением, если
    /// нужно), остальные — <c>default</c>; <c>out</c>-параметры — <c>out _</c>.
    /// </summary>
    private static string Arguments(CodeUnit unit, TypeGraph graph, string? previous, string? previousType)
    {
        string[] declared = DocIdParameters(unit.Id);
        Queue<string> types = new(unit.Inputs.Length > 0 ? unit.Inputs.Split('|') : []);
        bool fed = previous is null;
        var arguments = new List<string>();

        // В Inputs нет out-параметров, а doc ID помечает @ и out, и ref: сколько параметров не
        // хватает до Inputs, столько первых @ — это out.
        int outs = declared.Length - types.Count;

        foreach (string parameter in declared)
        {
            if (parameter.EndsWith('@') && outs > 0)
            {
                arguments.Add("out _");
                outs--;
                continue;
            }

            string type = types.Count > 0 ? types.Dequeue() : "object";

            if (!fed && graph.IsData(type))
            {
                (int distance, _) = graph.Link(previousType!, type);
                if (distance == 0) { arguments.Add(previous!); fed = true; continue; }
                if (distance == 1) { arguments.Add($"({type}){previous}"); fed = true; continue; }
            }

            arguments.Add($"default({type})!");
        }

        return string.Join(", ", arguments);
    }

    /// <summary>Параметры из XML doc ID верхнего уровня: <c>M:T.F(A,B{C,D},E@)</c> → A, B{C,D}, E@.</summary>
    private static string[] DocIdParameters(string id)
    {
        int open = id.IndexOf('(');
        if (open < 0) return [];

        string list = id[(open + 1)..id.LastIndexOf(')')];
        var parameters = new List<string>();
        int depth = 0, start = 0;

        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] is '{' or '[') depth++;
            else if (list[i] is '}' or ']') depth--;
            else if (list[i] == ',' && depth == 0)
            {
                parameters.Add(list[start..i]);
                start = i + 1;
            }
        }

        parameters.Add(list[start..]);
        return [.. parameters.Where(p => p.Length > 0)];
    }

    /// <summary>
    /// Сборки для компиляции: платформа, управляемые DLL рядом с утилитой и собранные сборки
    /// нужных проектов из <c>src/*/bin*</c>, если рядом с утилитой их нет.
    /// </summary>
    private static IEnumerable<MetadataReference> References(string root, IEnumerable<string> assemblies)
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string platform = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "";

        foreach (string path in platform.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Concat(Directory.GetFiles(AppContext.BaseDirectory, "*.dll")))
            paths.TryAdd(Path.GetFileName(path), path);

        foreach (string assembly in assemblies.Distinct())
        {
            if (paths.ContainsKey(assembly + ".dll")) continue;

            if (BuiltAssemblies.Find(root, assembly) is string built)
            {
                paths[assembly + ".dll"] = built;
                foreach (string neighbour in Directory.GetFiles(Path.GetDirectoryName(built)!, "*.dll")) paths.TryAdd(Path.GetFileName(neighbour), neighbour);
            }
        }

        return paths.Values.Where(IsManaged).Select(path => MetadataReference.CreateFromFile(path));
    }

    private static bool IsManaged(string path)
    {
        try
        {
            AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (Exception error) when (error is BadImageFormatException or FileLoadException)
        {
            return false;
        }
    }
}
