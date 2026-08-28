using AI.Script.Binding;
using AI.Script.Runtime;

namespace AI.Script.Std;

/// <summary>Пространство <c>str</c>: операции над строками.</summary>
[ScriptModule("str", "Операции над строками", Version = "0.1")]
public static class StrModule
{
    [ScriptFn("upper", "Верхний регистр", Example = "str.upper(\"код\")")]
    public static string Upper([ScriptParam("строка")] string text) => text.ToUpperInvariant();

    [ScriptFn("lower", "Нижний регистр", Example = "str.lower(\"КОД\")")]
    public static string Lower([ScriptParam("строка")] string text) => text.ToLowerInvariant();

    [ScriptFn("trim", "Убирает пробелы по краям", Example = "str.trim(\"  x \")")]
    public static string Trim([ScriptParam("строка")] string text) => text.Trim();

    [ScriptFn("split", "Разбивает строку по разделителю", Example = "str.split(line, by: \",\")")]
    public static ScriptList Split(
        [ScriptParam("строка")] string text,
        [ScriptParam("разделитель")] string by = " ",
        [ScriptParam("отбрасывать пустые части")] bool dropEmpty = false)
    {
        string[] parts = text.Split(
            by,
            dropEmpty ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);

        var items = new ScriptValue[parts.Length];
        for (int i = 0; i < parts.Length; i++) items[i] = ScriptValue.Str(parts[i]);

        return ScriptList.Own(items);
    }

    [ScriptFn("join", "Склеивает последовательность строк", Example = "parts |> str.join(\"\\n\")")]
    public static string Join(
        [ScriptParam("последовательность")] ScriptList parts,
        [ScriptParam("разделитель")] string by = "")
    {
        var texts = new List<string>(parts.Count);

        foreach (ScriptValue part in parts) texts.Add(ScriptFormatter.Format(part));

        return string.Join(by, texts);
    }

    [ScriptFn("replace", "Заменяет подстроку", Example = "str.replace(s, from: \",\", to: \".\")")]
    public static string Replace(
        [ScriptParam("строка")] string text,
        [ScriptParam("что заменить")] string from,
        [ScriptParam("на что заменить")] string to = "")
        => text.Replace(from, to, StringComparison.Ordinal);

    [ScriptFn("contains", "Содержит ли строка подстроку", Example = "str.contains(s, \"ошибка\")")]
    public static bool Contains(
        [ScriptParam("строка")] string text,
        [ScriptParam("подстрока")] string what)
        => text.Contains(what, StringComparison.Ordinal);

    [ScriptFn("starts_with", "Начинается ли строка с подстроки", Example = "str.starts_with(s, \"AIS\")")]
    public static bool StartsWith(
        [ScriptParam("строка")] string text,
        [ScriptParam("подстрока")] string what)
        => text.StartsWith(what, StringComparison.Ordinal);

    [ScriptFn("ends_with", "Заканчивается ли строка подстрокой", Example = "str.ends_with(name, \".ais\")")]
    public static bool EndsWith(
        [ScriptParam("строка")] string text,
        [ScriptParam("подстрока")] string what)
        => text.EndsWith(what, StringComparison.Ordinal);

    [ScriptFn("index_of", "Позиция подстроки; -1, если её нет", Example = "str.index_of(s, \"=\")")]
    public static double IndexOf(
        [ScriptParam("строка")] string text,
        [ScriptParam("подстрока")] string what)
        => text.IndexOf(what, StringComparison.Ordinal);

    [ScriptFn("sub", "Подстрока [from, to)", Example = "str.sub(s, from: 0, to: 5)")]
    public static string Sub(
        [ScriptParam("строка")] string text,
        [ScriptParam("начало включительно")] int from = 0,
        [ScriptParam("конец исключительно; -1 — до конца")] int to = -1)
    {
        int end = to < 0 ? text.Length : Math.Min(to, text.Length);
        int start = Math.Clamp(from, 0, end);

        return text[start..end];
    }

    [ScriptFn("repeat", "Повторяет строку", Example = "str.repeat(\"-\", times: 40)")]
    public static string Repeat(
        [ScriptParam("строка")] string text,
        [ScriptParam("сколько раз")] int times = 1)
        => times <= 0 ? string.Empty : string.Concat(Enumerable.Repeat(text, times));

    [ScriptFn("pad_left", "Дополняет строку слева до нужной длины", Example = "str.pad_left(s, width: 8)")]
    public static string PadLeft(
        [ScriptParam("строка")] string text,
        [ScriptParam("итоговая ширина")] int width,
        [ScriptParam("символ-заполнитель")] string with = " ")
        => text.PadLeft(width, with.Length > 0 ? with[0] : ' ');

    [ScriptFn("pad_right", "Дополняет строку справа до нужной длины", Example = "str.pad_right(s, width: 8)")]
    public static string PadRight(
        [ScriptParam("строка")] string text,
        [ScriptParam("итоговая ширина")] int width,
        [ScriptParam("символ-заполнитель")] string with = " ")
        => text.PadRight(width, with.Length > 0 ? with[0] : ' ');

    [ScriptFn("lines", "Разбивает текст на строки", Example = "str.lines(text)")]
    public static ScriptList Lines([ScriptParam("текст")] string text)
    {
        string[] parts = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var items = new ScriptValue[parts.Length];

        for (int i = 0; i < parts.Length; i++) items[i] = ScriptValue.Str(parts[i]);

        return ScriptList.Own(items);
    }
}
