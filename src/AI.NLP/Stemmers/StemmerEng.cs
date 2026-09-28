using System;

namespace AI.NLP.Stemmers;

/// <summary>
/// Стеммер Портера для английского языка (M. F. Porter, 1980, «An algorithm for suffix stripping»).
/// </summary>
/// <remarks>
/// Пара к <see cref="StemmerRus"/>: имена в коде английские, описания русские, и поиск по
/// библиотеке должен сводить «filters», «filtering» и «Filter» к одному ключу. Слова не из
/// латинских букв и слова короче трёх букв возвращаются в нижнем регистре без изменений.
/// </remarks>
public static class StemmerEng
{
    private static readonly (string Suffix, string Replacement)[] Step2 =
    [
        ("ational", "ate"), ("tional", "tion"), ("enci", "ence"), ("anci", "ance"), ("izer", "ize"),
        ("abli", "able"), ("alli", "al"), ("entli", "ent"), ("eli", "e"), ("ousli", "ous"),
        ("ization", "ize"), ("ation", "ate"), ("ator", "ate"), ("alism", "al"), ("iveness", "ive"),
        ("fulness", "ful"), ("ousness", "ous"), ("aliti", "al"), ("iviti", "ive"), ("biliti", "ble"),
    ];

    private static readonly (string Suffix, string Replacement)[] Step3 =
    [
        ("icate", "ic"), ("ative", ""), ("alize", "al"), ("iciti", "ic"), ("ical", "ic"), ("ful", ""), ("ness", ""),
    ];

    // Длинные раньше коротких с тем же концом: ement → ment → ent, ance/ence, able/ible.
    private static readonly string[] Step4 =
    [
        "al", "ance", "ence", "er", "ic", "able", "ible", "ant", "ement", "ment", "ent", "ion", "ou",
        "ism", "ate", "iti", "ous", "ive", "ize",
    ];

    /// <summary>Основа слова.</summary>
    public static string TransformingWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return word;

        string w = word.ToLowerInvariant();

        if (w.Length <= 2) return w;

        foreach (char c in w)
        {
            if (c is < 'a' or > 'z') return w;
        }

        w = Step1A(w);
        w = Step1B(w);
        w = Step1C(w);
        w = Replace(w, Step2, minMeasure: 1);
        w = Replace(w, Step3, minMeasure: 1);
        w = Step4Strip(w);
        return Step5(w);
    }

    /// <summary>Основы массива слов.</summary>
    public static string[] TransformingWordsArray(string[] words)
    {
        ArgumentNullException.ThrowIfNull(words);

        var result = new string[words.Length];
        for (int i = 0; i < words.Length; i++) result[i] = TransformingWord(words[i]);
        return result;
    }

    private static string Step1A(string w)
    {
        if (w.EndsWith("sses", StringComparison.Ordinal) || w.EndsWith("ies", StringComparison.Ordinal)) return w[..^2];
        if (w.EndsWith("ss", StringComparison.Ordinal)) return w;
        return w.EndsWith('s') ? w[..^1] : w;
    }

    private static string Step1B(string w)
    {
        if (w.EndsWith("eed", StringComparison.Ordinal)) return Measure(w[..^3]) > 0 ? w[..^1] : w;

        string stem;

        if (w.EndsWith("ed", StringComparison.Ordinal) && HasVowel(w[..^2])) stem = w[..^2];
        else if (w.EndsWith("ing", StringComparison.Ordinal) && HasVowel(w[..^3])) stem = w[..^3];
        else return w;

        if (stem.EndsWith("at", StringComparison.Ordinal) || stem.EndsWith("bl", StringComparison.Ordinal)
            || stem.EndsWith("iz", StringComparison.Ordinal)) return stem + "e";

        if (EndsWithDoubleConsonant(stem) && stem[^1] is not ('l' or 's' or 'z')) return stem[..^1];

        return Measure(stem) == 1 && EndsCvc(stem) ? stem + "e" : stem;
    }

    private static string Step1C(string w) =>
        w.EndsWith('y') && HasVowel(w[..^1]) ? w[..^1] + "i" : w;

    /// <summary>Первый подходящий суффикс списка; условие на меру не выполнено — слово не меняется.</summary>
    private static string Replace(string w, (string Suffix, string Replacement)[] rules, int minMeasure)
    {
        foreach ((string suffix, string replacement) in rules)
        {
            if (!w.EndsWith(suffix, StringComparison.Ordinal)) continue;

            string stem = w[..^suffix.Length];
            return Measure(stem) >= minMeasure ? stem + replacement : w;
        }

        return w;
    }

    private static string Step4Strip(string w)
    {
        foreach (string suffix in Step4)
        {
            if (!w.EndsWith(suffix, StringComparison.Ordinal)) continue;

            string stem = w[..^suffix.Length];

            if (suffix == "ion" && (stem.Length == 0 || stem[^1] is not ('s' or 't'))) return w;

            return Measure(stem) > 1 ? stem : w;
        }

        return w;
    }

    private static string Step5(string w)
    {
        if (w.EndsWith('e'))
        {
            string stem = w[..^1];
            int m = Measure(stem);

            if (m > 1 || (m == 1 && !EndsCvc(stem))) w = stem;
        }

        return Measure(w) > 1 && EndsWithDoubleConsonant(w) && w[^1] == 'l' ? w[..^1] : w;
    }

    private static bool IsConsonant(string s, int i) => s[i] switch
    {
        'a' or 'e' or 'i' or 'o' or 'u' => false,
        'y' => i == 0 || !IsConsonant(s, i - 1),
        _ => true,
    };

    /// <summary>Мера m основы: число пар «гласные — согласные» в записи [C](VC)^m[V].</summary>
    private static int Measure(string s)
    {
        int m = 0, i = 0;

        while (i < s.Length && IsConsonant(s, i)) i++;

        while (i < s.Length)
        {
            while (i < s.Length && !IsConsonant(s, i)) i++;
            if (i >= s.Length) break;

            m++;
            while (i < s.Length && IsConsonant(s, i)) i++;
        }

        return m;
    }

    private static bool HasVowel(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (!IsConsonant(s, i)) return true;
        }

        return false;
    }

    private static bool EndsWithDoubleConsonant(string s) =>
        s.Length >= 2 && s[^1] == s[^2] && IsConsonant(s, s.Length - 1);

    /// <summary>Окончание «согласная — гласная — согласная», последняя не w, x, y: hop, fil.</summary>
    private static bool EndsCvc(string s) =>
        s.Length >= 3 && IsConsonant(s, s.Length - 3) && !IsConsonant(s, s.Length - 2)
        && IsConsonant(s, s.Length - 1) && s[^1] is not ('w' or 'x' or 'y');
}
