using System.Text;
using System.Text.RegularExpressions;

namespace AiFramework.Tools.DocsLint;

/// <summary>
/// Выкладывает C#-сниппеты из документации в .cs-файлы, чтобы их
/// скомпилировал проект Tools/SnippetCheck.
///
/// Зачем это нужно: линтер канона проверяет структуру документа, но не может
/// заметить, что раздел «API» описывает несуществующий класс. Такие ошибки в
/// документации были массовыми — целые пространства имён (`AI.Graphs`,
/// `AI.Routing`) не существовали в репозитории. Компилятор ловит их за секунду.
///
/// Два правила раскладки, оба вытекают из того, как документация написана:
///
///   1. ВСЕ блоки одного документа склеиваются в один метод. Второй блок в
///      статье продолжает первый и опирается на его переменные — компилируя
///      их порознь, мы получили бы сотни ложных «имя не существует».
///
///   2. Документы с фрагментами-заглушками (`// ...`) пропускаются целиком:
///      это иллюстрации, а не самодостаточный код.
/// </summary>
internal static class SnippetEmitter
{
    /// <summary>Файл со списком документов, исключённых из компиляции.</summary>
    private const string SkipListName = "skip.txt";

    private static readonly Regex ReFence =
        new(@"(?s)```csharp\r?\n(.*?)```", RegexOptions.Compiled);

    /// <summary>
    /// Директива using, которую нужно поднять в начало файла.
    /// Требование «строка целиком и заканчивается точкой с запятой» существенно:
    /// иначе под шаблон попадают `using var idx = ...;` и `using (var s = ...)`,
    /// то есть операторы внутри метода, и файл разваливается на CS1529.
    /// </summary>
    private static readonly Regex ReUsingDirective =
        new(@"^\s*using\s+(static\s+)?[\w.]+\s*;\s*$|^\s*using\s+\w+\s*=\s*[\w.<>\[\],\s]+;\s*$",
            RegexOptions.Compiled);

    /// <summary>
    /// Многоточие-заглушка: «// ... заполнение графа», «new Vector(x1, x2), ...»,
    /// «…». Такой сниппет — иллюстрация, а не самодостаточный код.
    /// </summary>
    private static readonly Regex RePlaceholder =
        new(@"\.\.\.|…|/\*.*?\*/", RegexOptions.Compiled);

    /// <summary>
    /// Объявление типа на верхнем уровне сниппета: `public class WeatherPlugin`,
    /// `public interface IRefittable`. Внутрь метода такое не завернуть —
    /// локальные типы не могут быть public. Выносим в область пространства имён.
    /// </summary>
    private static readonly Regex ReTypeDecl =
        new(@"^(public|internal|abstract|sealed|static|partial)\b.*\b(class|interface|struct|record|enum)\b",
            RegexOptions.Compiled);

    /// <summary>Строковые и символьные литералы — в них многоточие не заглушка.</summary>
    private static readonly Regex ReLiteral =
        new(@"@""(?:[^""]|"""")*""|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])'", RegexOptions.Compiled);

    public static int Emit(string docsRoot, string outDir)
    {
        Directory.CreateDirectory(outDir);

        // Чистим только сгенерированное: рядом лежат skip.txt и csproj
        foreach (string old in Directory.EnumerateFiles(outDir, "*.g.cs"))
            File.Delete(old);

        var skip = ReadSkipList(Path.Combine(outDir, SkipListName));

        int emitted = 0, skippedPlaceholder = 0, skippedList = 0, noCode = 0;

        foreach (string file in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories).Order())
        {
            string rel = Path.GetRelativePath(docsRoot, file).Replace('\\', '/');

            string text = File.ReadAllText(file).Replace("\r\n", "\n");
            MatchCollection blocks = ReFence.Matches(text);
            if (blocks.Count == 0) { noCode++; continue; }

            if (skip.Contains(rel)) { skippedList++; continue; }

            var usings = new List<string>();
            var body = new List<string>();
            var types = new List<string>();
            bool placeholder = false;

            // Состояние разбора объявления типа. Открывающая скобка часто стоит
            // на следующей строке — пока она не встретилась, глубину не проверяем,
            // иначе тип «не закрывается» и проглатывает остаток документа.
            bool inType = false;
            bool sawOpenBrace = false;
            int braceDepth = 0;

            foreach (Match block in blocks)
            {
                foreach (string line in block.Groups[1].Value.Split('\n'))
                {
                    if (inType)
                    {
                        // Заглушки бывают и внутри типа: `StartAsync(...) { /* … */ }`
                        if (RePlaceholder.IsMatch(ReLiteral.Replace(line, "\"\"")))
                            placeholder = true;

                        types.Add(line);
                        braceDepth += CountBraces(line);
                        if (!sawOpenBrace && braceDepth > 0) sawOpenBrace = true;
                        if (sawOpenBrace && braceDepth <= 0) inType = false;
                        continue;
                    }

                    if (ReUsingDirective.IsMatch(line))
                    {
                        string u = line.Trim();
                        if (!usings.Contains(u)) usings.Add(u);
                        continue;
                    }

                    // Литералы вырезаем до проверки: "sk-or-..." — это ключ
                    // в примере, а не заглушка, и снимать документ с проверки
                    // из-за него неправильно.
                    if (RePlaceholder.IsMatch(ReLiteral.Replace(line, "\"\"")))
                        placeholder = true;

                    if (ReTypeDecl.IsMatch(line))
                    {
                        types.Add(line);
                        inType = true;
                        braceDepth = CountBraces(line);
                        sawOpenBrace = braceDepth > 0;
                        continue;
                    }

                    // `return;` в примере оборвал бы склейку следующих блоков
                    body.Add(Regex.IsMatch(line, @"^\s*return;\s*$") ? "    goto done;" : line);
                }
            }

            if (placeholder) { skippedPlaceholder++; continue; }

            string name = "S_" + Regex.Replace(rel[..^3], @"[^A-Za-z0-9]", "_");

            var sb = new StringBuilder();
            sb.AppendLine("// Сгенерировано из " + rel + " — не редактировать.");
            sb.AppendLine("// Источник правды: Docs/Tutorials/" + rel);
            foreach (string u in usings) sb.AppendLine(u);
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine();
            // Свой namespace на документ: типы из разных статей могут
            // называться одинаково, и в общем пространстве имён они бы столкнулись
            sb.AppendLine("namespace SnippetCheck." + name + ";");
            sb.AppendLine();
            sb.AppendLine("internal static class " + name);
            sb.AppendLine("{");

            // async только там, где есть await: примеры по LLM без него не
            // соберутся, а примеры со Span<T> — наоборот, не соберутся ВНУТРИ
            // async-метода (ref struct в асинхронном методе запрещён).
            bool needsAsync = body.Any(l => Regex.IsMatch(l, @"\bawait\s"));
            if (needsAsync)
            {
                sb.AppendLine("    internal static async Task Run()");
                sb.AppendLine("    {");
                sb.AppendLine("        await Task.CompletedTask;");
            }
            else
            {
                sb.AppendLine("    internal static void Run()");
                sb.AppendLine("    {");
            }
            foreach (string line in body) sb.AppendLine(line);
            sb.AppendLine("    done: ;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            // Типы из сниппета — рядом с классом-обёрткой, в том же namespace.
            // Имена уникальны в пределах файла, но между документами могут
            // совпасть, поэтому каждый файл получает свой namespace ниже.
            if (types.Count > 0)
            {
                sb.AppendLine();
                foreach (string line in types) sb.AppendLine(line);
            }

            File.WriteAllText(Path.Combine(outDir, name + ".g.cs"), sb.ToString());
            emitted++;
        }

        Console.WriteLine($"Сниппетов выложено: {emitted}. " +
                          $"Пропущено: заглушек {skippedPlaceholder}, по skip.txt {skippedList}. " +
                          $"Документов без кода: {noCode}.");
        return 0;
    }

    /// <summary>
    /// Баланс фигурных скобок в строке. Литералы вырезаются: скобка внутри
    /// JSON-строки в примере не должна влиять на глубину вложенности.
    /// </summary>
    private static int CountBraces(string line)
    {
        string clean = ReLiteral.Replace(line, "\"\"");
        int depth = 0;
        foreach (char c in clean)
        {
            if (c == '{') depth++;
            else if (c == '}') depth--;
        }
        return depth;
    }

    /// <summary>
    /// Читает skip.txt: по одному относительному пути на строку,
    /// пустые строки и строки с # игнорируются.
    /// </summary>
    private static HashSet<string> ReadSkipList(string path)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return set;

        foreach (string line in File.ReadAllLines(path))
        {
            string s = line.Trim();
            if (s.Length == 0 || s.StartsWith('#')) continue;
            set.Add(s.Replace('\\', '/'));
        }
        return set;
    }
}
