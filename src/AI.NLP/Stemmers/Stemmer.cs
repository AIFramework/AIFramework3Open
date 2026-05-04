//
// Автор кода стемера: SergeiGalkovskii
// Ссылка на репозиторий: https://github.com/SergeiGalkovskii/Porter-s-algorithm-for-stemming-for-russian-language-csharp
//Ссылка на оригинальный проект стемера:

//Лицензия на стемер


//MIT License

//Copyright(c) 2017 SergeiGalkovskii

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.



using System;
using System.Text.RegularExpressions;


namespace AI.NLP.Stemmers;

/// <summary>
/// Стемер русского языка
/// </summary>
[Serializable]
public static class StemmerRus
{
    // Было: "<;=[ая]" — опечатка, должна была быть lookbehind-группа "(?<=[ая])".
    private static readonly Regex PERFECTIVEGROUND = new Regex("((ив|ивши|ившись|ыв|ывши|ывшись)|((?<=[ая])(в|вши|вшись)))$", RegexOptions.Compiled);
    private static readonly Regex REFLEXIVE = new Regex("(с[яь])$", RegexOptions.Compiled);
    private static readonly Regex ADJECTIVE = new Regex("(ее|ие|ые|ое|ими|ыми|ей|ий|ый|ой|ем|им|ым|ом|его|ого|ему|ому|их|ых|ую|юю|ая|яя|ою|ею)$", RegexOptions.Compiled);
    private static readonly Regex PARTICIPLE = new Regex("((ивш|ывш|ующ)|((?<=[ая])(ем|нн|вш|ющ|щ)))$", RegexOptions.Compiled);
    private static readonly Regex VERB = new Regex("((ила|ыла|ена|ейте|уйте|ите|или|ыли|ей|уй|ил|ыл|им|ым|ен|ило|ыло|ено|ят|ует|уют|ит|ыт|ены|ить|ыть|ишь|ую|ю)|((?<=[ая])(ла|на|ете|йте|ли|й|л|ем|н|ло|но|ет|ют|ны|ть|ешь|нно)))$", RegexOptions.Compiled);
    private static readonly Regex NOUN = new Regex("(а|ев|ов|ие|ье|е|иями|ями|ами|еи|ии|и|ией|ей|ой|ий|й|иям|ям|ием|ем|ам|ом|о|у|ах|иях|ях|ы|ь|ию|ью|ю|ия|ья|я)$", RegexOptions.Compiled);
    private static readonly Regex RVRE = new Regex("^(.*?[аеиоуыэюя])(.*)$", RegexOptions.Compiled);
    private static readonly Regex DERIVATIONAL = new Regex(".*[^аеиоуыэюя]+[аеиоуыэюя].*ость?$", RegexOptions.Compiled);
    private static readonly Regex DER = new Regex("ость?$", RegexOptions.Compiled);
    private static readonly Regex SUPERLATIVE = new Regex("(ейше|ейш)$", RegexOptions.Compiled);
    private static readonly Regex I = new Regex("и$", RegexOptions.Compiled);
    private static readonly Regex P = new Regex("ь$", RegexOptions.Compiled);
    private static readonly Regex NN = new Regex("нн$", RegexOptions.Compiled);

    /// <summary>
    /// Стемминг массива слов
    /// </summary>
    /// <param name="words">Массив слов</param>
    public static string[] TransformingWordsArray(string[] words)
    {
        string[] strs = new string[words.Length];

        for (int i = 0; i < words.Length; i++)
            strs[i] = TransformingWord(words[i]);

        return strs;
    }

    /// <summary>
    /// стемминг
    /// </summary>
    /// <param name="word">слово</param>
    /// <returns>приставка+корень</returns>
    public static string TransformingWord(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word ?? string.Empty;

        word = word.ToLower().Replace('ё', 'е');

        Match m = RVRE.Match(word);
        if (!m.Success)
            return word;

        string pre = m.Groups[1].Value;
        string rv = m.Groups[2].Value;

        // Шаг 1: PERFECTIVEGROUND
        string after = PERFECTIVEGROUND.Replace(rv, string.Empty, 1);
        if (after != rv)
        {
            rv = after;
        }
        else
        {
            // Шаг 1.a: REFLEXIVE
            rv = REFLEXIVE.Replace(rv, string.Empty, 1);

            // Шаг 1.b: ADJECTIVE + PARTICIPLE
            after = ADJECTIVE.Replace(rv, string.Empty, 1);
            if (after != rv)
            {
                rv = after;
                rv = PARTICIPLE.Replace(rv, string.Empty, 1);
            }
            else
            {
                // Шаг 1.c: VERB / NOUN
                after = VERB.Replace(rv, string.Empty, 1);
                if (after != rv)
                    rv = after;
                else
                    rv = NOUN.Replace(rv, string.Empty, 1);
            }
        }

        // Шаг 2
        rv = I.Replace(rv, string.Empty, 1);

        // Шаг 3
        if (DERIVATIONAL.IsMatch(rv))
            rv = DER.Replace(rv, string.Empty, 1);

        // Шаг 4
        after = P.Replace(rv, string.Empty, 1);
        if (after != rv)
        {
            rv = after;
        }
        else
        {
            rv = SUPERLATIVE.Replace(rv, string.Empty, 1);
            rv = NN.Replace(rv, "н", 1);
        }

        return pre + rv;
    }
}
