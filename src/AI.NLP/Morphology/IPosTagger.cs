using System.Collections.Generic;

namespace AI.NLP.Morphology;

/// <summary>
/// Определитель части речи
/// </summary>
public interface IPosTagger
{
    /// <summary>Часть речи отдельного слова</summary>
    /// <param name="word">Слово</param>
    PartOfSpeech Tag(string word);

    /// <summary>Части речи для последовательности слов</summary>
    /// <param name="words">Слова</param>
    IReadOnlyList<PartOfSpeech> Tag(IReadOnlyList<string> words);
}
