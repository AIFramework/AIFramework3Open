using AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER.SpecialNers;
using System;
using System.Collections.Generic;

// ПЕРЕЕХАЛО из AI.DataPrepaire в AI.NLP: разбиение на предложения понадобилось поиску
// по документам (чанкер корпуса), а ссылка на AI.DataPrepaire затащила бы в закрытие
// ссылок веб-хостов AI.ML, AI.DSP, AI.KNN и AI.Fuzzy — ровно то, от чего Rag и Syntez
// избавлялись. Пространство имён СОХРАНЕНО: типы самодостаточны (только System.*), и
// прежние потребители видят их транзитивно — AI.DataPrepaire ссылается на AI.NLP.
namespace AI.DataPrepaire.NLPUtils.RegexpNLP;

/// <summary>
/// Разбивка текста на предложения.
/// </summary>
/// <remarks>
/// Конец предложения решает одно правило (знаки, сокращения из <see cref="SentenceAbbreviations"/>,
/// следующий токен) для всех путей: без NER, с NER и с собственными сокращениями.
/// </remarks>
[Serializable]
public class SentencesTokenizer
{
    /// <summary>
    /// Дополнительные сокращения пользователя (шаблоны регулярных выражений) — класс «никогда не конец»:
    /// точка внутри совпадения и в его конце предложение не завершает. По умолчанию нет.
    /// </summary>
    public AbbreviationsNerProcessor AbbreviationsProcessor { get; set; }

    /// <summary>
    /// Разбивка с таблицей сокращений <see cref="SentenceAbbreviations"/>.
    /// </summary>
    public SentencesTokenizer() { }

    /// <summary>
    /// Разбивка с таблицей сокращений и дополнительными сокращениями пользователя.
    /// </summary>
    /// <param name="abbreviations">Сокращения (шаблоны регулярных выражений), после которых предложение не кончается.</param>
    public SentencesTokenizer(IEnumerable<string> abbreviations)
    {
        AbbreviationsProcessor = new AbbreviationsNerProcessor(abbreviations);
    }

    /// <summary>
    /// Разделяет входной текст на предложения, учитывая сокращения.
    /// </summary>
    /// <param name="text">Текст для токенизации.</param>
    /// <param name="withTrim">Обрезание пробельных символов; без него склейка предложений равна тексту.</param>
    /// <returns>Список предложений, извлечённых из входного текста.</returns>
    public List<string> Tokenize(string text, bool withTrim = true) => Tokenize(text, null, withTrim);

    /// <summary>
    /// Разделяет текст на предложения, не разрывая заданные сущности.
    /// </summary>
    /// <param name="text">Текст для токенизации.</param>
    /// <param name="entities">Сущности (начало, длина): знак внутри сущности конца не даёт,
    /// знак последним символом сущности решает общее правило.</param>
    /// <param name="withTrim">Обрезание пробельных символов; без него склейка предложений равна тексту.</param>
    public List<string> Tokenize(string text, IEnumerable<(int Start, int Length)> entities, bool withTrim = true)
    {
        var sentences = new List<string>();
        if (string.IsNullOrEmpty(text)) return sentences;

        var ends = SentenceBoundaryRule.FindEnds(text, entities, AbbreviationsProcessor?.FindEntities(text));
        ends.Add(text.Length);

        int start = 0;
        foreach (int end in ends)
        {
            string piece = text[start..end];
            start = end;

            if (string.IsNullOrWhiteSpace(piece) && (withTrim || sentences.Count > 0))
            {
                if (!withTrim) sentences[^1] += piece;
                continue;
            }

            sentences.Add(withTrim ? piece.Trim() : piece);
        }

        return sentences;
    }

    /// <summary>
    /// Разделяет входной текст на предложения, учитывая сокращения и именованные сущности.
    /// </summary>
    /// <param name="text">Текст для токенизации.</param>
    /// <param name="withTrim">Обрезание пробельных символов</param>
    /// <returns>Список предложений, извлечённых из входного текста.</returns>
    public List<string> TokenizeWithNer(string text, bool withTrim = true) =>
        Tokenize(text, string.IsNullOrEmpty(text) ? null : new CombineNerProcessor().FindEntities(text), withTrim);
}
