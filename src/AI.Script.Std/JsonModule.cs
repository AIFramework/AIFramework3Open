using AI.Script.Binding;
using AI.Script.Runtime;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>json</c>: JSON из строки и обратно.
/// </summary>
/// <remarks>
/// Файлы JSON читает <c>io.read_json</c>, но ответы служб и поля документов приходят строкой, и
/// без разбора строки клей между шагами писался бы обходами через файл. Перевод тот же, что у
/// файлов (<see cref="Json"/>): объект — запись, массив — список, <c>null</c> — <c>none</c>.
/// </remarks>
[ScriptModule("json", "JSON из строки в значения и обратно", Version = "0.1", Group = "данные")]
public static class JsonModule
{
    [ScriptFn("parse", "Разбирает JSON из строки", Example = "json.parse(\"{\\\"a\\\": [1, 2]}\").a")]
    public static ScriptValue Parse([ScriptParam("текст JSON")] string text) => Json.Parse(text, "json.parse");

    [ScriptFn("write", "Печатает значение строкой JSON", Example = "json.write({ a: 1, b: [true, none] })")]
    public static string Write(
        [ScriptParam("значение")] ScriptValue value,
        [ScriptParam("человекочитаемое форматирование")] bool pretty = false)
        => Json.Write(value, pretty);
}
