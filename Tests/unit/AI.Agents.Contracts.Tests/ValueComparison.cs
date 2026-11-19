using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Сравнение по значению для круга через JSON: равенство записей сравнивает списки и JSON-элементы по
/// ссылке, поэтому здесь свой обход.
/// </summary>
internal static class ValueComparison
{
    /// <summary>
    /// Различия двух значений: путь и описание; пусто — значения равны.
    /// </summary>
    public static List<string> Differences(object? expected, object? actual)
    {
        var differences = new List<string>();
        Compare(expected, actual, "$", differences);
        return differences;
    }

    /// <summary>
    /// Свойства образца, которые не заполнены: <see langword="null"/>, пустая коллекция, <see langword="false"/>,
    /// нулевое значение. Проверяются только свойства с открытым set/init.
    /// </summary>
    public static List<string> UnfilledProperties(object sample)
    {
        var unfilled = new List<string>();

        foreach (PropertyInfo property in WritableProperties(sample.GetType()))
        {
            object? value = property.GetValue(sample);

            bool empty = value switch
            {
                null => true,
                string text => text.Length == 0,
                JsonElement element => element.ValueKind == JsonValueKind.Undefined,
                IEnumerable sequence => !sequence.GetEnumerator().MoveNext(),
                _ when value.GetType().IsValueType => value.Equals(Activator.CreateInstance(value.GetType())),
                _ => false,
            };

            if (empty)
                unfilled.Add(property.Name);
        }

        return unfilled;
    }

    /// <summary>Открытые свойства экземпляра с открытым set/init.</summary>
    public static IEnumerable<PropertyInfo> WritableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0 && property.SetMethod?.IsPublic == true);

    private static void Compare(object? expected, object? actual, string path, List<string> differences)
    {
        if (expected is null || actual is null)
        {
            if (expected is not null || actual is not null)
                differences.Add($"{path}: ожидалось {Show(expected)}, получено {Show(actual)}");

            return;
        }

        switch (expected)
        {
            case JsonElement left when actual is JsonElement right:
                if (!JsonElement.DeepEquals(left, right))
                    differences.Add($"{path}: JSON {left.GetRawText()} ≠ {right.GetRawText()}");
                return;

            case JsonNode left when actual is JsonNode right:
                if (!JsonNode.DeepEquals(left, right))
                    differences.Add($"{path}: JSON {left.ToJsonString()} ≠ {right.ToJsonString()}");
                return;

            case string or decimal or DateTimeOffset or DateTime or TimeSpan or Enum:
            case var _ when expected.GetType().IsPrimitive:
                if (!expected.Equals(actual))
                    differences.Add($"{path}: {Show(expected)} ≠ {Show(actual)}");
                return;

            case IDictionary left when actual is IDictionary right:
                CompareDictionaries(left, right, path, differences);
                return;

            case IEnumerable left when actual is IEnumerable right:
                CompareSequences(left, right, path, differences);
                return;
        }

        if (expected.GetType() != actual.GetType())
        {
            differences.Add($"{path}: тип {expected.GetType().Name} ≠ {actual.GetType().Name}");
            return;
        }

        foreach (PropertyInfo property in expected.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
                continue;

            Compare(property.GetValue(expected), property.GetValue(actual), $"{path}.{property.Name}", differences);
        }
    }

    private static void CompareDictionaries(IDictionary expected, IDictionary actual, string path, List<string> differences)
    {
        if (expected.Count != actual.Count)
            differences.Add($"{path}: ключей {expected.Count} ≠ {actual.Count}");

        foreach (DictionaryEntry entry in expected)
        {
            if (!actual.Contains(entry.Key))
            {
                differences.Add($"{path}[{entry.Key}]: ключа нет после чтения");
                continue;
            }

            Compare(entry.Value, actual[entry.Key], $"{path}[{entry.Key}]", differences);
        }
    }

    private static void CompareSequences(IEnumerable expected, IEnumerable actual, string path, List<string> differences)
    {
        List<object?> left = expected.Cast<object?>().ToList();
        List<object?> right = actual.Cast<object?>().ToList();

        if (left.Count != right.Count)
        {
            differences.Add($"{path}: элементов {left.Count} ≠ {right.Count}");
            return;
        }

        for (int i = 0; i < left.Count; i++)
            Compare(left[i], right[i], $"{path}[{i}]", differences);
    }

    private static string Show(object? value) => value is null ? "null" : $"«{value}»";
}
