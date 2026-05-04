using System.Collections.Generic;

namespace AI.NLP.Lemmatization;

/// <summary>
/// Лемматизатор — приводит словоформу к канонической (начальной) форме —
/// лемме. В отличие от стеммера результат всегда является валидной
/// словарной формой, а не обрезком корня.
/// </summary>
public interface ILemmatizer
{
    /// <summary>
    /// Лемматизировать одно слово.
    /// </summary>
    /// <param name="word">Исходная словоформа</param>
    /// <returns>Лемма (словарная форма)</returns>
    string Lemmatize(string word);

    /// <summary>
    /// Лемматизировать массив слов.
    /// </summary>
    string[] LemmatizeAll(IEnumerable<string> words);

    /// <summary>
    /// Разбить текст на слова и вернуть массив лемм.
    /// </summary>
    /// <param name="text">Текст (может содержать пунктуацию, переносы и т.п.)</param>
    /// <returns>Массив лемм найденных слов в порядке появления.</returns>
    string[] LemmatizeToWords(string text);

    /// <summary>
    /// Лемматизировать предложение/текст с сохранением пунктуации и пробелов.
    /// Каждая словоформа заменяется своей леммой, прочие символы не меняются.
    /// </summary>
    /// <param name="sentence">Исходное предложение или текст</param>
    /// <returns>Строка с лемматизированными словами и прежней пунктуацией.</returns>
    string LemmatizeSentence(string sentence);
}
