using AI.NLP.Stemmers;
using System;
using System.Collections.Generic;
using System.Text;

namespace AI.NLP;

/// <summary>
/// Вероятностный словарь
/// </summary>
[Serializable]
public class ProbabilityDictionary
{
    private static readonly char[] WordTrimChars = { '?', '!', '.', ',', ' ', '\t', '(', ')', '<', '>', '»', '«', ':', '\"', '*', '-', ';' };
    private static readonly char[] TokenSplitChars = { ' ', '\n', '\t', '[', ']', '-' };

    /// <summary>
    /// Вероятностный словарь
    /// </summary>
    public ProbabilityDictionaryData<string>[] pDictionary { get; private set; }

    private readonly List<string> words = new List<string>();
    private int n;

    /// <summary>
    /// Удалять ли цифры
    /// </summary>
    public bool IsDigitDel { get; set; }

    /// <summary>
    /// Использовать ли стеммер
    /// </summary>
    public bool IsStem { get; set; }

    /// <summary>
    /// Удалять ли стоп-слова у данного экземпляра.
    /// Раньше флаг «сохранялся» через мутацию статического поля <see cref="stop"/>,
    /// что приводило к общей гонке между инстансами. Теперь это обычный per-instance флаг.
    /// </summary>
    public bool IsStopDel { get; set; }

    /// <summary>
    /// Глобальный список стоп-слов (используется, если у экземпляра <see cref="IsStopDel"/> = true).
    /// Конструктор его больше НЕ меняет как побочный эффект. Поле оставлено
    /// для обратной совместимости: код, читавший/устанавливавший <c>ProbabilityDictionary.stop</c>,
    /// продолжит работать.
    /// </summary>
    public static string[] stop = new string[0];

    /// <summary>
    /// Слова не несущие смысла при стат. анализе
    /// </summary>
    public static string[] StopWords => stop;

    /// <summary>
    /// Вероятностный словарь
    /// </summary>
    /// <param name="isStopDel">Удалять ли стоп-слова (использовать <see cref="stop"/>)</param>
    /// <param name="isDigitDel">Удалять ли числа</param>
    /// <param name="isStem">Делать ли стеммеризацию</param>
    public ProbabilityDictionary(bool isStopDel = true, bool isDigitDel = true, bool isStem = true)
    {
        IsStopDel = isStopDel;
        IsDigitDel = isDigitDel;
        IsStem = isStem;
    }

    /// <summary>
    /// Данные вероятностного словаря
    /// </summary>
    /// <param name="text">Текст</param>
    /// <returns></returns>
    public ProbabilityDictionaryData<string>[] Run(string text)
    {
        GetWords(text);
        pDictionary = Analyze();
        return pDictionary;
    }

    /// <summary>
    /// Запуск генерации словаря с выводом всех слов
    /// </summary>
    /// <param name="text">Текст для генерации</param>
    public string[] GetWordsRunAll(string text)
    {
        ProbabilityDictionaryData<string>[] wsp = Run(text);
        string[] strs = new string[wsp.Length];

        for (int i = 0; i < wsp.Length; i++)
            strs[i] = wsp[i].Word;

        return strs;
    }

    /// <summary>
    /// Запуск генерации словаря с выводом определенного числа слов
    /// </summary>
    /// <param name="text">Текст для генерации</param>
    /// <param name="numW">Число слов</param>
    public string[] GetWordsRun(string text, int numW = 30)
    {
        ProbabilityDictionaryData<string>[] wsp = Run(text);
        int len = pDictionary.Length < numW ? pDictionary.Length : numW;

        string[] strs = new string[len];

        for (int i = 0; i < len; i++)
            strs[i] = wsp[i].Word;

        return strs;
    }

    private static bool ContainsDigitOrSeparator(string str)
    {
        for (int i = 0; i < str.Length; i++)
        {
            char ch = str[i];
            if (char.IsDigit(ch) || char.IsSeparator(ch))
                return true;
        }

        return false;
    }

    private bool IsStopInstance(string word)
    {
        if (!IsStopDel) return false;
        string[] s = stop;
        for (int i = 0; i < s.Length; i++)
            if (s[i] == word) return true;
        return false;
    }

    private static bool IsStopStatic(string word)
    {
        string[] s = stop;
        for (int i = 0; i < s.Length; i++)
            if (s[i] == word) return true;
        return false;
    }

    /// <summary>
    /// Анализ текста. Формирует и возвращает отсортированный словарь.
    /// </summary>
    private ProbabilityDictionaryData<string>[] Analyze()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < words.Count; i++)
        {
            string w = words[i];
            if (IsDigitDel && ContainsDigitOrSeparator(w))
                continue;
            if (IsStopInstance(w))
                continue;

            if (counts.TryGetValue(w, out int c))
                counts[w] = c + 1;
            else
                counts[w] = 1;
        }

        int denom = n <= 0 ? 1 : n;
        var result = new ProbabilityDictionaryData<string>[counts.Count];
        int k = 0;
        foreach (KeyValuePair<string, int> kv in counts)
        {
            result[k++] = new ProbabilityDictionaryData<string>
            {
                Probability = kv.Value / (double)denom,
                Word = kv.Key
            };
        }

        Array.Sort(result, (a, b) => b.Probability.CompareTo(a.Probability));
        return result;
    }

    /// <summary>
    /// Переводит частотный словарь в строку
    /// </summary>
    /// <param name="index">До какого индекса</param>
    /// <returns></returns>
    public string ToString(int index)
    {
        int len = pDictionary.Length < index ? pDictionary.Length : index;
        if (len <= 0)
            return string.Empty;

        var sb = new StringBuilder(len * 24);
        for (int i = 0; i < len; i++)
        {
            _ = sb.Append(pDictionary[i].Word).Append(' ').Append(pDictionary[i].Probability).Append('\n');
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Получение слов
    /// </summary>
    /// <param name="text">Текст</param>
    public void GetWords(string text)
    {
        words.Clear();

        // Приводим к нижнему регистру один раз и одновременно убираем \r
        string lower = text.ToLower().Replace("\r", string.Empty);
        string[] strs = lower.Split(TokenSplitChars);
        n = strs.Length;

        if (words.Capacity < strs.Length)
            words.Capacity = strs.Length;

        for (int i = 0; i < strs.Length; i++)
        {
            string str = strs[i];

            // Обрезаем пунктуацию сразу: это даёт корректный вид слова
            // для сравнения со стоп-словами и при стеммеризации.
            string word = str.Trim(WordTrimChars);

            if (word.Length == 0)
                continue;

            if (IsStopInstance(word))
                continue;

            if (IsStem)
                word = StemmerRus.TransformingWord(word);

            words.Add(word);
        }
    }

    /// <summary>
    /// Получение слов
    /// </summary>
    /// <param name="text"></param>
    /// <param name="IsStem"></param>
    /// <returns></returns>
    public static string[] GetWords(string text, bool IsStem)
    {
        string[] strs = TextStandard.OnlyCharsAndDigit(text).Split(' ');
        var result = new List<string>(strs.Length);

        for (int i = 0; i < strs.Length; i++)
        {
            string str = strs[i];
            if (str.Length == 0) continue;
            if (IsStopStatic(str)) continue;

            string word = IsStem ? StemmerRus.TransformingWord(str) : str;
            result.Add(word);
        }

        return result.ToArray();
    }
}
