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
    /// Лемматизатор русского языка «по умолчанию» — правила + кэш.
    /// </summary>
    public static ILemmatizer CreateRussian(bool withCache = true)
    {
        ILemmatizer lemm = RussianLemmatizer.Instance;
        return withCache ? new CachingLemmatizer(lemm) : lemm;
    }

    /// <summary>
    /// Загрузить словарь «словоформа -> лемма» из файла и использовать правила
    /// русского языка как резерв для неизвестных слов.
    /// </summary>
    /// <param name="dictionaryPath">Путь к файлу словаря (см. <see cref="DictionaryLemmatizer.LoadFromFile"/>).</param>
    /// <param name="withCache">Обернуть результат в <see cref="CachingLemmatizer"/>.</param>
    /// <param name="separator">Разделитель между формой и леммой в файле.</param>
    public static ILemmatizer CreateRussianFromFile(string dictionaryPath, bool withCache = true, char separator = '\t')
    {
        var dict = DictionaryLemmatizer.LoadFromFile(dictionaryPath, RussianLemmatizer.Instance, separator);
        return withCache ? (ILemmatizer)new CachingLemmatizer(dict) : dict;
    }

    /// <summary>
    /// Собрать лемматизатор из готового словаря. Неизвестные слова идут в <paramref name="fallback"/>
    /// (если не задан — используются правила русского языка).
    /// </summary>
    public static ILemmatizer CreateFromDictionary(IDictionary<string, string> dictionary,
        ILemmatizer fallback = null, bool withCache = true)
    {
        var dict = new DictionaryLemmatizer(dictionary, fallback ?? RussianLemmatizer.Instance);
        return withCache ? (ILemmatizer)new CachingLemmatizer(dict) : dict;
    }
}
