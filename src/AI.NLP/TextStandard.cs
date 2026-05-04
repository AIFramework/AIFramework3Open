using System;
using System.Collections.Generic;
using System.Text;

namespace AI.NLP;

/// <summary>
/// Стандартизация текста
/// </summary>
[Serializable]
public static class TextStandard
{
    /// <summary>
    ///  Стандартизация входного текста
    /// </summary>
    /// <param name="input">Входной текст</param>
    /// <param name="isLower">Переводить ли текст в нижний регистр</param>
    public static string Normalize(string input, bool isLower = true)
    {
        if (input == null)
            return string.Empty;

        if (input.Contains("base64"))
            return input;

        string output = isLower ? input.ToLower() : input;
        output = output.Replace("\r", "");
        output = output.Replace("\t", " ").Replace("\n", " ");
        output = output.Replace("!", ".").Replace("?", ".");
        output = output.Replace("—", "-").Replace("--", "-").Replace("ё", "е");


        while (output.Contains("  "))
            output = output.Replace("  ", " ");
        while (output.Contains(".."))
            output = output.Replace("..", ".");

        return output.Trim(' ');
    }

    /// <summary>
    /// В запросе остаются только буквы, цифры и знаки пробела
    /// </summary>
    /// <param name="input">Входной текст</param>
    /// <param name="isLower">Переводить ли в нижний регистр</param>
    public static string OnlyCharsAndDigit(string input, bool isLower = true)
    {
        string outp = Normalize(input, isLower);

        // StringBuilder + один проход вместо List<char>+ToArray+while(Replace).
        var sb = new StringBuilder(outp.Length);
        bool prevSpace = false;
        for (int i = 0; i < outp.Length; i++)
        {
            char ch = outp[i];
            bool isLetterDigit = char.IsLetterOrDigit(ch);
            if (isLetterDigit)
            {
                _ = sb.Append(ch);
                prevSpace = false;
            }
            else if (ch == ' ')
            {
                if (prevSpace) continue;
                _ = sb.Append(' ');
                prevSpace = true;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// В запросе остаются только буквы и знаки пробела
    /// </summary>
    /// <param name="input">Входной текст</param>
    /// <param name="isLower">Переводить ли в нижний регистр</param>
    public static string OnlyChars(string input, bool isLower = true)
    {
        string outp = Normalize(input, isLower);

        var sb = new StringBuilder(outp.Length);
        bool prevSpace = false;
        for (int i = 0; i < outp.Length; i++)
        {
            char ch = outp[i];
            if (char.IsLetter(ch))
            {
                _ = sb.Append(ch);
                prevSpace = false;
            }
            else if (ch == ' ')
            {
                if (prevSpace) continue;
                _ = sb.Append(' ');
                prevSpace = true;
            }
        }
        return sb.ToString();
    }


    /// <summary>
    /// В запросе остаются только буквы и знаки пробела
    /// </summary>
    /// <param name="input">Входной текст</param>
    public static string OnlyRusChars(string input)
    {
        string outp = Normalize(input);

        var sb = new StringBuilder(outp.Length);
        bool prevSpace = false;
        for (int i = 0; i < outp.Length; i++)
        {
            char ch = outp[i];
            if (IsRusLeter(ch))
            {
                _ = sb.Append(ch);
                prevSpace = false;
            }
            else if (ch == ' ')
            {
                if (prevSpace) continue;
                _ = sb.Append(' ');
                prevSpace = true;
            }
        }
        return sb.ToString();
    }

    private static bool IsRusLeter(char ch)
    {
        return ch >= 'а' && ch <= 'я';
    }

    /// <summary>
    /// Удаляет повторы слов (идущие подряд)
    /// </summary>
    /// <param name="input">Входной текст</param>
    public static string NoDoubleWord(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        StringBuilder stringBuilder = new StringBuilder(input.Length);
        string[] strs = input.Split(' ');
        string oldWord = null;

        for (int i = 0; i < strs.Length; i++)
        {
            if (!string.Equals(strs[i], oldWord, StringComparison.Ordinal))
            {
                if (stringBuilder.Length > 0) _ = stringBuilder.Append(' ');
                _ = stringBuilder.Append(strs[i]);
                oldWord = strs[i];
            }
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Выдает множество слов
    /// </summary>
    /// <param name="input">Входной текст</param>
    /// <param name="preprocessingString">Обработчик текста</param>
    /// <param name="preprocessingWord">Обработчик слов</param>
    /// <param name="appendWord">Добавалять ли слово в список</param>
    /// <returns></returns>
    public static HashSet<string> GetWords(string input, Func<string, string> preprocessingString, Func<string, string> preprocessingWord, Func<string, bool> appendWord)
    {
        HashSet<string> set = new HashSet<string>();
        string[] words = preprocessingString(input).Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            // set.Contains(words[i]) здесь избыточен: HashSet.Add сам обработает дубликат.
            // Но сохраняем прежнюю семантику: appendWord получает сырое слово, в множестве — обработанное.
            if (appendWord(words[i]))
                _ = set.Add(preprocessingWord(words[i]));
        }

        return set;
    }

    /// <summary>
    /// Сходство текстов на базе множеств
    /// </summary>
    public static double SimTextDice(HashSet<string> set1, HashSet<string> set2)
    {
        if (set1.Count == 0 && set2.Count == 0) return 0;

        // Итерируем по меньшему множеству для минимума вызовов Contains.
        HashSet<string> small = set1.Count <= set2.Count ? set1 : set2;
        HashSet<string> big = ReferenceEquals(small, set1) ? set2 : set1;

        double sim = 0;
        foreach (var item in small)
            if (big.Contains(item)) sim++;

        return 2 * sim / (set1.Count + set2.Count);
    }

    /// <summary>
    /// Асимvетричное сходство текстов на базе множеств
    /// </summary>
    public static double SimTextDiceAsymmetric(HashSet<string> main, HashSet<string> set)
    {
        if (main.Count == 0) return 0;

        double sim = 0;
        foreach (var item in main)
            if (set.Contains(item)) sim++;

        return sim / main.Count;
    }
}
