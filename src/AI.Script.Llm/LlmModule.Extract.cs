using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Llm;

/// <summary>
/// Извлечение полей из текстов моделью — сразу таблицей.
/// </summary>
/// <remarks>
/// Пара к <c>doc.extract</c>: там значение находят правила, здесь — модель. Разделены они не
/// по капризу, а по цене и по надёжности: правило бесплатно и повторяемо, модель стоит денег и
/// отвечает по-разному на один и тот же текст. Поэтому сначала правила, а моделью добирают то,
/// что правилами не берётся, — и в скрипте видно, где именно ушли деньги.
/// <para>
/// Ответ приводится к объявленной схеме теми же правилами, что и <c>llm.json</c>: «1 234,50 ₽»
/// в поле <c>dec</c> становится точным числом, а не строкой, на которой скрипт упадёт двумя
/// шагами ниже.
/// </para>
/// </remarks>
public sealed partial class LlmModule
{
    /// <summary>Сколько знаков текста уходит модели: дальше начинается плата за пересказ.</summary>
    private const int ExtractLimit = 12_000;

    [ScriptFn("extract", "Достаёт поля по схеме из каждого текста: строка на текст",
        Example = "llm.extract(тексты, schema: { срок: \"date\", сумма: \"dec\" })")]
    public async Task<ScriptTable> Extract(
        IScriptContext context,
        [ScriptParam("текст либо список текстов")] ScriptValue texts,
        [ScriptParam("схема: поле — тип (str, num, dec, bool, date)")] ScriptRecord schema,
        [ScriptParam("что это за тексты: подсказка модели")] string about = "",
        [ScriptParam("температура")] double temperature = 0)
    {
        IReadOnlyList<string> items = Texts(texts);

        if (schema.Count == 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "llm.extract: схема пуста, нечего доставать");

        var rows = new List<ScriptRecord>(items.Count);

        // Последовательно, а не пачкой: каждый текст — отдельный оплаченный запрос, и потолки
        // прогона обязаны сработать на середине корпуса, а не после того, как он весь ушёл в сеть.
        var failed = new List<int>();

        foreach (string text in items)
        {
            string answer = await Ask(context, Instruction(text, schema, about), Rules, temperature).ConfigureAwait(false);

            if (JsonIsland.TryExtract(answer, out ScriptValue parsed)
                && SchemaFit.TryFit(parsed, schema, out ScriptValue fitted, out _, missing: true))
            {
                rows.Add(fitted.AsRecord());
                continue;
            }

            failed.Add(rows.Count);
            rows.Add(ScriptRecord.Empty);
        }

        // Неразобранный ответ дает пустую строку, как и текст без полей; различие видно в журнале
        // прогона, иначе пустоту приняли бы за «в документе этого нет».
        if (failed.Count > 0)
            context.Print($"llm.extract: ответ модели не разобран для текстов {string.Join(", ", failed)}; их строки пусты");

        context.CountAllocation((long)rows.Count * (schema.Count + 1));

        return Table(rows, schema);
    }

    /// <summary>Системная инструкция: одна на все извлечения, чтобы ответы были сравнимы.</summary>
    private const string Rules =
        "Ты извлекаешь поля из документов. В ответе только объект JSON с запрошенными полями. "
        + "Чего в тексте нет, оставляй пустым (null) — не угадывай и не считай.";

    private static string Instruction(string text, ScriptRecord schema, string about)
    {
        string source = text.Length <= ExtractLimit ? text : text[..ExtractLimit];
        string what = string.IsNullOrWhiteSpace(about) ? "текста" : about;

        return $"Достань из {what} поля: {SchemaFit.Describe(schema)}.\n\nТекст:\n{source}";
    }

    /// <summary>Тексты из аргумента: одна строка, список строк либо колонка таблицы.</summary>
    private static IReadOnlyList<string> Texts(ScriptValue value)
    {
        if (value.Type == ScriptType.Str) return [value.AsString()];

        if (value.Type == ScriptType.List)
        {
            ScriptList list = value.AsList();
            var items = new List<string>(list.Count);

            for (int i = 0; i < list.Count; i++) items.Add(list[i].AsString("llm.extract: текст"));

            return items;
        }

        throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"llm.extract: ожидались текст либо список текстов, получено {value.Type.ToName()}",
            "колонка таблицы годится: llm.extract(table.column(t, \"текст\"), schema: { ... })");
    }

    /// <summary>
    /// Таблица из разобранных ответов: номер текста и колонка на каждое поле схемы.
    /// </summary>
    /// <remarks>
    /// Номер источника обязателен: извлечённое значение без указания, из какого текста оно
    /// взято, невозможно ни проверить, ни соединить с остальными данными.
    /// </remarks>
    private static ScriptTable Table(IReadOnlyList<ScriptRecord> rows, ScriptRecord schema)
    {
        var columns = new List<ScriptColumn>(schema.Count + 1);
        var source = new ScriptValue[rows.Count];

        for (int i = 0; i < rows.Count; i++) source[i] = ScriptValue.Num(i);

        columns.Add(ScriptColumn.Own("source", source));

        foreach (var field in schema.Pairs())
        {
            var values = new ScriptValue[rows.Count];

            for (int i = 0; i < rows.Count; i++)
                values[i] = rows[i].TryGet(field.Key, out ScriptValue value) ? value : ScriptValue.None;

            columns.Add(ScriptColumn.Own(field.Key, values));
        }

        return ScriptTable.Create(columns);
    }
}
