using AI.Extensions;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AI.NLP;

/// <summary>
/// Суммаризация
/// </summary>
[Serializable]
public class TextSummarization
{
    private readonly List<DataSummText> dataSummTexts = new List<DataSummText>();
    private ProbabilityDictionaryData<string>[] probabilityDictionaryDatas;
    private readonly ProbabilityDictionary probabilityDictionary = new ProbabilityDictionary();
    private ProbabilityDictionaryData<string>[][] probabilityDictionaryDataSeqs;
    private string[] seqs;
    private Dictionary<string, double> globalProb;
    private static readonly Regex nPat = new Regex(@"[А-Я]\.", RegexOptions.Compiled); // паттерн инициалов
    private static readonly Regex uKPat = new Regex(@" [а-я]\.", RegexOptions.Compiled); // паттерн различных сокращений
    private static readonly Regex uKPat2 = new Regex(@" [а-я]\.[а-я]\.", RegexOptions.Compiled); // паттерн сокращений типа т.п., т.н.
    private static readonly Regex rectPat = new Regex(@"\[[\w\s]*\]", RegexOptions.Compiled); // паттерн квадратных скобочек с описанием

    /// <summary>
    /// Суммаризация
    /// </summary>
    public TextSummarization()
    {

    }

    /// <summary>
    /// Суммаризация текста
    /// </summary>
    /// <param name="text">Текст</param>
    /// <param name="num">Кол-во предложений для описания текста</param>
    /// <returns></returns>
    public string Summarization(string text, int num = 1)
    {
        Step1(text);
        Step2();

        dataSummTexts.Sort((a, b) => b.W.CompareTo(a.W));

        int take = Math.Min(num, dataSummTexts.Count);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < take; i++)
            _ = sb.Append(dataSummTexts[i].Str).Append("\n\n");

        return sb.ToString();
    }

    /// <summary>
    /// Выдает предложения
    /// </summary>
    /// <param name="text">Текст</param>
    /// <returns></returns>
    public static string[] GetSeqs(string text)
    {
        string t = text;
        t = t.Replace("(", " ( ").Replace(")", " ) "); // скобки

        while (t.Contains("  "))
            t = t.Replace("  ", " ");

        //Сокращения
        t = t.Replace(" р.", " р").Replace(" д.", "д").Replace(" кв.", " квартира").Replace(" ул.", " улица").Replace(" г.", " г")
            .Replace("гг.", " гг").Replace(" др.", " др").Replace(" исл.", " исландский").Replace(" вел.", " вел").Replace(" кн.", " кн")
            .Replace("км.", " км").Replace(" тд.", " тд").Replace(" англ.", " английский").Replace(" кр.", " кр").Replace(" тк.", " тк").Replace("Рис.", " рис")
            .Replace(" рис.", " рис");

        //Форматирование
        t = t.Replace("\n", "").Replace("\r", "").Replace("\t", "");

        // Удаление паттернов
        t = nPat.Replace(t, string.Empty);
        t = uKPat.Replace(t, string.Empty);
        t = rectPat.Replace(t, string.Empty);
        t = uKPat2.Replace(t, string.Empty);

        // Замена знаков
        t = t.Replace("!", ".").Replace("?", ".");

        while (t.Contains(".."))
            t = t.Replace("..", ".");
        return t.Split('.').Transform(x => x.Trim() + ".");
    }

    /// <summary>
    /// Первый шаг алгоритма (составление вероятностных словарей)
    /// </summary>
    /// <param name="text">Текст</param>
    private void Step1(string text)
    {
        seqs = GetSeqs(text);

        probabilityDictionaryDataSeqs = new ProbabilityDictionaryData<string>[seqs.Length][];

        for (int i = 0; i < seqs.Length; i++)
            probabilityDictionaryDataSeqs[i] = probabilityDictionary.Run(seqs[i]);

        string text2 = string.Join(" ", seqs);
        probabilityDictionaryDatas = probabilityDictionary.Run(text2);

        // Индексируем глобальные вероятности по слову — избавляет GetW от O(n*m) перебора.
        globalProb = new Dictionary<string, double>(probabilityDictionaryDatas.Length, StringComparer.Ordinal);
        for (int i = 0; i < probabilityDictionaryDatas.Length; i++)
        {
            string w = probabilityDictionaryDatas[i].Word;
            if (!globalProb.ContainsKey(w))
                globalProb[w] = probabilityDictionaryDatas[i].Probability;
        }
    }


    // Второй шаг составления списка: предложение, вес
    private void Step2()
    {
        dataSummTexts.Clear();
        for (int i = 0; i < seqs.Length; i++)
        {
            double w = GetW(i);
            if (!(double.IsNaN(w) || double.IsInfinity(w)))
                dataSummTexts.Add(new DataSummText(seqs[i], w));
        }
    }


    // Расчет семантического веса
    private double GetW(int ind)
    {
        ProbabilityDictionaryData<string>[] seq = probabilityDictionaryDataSeqs[ind];
        int len = seq.Length;
        if (len == 0) return double.NaN;

        double w = 0;
        int valid = 0;
        for (int i = 0; i < len; i++)
        {
            double inSeqProb = seq[i].Probability;
            if (inSeqProb <= 0) continue;

            if (globalProb.TryGetValue(seq[i].Word, out double gp) && gp > 0)
            {
                w += gp / inSeqProb;
                valid++;
            }
        }

        return valid == 0 ? 0 : w / len;
    }



}

/// <summary>
/// Данные предложений
/// </summary>
public class DataSummText
{
    /// <summary>
    /// Вес
    /// </summary>
    public double W { get; set; }
    /// <summary>
    /// Содержание
    /// </summary>
    public string Str { get; set; }

    /// <summary>
    /// Данные предложений
    /// </summary>
    /// <param name="str">строка</param>
    /// <param name="w">вес</param>
    public DataSummText(string str, double w)
    {
        W = w;
        Str = str;
    }
}
