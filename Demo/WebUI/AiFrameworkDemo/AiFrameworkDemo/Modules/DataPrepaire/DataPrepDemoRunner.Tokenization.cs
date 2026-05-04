using AI.DataPrepaire.DataNormalizers;
using AI.DataPrepaire.DataLoader;
using AI.DataPrepaire.DataLoader.Formats;
using AI.DataPrepaire.Tokenizers.TextTokenizers;
using AI.DataPrepaire.NLPUtils;
using AI.DataPrepaire.NLPUtils.RegexpNLP;
using AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER;
using AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER.SpecialNers;
using AI.DataPrepaire.NLPUtils.TextClassification;
using AI.DataPrepaire.NLPUtils.TextGeneration;
using AI.DataStructs.Algebraic;
using AI.Charts;
using AiFrameworkDemo.Core;
using SkiaSharp;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.DataPrepaire
{
    public static partial class DataPrepDemoRunner
    {
        private static string DoWordTokenizer(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int topK    = I(p, "topK",     20);
            int corpus  = I(p, "corpusId",  0);
            bool lower  = I(p, "isLower",   1) != 0;

            var tok = new WordTokenizer(isLower: lower);
            tok.TrainFromText(Corpora[corpus]);

            int vocabSize = tok.DictLen;
            topK = Math.Min(topK, vocabSize);

            int[] allIds = tok.Encode(Corpora[corpus]);
            var freq = new Dictionary<int, int>();
            foreach (int id in allIds)
                freq[id] = freq.GetValueOrDefault(id) + 1;

            var sorted = freq.OrderByDescending(kv => kv.Value).Take(topK).ToArray();

            var xRank = new Vector(topK);
            var yFreq = new Vector(topK);
            for (int i = 0; i < topK; i++) { xRank[i] = i + 1; yFreq[i] = sorted[i].Value; }

            cv.AddPlot(xRank, yFreq, "Частота токена", C(0), 2);
            cv.AddScatter(xRank, yFreq, "Топ-N", C(0));

            string testStr = Corpora[corpus].Split(' ').Take(6).Aggregate((a, b) => a + " " + b);
            int[] encoded  = tok.Encode(testStr);
            string decoded = tok.DecodeObj(encoded);

            var sb = new StringBuilder();
            sb.AppendLine($"Словарь: {vocabSize} токенов");
            sb.AppendLine($"Текст корпуса: {Corpora[corpus].Split(' ').Length} слов");
            sb.AppendLine($"Токены (все): {allIds.Length}");
            sb.AppendLine();
            sb.AppendLine($"Тест кодирования: «{testStr}»");
            sb.AppendLine($"  Токены:  [{string.Join(", ", encoded)}]");
            sb.AppendLine($"  Декод:   «{decoded}»");
            sb.AppendLine();
            sb.AppendLine($"Топ-{topK} токенов:");
            for (int i = 0; i < Math.Min(topK, 10); i++)
            {
                var (id, cnt) = (sorted[i].Key, sorted[i].Value);
                string word = tok.DecodeObj(new[] { id });
                sb.AppendLine($"  #{i+1:D2} [{id,4}] «{word}»  {cnt} раз");
            }

            return sb.ToString();
        }

        private static string DoBPE(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int maxNG   = I(p, "maxNGram",  8);
            int corpusI = I(p, "corpusId",  0);

            var corpus = EnglishCorpora[corpusI];
            var words  = corpus.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var bpc = new BPECore { MaxNGrammSize = maxNG };
            var byteArrays = words.Select(BPECore.GetBytes).ToArray();
            bpc.TrainBPE(byteArrays);

            var xW = new Vector(words.Length);
            var yChars  = new Vector(words.Length);
            var yTokens = new Vector(words.Length);

            for (int i = 0; i < words.Length; i++)
            {
                int[] ids = bpc.Tokenize(words[i]);
                xW[i]      = i;
                yChars[i]  = words[i].Length;
                yTokens[i] = ids.Length;
            }

            cv.AddPlot(xW, yChars,  "Символов",       C(0), 2);
            cv.AddPlot(xW, yTokens, "BPE-токенов",    C(1), 2);

            double ratio = yTokens.Sum() / Math.Max(1, yChars.Sum());

            var sb = new StringBuilder();
            sb.AppendLine($"Корпус: {words.Length} слов, MaxNGram={maxNG}");
            sb.AppendLine();
            sb.AppendLine($"Символов итого:    {(int)yChars.Sum()}");
            sb.AppendLine($"BPE-токенов итого: {(int)yTokens.Sum()}");
            sb.AppendLine($"Коэффициент сжатия: {ratio:P1}");
            sb.AppendLine();
            sb.AppendLine("Детализация (первые 10 слов):");
            for (int i = 0; i < Math.Min(10, words.Length); i++)
            {
                int[] ids = bpc.Tokenize(words[i]);
                sb.AppendLine($"  «{words[i]}» -> {words[i].Length} симв. -> {ids.Length} BPE-токенов [{string.Join(",", ids.Take(5))}{(ids.Length > 5 ? "..." : "")}]");
            }

            return sb.ToString();
        }

        private static string DoSentTokenizer(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int textId = I(p, "textId", 0);

            var texts = new[]
            {
                "Искусственный интеллект развивается стремительно. В 2024 году появились новые модели. " +
                "Например, GPT-4 и Gemini. Они показывают впечатляющие результаты. Однако вопрос безопасности остаётся открытым. " +
                "По данным MIT, риски ИИ необходимо регулировать. Учёные работают над решением.",

                "Уважаемый Иван Иванович! Сообщаем Вам, что заседание состоится 15.01.2025 в 10:00. " +
                "Повестка дня включает вопросы бюджета на 2025 г. и утверждения плана работ. " +
                "Просим подтвердить участие до 12.01.2025. С уважением, Администрация.",

                "Биржевые индексы упали на 3.5% в среду. Нефть марки Brent торгуется по 85.2 долл. за баррель. " +
                "ФРС США оставила ставку без изменений. Эксперты ожидают снижения в I кв. 2025 г. " +
                "Рубль укрепился до 89.5 руб. за доллар. Аналитики Goldman Sachs прогнозируют рост.",
            };

            var st = new SentencesTokenizer();
            var sentences = st.Tokenize(texts[textId]);

            var xI    = new Vector(sentences.Count);
            var yLen  = new Vector(sentences.Count);
            for (int i = 0; i < sentences.Count; i++) { xI[i] = i + 1; yLen[i] = sentences[i].Split(' ').Length; }

            cv.AddPlot(xI, yLen, "Слов в предложении", C(0), 2);
            cv.AddScatter(xI, yLen, "Предложения", C(0));

            var sb = new StringBuilder();
            sb.AppendLine($"Текст -> {sentences.Count} предложений:");
            sb.AppendLine();
            for (int i = 0; i < sentences.Count; i++)
                sb.AppendLine($"  {i+1}. [{sentences[i].Split(' ').Length} слов] {sentences[i].Trim()}");
            sb.AppendLine();
            sb.AppendLine($"Средняя длина предложения: {yLen.Average():F1} слов");
            sb.AppendLine($"Мин/Макс: {yLen.Min():F0} / {yLen.Max():F0} слов");

            return sb.ToString();
        }
    }
}
