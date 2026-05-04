using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.NLP;

/// <summary>
/// TF-IDF
/// </summary>
[Serializable]
public class TFIDF
{
    internal readonly Dictionary<string, double>[] pDs;
    private readonly Dictionary<string, int> docFreqByTerm;
    private readonly Dictionary<string, double> idfCache;
    private readonly int documentCount;

    /// <summary>
    /// TF-IDF
    /// </summary>
    /// <param name="docs">Массив документов</param>
    public TFIDF(string[] docs)
    {
        documentCount = docs.Length;
        pDs = new Dictionary<string, double>[docs.Length];
        var probabilityDictionary = new ProbabilityDictionaryHash();

        for (int i = 0; i < docs.Length; i++)
            pDs[i] = probabilityDictionary.Run(docs[i]);

        docFreqByTerm = BuildDocumentFrequency(pDs);

        // Предрасчёт IDF для всех известных термов: при частых запросах TF_IDF_Str
        // это избавляет от повторного Math.Log10 на каждый вызов.
        idfCache = new Dictionary<string, double>(docFreqByTerm.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> kv in docFreqByTerm)
            idfCache[kv.Key] = ComputeIdf(kv.Value);
    }

    private static Dictionary<string, int> BuildDocumentFrequency(Dictionary<string, double>[] perDoc)
    {
        var df = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < perDoc.Length; i++)
        {
            foreach (string term in perDoc[i].Keys)
            {
                if (df.TryGetValue(term, out int c))
                    df[term] = c + 1;
                else
                    df[term] = 1;
            }
        }

        return df;
    }

    private double ComputeIdf(int df)
    {
        return Math.Log10(0.1 + (documentCount / (df + 0.001)));
    }

    /// <summary>
    /// tf
    /// </summary>
    /// <param name="t"></param>
    /// <param name="dIndex"></param>
    /// <returns></returns>
    public double TFWord(string t, int dIndex)
    {
        return pDs[dIndex].TryGetValue(t, out double tf) ? tf : 0;
    }

    /// <summary>
    /// Idf
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    public double IDFWord(string t)
    {
        if (idfCache.TryGetValue(t, out double idf))
            return idf;

        // Термин не встречался ни в одном документе — считаем на лету.
        return ComputeIdf(0);
    }

    /// <summary>
    /// TF-IDF
    /// </summary>
    /// <param name="t"></param>
    /// <param name="dIndex"></param>
    /// <returns></returns>
    public double TF_IDF(string t, int dIndex)
    {
        double tf = pDs[dIndex].TryGetValue(t, out double v) ? v : 0;
        if (tf == 0) return 0;
        return tf * IDFWord(t);
    }

    /// <summary>
    /// Принадлежность строки к определенному документу
    /// </summary>
    /// <param name="str">Строка</param>
    /// <param name="dIndex">Индекс документа</param>
    public double TF_IDF_Str(string str, int dIndex)
    {
        string[] strs = ProbabilityDictionary.GetWords(str, true);
        if (strs.Length == 0)
            return 0;

        Dictionary<string, double> doc = pDs[dIndex];
        double sum = 0;
        for (int i = 0; i < strs.Length; i++)
        {
            string t = strs[i];
            if (doc.TryGetValue(t, out double tf) && tf != 0)
                sum += tf * IDFWord(t);
        }

        return sum / strs.Length;
    }

    /// <summary>
    /// Поиск документа
    /// </summary>
    /// <param name="req">Запрос</param>
    public int Search(string req)
    {
        Vector ind = new Vector(pDs.Length);
        for (int i = 0; i < ind.Count; i++)
            ind[i] = TF_IDF_Str(req, i);

        return ind.MaxElementIndex();
    }
}
