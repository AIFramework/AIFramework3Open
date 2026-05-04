using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;
using System.IO;

namespace AI.NLP;

/// <summary>
/// TF-IDF словарь
/// </summary>
[Serializable]
public class TFIDFDictionary
{
    /// <summary>
    /// TF-IDF алгоритм
    /// </summary>
    public TFIDF TfIdf { get; set; }

    private readonly int _n;

    /// <summary>
    /// Создание словаря tf-idf
    /// </summary>
    /// <param name="pathToDir">Путь до директории с документами</param>
    public TFIDFDictionary(string pathToDir)
    {
        string[] fs = Directory.GetFiles(pathToDir);
        string[] strs = new string[fs.Length];

        _n = fs.Length;

        for (int i = 0; i < fs.Length; i++)
            strs[i] = File.ReadAllText(fs[i]);

        TfIdf = new TFIDF(strs);
    }

    /// <summary>
    /// Преобразование слова в вектор
    /// </summary>
    /// <param name="word">Слово</param>
    public Vector ToVect(string word)
    {
        Vector ind = new Vector(_n);
        for (int i = 0; i < ind.Count; i++)
            ind[i] = TfIdf.TF_IDF_Str(word, i);

        double max = ind.Max();
        return max > 0 ? ind / max : ind;
    }

    /// <summary>
    /// Расчет близости слов
    /// </summary>
    /// <returns></returns>
    public double Sim(string word1, string word2)
    {
        return Statistics.Statistic.CorrelationCoefficient(ToVect(word1), ToVect(word2));
    }

    /// <summary>
    /// Составление векторного словаря
    /// </summary>
    public Dictionary<string, Vector> VectorDictionary()
    {
        Dictionary<string, double>[] dotDicts = TfIdf.pDs;

        // Собираем уникальные термы одним проходом.
        var terms = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dotDicts.Length; i++)
            foreach (string w in dotDicts[i].Keys)
                _ = terms.Add(w);

        var vecDict = new Dictionary<string, Vector>(terms.Count, StringComparer.Ordinal);

        // Работаем напрямую с уже токенизированными/стеммированными термами:
        // избегаем повторной токенизации внутри TF_IDF_Str для каждой пары (term, doc).
        foreach (string term in terms)
        {
            double idf = TfIdf.IDFWord(term);
            Vector v = new Vector(_n);
            double max = 0;

            for (int i = 0; i < _n; i++)
            {
                if (dotDicts[i].TryGetValue(term, out double tf) && tf != 0)
                {
                    double val = tf * idf;
                    v[i] = val;
                    if (val > max) max = val;
                }
            }

            if (max > 0) v /= max;
            vecDict.Add(term, v);
        }

        return vecDict;
    }
}
