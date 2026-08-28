using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Text;

namespace AI.Script.Docs;

/// <summary>
/// Построение справки по зарегистрированным модулям.
/// </summary>
/// <remarks>
/// Справка выводится из тех же объектов, что и вызов (см. принцип П6 в DESIGN.md), поэтому
/// разойтись с реальностью не может. Двухуровневость — список пространств отдельно, сигнатуры
/// по запросу — нужна затем, что полный перечень на сотни функций в промпт модели не помещается
/// и помещаться не должен.
/// </remarks>
public static class Manifest
{
    /// <summary>Список пространств имён с описанием и числом функций.</summary>
    public static string Namespaces(IReadOnlyList<IScriptModule> modules)
    {
        var builder = new StringBuilder("Пространства имён:");

        foreach (IScriptModule module in Ordered(modules))
        {
            _ = builder.AppendLine()
                .Append("  ").Append(module.Name.PadRight(10))
                .Append(module.Description);

            _ = builder.Append(" (функций: ").Append(module.Functions.Count).Append(')');
        }

        _ = builder.AppendLine().AppendLine()
            .Append("Подробности: help(\"имя_пространства\") либо help(\"пространство.функция\").");

        return builder.ToString();
    }

    /// <summary>Справка по пространству имён либо по конкретной функции.</summary>
    public static string Describe(IReadOnlyList<IScriptModule> modules, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Namespaces(modules);

        foreach (IScriptModule module in modules)
        {
            if (!string.Equals(module.Name, query, StringComparison.Ordinal)) continue;

            return DescribeModule(module);
        }

        foreach (IScriptModule module in modules)
        {
            foreach (ScriptFunction function in module.Functions)
            {
                if (string.Equals(function.FullName, query, StringComparison.Ordinal)
                    || string.Equals(function.Name, query, StringComparison.Ordinal))
                {
                    return DescribeFunction(function);
                }
            }
        }

        return NotFound(modules, query);
    }

    private static string DescribeModule(IScriptModule module)
    {
        var builder = new StringBuilder()
            .Append(module.Name).Append(" — ").Append(module.Description)
            .AppendLine().AppendLine();

        var sorted = new List<ScriptFunction>(module.Functions);
        sorted.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

        foreach (ScriptFunction function in sorted)
        {
            _ = builder.Append("  ").AppendLine(function.Signature);

            if (function.Description.Length > 0) _ = builder.Append("      ").AppendLine(function.Description);
        }

        return builder.ToString().TrimEnd();
    }

    private static string DescribeFunction(ScriptFunction function)
    {
        var builder = new StringBuilder()
            .AppendLine(function.Signature);

        if (function.Description.Length > 0) _ = builder.AppendLine(function.Description);

        if (function.Parameters.Count > 0)
        {
            _ = builder.AppendLine("Аргументы:");

            foreach (ScriptParameter parameter in function.Parameters)
            {
                _ = builder.Append("  ").Append(parameter.Name).Append(": ").Append(parameter.Type.ToName());

                if (parameter.IsOptional)
                {
                    _ = builder.Append(" = ").Append(Runtime.ScriptFormatter.Format(parameter.Default, quoteStrings: true));
                }
                else
                {
                    _ = builder.Append(" (обязательный)");
                }

                if (parameter.Description.Length > 0) _ = builder.Append(" — ").Append(parameter.Description);

                _ = builder.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(function.Example)) _ = builder.Append("Пример:\n  ").Append(function.Example);

        return builder.ToString().TrimEnd();
    }

    private static string NotFound(IReadOnlyList<IScriptModule> modules, string query)
    {
        var names = new List<string>();

        foreach (IScriptModule module in modules)
        {
            names.Add(module.Name);

            foreach (ScriptFunction function in module.Functions) names.Add(function.FullName);
        }

        IReadOnlyList<string> nearest = Suggestions.Nearest(query, names);

        return nearest.Count > 0
            ? $"Ничего не найдено по запросу '{query}'. Возможно: {string.Join(", ", nearest)}."
            : $"Ничего не найдено по запросу '{query}'. Список пространств: help().";
    }

    private static IEnumerable<IScriptModule> Ordered(IReadOnlyList<IScriptModule> modules)
    {
        var sorted = new List<IScriptModule>(modules);
        sorted.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        return sorted;
    }
}
