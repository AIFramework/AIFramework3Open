using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Индексация исходников репозитория: проекты через MSBuildWorkspace, единицы и вызовы по
/// семантической модели Roslyn.
/// </summary>
/// <remarks>
/// Файл, текст которого не изменился, не разбирается вовсе: семантическая модель — самая
/// дорогая часть прогона, и повторный прогон упирается только в загрузку проектов.
/// </remarks>
public static class SourceIndexer
{
    /// <summary>Проект сниппетов туториалов: сниппеты генерирует DocsLint, в решения он не входит.</summary>
    private const string SnippetProject = "Tools/SnippetCheck/SnippetCheck.csproj";

    private static readonly Regex SnippetSource = new(@"^// Источник правды: (?<path>\S+)", RegexOptions.Multiline);

    /// <summary>Итог прогона.</summary>
    public sealed record Report(
        string Solution, int Projects, int Files, int ParsedFiles, int RemovedFiles, int Units, int ChangedUnits,
        int Calls, TimeSpan Load, TimeSpan Scan, IReadOnlyList<string> Failures);

    /// <summary>Индексирует репозиторий.</summary>
    /// <param name="root">Корень репозитория.</param>
    /// <param name="store">Индекс.</param>
    /// <param name="solution">Решение; <c>null</c> — самое полное в корне.</param>
    public static async Task<Report> RunAsync(string root, UnitStore store, string? solution = null, CancellationToken cancellation = default)
    {
        root = Path.GetFullPath(root);
        solution ??= PickSolution(root);

        // Состояние берётся до разбора: правка во время индексации даст другое состояние в следующий раз.
        string state = GitRepository.State(root);

        var clock = Stopwatch.StartNew();
        var failures = new List<string>();

        using MSBuildWorkspace workspace = MSBuildWorkspace.Create();
        using IDisposable handler = workspace.RegisterWorkspaceFailedHandler(e =>
        {
            // Предупреждения восстановления пакетов (NU1701 и т. п.) загрузке не мешают.
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure) failures.Add(e.Diagnostic.Message);
        });

        await workspace.OpenSolutionAsync(solution, cancellationToken: cancellation);

        string snippets = Path.Combine(root, SnippetProject);
        if (File.Exists(snippets) && !workspace.CurrentSolution.Projects.Any(p => SamePath(p.FilePath, snippets)))
            await workspace.OpenProjectAsync(snippets, cancellationToken: cancellation);

        Solution loaded = WithParsedDocumentation(workspace.CurrentSolution);
        TimeSpan load = clock.Elapsed;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int files = 0, parsed = 0, changed = 0, projects = 0;

        foreach (Project project in DistinctProjects(loaded))
        {
            projects++;
            string area = Area(root, project);

            foreach (Document document in project.Documents)
            {
                string? path = Relative(root, document.FilePath);
                if (path is null || !seen.Add(path)) continue;

                files++;
                string text = (await document.GetTextAsync(cancellation)).ToString();
                string hash = SymbolUnits.Hash(text);

                if (store.IsCurrent(path, hash)) continue;

                SemanticModel model = await document.GetSemanticModelAsync(cancellation)
                    ?? throw new InvalidOperationException($"Нет семантической модели для {path}.");

                (IReadOnlyList<CodeUnit> units, IReadOnlyList<CodeCall> calls, IReadOnlyList<CodeFlow> flows) = Extract(model, path);
                changed += store.ReplaceFile(new SourceFile(path, hash, area, ShownAs(area, text)), units, calls, flows);
                parsed++;
            }
        }

        int removed = store.RemoveFilesExcept(seen);
        store.SetMeta(UnitStore.StateKey, state);

        return new Report(Path.GetFileName(solution), projects, files, parsed, removed, store.Count, changed,
            store.CallCount, load, clock.Elapsed - load, failures);
    }

    /// <summary>
    /// Единицы и вызовы одного файла.
    /// </summary>
    /// <remarks>
    /// Тип попадает в индекс из файла, где объявлена его первая часть: у разделяемого типа
    /// несколько файлов, а единица должна быть одна. У разделяемого метода берётся часть с телом.
    /// </remarks>
    internal static (IReadOnlyList<CodeUnit> Units, IReadOnlyList<CodeCall> Calls, IReadOnlyList<CodeFlow> Flows) Extract(SemanticModel model, string file)
    {
        var units = new Dictionary<(string, string), CodeUnit>();
        var calls = new List<CodeCall>();
        var flows = new Dictionary<((string, string), (string, string)), int>();

        foreach (MemberDeclarationSyntax node in model.SyntaxTree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            if (node is not (BaseTypeDeclarationSyntax or BaseMethodDeclarationSyntax)) continue;
            if (model.GetDeclaredSymbol(node) is not { } symbol || !SymbolUnits.IsUnit(symbol)) continue;
            if (symbol is IMethodSymbol { PartialImplementationPart: not null }) continue;
            if (symbol is INamedTypeSymbol && symbol.DeclaringSyntaxReferences[0].SyntaxTree != model.SyntaxTree) continue;

            Location location = symbol.Locations.FirstOrDefault(l => l.SourceTree == model.SyntaxTree) ?? node.GetLocation();
            int line = location.GetLineSpan().StartLinePosition.Line + 1;

            CodeUnit unit = SymbolUnits.From(symbol, file, line, Body(node));
            if (!units.TryAdd((unit.Project, unit.Id), unit)) continue;

            if (node is BaseMethodDeclarationSyntax)
            {
                foreach ((string project, string id, int count) in Callees(model, node))
                    calls.Add(new CodeCall(unit.Project, unit.Id, project, id, count));

                CollectFlows(model, node, flows);
            }

            if (symbol is INamedTypeSymbol type)
            {
                foreach (IMethodSymbol constructor in ConstructorsWithoutDeclaration(type))
                    units.TryAdd((unit.Project, constructor.GetDocumentationCommentId()!), SymbolUnits.From(constructor, file, line, ""));
            }
        }

        return ([.. units.Values], calls,
            [.. flows.Select(flow => new CodeFlow(flow.Key.Item1.Item1, flow.Key.Item1.Item2, flow.Key.Item2.Item1, flow.Key.Item2.Item2, flow.Value))]);
    }

    /// <summary>
    /// Потоки данных внутри тела: результат вызова A попадает во вход вызова B напрямую
    /// (<c>B(A(x))</c>, <c>A().B()</c>) или через локальную переменную (<c>var s = A(x); B(s)</c>).
    /// </summary>
    /// <remarks>
    /// Это приближение, а не анализ потока данных: переменная помнит последний вызов, который
    /// её задал, в порядке текста. Для статистики «что обычно идёт после чего» этого хватает,
    /// а ветвления и циклы лишь добавляют редкие ложные пары.
    /// </remarks>
    private static void CollectFlows(SemanticModel model, SyntaxNode body, Dictionary<((string, string), (string, string)), int> flows)
    {
        var producers = new Dictionary<ISymbol, (string, string)>(SymbolEqualityComparer.Default);

        foreach (SyntaxNode node in body.DescendantNodes())
        {
            switch (node)
            {
                case VariableDeclaratorSyntax { Initializer.Value: { } value } declarator
                    when model.GetDeclaredSymbol(declarator) is ILocalSymbol local && Producer(model, value) is { } made:
                    producers[local] = made;
                    break;

                case AssignmentExpressionSyntax { Left: IdentifierNameSyntax left } assignment
                    when model.GetSymbolInfo(left).Symbol is ILocalSymbol local && Producer(model, assignment.Right) is { } made:
                    producers[local] = made;
                    break;
            }

            if (node is not (InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax) || MethodKey(model, node) is not { } to) continue;

            foreach (ExpressionSyntax input in CallInputs(node))
            {
                (string, string)? from = Producer(model, input)
                    ?? (model.GetSymbolInfo(Unwrap(input)).Symbol is ILocalSymbol local && producers.TryGetValue(local, out var made) ? made : null);

                if (from is { } source && source != to) flows[(source, to)] = flows.GetValueOrDefault((source, to)) + 1;
            }
        }
    }

    /// <summary>Аргументы вызова и объект, у которого он вызван.</summary>
    private static IEnumerable<ExpressionSyntax> CallInputs(SyntaxNode call)
    {
        if (call is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member }) yield return member.Expression;

        ArgumentListSyntax? arguments = call switch
        {
            InvocationExpressionSyntax invocation => invocation.ArgumentList,
            BaseObjectCreationExpressionSyntax creation => creation.ArgumentList,
            _ => null,
        };

        foreach (ArgumentSyntax argument in arguments?.Arguments ?? []) yield return argument.Expression;
    }

    private static (string, string)? Producer(SemanticModel model, ExpressionSyntax expression) =>
        Unwrap(expression) is var inner && inner is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax ? MethodKey(model, inner) : null;

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => Unwrap(parenthesized.Expression),
        AwaitExpressionSyntax awaited => Unwrap(awaited.Expression),
        CastExpressionSyntax cast => Unwrap(cast.Expression),
        PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed => Unwrap(suppressed.Operand),
        _ => expression,
    };

    /// <summary>Ключ вызываемого метода: сборка и XML doc ID исходного определения.</summary>
    private static (string, string)? MethodKey(SemanticModel model, SyntaxNode call)
    {
        if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol method) return null;

        method = (method.ReducedFrom ?? method).OriginalDefinition;

        return method.MethodKind != MethodKind.LocalFunction && method.GetDocumentationCommentId() is { } id
            ? (method.ContainingAssembly?.Name ?? "", id)
            : null;
    }

    /// <summary>
    /// Конструкторы без собственного узла в синтаксисе: неявный по умолчанию и первичный
    /// (<c>record Point(double X, double Y)</c>). В DLL оба есть, и без них составы разошлись бы.
    /// </summary>
    private static IEnumerable<IMethodSymbol> ConstructorsWithoutDeclaration(INamedTypeSymbol type) =>
        type.InstanceConstructors.Where(constructor => SymbolUnits.IsUnit(constructor)
            && (constructor.IsImplicitlyDeclared
                || constructor.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is TypeDeclarationSyntax)));

    /// <summary>
    /// Кого вызывает тело: методы, конструкторы, <c>base(...)</c> и <c>this(...)</c>, включая
    /// вызовы из лямбд и локальных функций. Вызов обобщённого метода сводится к исходному
    /// определению, вызов расширения — к статическому методу.
    /// </summary>
    private static IEnumerable<(string Project, string Id, int Count)> Callees(SemanticModel model, SyntaxNode body)
    {
        var counts = new Dictionary<(string, string), int>();

        foreach (SyntaxNode node in body.DescendantNodes())
        {
            if (node is not (InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax or ConstructorInitializerSyntax)) continue;
            if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol method) continue;

            method = (method.ReducedFrom ?? method).OriginalDefinition;

            if (method.MethodKind == MethodKind.LocalFunction || method.GetDocumentationCommentId() is not { } id) continue;

            var key = (method.ContainingAssembly?.Name ?? "", id);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts.Select(pair => (pair.Key.Item1, pair.Key.Item2, pair.Value));
    }

    private static string Body(MemberDeclarationSyntax node) => node switch
    {
        BaseMethodDeclarationSyntax method => (method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? "").Trim(),
        _ => "",
    };

    /// <summary>
    /// Разбирать XML-комментарии во всех проектах. Без этого проект без
    /// <c>GenerateDocumentationFile</c> отдаёт комментарии как простой текст, и описаний у него нет.
    /// </summary>
    private static Solution WithParsedDocumentation(Solution solution)
    {
        foreach (Project project in solution.Projects)
        {
            if (project.ParseOptions is CSharpParseOptions options && options.DocumentationMode != DocumentationMode.Diagnose)
                solution = solution.WithProjectParseOptions(project.Id, options.WithDocumentationMode(DocumentationMode.Parse));
        }

        return solution;
    }

    /// <summary>C#-проекты по одному на файл: у проекта с несколькими целевыми платформами их несколько.</summary>
    private static IEnumerable<Project> DistinctProjects(Solution solution) =>
        solution.Projects
            .Where(project => project.Language == LanguageNames.CSharp && project.FilePath != null)
            .GroupBy(project => project.FilePath!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());

    /// <summary>
    /// Область проекта по его месту в репозитории: библиотека, тест, демо, туториал, утилита.
    /// </summary>
    internal static string Area(string root, Project project)
    {
        string path = Relative(root, project.FilePath) ?? "";
        string top = path.Split('/')[0];

        if (path.StartsWith("Tools/SnippetCheck/", StringComparison.OrdinalIgnoreCase)) return "tutorial";
        if (top.Equals("Tests", StringComparison.OrdinalIgnoreCase) || project.Name.EndsWith("Tests", StringComparison.Ordinal)) return "test";
        if (top.Equals("Demo", StringComparison.OrdinalIgnoreCase)) return "demo";
        if (top.Equals("Tools", StringComparison.OrdinalIgnoreCase)) return "tool";

        return "library";
    }

    /// <summary>Самое полное решение в корне (<c>.sln</c> или <c>.slnx</c>) — с наибольшим числом проектов.</summary>
    private static string PickSolution(string root)
    {
        string[] solutions = [.. Directory.GetFiles(root, "*.sln*").Where(path => Path.GetExtension(path) is ".sln" or ".slnx")];

        if (solutions.Length == 0) throw new InvalidOperationException($"В {root} нет файла решения .sln или .slnx.");

        return solutions.MaxBy(path => Regex.Count(File.ReadAllText(path), @"\.csproj"""))!;
    }

    /// <summary>У сниппета туториала показывается сам туториал, путь к нему — в шапке файла.</summary>
    private static string? ShownAs(string area, string text) =>
        area == "tutorial" && SnippetSource.Match(text) is { Success: true } match ? match.Groups["path"].Value : null;

    /// <summary>Путь от корня через прямые косые; файлы вне корня и из obj/bin — <c>null</c>.</summary>
    private static string? Relative(string root, string? path)
    {
        if (path is null) return null;

        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)) return null;

        // Сгенерированное сборкой (AssemblyInfo, глобальные using) — не код репозитория.
        foreach (string segment in relative.Split('/'))
        {
            if (segment.StartsWith("obj", StringComparison.OrdinalIgnoreCase) || segment.StartsWith("bin", StringComparison.OrdinalIgnoreCase))
            {
                if (segment.Length == 3 || segment[3] == '-') return null;
            }
        }

        return relative;
    }

    private static bool SamePath(string? left, string right) =>
        left != null && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
