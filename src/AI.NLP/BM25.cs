using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.NLP;

/// <summary>
/// Okapi BM25 — вероятностная модель ранжирования документов.
/// Улучшает TF-IDF за счёт насыщения частот (k1) и нормализации длины документа (b).
/// </summary>
/// <remarks>
/// Поиск идёт по обратному индексу: оцениваются только документы, где встречается хотя бы
/// одно слово запроса. На корпусе в десятки тысяч коротких документов это разница между
/// перебором всего корпуса и чтением нескольких списков. Результаты совпадают с перебором
/// до бита: порядок сложения слагаемых тот же.
/// <para>
/// Токены можно подать готовыми — тогда разбор текста за вызывающим (своя лемматизация,
/// разбиение идентификаторов). Строковые перегрузки разбирают текст
/// <see cref="ProbabilityDictionary.GetWords"/> с русским стеммингом.
/// </para>
/// </remarks>
[Serializable]
public class BM25
{
    private readonly Dictionary<string, int>[] _rawCounts;
    private readonly Dictionary<string, int>  _docFreq;
    private readonly Dictionary<string, double> _idfCache;
    private readonly int[]    _docLengths;
    private readonly double   _avgDocLength;
    private readonly int      _documentCount;
    private readonly double   _k1;
    private readonly double   _b;

    // Строится по _rawCounts при первом поиске: и после конструктора, и после десериализации
    // объекта, сохранённого до появления индекса.
    [NonSerialized]
    private Dictionary<string, List<(int Doc, int Tf)>> _postings;

    /// <summary>Параметр насыщения частоты термина (рекомендуется 1.2–2.0).</summary>
    public double K1 => _k1;

    /// <summary>Параметр нормализации длины документа (рекомендуется 0.75).</summary>
    public double B => _b;

    /// <summary>Число документов в корпусе.</summary>
    public int DocumentCount => _documentCount;

    /// <summary>
    /// Okapi BM25
    /// </summary>
    /// <param name="docs">Массив текстовых документов</param>
    /// <param name="k1">Параметр насыщения TF (по умолчанию 1.5)</param>
    /// <param name="b">Коэффициент нормализации длины (по умолчанию 0.75)</param>
    public BM25(string[] docs, double k1 = 1.5, double b = 0.75)
        : this(Tokenize(docs), k1, b)
    {
    }

    /// <summary>
    /// Okapi BM25 по готовым токенам
    /// </summary>
    /// <param name="tokenizedDocs">Токены каждого документа</param>
    /// <param name="k1">Параметр насыщения TF (по умолчанию 1.5)</param>
    /// <param name="b">Коэффициент нормализации длины (по умолчанию 0.75)</param>
    public BM25(IReadOnlyList<IReadOnlyList<string>> tokenizedDocs, double k1 = 1.5, double b = 0.75)
    {
        if (tokenizedDocs is null || tokenizedDocs.Count == 0)
            throw new ArgumentException("Корпус документов не может быть пустым.", nameof(tokenizedDocs));

        _k1 = k1;
        _b  = b;
        _documentCount = tokenizedDocs.Count;
        _rawCounts  = new Dictionary<string, int>[_documentCount];
        _docLengths = new int[_documentCount];
        _docFreq    = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < _documentCount; i++)
        {
            IReadOnlyList<string> words = tokenizedDocs[i] ?? [];
            _docLengths[i] = words.Count;

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string w in words)
            {
                if (counts.TryGetValue(w, out int c)) counts[w] = c + 1;
                else counts[w] = 1;
            }
            _rawCounts[i] = counts;

            foreach (string term in counts.Keys)
            {
                if (_docFreq.TryGetValue(term, out int df)) _docFreq[term] = df + 1;
                else _docFreq[term] = 1;
            }
        }

        _avgDocLength = _docLengths.Length > 0 ? _docLengths.Average() : 1.0;

        // Предрасчёт IDF для всех известных термов
        _idfCache = new Dictionary<string, double>(_docFreq.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> kv in _docFreq)
            _idfCache[kv.Key] = ComputeIdf(kv.Value);
    }

    // IDF по формуле Robertson-Sparck Jones (сглаженная версия)
    private double ComputeIdf(int df)
        => Math.Log((_documentCount - df + 0.5) / (df + 0.5) + 1.0);

    /// <summary>
    /// IDF для термина
    /// </summary>
    public double IDFWord(string term)
    {
        if (_idfCache.TryGetValue(term, out double idf)) return idf;
        return ComputeIdf(0);
    }

    /// <summary>
    /// В скольких документах встречается термин
    /// </summary>
    public int DocumentFrequency(string term) => _docFreq.TryGetValue(term, out int df) ? df : 0;

    /// <summary>
    /// Сырая частота термина в документе
    /// </summary>
    public int TFWord(string term, int docIndex)
    {
        _rawCounts[docIndex].TryGetValue(term, out int tf);
        return tf;
    }

    /// <summary>
    /// BM25-скор документа относительно запроса
    /// </summary>
    /// <param name="query">Поисковый запрос</param>
    /// <param name="docIndex">Индекс документа в корпусе</param>
    public double Score(string query, int docIndex) =>
        Score(ProbabilityDictionary.GetWords(query, IsStem: true), docIndex);

    /// <summary>
    /// BM25-скор документа относительно готовых токенов запроса
    /// </summary>
    /// <param name="terms">Токены запроса; повтор токена повторяет его вклад</param>
    /// <param name="docIndex">Индекс документа в корпусе</param>
    public double Score(IReadOnlyList<string> terms, int docIndex)
    {
        if (terms is null || terms.Count == 0) return 0;

        var counts = _rawCounts[docIndex];

        double score = 0;
        foreach (string t in terms)
        {
            counts.TryGetValue(t, out int tf);
            if (tf == 0) continue;

            score += Term(t, tf, docIndex);
        }
        return score;
    }

    /// <summary>
    /// Возвращает индекс наиболее релевантного документа
    /// </summary>
    /// <param name="query">Поисковый запрос</param>
    public int Search(string query) => SearchTopN(query, 1)[0].index;

    /// <summary>
    /// Возвращает топ-N наиболее релевантных документов
    /// </summary>
    /// <param name="query">Поисковый запрос</param>
    /// <param name="n">Количество результатов</param>
    public (int index, double score)[] SearchTopN(string query, int n) =>
        SearchTopN(ProbabilityDictionary.GetWords(query, IsStem: true), n);

    /// <summary>
    /// Возвращает топ-N документов по готовым токенам запроса
    /// </summary>
    /// <param name="terms">Токены запроса; повтор токена повторяет его вклад</param>
    /// <param name="n">Количество результатов</param>
    /// <remarks>
    /// При равных оценках раньше идёт документ с меньшим индексом. Если документов с
    /// ненулевой оценкой меньше <paramref name="n"/>, список дополняется документами с нулевой
    /// оценкой по порядку индексов — так же, как при сортировке полного перебора.
    /// </remarks>
    public (int index, double score)[] SearchTopN(IReadOnlyList<string> terms, int n)
    {
        var scores = new Dictionary<int, double>();

        foreach (string t in terms ?? [])
        {
            if (!Postings().TryGetValue(t, out List<(int Doc, int Tf)> list)) continue;

            foreach ((int doc, int tf) in list)
                scores[doc] = scores.GetValueOrDefault(doc) + Term(t, tf, doc);
        }

        int take = Math.Min(n, _documentCount);

        var result = scores
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
            .Take(take)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();

        for (int i = 0; result.Count < take && i < _documentCount; i++)
        {
            if (!scores.TryGetValue(i, out double s) || s <= 0) result.Add((i, 0.0));
        }

        return [.. result];
    }

    private double Term(string term, int tf, int docIndex)
    {
        double norm = _avgDocLength > 0 ? _docLengths[docIndex] / _avgDocLength : 1.0;
        double numerator   = tf * (_k1 + 1.0);
        double denominator = tf + _k1 * (1.0 - _b + _b * norm);
        return IDFWord(term) * numerator / denominator;
    }

    private Dictionary<string, List<(int Doc, int Tf)>> Postings()
    {
        if (_postings != null) return _postings;

        var postings = new Dictionary<string, List<(int Doc, int Tf)>>(_docFreq.Count, StringComparer.Ordinal);

        for (int i = 0; i < _documentCount; i++)
        {
            foreach (KeyValuePair<string, int> kv in _rawCounts[i])
            {
                if (!postings.TryGetValue(kv.Key, out var list)) postings[kv.Key] = list = [];
                list.Add((i, kv.Value));
            }
        }

        return _postings = postings;
    }

    private static IReadOnlyList<IReadOnlyList<string>> Tokenize(string[] docs)
    {
        if (docs is null || docs.Length == 0)
            throw new ArgumentException("Корпус документов не может быть пустым.", nameof(docs));

        return docs.Select(doc => (IReadOnlyList<string>)ProbabilityDictionary.GetWords(doc, IsStem: true)).ToArray();
    }
}
