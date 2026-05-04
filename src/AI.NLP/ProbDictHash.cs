using System;
using System.Collections.Generic;

namespace AI.NLP;

/// <summary>
/// Вероятностный словарь
/// </summary>
[Serializable]
public class ProbabilityDictionaryHash
{
    private readonly List<string> words = new List<string>();

    /// <summary>
    /// Вероятностный словарь
    /// </summary>
    public Dictionary<string, double> pDictionary { get; private set; }

    /// <summary>
    /// Применять ли стеммер
    /// </summary>
    public bool IsStem { get; set; }

    /// <summary>
    /// Вероятностный словарь
    /// </summary>
    /// <param name="isStem">Делать ли стеммеризацию</param>
    public ProbabilityDictionaryHash(bool isStem = true)
    {
        pDictionary = new Dictionary<string, double>();
        IsStem = isStem;
    }

    /// <summary>
    /// Данные вероятностного словаря
    /// </summary>
    /// <param name="text">Текст</param>
    /// <returns></returns>
    public Dictionary<string, double> Run(string text)
    {
        words.Clear();
        words.AddRange(ProbabilityDictionary.GetWords(text, IsStem));
        Analyze();
        return pDictionary;
    }

    private void Analyze()
    {
        var data = new Dictionary<string, double>();
        int total = words.Count;
        if (total == 0)
        {
            pDictionary = new Dictionary<string, double>();
            return;
        }

        for (int i = 0; i < words.Count; i++)
        {
            string w = words[i];
            if (data.ContainsKey(w))
                data[w]++;
            else
                data.Add(w, 1);
        }

        pDictionary = new Dictionary<string, double>(data.Count);
        foreach (KeyValuePair<string, double> kv in data)
            pDictionary.Add(kv.Key, kv.Value / total);
    }
}
