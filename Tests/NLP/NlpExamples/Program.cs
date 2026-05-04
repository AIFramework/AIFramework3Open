using System.Globalization;
using System.IO;
using System.Text;
using AI.DataStructs.Algebraic;
using AI.NLP;
using AI.NLP.Stemmers;

namespace NlpExamples;

internal static class Program
{
    public static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("=== Примеры AI.NLP (вероятностные словари, TF‑IDF, токенизация) ===\n");

        try
        {
            DemoProbabilityDictionary();
            DemoProbabilityDictionaryHash();
            DemoTfIdf();
            DemoStaticGetWords();
            DemoTextTokenizer();
            DemoBoWModelTempFile();
            DemoTextStandard();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка: " + ex.Message);
            return 1;
        }

        Console.WriteLine("Готово.");
        return 0;
    }

    private static void DemoProbabilityDictionary()
    {
        Console.WriteLine("-- ProbabilityDictionary: частоты и вероятности по токенам");
        const string text = "машинное обучение и обработка естественного языка. язык и обучение — основа nlp.";
        var pd = new ProbabilityDictionary(isStopDel: true, isDigitDel: true, isStem: true);
        ProbabilityDictionaryData<string>[] table = pd.Run(text);

        int show = Math.Min(8, table.Length);
        for (int i = 0; i < show; i++)
        {
            string p = table[i].Probability.ToString("F5", CultureInfo.InvariantCulture);
            Console.WriteLine($"   [{i}] {table[i].Word,-20} p={p}");
        }

        string[] top = pd.GetWordsRun(text, numW: 5);
        Console.WriteLine("   Топ-5 (GetWordsRun): " + string.Join(", ", top));
        Console.WriteLine();
    }

    private static void DemoProbabilityDictionaryHash()
    {
        Console.WriteLine("-- ProbabilityDictionaryHash: нормированные частоты в Dictionary");
        const string text = "один два три два один один";
        var hash = new ProbabilityDictionaryHash(isStem: false);
        Dictionary<string, double> map = hash.Run(text);

        foreach (KeyValuePair<string, double> kv in map.OrderByDescending(x => x.Value))
        {
            string v = kv.Value.ToString("F5", CultureInfo.InvariantCulture);
            Console.WriteLine($"   {kv.Key,-10} -> {v}");
        }

        Console.WriteLine();
    }

    private static void DemoTfIdf()
    {
        Console.WriteLine("-- TFIDF: tf, idf, поиск документа по запросу");
        string[] docs =
        {
            "кошка сидит на ковре и спит",
            "собака бегает по двору и лает",
            "кошка и собака живут в одном доме",
        };

        var tfidf = new TFIDF(docs);
        // Ключи в словарях — после стемминга (как в ProbabilityDictionaryHash)
        string[] stem0 = ProbabilityDictionary.GetWords(docs[0], IsStem: true);
        string term = stem0.Length > 0 ? stem0[0] : "кошка";
        string idfStr = tfidf.IDFWord(term).ToString("F5", CultureInfo.InvariantCulture);
        Console.WriteLine($"   Термин (стем из doc[0]): \"{term}\"  IDF = {idfStr}");

        for (int d = 0; d < docs.Length; d++)
        {
            double tf = tfidf.TFWord(term, d);
            double v = tfidf.TF_IDF(term, d);
            string tfS = tf.ToString("F5", CultureInfo.InvariantCulture);
            string vS = v.ToString("F5", CultureInfo.InvariantCulture);
            Console.WriteLine($"   doc[{d}] TF={tfS}  TF-IDF={vS}");
        }

        string query = "кошка спит";
        int best = tfidf.Search(query);
        Console.WriteLine($"   Search(\"{query}\") -> документ с индексом {best}: \"{docs[best]}\"");
        Console.WriteLine();
    }

    private static void DemoStaticGetWords()
    {
        Console.WriteLine("-- ProbabilityDictionary.GetWords(text, IsStem): токены для TF-IDF и словарей");
        string text = "Быстрая обработка текстов и стемминг слов.";
        string[] tokens = ProbabilityDictionary.GetWords(text, IsStem: true);
        Console.WriteLine("   " + string.Join(" | ", tokens));
        Console.WriteLine();
    }

    private static void DemoTextTokenizer()
    {
        Console.WriteLine("-- TextTokenizer: словарь по обучающему тексту и вектор индексов");
        var tok = new TextTokenizer(isLower: true, isStem: true)
        {
            Count = 12,
            WordCount = 20,
        };
        string train = "нейросеть учится на данных. данные и обучение — основа модели.";
        tok.Train(train);

        string seq = "обучение модели на данных";
        Vector v = tok.GetSeq2Tokens(seq);
        Console.WriteLine("   Фрагмент вектора индексов (первые 8): " +
            string.Join(", ", Enumerable.Range(0, Math.Min(8, v.Count)).Select(i => v[i].ToString(CultureInfo.InvariantCulture))));
        Console.WriteLine();
    }

    private static void DemoBoWModelTempFile()
    {
        Console.WriteLine("-- BoWModel: словарь из файла и вектор частот");
        string vocabPath = Path.Combine(Path.GetTempPath(), "nlp_examples_bow_vocab.txt");
        try
        {
            const string corpus = "раз два три четыре пять шесть семь восемь девять десять";
            BoWModel.ModelGen(corpus, vocabPath, isStop: false);

            var bow = new BoWModel(vocabPath);
            string phrase = "три четыре пять шесть";
            Vector vec = bow.GetVector(phrase);
            string sumS = vec.Sum().ToString("F2", CultureInfo.InvariantCulture);
            Console.WriteLine($"   Len={bow.Len}, сумма счётчиков={sumS} (фраза: \"{phrase}\")");
        }
        finally
        {
            if (File.Exists(vocabPath))
                File.Delete(vocabPath);
        }

        Console.WriteLine();
    }

    private static void DemoTextStandard()
    {
        Console.WriteLine("-- TextStandard: нормализация и только буквы/цифры");
        string raw = "Привет!!  Как\tдела?  ";
        string n = TextStandard.Normalize(raw, isLower: true);
        string only = TextStandard.OnlyCharsAndDigit("Строка-123 (тест)");
        Console.WriteLine($"   Normalize: \"{n}\"");
        Console.WriteLine($"   OnlyCharsAndDigit: \"{only}\"");
        Console.WriteLine($"   StemmerRus(\"машинное\") -> \"{StemmerRus.TransformingWord("машинное")}\"");
        Console.WriteLine();
    }
}
