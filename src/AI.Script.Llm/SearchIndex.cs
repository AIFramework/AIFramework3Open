using AI.DataStructs.Algebraic;
using AI.NLP;
using AI.Script.Semantics;
using AI.Script.Runtime;
using AI.Script.Std;

namespace AI.Script.Llm;

/// <summary>
/// Индекс корпуса: векторы, слова либо и то и другое.
/// </summary>
/// <remarks>
/// Три вида поиска за одним типом, потому что различаются они одним — чем считается оценка
/// документа. Разводить их по трём дескрипторам значило бы заставить скрипт помнить, какой из
/// них у него в руках, ради разницы в одну строку кода.
/// </remarks>
public sealed class SearchIndex
{
    private readonly string[] _texts;
    private readonly Vector[]? _vectors;
    private readonly TextIndex? _words;

    /// <summary>Создаёт индекс.</summary>
    /// <param name="texts">Документы корпуса.</param>
    /// <param name="vectors">Векторы документов; <c>null</c> — семантического поиска нет.</param>
    /// <param name="words">Словесный индекс; <c>null</c> — словесного поиска нет.</param>
    public SearchIndex(IReadOnlyList<string> texts, Vector[]? vectors, TextIndex? words)
    {
        ArgumentNullException.ThrowIfNull(texts);

        _texts = [.. texts];
        _vectors = vectors;
        _words = words;

        Kind = (vectors, words) switch
        {
            (not null, not null) => "hybrid",
            (not null, null) => "semantic",
            (null, not null) => "words",
            _ => throw new ArgumentException("индекс без векторов и без слов ничего не ищет", nameof(vectors)),
        };
    }

    /// <summary>Вид индекса.</summary>
    public string Kind { get; }

    /// <summary>Сколько документов в корпусе.</summary>
    public int Count => _texts.Length;

    /// <summary>Нужен ли вектор запроса для поиска.</summary>
    public bool NeedsEmbedding => _vectors != null;

    /// <summary>Текст документа.</summary>
    public string Text(int document) => document >= 0 && document < _texts.Length
        ? _texts[document]
        : throw new ScriptError(
            DiagnosticCodes.IndexOutOfRange,
            $"номер документа {document} вне границ: в корпусе {_texts.Length}");

    /// <summary>
    /// Лучшие документы по запросу, от самого подходящего.
    /// </summary>
    /// <param name="query">Текст запроса — нужен словесной части.</param>
    /// <param name="embedding">Вектор запроса; <c>null</c>, если индекс словесный.</param>
    /// <param name="top">Сколько вернуть.</param>
    /// <remarks>
    /// В гибридном режиме оценки складываются после приведения каждой к отрезку [0, 1] по
    /// собственному максимуму выдачи. Складывать косинус с баллом BM25 напрямую нельзя: первый
    /// лежит около единицы, второй бывает и двадцатым, и словесная часть просто съела бы
    /// смысловую.
    /// </remarks>
    public IReadOnlyList<(int Document, double Score)> Search(string query, Vector? embedding, int top)
    {
        var scores = new double[Count];

        if (_vectors != null && embedding != null)
        {
            for (int i = 0; i < Count; i++) scores[i] = Embeddings.Cosine(embedding, _vectors[i]);
        }

        if (_words != null)
        {
            var wordScores = new double[Count];

            for (int i = 0; i < Count; i++) wordScores[i] = _words.Score(query, i);

            Normalise(wordScores);

            if (_vectors == null)
            {
                scores = wordScores;
            }
            else
            {
                Normalise(scores);

                for (int i = 0; i < Count; i++) scores[i] = (scores[i] + wordScores[i]) / 2;
            }
        }

        var order = new int[Count];

        for (int i = 0; i < Count; i++) order[i] = i;

        Array.Sort(order, (a, b) => scores[b].CompareTo(scores[a]));

        int take = Math.Min(top, Count);
        var result = new List<(int, double)>(take);

        for (int i = 0; i < take; i++) result.Add((order[i], scores[order[i]]));

        return result;
    }

    private static void Normalise(double[] values)
    {
        double max = 0;

        foreach (double value in values) max = Math.Max(max, value);

        if (max <= 0) return;

        for (int i = 0; i < values.Length; i++) values[i] /= max;
    }
}

/// <summary>Построение словесных индексов для <see cref="SearchIndex"/>.</summary>
internal static class TextIndexes
{
    /// <summary>Индекс BM25 по корпусу.</summary>
    public static TextIndex Bm25(IReadOnlyList<string> texts)
    {
        string[] docs = [.. texts];

        return new TextIndex(new BM25(docs), docs.Length);
    }
}
