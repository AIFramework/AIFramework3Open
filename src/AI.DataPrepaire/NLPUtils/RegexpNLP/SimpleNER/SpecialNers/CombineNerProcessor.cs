using System;
using System.Collections.Generic;

namespace AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER.SpecialNers;

/// <summary>
/// Комбинированный NER
/// </summary>
[Serializable]
public class CombineNerProcessor : INerProcessor
{
    RegexNer[] _regexNers;

    /// <summary>
    /// Словарь преобразования нера в токен (агрегированный)
    /// </summary>
    public Dictionary<string, string> NerToNerToken
    {
        get
        {
            var result = new Dictionary<string, string>();
            foreach (var ner in _regexNers)
                foreach (var pair in ner.NerToNerToken)
                    result[pair.Key] = pair.Value;
            return result;
        }
    }

    /// <summary>
    /// Словарь преобразования токена в нер (агрегированный)
    /// </summary>
    public Dictionary<string, string> NerTokenToNer
    {
        get
        {
            var result = new Dictionary<string, string>();
            foreach (var ner in _regexNers)
                foreach (var pair in ner.NerTokenToNer)
                    result[pair.Key] = pair.Value;
            return result;
        }
    }

    /// <summary>
    /// Комбинированный NER
    /// </summary>
    public CombineNerProcessor()
    {
        // Важно что mail перед site
        _regexNers = new RegexNer[]
        {
            new TimeProcessor(),
            new EmailAdressProcessor(),
            new SiteAdressProcessor(),
            new AdressProcessor(),
            new PhoneNerProcessor(),
            new NameRusNerProcessor(),
            new OrderNumberNerProcessor()
        };
    }

    /// <summary>
    /// Запуск сегментации текста
    /// </summary>
    /// <param name="text"></param>
    public string RunProcessor(string text)
    {
        string outStr = text;

        foreach (var item in _regexNers)
            outStr = item.RunProcessor(outStr);

        return outStr;
    }


    /// <summary>
    /// Запуск декодирования текста
    /// </summary>
    /// <param name="text"></param>
    public string NerDecoder(string text)
    {
        string outStr = text;

        foreach (var item in _regexNers)
            outStr = item.NerDecoder(outStr);

        return outStr;
    }
}
