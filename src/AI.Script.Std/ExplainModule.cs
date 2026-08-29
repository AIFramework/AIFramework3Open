using AI.ExplainitALL.Metrics;
using AI.ExplainitALL.Metrics.SimAlgs;
using AI.ExplainitALL.Metrics.SimMetrics;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>explain</c>: проверка ответа на опору в источнике.
/// </summary>
/// <remarks>
/// Отвечает на вопрос, который встаёт сразу после <c>llm.ask</c>: сказано ли это в документе
/// или придумано. Проверка не понимает смысла — она сопоставляет куски ответа кускам
/// источника по совпадению n-грамм, и поэтому её вывод асимметричен: низкая опора означает
/// «не нашлось подтверждения», а не «неправда». Обратное сильнее и этим методом не
/// доказывается.
/// <para>
/// Сеть и ключи не нужны: сравнение идёт по тексту. Тем и ценно — проверять ответ модели
/// второй моделью значит удваивать и стоимость, и сомнение.
/// </para>
/// </remarks>
[ScriptModule("explain", "Проверка ответа на опору в источнике — без сети: сходство, подтверждения",
    Version = "0.1")]
public static class ExplainModule
{
    /// <summary>Размер n-граммы по умолчанию.</summary>
    private const int DefaultNgram = 2;

    /// <summary>Порог, выше которого кусок считается подтверждённым.</summary>
    private const double DefaultThreshold = 0.6;

    [ScriptFn("similarity", "Сходство двух текстов по совпадению n-грамм от 0 до 1",
        Example = "explain.similarity(ответ, источник)")]
    public static double Similarity(
        [ScriptParam("первый текст")] string a,
        [ScriptParam("второй текст")] string b,
        [ScriptParam("размер n-граммы")] int n = DefaultNgram)
    {
        RequireNgram(n, "explain.similarity");

        return new NgramJaccardMetric(n).Sim(a ?? string.Empty, b ?? string.Empty);
    }

    [ScriptFn("blocks", "Разбиение текста на блоки, которыми идёт сверка",
        Example = "explain.blocks(документ)")]
    public static ScriptList Blocks(
        IScriptContext context,
        [ScriptParam("текст")] string text)
    {
        string[] parts = Checker(DefaultNgram).LoadDoc(text ?? string.Empty);
        var items = new List<ScriptValue>(parts.Length);

        foreach (string part in parts) items.Add(ScriptValue.Str(part));

        context.CountAllocation(parts.Length);

        return ScriptList.From(items);
    }

    /// <summary>
    /// Доля ответа, подтверждённая источником.
    /// </summary>
    /// <remarks>
    /// Считается по длине подтверждённых кусков, а не по их числу: одно подтверждённое слово
    /// и подтверждённый абзац — не одно и то же, а по количеству блоков они равны.
    /// </remarks>
    [ScriptFn("grounded", "Доля ответа, опирающаяся на источник, от 0 до 1",
        Example = "explain.grounded(doc: источник, answer: ответ)")]
    public static double Grounded(
        [ScriptParam("текст источника")] string doc,
        [ScriptParam("проверяемый ответ")] string answer,
        [ScriptParam("порог сходства от 0 до 1")] double threshold = DefaultThreshold,
        [ScriptParam("размер n-граммы")] int n = DefaultNgram)
    {
        RequireTexts(doc, answer, "explain.grounded");
        RequireThreshold(threshold, "explain.grounded");
        RequireNgram(n, "explain.grounded");

        return Checker(n).GetConf(doc, answer, threshold);
    }

    [ScriptFn("hallucination", "Доля ответа без опоры в источнике, от 0 до 1",
        Example = "explain.hallucination(doc: источник, answer: ответ)")]
    public static double Hallucination(
        [ScriptParam("текст источника")] string doc,
        [ScriptParam("проверяемый ответ")] string answer,
        [ScriptParam("порог сходства от 0 до 1")] double threshold = DefaultThreshold,
        [ScriptParam("размер n-граммы")] int n = DefaultNgram)
    {
        RequireTexts(doc, answer, "explain.hallucination");
        RequireThreshold(threshold, "explain.hallucination");
        RequireNgram(n, "explain.hallucination");

        return Checker(n).GetHallucinationsProb(doc, answer, threshold);
    }

    /// <summary>
    /// Что именно в источнике подтверждает каждый кусок ответа.
    /// </summary>
    /// <remarks>
    /// Возвращаются только подтверждённые куски: неподтверждённых в таблице нет, и их наличие
    /// видно по разнице с <c>explain.blocks(ответ)</c>. Так отчёт отвечает на вопрос «на чём
    /// это основано», а не «чего не нашлось», — а второй вопрос уже мерит
    /// <c>explain.hallucination</c>.
    /// </remarks>
    [ScriptFn("support", "Подтверждения из источника для кусков ответа таблицей",
        Example = "show explain.support(doc: источник, answer: ответ, top: 2)")]
    public static ScriptTable Support(
        IScriptContext context,
        [ScriptParam("текст источника")] string doc,
        [ScriptParam("проверяемый ответ")] string answer,
        [ScriptParam("порог сходства от 0 до 1")] double threshold = DefaultThreshold,
        [ScriptParam("сколько подтверждений искать на кусок")] int top = 1,
        [ScriptParam("размер n-граммы")] int n = DefaultNgram)
    {
        RequireTexts(doc, answer, "explain.support");
        RequireThreshold(threshold, "explain.support");
        RequireNgram(n, "explain.support");

        if (top < 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "explain.support: нужно хотя бы одно подтверждение");

        List<AnalyzeElement> found = Checker(n).GetSupportSeq(doc, answer, threshold, top);

        var claims = new List<ScriptValue>();
        var supports = new List<ScriptValue>();
        var positions = new List<ScriptValue>();

        foreach (AnalyzeElement element in found)
        {
            for (int i = 0; i < element.SupportBlocks.Count; i++)
            {
                claims.Add(ScriptValue.Str(element.AnswerBlock));
                supports.Add(ScriptValue.Str(element.SupportBlocks[i]));
                positions.Add(ScriptValue.Num(element.SupportBlocksIndexInDoc[i]));
            }
        }

        context.CountAllocation(claims.Count * 3L);

        return ScriptTable.Create(
        [
            ScriptColumn.Own("claim", [.. claims]),
            ScriptColumn.Own("support", [.. supports]),
            ScriptColumn.Own("position", [.. positions]),
        ]);
    }

    // --- внутреннее ---

    private static CheckingForHallucinations Checker(int n) => new(new NgramJaccardSim(n));

    private static void RequireTexts(string doc, string answer, string what)
    {
        if (string.IsNullOrWhiteSpace(doc))
            throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: источник пуст");

        if (string.IsNullOrWhiteSpace(answer))
            throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: проверяемый ответ пуст");
    }

    private static void RequireThreshold(double threshold, string what)
    {
        if (threshold is < 0 or > 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: порог сходства лежит в [0, 1]");
    }

    private static void RequireNgram(int n, string what)
    {
        if (n < 1) throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: размер n-граммы не меньше единицы");
    }
}
