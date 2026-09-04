using AI.NLP.Morphology;
using System;

namespace AI.NLP.Lemmatization;

/// <summary>
/// Лемматизатор, который сначала определяет часть речи, а потом применяет правила
/// только этой части речи.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RussianLemmatizer"/> применяет свою таблицу суффиксов ко всему подряд,
/// и там, где суффикс существительного совпадает с глагольным, слово портится:
/// «конём» становится «конать», «поэтому» — «поэтый». Определитель части речи
/// эту порчу снимает: к существительному применяются правила склонения,
/// к неизменяемым словам — никакие.
/// </para>
/// <para>
/// Прилагательные и глаголы по-прежнему разбираются <see cref="RussianLemmatizer"/>:
/// его таблица покрывает обе части речи и проверена тестами, дублировать её здесь
/// незачем. Различение прилагательного и причастия на результат не влияет — обе
/// формы разбирает одна таблица.
/// </para>
/// <para>
/// Разбор идёт по одному слову, без учёта соседей, поэтому омонимия остаётся
/// неразрешимой: «стали» без контекста может быть и глаголом, и существительным.
/// </para>
/// </remarks>
[Serializable]
public sealed class MorphologicalLemmatizer : LemmatizerBase
{
    [NonSerialized]
    private readonly IPosTagger _tagger;

    /// <summary>Создаёт лемматизатор с определителем части речи по умолчанию</summary>
    public MorphologicalLemmatizer() : this(RussianPosTagger.Instance)
    {
    }

    /// <summary>Создаёт лемматизатор с заданным определителем части речи</summary>
    /// <param name="tagger">Определитель части речи</param>
    public MorphologicalLemmatizer(IPosTagger tagger)
    {
        _tagger = tagger ?? throw new ArgumentNullException(nameof(tagger));
    }

    /// <summary>Общий потокобезопасный экземпляр (лемматизатор без состояния)</summary>
    public static readonly MorphologicalLemmatizer Instance = new MorphologicalLemmatizer();

    /// <inheritdoc />
    public override string Lemmatize(string word) => Analyze(word).Lemma;

    /// <summary>
    /// Разбирает слово: возвращает лемму и часть речи
    /// </summary>
    /// <param name="word">Словоформа</param>
    public MorphAnalysis Analyze(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return new MorphAnalysis(word ?? string.Empty, PartOfSpeech.Unknown);

        string w = RussianPhonetics.Normalize(word.Trim());

        if (w.Length == 0)
            return new MorphAnalysis(string.Empty, PartOfSpeech.Unknown);

        // Закрытые классы перечислены полностью: и лемма, и часть речи известны точно
        if (RussianClosedClassLexicon.TryLookupNormalized(w, out MorphAnalysis known))
            return known;

        PartOfSpeech pos = _tagger.Tag(w);

        switch (pos)
        {
            case PartOfSpeech.Noun:
                return new MorphAnalysis(RussianNounInflection.ToNominative(w), pos);

            // Наречия, предлоги, союзы, частицы и междометия не изменяются: любое
            // правило здесь может только испортить слово.
            case PartOfSpeech.Adverb:
            case PartOfSpeech.Preposition:
            case PartOfSpeech.Conjunction:
            case PartOfSpeech.Particle:
            case PartOfSpeech.Interjection:
                return new MorphAnalysis(w, pos);

            // Числительные и местоимения, не попавшие в словарь закрытых классов,
            // правилами не берутся: их парадигмы нерегулярны.
            case PartOfSpeech.Numeral:
            case PartOfSpeech.Pronoun:
                return new MorphAnalysis(w, pos);

            default:
                return new MorphAnalysis(RussianLemmatizer.Instance.Lemmatize(w), pos);
        }
    }
}
