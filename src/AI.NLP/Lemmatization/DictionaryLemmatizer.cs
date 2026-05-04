using System;
using System.Collections.Generic;
using System.IO;

namespace AI.NLP.Lemmatization;

/// <summary>
/// Словарный лемматизатор. Для каждой словоформы ищет лемму в переданной
/// хеш-таблице. Если слово не найдено — делегирует запрос резервному
/// лемматизатору (по умолчанию — <see cref="IdentityLemmatizer"/>).
///
/// Файл словаря читается в формате «форма &lt;разделитель&gt; лемма» по одной
/// паре на строку. Поддерживаются комментарии (строки, начинающиеся с «#»)
/// и пустые строки.
/// </summary>
[Serializable]
public sealed class DictionaryLemmatizer : LemmatizerBase
{
    private readonly Dictionary<string, string> _dict;
    private readonly ILemmatizer _fallback;

    /// <summary>
    /// Размер загруженного словаря (количество словоформ).
    /// </summary>
    public int Count => _dict.Count;

    /// <summary>
    /// Резервный лемматизатор для неизвестных слов.
    /// </summary>
    public ILemmatizer Fallback => _fallback;

    /// <summary>
    /// Создать словарный лемматизатор с готовой хеш-таблицей.
    /// </summary>
    /// <param name="wordFormToLemma">Словарь «словоформа -> лемма» (регистр игнорируется).</param>
    /// <param name="fallback">Резервный лемматизатор. Если null — <see cref="IdentityLemmatizer"/>.</param>
    public DictionaryLemmatizer(IDictionary<string, string> wordFormToLemma, ILemmatizer fallback = null)
    {
        if (wordFormToLemma == null) throw new ArgumentNullException(nameof(wordFormToLemma));

        _dict = new Dictionary<string, string>(wordFormToLemma.Count, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> kv in wordFormToLemma)
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;
            _dict[NormalizeKey(kv.Key)] = kv.Value ?? kv.Key;
        }

        _fallback = fallback ?? IdentityLemmatizer.Instance;
    }

    /// <inheritdoc />
    public override string Lemmatize(string word)
    {
        if (string.IsNullOrEmpty(word)) return word ?? string.Empty;

        string key = NormalizeKey(word);
        if (_dict.TryGetValue(key, out string lemma))
            return lemma;

        return _fallback.Lemmatize(word);
    }

    /// <summary>
    /// Добавить или обновить запись в словаре.
    /// </summary>
    public void Add(string form, string lemma)
    {
        if (string.IsNullOrEmpty(form)) return;
        _dict[NormalizeKey(form)] = lemma ?? form;
    }

    /// <summary>
    /// Загрузить словарь из файла. Формат: «форма&lt;sep&gt;лемма» в каждой строке.
    /// Пустые строки и строки, начинающиеся с «#», игнорируются.
    /// </summary>
    /// <param name="path">Путь к файлу словаря.</param>
    /// <param name="fallback">Резервный лемматизатор (может быть null).</param>
    /// <param name="separator">Разделитель между формой и леммой. По умолчанию — табуляция.</param>
    public static DictionaryLemmatizer LoadFromFile(string path, ILemmatizer fallback = null, char separator = '\t')
    {
        if (path == null) throw new ArgumentNullException(nameof(path));

        using (var sr = new StreamReader(path))
            return LoadFromReader(sr, fallback, separator);
    }

    /// <summary>
    /// Загрузить словарь из потока. Поток не закрывается — это ответственность вызывающего.
    /// </summary>
    public static DictionaryLemmatizer LoadFromReader(TextReader reader, ILemmatizer fallback = null, char separator = '\t')
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0) continue;
            if (line[0] == '#') continue;

            int sep = line.IndexOf(separator);
            if (sep <= 0) continue; // строка без разделителя или с пустым ключом — пропускаем

            string form = line.Substring(0, sep).Trim();
            string lemma = line.Substring(sep + 1).Trim();
            if (form.Length == 0) continue;

            dict[NormalizeKey(form)] = lemma.Length == 0 ? form : lemma;
        }

        return new DictionaryLemmatizer(dict, fallback);
    }

    private static string NormalizeKey(string s)
    {
        return s.Trim().ToLowerInvariant().Replace('ё', 'е');
    }
}
