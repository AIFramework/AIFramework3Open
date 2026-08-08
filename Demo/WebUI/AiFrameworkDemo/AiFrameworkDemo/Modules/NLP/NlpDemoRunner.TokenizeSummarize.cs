using AI.NLP;
using AI.DataStructs.Algebraic;
using AI.Charts;
using AiFrameworkDemo.Core;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.NLP
{
    public static partial class NlpDemoRunner
    {
        // -- 4. Токенизация ---------------------------------------------

        private static string DoTextTokenizer(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
        {
            int  vocabSize = I(p, "vocabSize", 30);
            bool isStem    = I(p, "isStem",     0) != 0;
            int  textId    = I(p, "textId",      0);
            string text    = Texts[textId];

            var tok = new TextTokenizer(isLower: true, isStem: isStem) { Count = vocabSize };
            tok.Train(text);

            int      dim   = tok.GetDimWithUnKnowWord();
            string[] vocab = tok.Words ?? [];

            string sentence = text.Split('.')[0] + ".";
            var seqVec = tok.GetSeq2Tokens(sentence);
            int seqLen = seqVec.Count;

            Vector? oneHot  = null;
            string firstWord = "";
            if (vocab.Length > 0) { firstWord = vocab[0]; oneHot = tok.GetWord2OneHot(firstWord); }

            // -- График: id токена по позициям предложения -----------------
            // GetSeq2Tokens отдаёт вектор фиксированной длины Count, заранее
            // заполненный −1: за пределами реальных слов это паддинг, а внутри
            // них −1 означает слово вне словаря (OOV). Рисуем только реальные
            // позиции, иначе весь хвост паддинга выглядел бы как OOV.
            var sentWords = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int realLen   = Math.Min(sentWords.Length, seqLen);
            var oovX = new List<double>();
            var oovY = new List<double>();
            for (int i = 0; i < realLen; i++)
                if (seqVec[i] < 0) { oovX.Add(i + 1); oovY.Add(0); }

            cv.ChartName = $"TextTokenizer: словарь {vocab.Length} слов, OOV {oovX.Count} из {realLen}";
            Axes(cv, "позиция слова в предложении", "id токена (−1 = вне словаря)");
            if (realLen > 0)
                cv.AddBar(Idx(realLen), Vec(Enumerable.Range(0, realLen).Select(i => (double)seqVec[i])),
                          "id токена", C(1));
            if (oovX.Count > 0)
                cv.AddScatterMark6(Vec(oovX), Vec(oovY), "вне словаря (OOV)", C(4));

            var sb = new StringBuilder();
            sb.AppendLine($"TextTokenizer: vocabSize={vocabSize}, isStem={isStem}");
            sb.AppendLine($"Реальных слов: {realLen}, паддинга до длины {seqLen}: {seqLen - realLen}, OOV: {oovX.Count}");
            if (realLen > 0)
                sb.AppendLine(AxisLegend(sentWords.Take(realLen), "Слова на оси X"));
            sb.AppendLine($"Размерность с OOV: {dim}");
            sb.AppendLine($"Реальный словарь: {vocab.Length} слов");
            sb.AppendLine();
            sb.AppendLine($"Предложение: «{sentence}»");
            sb.AppendLine($"Токены ({seqLen}): [{string.Join(", ", Enumerable.Range(0, seqLen).Select(i => (int)seqVec[i]))}]");
            sb.AppendLine();

            double coverage = realLen > 0 ? 1.0 - (double)oovX.Count / realLen : 0;
            rep.Metric("Размер словаря", vocab.Length, "слов", hint: "Реально обученных токенов")
               .Metric("Размерность с OOV", dim, hint: "Словарь + позиция для неизвестного слова")
               .Metric("Покрытие предложения", coverage,
                       hint: "Доля слов, найденных в словаре",
                       tone: coverage > 0.8 ? MetricTone.Good : coverage > 0.5 ? MetricTone.Warn : MetricTone.Bad,
                       format: "P0")
               .Metric("Вне словаря (OOV)", oovX.Count, "шт.",
                       tone: oovX.Count == 0 ? MetricTone.Good : MetricTone.Warn)
               .Metric("Стемминг", isStem ? "включён" : "выключен")
               .Note($"Вектор последовательности имеет фиксированную длину {seqLen} и заранее заполнен −1: " +
                     $"за пределами {realLen} реальных слов это паддинг, а внутри — слово вне словаря.");

            if (realLen > 0)
            {
                var seqTable = rep.Table("Токенизация предложения",
                    ["Позиция", "Слово", "id токена", "Статус"],
                    numeric: [true, false, true, false]);

                for (int i = 0; i < realLen; i++)
                {
                    int id = (int)seqVec[i];
                    seqTable.Row((i + 1).ToString(), sentWords[i], id.ToString(),
                                 id < 0 ? "вне словаря" : "в словаре");
                }
            }

            if (vocab.Length > 0)
            {
                var vocabTable = rep.Table("Словарь (первые 15 слов)",
                    ["#", "Слово", "id"], numeric: [true, false, true]);

                sb.AppendLine($"Словарь (первые 15 слов):");
                for (int i = 0; i < System.Math.Min(15, vocab.Length); i++)
                {
                    sb.AppendLine($"  [{i,3}] «{vocab[i]}»  id={tok.GetWord2Token(vocab[i])}");
                    vocabTable.Row((i + 1).ToString(), vocab[i], tok.GetWord2Token(vocab[i]).ToString());
                }
                sb.AppendLine();
                sb.AppendLine($"One-hot для «{firstWord}»: размерность {oneHot?.Count}, ненулевых: {oneHot?.Count(v => v > 0.5)}");
            }
            return sb.ToString();
        }

        // -- 7. Суммаризация --------------------------------------------

        private static string DoSummarize(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
        {
            int numSents = I(p, "numSents", 2);
            int textId   = I(p, "textId",   0);
            string text  = Texts[textId];

            var ts      = new TextSummarization();
            string summary   = ts.Summarization(text, num: numSents);
            string[] allSents = TextSummarization.GetSeqs(text);
            int n = allSents.Length;

            // -- График: длина предложений и какие из них попали в резюме --
            var lens     = new List<double>(n);
            var pickedX  = new List<double>();
            var pickedY  = new List<double>();
            for (int i = 0; i < n; i++)
            {
                string s = allSents[i].Trim();
                double len = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                lens.Add(len);
                if (s.Length > 0 && summary.Contains(s, StringComparison.OrdinalIgnoreCase))
                {
                    pickedX.Add(i + 1);
                    pickedY.Add(len);
                }
            }

            cv.ChartName = $"TextSummarization: {n} предложений → {pickedX.Count} в резюме";
            Axes(cv, "номер предложения в тексте", "длина, слов");
            cv.AddBar(Idx(n), Vec(lens), "предложение", C(0));
            if (pickedX.Count > 0)
                cv.AddScatterMark6(Vec(pickedX), Vec(pickedY), "вошло в резюме", C(2));

            int totalWords = (int)lens.Sum();
            int keptWords  = (int)pickedY.Sum();

            rep.Metric("Предложений в тексте", n)
               .Metric("Вошло в резюме", pickedX.Count, "шт.", tone: MetricTone.Good,
                       hint: "Определено сопоставлением с текстом резюме")
               .Metric("Сжатие по словам", totalWords > 0 ? (double)keptWords / totalWords : 0,
                       hint: $"{keptWords} слов из {totalWords}", format: "P0")
               .Metric("Слов в резюме", keptWords, "шт.")
               .Note("Точки на графике отмечают предложения, попавшие в резюме. " +
                     "Экстрактивная суммаризация не переписывает текст — она выбирает исходные предложения целиком.");

            var t = rep.Table("Предложения исходного текста",
                ["#", "Слов", "В резюме", "Предложение"],
                numeric: [true, true, false, false]);

            var sb = new StringBuilder();
            sb.AppendLine($"TextSummarization: {n} предложений -> {numSents} в резюме");
            sb.AppendLine();
            sb.AppendLine("=== Исходный текст ===");
            for (int i = 0; i < n; i++)
            {
                sb.AppendLine($"  {i+1}. {allSents[i].Trim()}");
                bool picked = pickedX.Contains(i + 1);
                t.Row((i + 1).ToString(), ((int)lens[i]).ToString(), picked ? "✓" : "", allSents[i].Trim());
            }
            sb.AppendLine();
            sb.AppendLine("=== Резюме ===");
            sb.AppendLine(summary);
            sb.AppendLine();
            sb.AppendLine($"Сжатие: {n} -> ~{numSents} предложений ({(double)numSents / n:P0} от оригинала)");
            return sb.ToString();
        }
    }
}
