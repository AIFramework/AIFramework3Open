using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AI.NLP.Lemmatization;

/// <summary>
/// Базовый класс-лемматизатор. Наследникам достаточно реализовать
/// <see cref="Lemmatize(string)"/>: токенизация предложения, сбор массивов
/// и обход коллекций реализованы здесь.
/// </summary>
[Serializable]
public abstract class LemmatizerBase : ILemmatizer
{
    /// <summary>
    /// Регулярка для поиска слов в тексте: последовательности букв
    /// (русских и латинских, с учётом диакритиков Unicode).
    /// </summary>
    protected static readonly Regex WordRegex =
        new Regex(@"[\p{L}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public abstract string Lemmatize(string word);

    /// <inheritdoc />
    public virtual string[] LemmatizeAll(IEnumerable<string> words)
    {
        if (words == null) return new string[0];

        if (words is ICollection<string> col)
        {
            string[] res = new string[col.Count];
            int i = 0;
            foreach (string w in col)
                res[i++] = Lemmatize(w);
            return res;
        }

        var list = new List<string>();
        foreach (string w in words)
            list.Add(Lemmatize(w));
        return list.ToArray();
    }

    /// <inheritdoc />
    public virtual string[] LemmatizeToWords(string text)
    {
        if (string.IsNullOrEmpty(text)) return new string[0];

        MatchCollection matches = WordRegex.Matches(text);
        string[] res = new string[matches.Count];
        for (int i = 0; i < matches.Count; i++)
            res[i] = Lemmatize(matches[i].Value);
        return res;
    }

    /// <inheritdoc />
    public virtual string LemmatizeSentence(string sentence)
    {
        if (string.IsNullOrEmpty(sentence)) return sentence ?? string.Empty;

        return WordRegex.Replace(sentence, m => Lemmatize(m.Value));
    }
}
