using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace AiFramework.Tools.Atlas;

/// <summary>Вопрос эталона: запрос и типы, среди членов которых лежит правильный ответ.</summary>
/// <param name="Source"><c>tutorial</c> или <c>demo</c>.</param>
/// <param name="Name">Откуда вопрос: файл туториала или ключ алгоритма в демо.</param>
/// <param name="Query">Запрос на естественном языке.</param>
/// <param name="Types">Имена типов: короткие (<c>Garch</c>) или полные (<c>AI.LLM.Agents.Agent</c>).</param>
public sealed record GoldItem(string Source, string Name, string Query, IReadOnlyList<string> Types)
{
    /// <summary>
    /// Запрос уже содержит имя ответа («оценка через `Garch`»): такой вопрос проверяет
    /// совпадение слов, а не поиск по задаче, и в замере учитывается отдельно.
    /// </summary>
    public bool NamesAnswer { get; } = Types.Any(type =>
    {
        HashSet<string> query = [.. CodeTokenizer.Tokens(Query)];
        List<string> name = CodeTokenizer.Tokens(type[(type.LastIndexOf('.') + 1)..]);
        return name.Count > 0 && name.All(query.Contains);
    });
}

/// <summary>
/// Эталон поиска, собранный из того, что в репозитории уже есть.
/// </summary>
/// <remarks>
/// Туториалы канонической структуры: запрос — раздел «Постановка задачи», ответ — типы из
/// таблицы раздела «API». Демо: запрос — название и подзаголовок алгоритма, ответ —
/// <c>ApiClass</c> из <c>AlgoDef</c>. Заголовок туториала в запрос не идёт: в нём обычно стоит
/// имя метода («GARCH»), и эталон проверял бы совпадение слов, а не поиск по задаче.
/// </remarks>
public static class GoldSet
{
    private const int MaxQueryLength = 600;

    private static readonly Regex Section = new(@"^##\s+(?<title>.+?)\s*$", RegexOptions.Multiline);
    private static readonly Regex CodeSpan = new(@"`(?<code>[^`]+)`");
    private static readonly Regex TypeName = new(@"^[A-Z][A-Za-z0-9_]*$");

    /// <summary>Все вопросы эталона.</summary>
    public static IReadOnlyList<GoldItem> Load(string root) => [.. Tutorials(root), .. Demo(root)];

    /// <summary>Совпадает ли единица с ответом: это сам тип или его член.</summary>
    public static bool Matches(CodeUnit unit, GoldItem item) => Matches(TypeOf(unit), item);

    /// <summary>Совпадает ли тип с ответом вопроса.</summary>
    public static bool Matches(string type, GoldItem item)
    {
        string simple = type[(type.LastIndexOf('.') + 1)..];

        return item.Types.Any(expected => expected.Contains('.')
            ? type == expected || type.EndsWith("." + expected, StringComparison.Ordinal)
            : simple == expected);
    }

    /// <summary>Полное имя типа единицы: сам тип или тип, которому принадлежит член.</summary>
    public static string TypeOf(CodeUnit unit)
    {
        string name = ApiSearch.QualifiedName(unit.Id);
        return unit.Kind == "type" ? name : name[..Math.Max(0, name.LastIndexOf('.'))];
    }

    private static IEnumerable<GoldItem> Tutorials(string root)
    {
        string folder = Path.Combine(root, "Docs", "Tutorials");
        if (!Directory.Exists(folder)) yield break;

        foreach (string file in Directory.GetFiles(folder, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string text = File.ReadAllText(file);
            string? task = SectionText(text, "Постановка задачи");
            string? api = SectionText(text, "API");

            if (task is null || api is null) continue;

            string[] types = [.. ApiTypes(api)];
            if (types.Length == 0) continue;

            string query = Regex.Replace(Regex.Replace(task, @"[*_`#>|]", " "), @"\s+", " ").Trim();
            if (query.Length > MaxQueryLength) query = query[..MaxQueryLength];

            yield return new GoldItem("tutorial", Path.GetRelativePath(root, file).Replace('\\', '/'), query, types);
        }
    }

    /// <summary>Типы из таблицы API: первое имя с заглавной буквы в каждом <c>`коде`</c> первого столбца.</summary>
    private static IEnumerable<string> ApiTypes(string api)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string line in api.Split('\n'))
        {
            if (!line.TrimStart().StartsWith('|')) continue;

            string firstCell = line.Split('|', StringSplitOptions.None).ElementAtOrDefault(1) ?? "";

            foreach (Match span in CodeSpan.Matches(firstCell))
            {
                string head = span.Groups["code"].Value.Split('(', '<', ' ', '[')[0];
                string type = head.Split('.')[0];

                if (TypeName.IsMatch(type) && seen.Add(type)) yield return type;
            }
        }
    }

    private static string? SectionText(string markdown, string title)
    {
        Match? start = Section.Matches(markdown).FirstOrDefault(match => match.Groups["title"].Value.Contains(title, StringComparison.Ordinal));
        if (start is null) return null;

        Match next = Section.Match(markdown, start.Index + start.Length);
        return markdown[(start.Index + start.Length)..(next.Success ? next.Index : markdown.Length)].Trim();
    }

    /// <summary>
    /// Алгоритмы демо: <c>new AlgoDef(ключ, название, подзаголовок, ApiClass, …)</c>, строки
    /// собираются из литералов и их конкатенаций.
    /// </summary>
    private static IEnumerable<GoldItem> Demo(string root)
    {
        string folder = Path.Combine(root, "Demo");
        if (!Directory.Exists(folder)) yield break;

        foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;

            string text = File.ReadAllText(file);
            if (!text.Contains("AlgoDef(", StringComparison.Ordinal)) continue;

            foreach (ObjectCreationExpressionSyntax creation in CSharpSyntaxTree.ParseText(text).GetRoot()
                .DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (creation.Type.ToString() != "AlgoDef" || creation.ArgumentList is not { Arguments.Count: >= 4 } arguments) continue;

                string? key = Literal(arguments.Arguments[0].Expression);
                string? title = Literal(arguments.Arguments[1].Expression);
                string? subtitle = Literal(arguments.Arguments[2].Expression);
                string? api = Literal(arguments.Arguments[3].Expression);

                if (key is null || title is null || string.IsNullOrWhiteSpace(api)) continue;

                yield return new GoldItem("demo", key, $"{title}. {subtitle}".Trim(), [api.Split('<')[0]]);
            }
        }
    }

    private static string? Literal(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) => Literal(binary.Left) + Literal(binary.Right),
        ParenthesizedExpressionSyntax parenthesized => Literal(parenthesized.Expression),
        _ => null,
    };
}
