using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;
namespace AI.NLP;

/// <summary>
/// Токенизатор (ТoDo: удалить и перенести функционал в токенизатор AI.DataPrepaire)
/// </summary>
[Serializable]
public class TextTokenizer
{

    /// <summary>
    /// Whether to convert text to lowercase
    /// </summary>
    public bool IsLower { get; set; }
    /// <summary>
    /// Whether to stem the text
    /// </summary>
    public bool IsStem { get; set; }
    /// <summary>
    /// Deleted characters
    /// </summary>
    public char[] DelChars { get; set; }
    /// <summary>
    /// Characters or strings to separate words
    /// </summary>
    public string[] Separaters { get; set; }
    /// <summary>
    /// Вектор выхода dimention
    /// </summary>
    public int Count { get; set; } = 50;
    /// <summary>
    /// Number of words in the frequency dictionary
    /// </summary>
    public int WordCount { get; set; } = -1;

    private readonly Dictionary<string, int> dictionary = new Dictionary<string, int>();
    /// <summary>
    /// Слова
    /// </summary>
    public string[] Words;


    /// <summary>
    /// Class for tokenizing text
    /// </summary>
    /// <param name="isLower">Whether to convert text to lowercase</param>
    /// <param name="isStem">Whether to stem the text</param>
    /// <param name="deleted">Deleted characters (default [',' ';' '*' '?' '!' '.'])  </param>
    /// <param name="separaters">Characters or strings to separate words (default ["\t" " " "\n"])</param>
    public TextTokenizer(bool isLower = true, bool isStem = false, char[] deleted = null, string[] separaters = null)
    {
        DelChars = deleted ?? new[] { ',', ';', '*', '?', '!', '.' };
        Separaters = separaters ?? new[] { "\t", " " };
        IsLower = isLower;
        IsStem = isStem;
    }

    /// <summary>
    /// Обучение модели
    /// </summary>
    /// <param name="text"></param>
    public void Train(string text)
    {
        string inp = Preproc(text);
        ProbabilityDictionary probabilityDictionary = new ProbabilityDictionary(false, false, IsStem);
        ProbabilityDictionaryData<string>[] dic = probabilityDictionary.Run(inp);

        int limit = WordCount == -1 ? dic.Length : Math.Min(WordCount, dic.Length);

        for (int i = 0; i < limit; i++)
        {
            string w = dic[i].Word;
            if (!dictionary.ContainsKey(w))
                dictionary.Add(w, dictionary.Count);
        }

        Words = new string[dictionary.Count];
        foreach (KeyValuePair<string, int> word in dictionary)
            Words[word.Value] = word.Key;
    }

    /// <summary>
    /// Токенизация последовательности
    /// </summary>
    /// <param name="seq"></param>
    /// <returns></returns>
    public Vector GetSeq2Tokens(string seq)
    {
        Vector vec = new Vector(Count) - 1;
        string outp = Preproc(seq);
        string[] array = outp.Split(' ');
        int len = Math.Min(array.Length, Count);

        for (int i = 0; i < len; i++)
        {
            string word = IsStem ? Stemmers.StemmerRus.TransformingWord(array[i]) : array[i];
            vec[i] = dictionary.TryGetValue(word, out int idx) ? idx : -1;
        }

        return vec;
    }
    /// <summary>
    /// Токенизация слова
    /// </summary>
    public int GetWord2Token(string word)
    {
        string outp = Preproc(word);
        string wordP = IsStem ? Stemmers.StemmerRus.TransformingWord(outp) : outp;

        return dictionary.TryGetValue(wordP, out int idx) ? idx : dictionary.Count;
    }

    /// <summary>
    /// Токенизация слова с помощью one-hot
    /// </summary>
    public Vector GetWord2OneHot(string word)
    {
        Vector ret = new Vector(dictionary.Count + 1);

        int indMax = GetWord2Token(word);
        ret[indMax] = 1;

        return ret;
    }

    /// <summary>
    /// Получить размерность с неизвестными словами
    /// </summary>
    /// <returns></returns>
    public int GetDimWithUnKnowWord()
    {
        return dictionary.Count + 1;
    }

    private string Preproc(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        string outp = text;

        if (IsLower)
            outp = outp.ToLower();

        // Удаляем служебные символы. Оригинал подменял их на '\0' (Replace(char,char)),
        // что лишь вставляло NUL внутрь токенов — это был баг. Подменяем на пробел.
        if (DelChars != null)
        {
            var sb = new System.Text.StringBuilder(outp.Length);
            for (int i = 0; i < outp.Length; i++)
            {
                char ch = outp[i];
                bool isDel = false;
                for (int k = 0; k < DelChars.Length; k++)
                {
                    if (DelChars[k] == ch) { isDel = true; break; }
                }
                _ = sb.Append(isDel ? ' ' : ch);
            }
            outp = sb.ToString();
        }

        // Заменяем все строковые разделители на пробел. Не мутируем поле Separaters
        // (в оригинале оно перезаписывалось внутри метода как побочный эффект).
        if (Separaters != null)
        {
            for (int i = 0; i < Separaters.Length; i++)
            {
                string sep = Separaters[i];
                if (!string.IsNullOrEmpty(sep) && sep != " ")
                    outp = outp.Replace(sep, " ");
            }
        }

        // Схлопываем подряд идущие пробелы за один проход.
        if (outp.IndexOf("  ", StringComparison.Ordinal) >= 0)
        {
            var sb = new System.Text.StringBuilder(outp.Length);
            bool prevSpace = false;
            for (int i = 0; i < outp.Length; i++)
            {
                char ch = outp[i];
                if (ch == ' ')
                {
                    if (prevSpace) continue;
                    prevSpace = true;
                }
                else prevSpace = false;
                _ = sb.Append(ch);
            }
            outp = sb.ToString();
        }

        return outp;
    }

}
