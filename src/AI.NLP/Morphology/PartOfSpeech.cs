namespace AI.NLP.Morphology;

/// <summary>
/// Часть речи. Набор соответствует разметке Universal Dependencies в той части,
/// которая различима без синтаксического разбора.
/// </summary>
public enum PartOfSpeech
{
    /// <summary>Часть речи не определена</summary>
    Unknown = 0,

    /// <summary>Существительное</summary>
    Noun,

    /// <summary>Прилагательное</summary>
    Adjective,

    /// <summary>Глагол, включая причастия и деепричастия</summary>
    Verb,

    /// <summary>Наречие</summary>
    Adverb,

    /// <summary>Местоимение</summary>
    Pronoun,

    /// <summary>Числительное</summary>
    Numeral,

    /// <summary>Предлог</summary>
    Preposition,

    /// <summary>Союз</summary>
    Conjunction,

    /// <summary>Частица</summary>
    Particle,

    /// <summary>Междометие</summary>
    Interjection
}
