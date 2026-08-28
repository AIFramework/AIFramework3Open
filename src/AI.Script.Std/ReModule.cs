using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Text.RegularExpressions;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>re</c>: регулярные выражения.
/// </summary>
/// <remarks>
/// У каждого вызова свой таймаут: выражение вида <c>(a+)+$</c> на неудачной строке уходит в
/// экспоненциальный перебор, а скрипт пишет модель — значит, такое выражение рано или поздно
/// появится. Отказ по таймауту предпочтительнее зависшего прогона.
/// </remarks>
[ScriptModule("re", "Регулярные выражения: поиск, разбор, замена", Version = "0.1")]
public static class ReModule
{
    /// <summary>Потолок времени на одно сопоставление.</summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    [ScriptFn("test", "Есть ли в строке совпадение", Example = "re.test(s, \"^AIS[0-9]+$\")")]
    public static bool Test(
        [ScriptParam("строка")] string text,
        [ScriptParam("регулярное выражение")] string pattern,
        [ScriptParam("без учёта регистра")] bool ignoreCase = false)
        => Compile(pattern, ignoreCase).IsMatch(text);

    [ScriptFn("find", "Первое совпадение либо none", Example = "re.find(s, \"[0-9]+\")")]
    public static ScriptValue Find(
        [ScriptParam("строка")] string text,
        [ScriptParam("регулярное выражение")] string pattern,
        [ScriptParam("без учёта регистра")] bool ignoreCase = false)
    {
        Match match = Compile(pattern, ignoreCase).Match(text);

        return match.Success ? Describe(match) : ScriptValue.None;
    }

    [ScriptFn("find_all", "Все совпадения", Example = "re.find_all(s, \"[0-9]+\")")]
    public static ScriptList FindAll(
        [ScriptParam("строка")] string text,
        [ScriptParam("регулярное выражение")] string pattern,
        [ScriptParam("без учёта регистра")] bool ignoreCase = false)
    {
        var items = new List<ScriptValue>();

        foreach (Match match in Compile(pattern, ignoreCase).Matches(text)) items.Add(Describe(match));

        return ScriptList.From(items);
    }

    [ScriptFn("replace", "Заменяет совпадения", Example = "re.replace(s, \"\\\\s+\", to: \" \")")]
    public static string Replace(
        [ScriptParam("строка")] string text,
        [ScriptParam("регулярное выражение")] string pattern,
        [ScriptParam("чем заменить; $1 — первая группа")] string to = "",
        [ScriptParam("без учёта регистра")] bool ignoreCase = false)
        => Compile(pattern, ignoreCase).Replace(text, to);

    [ScriptFn("split", "Разбивает строку по выражению", Example = "re.split(s, \"[,;]\")")]
    public static ScriptList Split(
        [ScriptParam("строка")] string text,
        [ScriptParam("регулярное выражение")] string pattern,
        [ScriptParam("без учёта регистра")] bool ignoreCase = false)
    {
        string[] parts = Compile(pattern, ignoreCase).Split(text);
        var items = new ScriptValue[parts.Length];

        for (int i = 0; i < parts.Length; i++) items[i] = ScriptValue.Str(parts[i]);

        return ScriptList.Own(items);
    }

    /// <summary>
    /// Описывает совпадение записью: текст, позиция, группы.
    /// </summary>
    /// <remarks>
    /// Именованные группы попадают в поле <c>named</c>: без них разбор строки превращается в
    /// счёт по номерам, а номер группы меняется от любой правки выражения.
    /// </remarks>
    private static ScriptValue Describe(Match match)
    {
        var groups = new List<ScriptValue>();
        var named = new List<KeyValuePair<string, ScriptValue>>();

        for (int i = 1; i < match.Groups.Count; i++)
        {
            Group group = match.Groups[i];
            ScriptValue value = group.Success ? ScriptValue.Str(group.Value) : ScriptValue.None;

            groups.Add(value);

            if (!int.TryParse(group.Name, out _))
                named.Add(new KeyValuePair<string, ScriptValue>(group.Name, value));
        }

        return ScriptValue.Record(ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("text", ScriptValue.Str(match.Value)),
            new KeyValuePair<string, ScriptValue>("at", ScriptValue.Num(match.Index)),
            new KeyValuePair<string, ScriptValue>("groups", ScriptValue.List(ScriptList.From(groups))),
            new KeyValuePair<string, ScriptValue>("named", ScriptValue.Record(ScriptRecord.From(named))),
        ]));
    }

    private static Regex Compile(string pattern, bool ignoreCase)
    {
        RegexOptions options = RegexOptions.CultureInvariant;

        if (ignoreCase) options |= RegexOptions.IgnoreCase;

        try
        {
            return new Regex(pattern, options, MatchTimeout);
        }
        catch (ArgumentException exception)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"некорректное регулярное выражение: {exception.Message}",
                "обратная косая черта в строке пишется дважды: \"\\\\d+\"");
        }
    }
}
