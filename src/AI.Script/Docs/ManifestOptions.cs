namespace AI.Script.Docs;

/// <summary>Формат манифеста возможностей.</summary>
public enum ManifestFormat
{
    /// <summary>Markdown для системного промпта и для чтения человеком.</summary>
    Markdown,

    /// <summary>JSON для инструментов и внешних клиентов.</summary>
    Json,

    /// <summary>Только сигнатуры, по одной в строке: самый экономный по токенам вид.</summary>
    Compact,
}

/// <summary>
/// Что включить в манифест возможностей.
/// </summary>
/// <remarks>
/// Полный перечень на сотни функций в системный промпт не помещается и помещаться не должен
/// (DESIGN.md §14.1). Поэтому манифест строится не «весь или ничего», а срезами: список
/// пространств всегда, подробности — по запросу и по конкретному пространству.
/// </remarks>
public sealed class ManifestOptions
{
    /// <summary>Формат вывода.</summary>
    public ManifestFormat Format { get; set; } = ManifestFormat.Markdown;

    /// <summary>Пространства имён; пусто — все.</summary>
    public IReadOnlyCollection<string> Namespaces { get; set; } = [];

    /// <summary>Включать описания параметров.</summary>
    public bool IncludeParameters { get; set; } = true;

    /// <summary>Включать примеры вызова.</summary>
    public bool IncludeExamples { get; set; }

    /// <summary>
    /// Потолок числа функций; ноль и меньше — без потолка.
    /// </summary>
    /// <remarks>
    /// Усечение не молчаливое: сколько функций осталось за кадром, всегда написано в тексте.
    /// Иначе модель читает урезанный список как исчерпывающий и «узнаёт», что нужной функции
    /// не существует.
    /// </remarks>
    public int MaxFunctions { get; set; }

    /// <summary>Только список пространств имён, без функций.</summary>
    public bool IndexOnly { get; set; }

    /// <summary>Краткий индекс пространств: то, что кладётся в системный промпт всегда.</summary>
    public static ManifestOptions Index => new() { IndexOnly = true };

    /// <summary>Полный манифест с примерами: для документации и для внешних клиентов.</summary>
    public static ManifestOptions Full => new() { IncludeExamples = true };
}
