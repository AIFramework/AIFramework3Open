using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.NLP;

/// <summary>
/// Okapi BM25 — вероятностная модель ранжирования документов.
/// Улучшает TF-IDF за счёт насыщения частот (k1) и нормализации длины документа (b).
/// </summary>
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

    /// <summary>Параметр насыщения частоты термина (рекомендуется 1.2–2.0).</summary>
    public double K1 => _k1;

    /// <summary>Параметр нормализации длины документа (рекомендуется 0.75).</summary>
    public double B => _b;

    /// <summary>
    /// Okapi BM25
    /// </summary>
    /// <param name="docs">Массив текстовых документов</param>
    /// <param name="k1">Параметр насыщения TF (по умолчанию 1.5)</param>
    /// <param name="b">Коэффициент нормализации длины (по умолчанию 0.75)</param>
    public BM25(string[] docs, double k1 = 1.5, double b = 0.75)
    {
        if (docs is null || docs.Length == 0)
            throw new ArgumentException("Корпус документов не может быть пустым.", nameof(docs));

        _k1 = k1;
        _b  = b;
        _documentCount = docs.Length;
        _rawCounts  = new Dictionary<string, int>[docs.Length];
        _docLengths = new int[docs.Length];
        _docFreq    = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < docs.Length; i++)
        {
            string[] words = ProbabilityDictionary.GetWords(docs[i], IsStem: true);
            _docLengths[i] = words.Length;

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
    public double Score(string query, int docIndex)
    {
        string[] terms = ProbabilityDictionary.GetWords(query, IsStem: true);
        if (terms.Length == 0) return 0;

        int dl = _docLengths[docIndex];
        double norm = _avgDocLength > 0 ? dl / _avgDocLength : 1.0;
        var counts = _rawCounts[docIndex];

        double score = 0;
        foreach (string t in terms)
        {
            counts.TryGetValue(t, out int tf);
            if (tf == 0) continue;

            double idf = IDFWord(t);
            double numerator   = tf * (_k1 + 1.0);
            double denominator = tf + _k1 * (1.0 - _b + _b * norm);
            score += idf * numerator / denominator;
        }
        return score;
    }

    /// <summary>
    /// Возвращает индекс наиболее релевантного документа
    /// </summary>
    /// <param name="query">Поисковый запрос</param>
    public int Search(string query)
    {
        double best = double.MinValue;
        int bestIdx = 0;
        for (int i = 0; i < _documentCount; i++)
        {
            double s = Score(query, i);
            if (s > best) { best = s; bestIdx = i; }
        }
        return bestIdx;
    }

    /// <summary>
    /// Возвращает топ-N наиболее релевантных документов
    /// </summary>
    /// <param name="query">Поисковый запрос</param>
    /// <param name="n">Количество результатов</param>
    public (int index, double score)[] SearchTopN(string query, int n)
    {
        var all = new (int index, double score)[_documentCount];
        for (int i = 0; i < _documentCount; i++)
            all[i] = (i, Score(query, i));

        return all
            .OrderByDescending(x => x.score)
            .Take(Math.Min(n, _documentCount))
            .ToArray();
    }
}
