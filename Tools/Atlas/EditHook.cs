using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Хук Claude Code после правки (<c>PostToolUse</c> на Edit, Write, MultiEdit): новые и
/// изменённые методы правленного файла сравниваются с индексом, и если похожее уже есть —
/// замечание уходит модели в том же ходе.
/// </summary>
/// <remarks>
/// Быстрый путь: без LLM, без исполнения, без переиндексации — сравнение с индексом, каким он
/// был до правки, по формам тел из <see cref="ShapeCache"/>. Метод, чьё тело совпадает с
/// индексом, не проверяется: иначе каждая правка файла повторяла бы старые находки. Хук
/// молчит, если индекса нет, файл не C#, это тест или что-то пошло не так: он не должен
/// мешать работе.
/// </remarks>
public static class EditHook
{
    private const int PerMethod = 3;
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Читает событие хука из <paramref name="input"/> и пишет ответ в <paramref name="output"/>.</summary>
    public static int Run(TextReader input, TextWriter output)
    {
        try
        {
            JsonNode? payload = JsonNode.Parse(input.ReadToEnd());
            string? path = payload?["tool_input"]?["file_path"]?.GetValue<string>();

            if (path is null || Review(path) is not { } note) return 0;

            output.Write(new JsonObject
            {
                ["hookSpecificOutput"] = new JsonObject { ["hookEventName"] = "PostToolUse", ["additionalContext"] = note },
            }.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
        catch (Exception error)
        {
            Log(error);
        }

        return 0;
    }

    /// <summary>Замечание по файлу; <c>null</c> — сказать нечего.</summary>
    public static string? Review(string path)
    {
        path = Path.GetFullPath(path);
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || !File.Exists(path) || IsTest(path)) return null;

        string root = GitRepository.TopLevel(Path.GetDirectoryName(path)!);
        LibraryLink? link = LibraryLinks.Detect(root);
        if (!File.Exists(link is null ? AtlasSettings.IndexPath(root) : LibrarySnapshot.IndexPath(link))) return null;

        using AtlasContext context = AtlasContext.Open(root);
        UnitStore own = context.Project?.Store ?? context.Store;
        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');

        // Что было в индексе до правки: тела методов этого файла по простому имени.
        ILookup<string, string> indexed = own.Units()
            .Where(unit => unit.File == relative && unit.Kind != "type")
            .ToLookup(unit => SimpleName(unit.Id), unit => Normalize(unit.Body));

        var builder = new StringBuilder();
        foreach (BaseMethodDeclarationSyntax method in CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Body != null || m.ExpressionBody != null))
        {
            string name = ((MethodDeclarationSyntax)method).Identifier.ValueText;
            string body = Normalize(method.Body?.ToString() ?? ((MethodDeclarationSyntax)method).ExpressionBody!.ToString());
            if (indexed[name].Contains(body)) continue;

            DupFinding[] found = [.. context.Dups.Check(method.ToFullString(), null)
                .Where(finding => !(finding.B.File == relative && SimpleName(finding.B.Id) == name))
                .Take(PerMethod)];

            if (found.Length == 0) continue;

            builder.AppendLine($"{name} ({relative}): похожее уже есть —");
            foreach (DupFinding finding in found)
                builder.AppendLine($"  [{finding.Verdict}] {finding.B.Signature}  [{finding.B.Project}]  {finding.B.File}:{finding.B.Line}");
        }

        if (builder.Length == 0) return null;

        return "Атлас после правки: " + builder.ToString().TrimEnd()
            + "\nЕсли это то же самое, вызови существующий метод вместо своего. Разобрать пару — dup_explain; сознательное перекрытие — объясни и запиши через dup_decide по решению пользователя.";
    }

    private static string SimpleName(string id)
    {
        string name = ApiSearch.QualifiedName(id);
        return name[(name.LastIndexOf('.') + 1)..];
    }

    private static string Normalize(string body) => Spaces.Replace(body, " ").Trim();

    private static bool IsTest(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("Tests", StringComparison.OrdinalIgnoreCase) || segment.EndsWith("Tests", StringComparison.Ordinal));

    private static void Log(Exception error)
    {
        try
        {
            Directory.CreateDirectory(AtlasSettings.CacheRoot);
            File.AppendAllText(Path.Combine(AtlasSettings.CacheRoot, "hook.log"), $"{DateTimeOffset.Now:u} {error.GetType().Name}: {error.Message}\n");
        }
        catch (IOException)
        {
        }
    }
}
