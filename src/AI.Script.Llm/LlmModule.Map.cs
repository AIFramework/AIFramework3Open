using AI.LLM.Core.Models.Common.Messages;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Llm;

/// <summary>
/// Пакетная генерация: запрос на каждую строку таблицы, ответ — новой колонкой.
/// </summary>
/// <remarks>
/// «Опиши каждый товар» и «сравни три промпта на ста товарах» — это таблица на входе и таблица на
/// выходе. Цикл с <c>llm.ask</c> делает то же самое, но каждый раз по-своему: без параллельности,
/// без кэша и с потерей связи ответа со строкой.
/// <para>
/// Ответ кэшируется по отпечатку запроса — тексту, системной инструкции и температуре. Повторный
/// прогон опыта с теми же промптами не тратит ни одного токена: именно так сравнение трёх
/// промптов перестаёт стоить втрое при каждом исправлении скрипта.
/// </para>
/// </remarks>
public sealed partial class LlmModule
{
    /// <summary>Колонка ответа по умолчанию.</summary>
    public const string MapColumn = "answer";

    [ScriptFn("map", "Запрос модели на каждую строку: ответы новой колонкой, с кэшем",
        Example = "товары |> llm.map(prompt: row => \"Опиши ${row.name} в 40 словах\")")]
    public async Task<ScriptTable> Map(
        IScriptContext context,
        [ScriptParam("таблица строк")] ScriptTable rows,
        [ScriptParam("запрос: функция от строки, возвращающая текст")] ScriptCallable prompt,
        [ScriptParam("имя колонки для ответа")] string to = MapColumn,
        [ScriptParam("системная инструкция")] string system = "",
        [ScriptParam("температура")] double temperature = 0,
        [ScriptParam("отправлять запросы одновременно")] bool parallel = false)
    {
        var records = new ScriptValue[rows.RowCount];

        for (int i = 0; i < rows.RowCount; i++) records[i] = ScriptValue.Record(rows.Row(i));

        // Тексты запросов строятся заранее и асинхронно: лямбда запроса — обычный код скрипта,
        // и выносить её вызов в параллельные ветви модели значило бы плодить ветви интерпретатора.
        ScriptValue[] prompts = await context.CallEachAsync(ScriptValue.Fn(prompt), records, 1).ConfigureAwait(false);
        var answers = new ScriptValue[rows.RowCount];
        int degree = parallel ? context.Parallelism : 1;

        await Parallel.ForAsync(0, rows.RowCount,
            new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = context.Cancellation },
            async (i, _) =>
            {
                string text = prompts[i].AsString("llm.map: запрос");
                answers[i] = await CachedAsync(context, text, system, temperature).ConfigureAwait(false);
            }).ConfigureAwait(false);

        context.CountAllocation(rows.RowCount);

        return rows.With(ScriptColumn.Own(to, answers));
    }

    /// <summary>
    /// Ответ модели из кэша прогона либо из сети.
    /// </summary>
    /// <remarks>
    /// Ключ — отпечаток всего, что определяет ответ: запроса, инструкции и температуры. Модель в
    /// ключ не входит: её выбирает хост, а не скрипт, и при смене модели хост сам решает, сбросить
    /// ли кэш.
    /// </remarks>
    private async Task<ScriptValue> CachedAsync(IScriptContext context, string text, string system, double temperature)
    {
        string key = "llm.map" + ValueDigest.Hash(string.Join('', system, temperature.ToString("R"), text));

        if (context.Cache.TryGet(key, out ScriptValue cached)) return cached;

        var messages = new List<LLMMessage>(2);

        if (!string.IsNullOrWhiteSpace(system)) messages.Add(LLMMessage.CreateMessage(Roles.System, system));

        messages.Add(LLMMessage.CreateMessage(Roles.User, text));

        ScriptValue answer = ScriptValue.Str(
            await SendAsync(context, messages, temperature, 0, "llm.map").ConfigureAwait(false));

        context.Cache.Put(key, answer);

        return answer;
    }
}
