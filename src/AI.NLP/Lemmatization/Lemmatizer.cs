using System;
using System.Collections.Generic;
using System.IO;

namespace AI.NLP.Lemmatization;

/// <summary>
/// Фасад с фабричными методами для быстрого создания готовых цепочек
/// лемматизаторов. Использовать, если не нужно вручную собирать декораторы.
/// </summary>
public static class Lemmatizer
{
    /// <summary>
    /// Лемматизатор русского языка «по умолчанию» — морфологический разбор
    /// с определением части речи плюс кэш.
    /// </summary>
    /// <param name="withCache">Обернуть результат в <see cref="CachingLemmatizer"/>.</param>
    /// <remarks>
    /// <para>
    /// Замер на эталонном корпусе: 82.8 % против 61.1 % у одних правил по суффиксу
    /// (<see cref="CreateRussianRules"/>). Вся разница — существительные: 61.5 % против 4.4 %,
    /// потому что правил склонения в суффиксной таблице нет вовсе.
    /// </para>
    /// <para>
    /// Обратная сторона — характер ошибки. Правила без части речи почти всегда отказывались
    /// разбирать (слово возвращалось нетронутым и оставалось узнаваемым), морфологический
    /// разбор чаще приводит слово к основе: «книгой» → «книг». Для поиска и индексации это
    /// выигрыш — все формы слова сходятся к одному ключу; для показа пользователю — нет.
    /// Кому нужно прежнее осторожное поведение, тот берёт <see cref="CreateRussianRules"/>.
    /// </para>
    /// </remarks>
    public static ILemmatizer CreateRussian(bool withCache = true)
    {
        ILemmatizer lemm = MorphologicalLemmatizer.Instance;
        return withCache ? new CachingLemmatizer(lemm) : lemm;
    }

    /// <summary>
    /// Лемматизатор русского языка на одних суффиксальных правилах, без определения
    /// части речи.
    /// </summary>
    /// <param name="withCache">Обернуть результат в <see cref="CachingLemmatizer"/>.</param>
    /// <remarks>
    /// Разбирает хуже (61.1 % против 82.8 %), но почти никогда не портит слово: не найдя
    /// правила, возвращает исходную форму. Существительные при этом не разбираются вовсе.
    /// </remarks>
    public static ILemmatizer CreateRussianRules(bool withCache = true)
    {
        ILemmatizer lemm = RussianLemmatizer.Instance;
        return withCache ? new CachingLemmatizer(lemm) : lemm;
    }

    /// <summary>
    /// Загрузить словарь «словоформа -> лемма» из файла и использовать морфологический
    /// разбор как резерв для неизвестных слов.
    /// </summary>
    /// <param name="dictionaryPath">Путь к файлу словаря (см. <see cref="DictionaryLemmatizer.LoadFromFile"/>).</param>
    /// <param name="withCache">Обернуть результат в <see cref="CachingLemmatizer"/>.</param>
    /// <param name="separator">Разделитель между формой и леммой в файле.</param>
    public static ILemmatizer CreateRussianFromFile(string dictionaryPath, bool withCache = true, char separator = '\t')
    {
        var dict = DictionaryLemmatizer.LoadFromFile(dictionaryPath, MorphologicalLemmatizer.Instance, separator);
        return withCache ? (ILemmatizer)new CachingLemmatizer(dict) : dict;
    }

    /// <summary>
    /// Собрать лемматизатор из готового словаря. Неизвестные слова идут в <paramref name="fallback"/>
    /// (если не задан — используется морфологический разбор).
    /// </summary>
    public static ILemmatizer CreateFromDictionary(IDictionary<string, string> dictionary,
        ILemmatizer fallback = null, bool withCache = true)
    {
        var dict = new DictionaryLemmatizer(dictionary, fallback ?? MorphologicalLemmatizer.Instance);
        return withCache ? (ILemmatizer)new CachingLemmatizer(dict) : dict;
    }
}
