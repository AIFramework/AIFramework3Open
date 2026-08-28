using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Globalization;
using System.Text;

namespace AI.Script.Docs;

/// <summary>Найденная функция и то, насколько она подошла запросу.</summary>
public sealed class ManifestMatch
{
    /// <summary>Функция.</summary>
    public ScriptFunction Function { get; init; } = null!;

    /// <summary>Оценка совпадения: больше — ближе.</summary>
    public int Score { get; init; }
}

/// <summary>
/// Построение манифеста возможностей: что язык умеет и как это вызвать.
/// </summary>
/// <remarks>
/// Манифест выводится из тех же объектов, что и вызов (принцип П6 в DESIGN.md), поэтому
/// разойтись с реальностью не может. В системе, где по документации генерируют код,
/// разошедшаяся документация — не неудобство, а поломка.
/// </remarks>
public static class ManifestBuilder
{
    /// <summary>Строит манифест по модулям.</summary>
    public static string Build(IReadOnlyList<IScriptModule> modules, ManifestOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(modules);

        options ??= new ManifestOptions();

        List<IScriptModule> selected = Select(modules, options.Namespaces);

        return options.Format switch
        {
            ManifestFormat.Json => Json(selected, options),
            ManifestFormat.Compact => Compact(selected, options),
            _ => Markdown(selected, options),
        };
    }

    /// <summary>
    /// Ищет функции по имени и описанию.
    /// </summary>
    /// <remarks>
    /// Второй уровень манифеста: модель сначала выбирает пространство, потом спрашивает точные
    /// сигнатуры — и не выдумывает аргументы. Поиск идёт и по описанию, потому что искать
    /// будут словами задачи («корреляция»), а не именем функции.
    /// </remarks>
    public static IReadOnlyList<ManifestMatch> Search(
        IReadOnlyList<IScriptModule> modules,
        string query,
        int limit = 10)
    {
        ArgumentNullException.ThrowIfNull(modules);

        if (string.IsNullOrWhiteSpace(query)) return [];

        string needle = query.Trim().ToLowerInvariant();
        var matches = new List<ManifestMatch>();

        foreach (IScriptModule module in modules)
        {
            foreach (ScriptFunction function in module.Functions)
            {
                int score = Score(function, needle);
                if (score > 0) matches.Add(new ManifestMatch { Function = function, Score = score });
            }
        }

        matches.Sort((left, right) => left.Score != right.Score
            ? right.Score.CompareTo(left.Score)
            : string.CompareOrdinal(left.Function.FullName, right.Function.FullName));

        return limit > 0 && matches.Count > limit ? matches.GetRange(0, limit) : matches;
    }

    /// <summary>
    /// Насколько функция подходит запросу.
    /// </summary>
    /// <remarks>
    /// Совпадение по описанию ищется по ОБЩЕМУ НАЧАЛУ слов, а не по подстроке: искать будут
    /// словами задачи («корреляция»), а в описании стоит другая форма того же слова
    /// («корреляции»). Полноценная лемматизация есть в <c>AI.NLP</c>, но тянуть её сюда ради
    /// поиска по одной строке дороже, чем сравнить начала слов.
    /// </remarks>
    private static int Score(ScriptFunction function, string needle)
    {
        string name = function.Name.ToLowerInvariant();
        string full = function.FullName.ToLowerInvariant();
        string description = function.Description.ToLowerInvariant();

        if (full == needle || name == needle) return 100;
        if (name.StartsWith(needle, StringComparison.Ordinal)) return 80;
        if (full.Contains(needle, StringComparison.Ordinal)) return 60;
        if (description.Contains(needle, StringComparison.Ordinal)) return 40;
        if (MatchesWordStem(description, needle)) return 35;

        return Suggestions.Distance(needle, name, 2) <= 2 ? 20 : 0;
    }

    private static bool MatchesWordStem(string description, string needle)
    {
        int required = Math.Min(5, needle.Length);

        if (required < 3) return false;

        foreach (string word in description.Split([' ', ',', '.', ';', ':', '(', ')', '«', '»', '—', '-', '/'],
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (CommonPrefix(word, needle) >= required) return true;
        }

        return false;
    }

    private static int CommonPrefix(string left, string right)
    {
        int limit = Math.Min(left.Length, right.Length);
        int count = 0;

        while (count < limit && left[count] == right[count]) count++;

        return count;
    }

    private static List<IScriptModule> Select(IReadOnlyList<IScriptModule> modules, IReadOnlyCollection<string> names)
    {
        var selected = new List<IScriptModule>();

        foreach (IScriptModule module in modules)
        {
            if (names.Count > 0 && !names.Contains(module.Name)) continue;

            selected.Add(module);
        }

        selected.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

        return selected;
    }

    private static List<ScriptFunction> Functions(IReadOnlyList<IScriptModule> modules, ManifestOptions options, out int dropped)
    {
        var functions = new List<ScriptFunction>();

        foreach (IScriptModule module in modules)
        {
            var sorted = new List<ScriptFunction>(module.Functions);
            sorted.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            functions.AddRange(sorted);
        }

        if (options.MaxFunctions <= 0 || functions.Count <= options.MaxFunctions)
        {
            dropped = 0;
            return functions;
        }

        dropped = functions.Count - options.MaxFunctions;

        return functions.GetRange(0, options.MaxFunctions);
    }

    private static string Markdown(IReadOnlyList<IScriptModule> modules, ManifestOptions options)
    {
        var builder = new StringBuilder();

        _ = builder.AppendLine("# Возможности AIScript").AppendLine();
        _ = builder.AppendLine("## Пространства имён").AppendLine();

        foreach (IScriptModule module in modules)
        {
            // Число функций в скобках сразу после имени, а не отдельными словами в конце
            // строки: индекс целиком уходит в системный промпт, и слово «функций» на каждой
            // строке — это два десятка токенов, не сообщающих читателю ничего нового.
            _ = builder.Append("- **").Append(module.Name).Append("** (").Append(module.Functions.Count)
                .Append(") — ").Append(module.Description).AppendLine();
        }

        if (options.IndexOnly)
        {
            _ = builder.AppendLine()
                .AppendLine("Подробности по пространству: `help(\"имя\")`; по функции: `help(\"пространство.функция\")`.");

            return builder.ToString().TrimEnd();
        }

        foreach (IScriptModule module in modules)
        {
            _ = builder.AppendLine().Append("## ").AppendLine(module.Name).AppendLine();

            var sorted = new List<ScriptFunction>(module.Functions);
            sorted.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

            int shown = 0;

            foreach (ScriptFunction function in sorted)
            {
                if (options.MaxFunctions > 0 && shown >= options.MaxFunctions) break;

                shown++;
                AppendMarkdownFunction(builder, function, options);
            }

            if (options.MaxFunctions > 0 && sorted.Count > shown)
            {
                _ = builder.Append("- … ещё ").Append(sorted.Count - shown)
                    .Append(" функций в этом пространстве: `help(\"").Append(module.Name).AppendLine("\")`");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendMarkdownFunction(StringBuilder builder, ScriptFunction function, ManifestOptions options)
    {
        _ = builder.Append("### `").Append(function.Signature).AppendLine("`").AppendLine();

        if (function.Description.Length > 0) _ = builder.AppendLine(function.Description).AppendLine();

        if (options.IncludeParameters && function.Parameters.Count > 0)
        {
            foreach (ScriptParameter parameter in function.Parameters)
            {
                _ = builder.Append("- `").Append(parameter.Name).Append("`: ").Append(parameter.Type.ToName());

                _ = parameter.IsOptional
                    ? builder.Append(" = ").Append(ScriptFormatter.Format(parameter.Default, quoteStrings: true))
                    : builder.Append(" (обязательный)");

                if (parameter.Description.Length > 0) _ = builder.Append(" — ").Append(parameter.Description);

                _ = builder.AppendLine();
            }

            _ = builder.AppendLine();
        }

        if (!options.IncludeExamples || string.IsNullOrWhiteSpace(function.Example)) return;

        _ = builder.AppendLine("```python").AppendLine(function.Example).AppendLine("```").AppendLine();
    }

    private static string Compact(IReadOnlyList<IScriptModule> modules, ManifestOptions options)
    {
        var builder = new StringBuilder();

        if (options.IndexOnly)
        {
            foreach (IScriptModule module in modules)
                _ = builder.Append(module.Name).Append(" — ").AppendLine(module.Description);

            return builder.ToString().TrimEnd();
        }

        List<ScriptFunction> functions = Functions(modules, options, out int dropped);

        foreach (ScriptFunction function in functions) _ = builder.AppendLine(function.Signature);

        if (dropped > 0) _ = builder.Append("… ещё ").Append(dropped).AppendLine(" функций не показано");

        return builder.ToString().TrimEnd();
    }

    private static string Json(IReadOnlyList<IScriptModule> modules, ManifestOptions options)
    {
        var builder = new StringBuilder();

        _ = builder.Append("{\"namespaces\":[");

        for (int i = 0; i < modules.Count; i++)
        {
            IScriptModule module = modules[i];

            if (i > 0) _ = builder.Append(',');

            _ = builder.Append('{');
            AppendJsonField(builder, "name", module.Name, first: true);
            AppendJsonField(builder, "description", module.Description);
            AppendJsonField(builder, "version", module.Version);
            _ = builder.Append(",\"count\":").Append(module.Functions.Count.ToString(CultureInfo.InvariantCulture));

            if (!options.IndexOnly) AppendJsonFunctions(builder, module, options);

            _ = builder.Append('}');
        }

        _ = builder.Append("]}");

        return builder.ToString();
    }

    private static void AppendJsonFunctions(StringBuilder builder, IScriptModule module, ManifestOptions options)
    {
        var sorted = new List<ScriptFunction>(module.Functions);
        sorted.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

        _ = builder.Append(",\"functions\":[");

        int shown = 0;

        foreach (ScriptFunction function in sorted)
        {
            if (options.MaxFunctions > 0 && shown >= options.MaxFunctions) break;

            if (shown > 0) _ = builder.Append(',');
            shown++;

            _ = builder.Append('{');
            AppendJsonField(builder, "name", function.FullName, first: true);
            AppendJsonField(builder, "signature", function.Signature);
            AppendJsonField(builder, "description", function.Description);
            AppendJsonField(builder, "returns", function.ReturnHandleType ?? function.ReturnType.ToName());

            if (options.IncludeExamples && !string.IsNullOrWhiteSpace(function.Example))
                AppendJsonField(builder, "example", function.Example);

            if (options.IncludeParameters) AppendJsonParameters(builder, function);

            _ = builder.Append('}');
        }

        _ = builder.Append(']');

        if (sorted.Count > shown) _ = builder.Append(",\"omitted\":").Append((sorted.Count - shown).ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendJsonParameters(StringBuilder builder, ScriptFunction function)
    {
        _ = builder.Append(",\"parameters\":[");

        for (int i = 0; i < function.Parameters.Count; i++)
        {
            ScriptParameter parameter = function.Parameters[i];

            if (i > 0) _ = builder.Append(',');

            _ = builder.Append('{');
            AppendJsonField(builder, "name", parameter.Name, first: true);
            AppendJsonField(builder, "type", parameter.Type.ToName());
            AppendJsonField(builder, "description", parameter.Description);
            _ = builder.Append(",\"required\":").Append(parameter.IsOptional ? "false" : "true");

            if (parameter.IsOptional)
                AppendJsonField(builder, "default", ScriptFormatter.Format(parameter.Default, quoteStrings: false));

            _ = builder.Append('}');
        }

        _ = builder.Append(']');
    }

    private static void AppendJsonField(StringBuilder builder, string name, string value, bool first = false)
    {
        if (!first) _ = builder.Append(',');

        _ = builder.Append('"').Append(name).Append("\":");
        AppendJsonString(builder, value);
    }

    private static void AppendJsonString(StringBuilder builder, string text)
    {
        _ = builder.Append('"');

        foreach (char c in text)
        {
            switch (c)
            {
                case '"': _ = builder.Append("\\\""); break;
                case '\\': _ = builder.Append("\\\\"); break;
                case '\n': _ = builder.Append("\\n"); break;
                case '\r': _ = builder.Append("\\r"); break;
                case '\t': _ = builder.Append("\\t"); break;
                default:
                    if (c < ' ') _ = builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else _ = builder.Append(c);
                    break;
            }
        }

        _ = builder.Append('"');
    }
}
