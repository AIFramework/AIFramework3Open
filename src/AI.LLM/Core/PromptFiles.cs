using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AI.LLM.Core;

/// <summary>
/// Тексты промптов движка из встроенных ресурсов: файл <c>Resources/Prompts/имя.md</c> ресурсом <c>Prompts.имя.md</c>.
/// </summary>
/// <remarks>
/// Тот же вид, что у промптов продуктов поверх движка: файл, а не строка в коде, поэтому текст правится и сверяется как
/// текст, а отладчики находят его по имени ресурса. Подстановка <c>{ключ}</c> проходит шаблон один раз: подставленное
/// значение повторно не разбирается, и фигурные скобки JSON в примерах ответа ключами не считаются. Подмена
/// (<see cref="Override"/>) нужна отладчику: правка проверяется ходом без пересборки.
/// </remarks>
public static partial class PromptFiles
{
    private static readonly ConcurrentDictionary<string, string> Overrides = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    /// <summary>Текст промпта; подмена отладчика сильнее ресурса. Ресурса нет, и это ошибка сборки, а не пустая строка.</summary>
    public static string Load(string name) =>
        Overrides.TryGetValue(name, out var text) ? text : Cache.GetOrAdd(name, static n => Read(n));

    /// <summary>Текст промпта с подстановкой <c>{ключ}</c>; ключ без значения остается как есть.</summary>
    public static string Format(string name, params (string Key, string Value)[] values)
    {
        var map = values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
        return Key().Replace(Load(name), m => map.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
    }

    /// <summary>Подменяет текст промпта до конца процесса; <c>null</c> снимает подмену.</summary>
    public static void Override(string name, string text)
    {
        if (text is null) Overrides.TryRemove(name, out _);
        else Overrides[name] = text;
    }

    private static string Read(string name)
    {
        var resource = "Prompts." + name + ".md";
        using var stream = typeof(PromptFiles).Assembly.GetManifestResourceStream(resource)
                           ?? throw new InvalidOperationException($"Промпт {resource} не встроен в {typeof(PromptFiles).Assembly.GetName().Name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"\{([A-Za-z0-9_]+)\}")]
    private static partial Regex Key();
}
