using AI.NLP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AI.DataPrepaire.Tokenizers.TextTokenizers;

//(ToDo: сделать ограничение по словарю)
/// <summary>
/// Токенизатор на уровне слов
/// </summary>
[Serializable]
public class WordTokenizer : TokenizerBase<string>
{
    /// <summary>
    /// Переводить ли в нижний регистр
    /// </summary>
    public bool IsLower { get; set; } = true;

    /// <summary>
    /// Длинна словаря
    /// </summary>
    public int DictLen { get { return decoder.Length; } }

    /// <summary>
    /// Трансформация строки
    /// </summary>
    public Func<string, string> TransformerStr { get; set; }

    /// <summary>
    /// Токенизатор на уровне слов 
    /// </summary>
    public WordTokenizer(string[] decoder, Dictionary<string, int> encoder, Func<string, string> transformerStr = null) : base(decoder, encoder)
    {
        TransformerStr = transformerStr;
    }

    /// <summary>
    /// Токенизатор на уровне слов 
    /// </summary>
    public WordTokenizer(string path_to_text, bool isLower = true, Func<string, string> transformerStr = null)
    {
        IsLower = isLower;
        TransformerStr = transformerStr;
        TrainFromTextFile(path_to_text);
    }

    /// <summary>
    /// Токенизатор на уровне слов 
    /// </summary>
    public WordTokenizer(bool isLower = true, Func<string, string> transformerStr = null)
    {
        IsLower = isLower;
        TransformerStr = transformerStr;
    }



    /// <summary>
    /// Кодирование текста
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public override int[] Encode(string data)
    {
        string newStr = TransformerStr == null ? TextStandard.OnlyCharsAndDigit(data, IsLower) : TransformerStr(data);
        string[] words = newStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return Encode(words);
    }

    /// <summary>
    /// Декодирование массива индексов в строку
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    public override string DecodeObj(IEnumerable<int> ids)
    {
        StringBuilder stringBuilder = new StringBuilder();
        string[] strs = Decode(ids);


        foreach (var str in strs)
        {
            stringBuilder.Append(str);
            stringBuilder.Append(' ');
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Обучение/создание токенизатора
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public void TrainFromTextFile(string path)
    {
        string text = File.ReadAllText(path);
        TrainFromText(text);
    }

    /// <summary>
    /// Обучение/создание токенизатора
    /// </summary>
    public void TrainFromText(string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        // Словарь строится в ProbabilityDictionaryHash.Run -> GetWords -> OnlyCharsAndDigit (по умолчанию lower).
        // Encode должен использовать тот же регистр (IsLower), иначе почти все слова станут <UNK>.
        string forVocab = TransformerStr == null ? text : TransformerStr(text);
        ProbabilityDictionaryHash probability = new ProbabilityDictionaryHash(false);
        Dictionary<string, double> data = probability.Run(forVocab);

        Dictionary<string, int> words = new Dictionary<string, int>();
        string[] decoder = new string[data.Count + 4];

        decoder[UnknowToken] = "<UNK>";
        decoder[PadToken] = "<pad>";
        decoder[StartToken] = "<s>";
        decoder[EndToken] = "</s>";

        words.Add("<UNK>", UnknowToken);
        words.Add("<pad>", PadToken);
        words.Add("<s>", StartToken);
        words.Add("</s>", EndToken);

        int tokenIndex = 4;
        foreach (KeyValuePair<string, double> item in data)
        {
            if (tokenIndex >= decoder.Length)
            {
                break;
            }

            decoder[tokenIndex] = item.Key;
            words[item.Key] = tokenIndex;
            tokenIndex++;
        }

        this.decoder = decoder;
        encoder = words;
    }
}
