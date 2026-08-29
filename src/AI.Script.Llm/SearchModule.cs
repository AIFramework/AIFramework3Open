using AI.DataStructs.Algebraic;
using AI.LLM.Core.Abstractions;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Std;

namespace AI.Script.Llm;

/// <summary>
/// Пространство <c>search</c>: семантический поиск по корпусу документов.
/// </summary>
/// <remarks>
/// Индекс точный, перебором, без приближённых структур. Это сознательное ограничение:
/// прототип конвейера работает с сотнями и тысячами документов, где перебор занимает
/// миллисекунды, а приближённый индекс из <c>AI.Faiss</c> втащил бы в сборку нативные
/// библиотеки под каждую платформу ради выигрыша, которого на таких размерах не видно.
/// Когда понадобятся миллионы векторов, это отдельная реализация за тем же интерфейсом,
/// а не переделка языка.
/// </remarks>
[ScriptModule("search", "Поиск по корпусу: семантический, словесный и гибридный")]
public sealed class SearchModule
{
    /// <summary>Тип-тег дескриптора индекса.</summary>
    public const string IndexHandle = "search.index";

    private readonly IEmbedderService? _embedder;

    /// <summary>Создаёт модуль поверх службы эмбеддингов хоста.</summary>
    /// <param name="embedder">Эмбеддер; <c>null</c> — доступен только словесный поиск.</param>
    public SearchModule(IEmbedderService? embedder = null) => _embedder = embedder;

    /// <summary>Собирает модуль языка.</summary>
    public ScriptModule ToScriptModule() => ScriptModule.FromObject(this);

    [ScriptFn("of", "Строит индекс по списку документов", Returns = IndexHandle,
        Example = "let index = search.of(docs)")]
    public async Task<ScriptHandle> Of(
        IScriptContext context,
        [ScriptParam("список текстов")] ScriptList documents,
        [ScriptParam("вид индекса: \"semantic\", \"words\" либо \"hybrid\"")] string kind = "semantic")
    {
        var texts = new List<string>(documents.Count);

        for (int i = 0; i < documents.Count; i++) texts.Add(ScriptFormatter.Format(documents[i]));

        if (texts.Count == 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "search.of: список документов пуст");

        SearchIndex index = await BuildAsync(context, texts, kind).ConfigureAwait(false);

        return new ScriptHandle(IndexHandle, index, $"{index.Kind}, документов: {index.Count}");
    }

    [ScriptFn("size", "Сколько документов в индексе", Example = "index.size()")]
    [ScriptMethod(IndexHandle)]
    public static double Size([ScriptParam("индекс")] ScriptHandle index) => Unwrap(index).Count;

    [ScriptFn("kind", "Вид индекса", Example = "index.kind()")]
    [ScriptMethod(IndexHandle)]
    public static string Kind([ScriptParam("индекс")] ScriptHandle index) => Unwrap(index).Kind;

    /// <summary>
    /// Выдача по запросу таблицей: номер документа, текст, оценка.
    /// </summary>
    /// <remarks>
    /// Таблица, а не список записей: результат поиска почти всегда идёт дальше в
    /// <c>table.filter</c> либо в отчёт, и превращать его туда-обратно незачем.
    /// </remarks>
    [ScriptFn("query", "Ищет по запросу; возвращает таблицу с полями doc, text, score",
        Example = "let found = index.query(\"как настроить прокси\", top: 3)")]
    [ScriptMethod(IndexHandle)]
    public async Task<ScriptTable> Query(
        IScriptContext context,
        [ScriptParam("индекс")] ScriptHandle handle,
        [ScriptParam("текст запроса")] string query,
        [ScriptParam("сколько лучших вернуть")] int top = 5)
    {
        if (top <= 0) throw new ScriptError(DiagnosticCodes.BadOperand, "search.query: 'top' должен быть больше нуля");

        SearchIndex index = Unwrap(handle);

        Vector? embedding = index.NeedsEmbedding
            ? await EmbedAsync(context, query, "search.query").ConfigureAwait(false)
            : null;

        IReadOnlyList<(int Document, double Score)> found = index.Search(query, embedding, top);

        var docs = new ScriptValue[found.Count];
        var texts = new ScriptValue[found.Count];
        var scores = new ScriptValue[found.Count];

        for (int i = 0; i < found.Count; i++)
        {
            docs[i] = ScriptValue.Num(found[i].Document);
            texts[i] = ScriptValue.Str(index.Text(found[i].Document));
            scores[i] = ScriptValue.Num(found[i].Score);
        }

        context.CountAllocation(found.Count * 3L);

        return ScriptTable.Create(
        [
            ScriptColumn.Own("doc", docs),
            ScriptColumn.Own("text", texts),
            ScriptColumn.Own("score", scores),
        ]);
    }

    /// <summary>
    /// Собранный из выдачи контекст для запроса к модели.
    /// </summary>
    /// <remarks>
    /// Самая частая операция после поиска — склеить найденное в один кусок текста для промпта.
    /// Делая это в скрипте руками, каждый раз заново решают, чем разделять и нумеровать ли
    /// фрагменты; здесь решено один раз и одинаково.
    /// </remarks>
    [ScriptFn("context", "Склеивает найденное в текст для промпта",
        Example = "let ctx = index.context(question, top: 4)")]
    [ScriptMethod(IndexHandle)]
    public async Task<string> Context(
        IScriptContext context,
        [ScriptParam("индекс")] ScriptHandle handle,
        [ScriptParam("текст запроса")] string query,
        [ScriptParam("сколько фрагментов взять")] int top = 5,
        [ScriptParam("потолок длины в символах")] int limit = 4000)
    {
        SearchIndex index = Unwrap(handle);

        Vector? embedding = index.NeedsEmbedding
            ? await EmbedAsync(context, query, "search.context").ConfigureAwait(false)
            : null;

        IReadOnlyList<(int Document, double Score)> found = index.Search(query, embedding, top);
        var builder = new System.Text.StringBuilder();

        foreach ((int document, double _) in found)
        {
            string text = index.Text(document);

            if (limit > 0 && builder.Length + text.Length > limit) break;

            _ = builder.Append('[').Append(document).Append("] ").Append(text).Append('\n');
        }

        return builder.ToString().TrimEnd('\n');
    }

    private async Task<SearchIndex> BuildAsync(IScriptContext context, IReadOnlyList<string> texts, string kind)
    {
        switch (kind)
        {
            case "words":
                return new SearchIndex(texts, null, Words(texts));

            case "semantic":
                return new SearchIndex(texts, await EmbedAllAsync(context, texts).ConfigureAwait(false), null);

            case "hybrid":
                return new SearchIndex(
                    texts,
                    await EmbedAllAsync(context, texts).ConfigureAwait(false),
                    Words(texts));

            default:
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"search.of: неизвестный вид индекса '{kind}'",
                    "известны: \"semantic\" — по смыслу, \"words\" — по словам (BM25), \"hybrid\" — оба сразу");
        }
    }

    private static TextIndex Words(IReadOnlyList<string> texts) => TextIndexes.Bm25(texts);

    private async Task<Vector[]> EmbedAllAsync(IScriptContext context, IReadOnlyList<string> texts)
    {
        IEmbedderService embedder = RequireEmbedder("search.of");

        context.Network.Require("search.of");

        context.BeginExternalCall();

        Vector[] vectors = await embedder.EncodeAsync(texts, context.Cancellation).ConfigureAwait(false);

        context.CountExternal();

        if (vectors.Length == texts.Count) return vectors;

        throw new ScriptError(
            DiagnosticCodes.SizeMismatch,
            $"search.of: эмбеддер вернул {vectors.Length} векторов на {texts.Count} документов");
    }

    private async Task<Vector> EmbedAsync(IScriptContext context, string query, string what)
    {
        IEmbedderService embedder = RequireEmbedder(what);

        context.Network.Require(what);

        context.BeginExternalCall();

        Vector vector = await embedder.EncodeQuestionAsync(query, context.Cancellation).ConfigureAwait(false);

        context.CountExternal();

        return vector;
    }

    private static SearchIndex Unwrap(ScriptHandle handle) => (SearchIndex)handle.Target;

    private IEmbedderService RequireEmbedder(string what) =>
        _embedder ?? throw new ScriptError(
            DiagnosticCodes.UnknownFunction,
            $"{what}: служба эмбеддингов не подключена хостом",
            "постройте словесный индекс: search.of(документы, kind: \"words\")");
}
