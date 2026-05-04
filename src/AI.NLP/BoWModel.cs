using AI.DataStructs.Algebraic;
using AI.Statistics;
using System;
using System.Collections.Generic;
using System.IO;


namespace AI.NLP;

/// <summary>
/// Bag-of-Words (Мешок слов)
/// </summary>
[Serializable]
public class BoWModel
{
    private static readonly char[] ModelSplitChars = { ' ', '.', ',', '!', '\t', '\n' };

    private readonly string[] model;
    private readonly Dictionary<string, int> modelIndex;

    /// <summary>
    /// Вектор, в котором все 0, кроме позиции слова
    /// </summary>
	public Vector vector;
    /// <summary>
    /// Анализировать ли стоп слова
    /// </summary>
	public bool isStop { get; set; }
    /// <summary>
    /// Пропускать ли числа
    /// </summary>
	public bool isDig { get; set; }
    /// <summary>
    /// Размерность вектора/словаря
    /// </summary>
	public int Len;
    /// <summary>
    /// Нужно ли нормализовать вектор
    /// </summary>
	public bool IsNormalise { get; set; }

    /// <summary>
    ///  Bag-of-Words (Мешок слов)
    /// </summary>
    public BoWModel(string pathModel)
    {
        string[] raw = File.ReadAllText(pathModel).Split(ModelSplitChars);

        // Предрасчёт модели: убираем \r и пустые, строим индекс O(1)
        model = new string[raw.Length];
        modelIndex = new Dictionary<string, int>(raw.Length, StringComparer.Ordinal);

        int write = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            string w = raw[i].Trim('\r');
            model[write] = w;
            if (!string.IsNullOrEmpty(w) && !modelIndex.ContainsKey(w))
                modelIndex.Add(w, write);
            write++;
        }

        Len = model.Length;
        vector = new Vector(model.Length);
        isStop = false;
        IsNormalise = false;
        isDig = false;
    }

    /// <summary>
    /// Вычислить вектор из текста
    /// </summary>
    /// <param name="text">Текст</param>
    public Vector GetVector(string text)
    {
        ProbabilityDictionary prob = new ProbabilityDictionary(isStop, isDig);
        ProbabilityDictionaryData<string>[] pds = prob.Run(text);

        vector = new Vector(model.Length);

        // O(|pds|) благодаря хэш-таблице модели вместо O(|pds|*|model|)
        for (int i = 0; i < pds.Length; i++)
        {
            if (modelIndex.TryGetValue(pds[i].Word, out int idx))
                vector[idx]++;
        }

        if (IsNormalise)
        {
            vector /= Statistic.MaximalValue(vector) + 1e-6;
            vector -= Statistic.ExpectedValue(vector);
        }

        return vector;
    }

    /// <summary>
    /// Генерация/создание модели
    /// </summary>
    public static void ModelGen(string text, string path, bool isStop = false)
    {
        ProbabilityDictionary prob = new ProbabilityDictionary(isStop);
        ProbabilityDictionaryData<string>[] pb = prob.Run(text);
        int len = pb.Length;
        string[] newModel = new string[len];

        for (int i = 0; i < len; i++)
            newModel[i] = pb[i].Word;

        File.WriteAllLines(path, newModel);
    }
}
