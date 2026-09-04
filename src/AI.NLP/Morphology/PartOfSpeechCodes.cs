using System;
using System.Collections.Generic;

namespace AI.NLP.Morphology;

/// <summary>
/// Перевод части речи в код разметки и обратно, а также русское название.
/// </summary>
/// <remarks>
/// Коды совпадают с теми, что стоят в эталонном корпусе <c>lemmas-ru.tsv</c>:
/// разметка и разбор должны говорить на одном языке, иначе сравнивать их нельзя.
/// </remarks>
public static class PartOfSpeechCodes
{
    private static readonly Dictionary<PartOfSpeech, string> Codes = new Dictionary<PartOfSpeech, string>
    {
        [PartOfSpeech.Noun] = "NOUN",
        [PartOfSpeech.Adjective] = "ADJ",
        [PartOfSpeech.Verb] = "VERB",
        [PartOfSpeech.Adverb] = "ADV",
        [PartOfSpeech.Pronoun] = "PRON",
        [PartOfSpeech.Numeral] = "NUM",
        [PartOfSpeech.Preposition] = "PREP",
        [PartOfSpeech.Conjunction] = "CONJ",
        [PartOfSpeech.Particle] = "PRCL",
        [PartOfSpeech.Interjection] = "INTJ",
        [PartOfSpeech.Unknown] = "X"
    };

    private static readonly Dictionary<PartOfSpeech, string> Names = new Dictionary<PartOfSpeech, string>
    {
        [PartOfSpeech.Noun] = "существительное",
        [PartOfSpeech.Adjective] = "прилагательное",
        [PartOfSpeech.Verb] = "глагол",
        [PartOfSpeech.Adverb] = "наречие",
        [PartOfSpeech.Pronoun] = "местоимение",
        [PartOfSpeech.Numeral] = "числительное",
        [PartOfSpeech.Preposition] = "предлог",
        [PartOfSpeech.Conjunction] = "союз",
        [PartOfSpeech.Particle] = "частица",
        [PartOfSpeech.Interjection] = "междометие",
        [PartOfSpeech.Unknown] = "не определена"
    };

    private static readonly Dictionary<string, PartOfSpeech> ByCode = BuildReverse();

    /// <summary>Код разметки для части речи</summary>
    /// <param name="partOfSpeech">Часть речи</param>
    public static string ToCode(this PartOfSpeech partOfSpeech)
        => Codes.TryGetValue(partOfSpeech, out string code) ? code : "X";

    /// <summary>Русское название части речи</summary>
    /// <param name="partOfSpeech">Часть речи</param>
    public static string ToRussian(this PartOfSpeech partOfSpeech)
        => Names.TryGetValue(partOfSpeech, out string name) ? name : "не определена";

    /// <summary>
    /// Часть речи по коду разметки; для неизвестного кода — <see cref="PartOfSpeech.Unknown"/>
    /// </summary>
    /// <param name="code">Код разметки, например «NOUN»</param>
    public static PartOfSpeech Parse(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return PartOfSpeech.Unknown;

        return ByCode.TryGetValue(code.Trim().ToUpperInvariant(), out PartOfSpeech pos)
            ? pos
            : PartOfSpeech.Unknown;
    }

    private static Dictionary<string, PartOfSpeech> BuildReverse()
    {
        var reverse = new Dictionary<string, PartOfSpeech>(StringComparer.Ordinal);

        foreach (KeyValuePair<PartOfSpeech, string> entry in Codes)
            reverse[entry.Value] = entry.Key;

        // Синонимы разметки Universal Dependencies: предлог там ADP, частица PART,
        // а союзы разделены на сочинительные и подчинительные.
        reverse["ADP"] = PartOfSpeech.Preposition;
        reverse["PART"] = PartOfSpeech.Particle;
        reverse["CCONJ"] = PartOfSpeech.Conjunction;
        reverse["SCONJ"] = PartOfSpeech.Conjunction;

        return reverse;
    }
}
