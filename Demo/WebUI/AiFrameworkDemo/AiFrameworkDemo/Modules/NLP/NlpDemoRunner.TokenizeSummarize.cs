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

        private static string DoTextTokenizer(IReadOnlyDictionary<string, double> p, ChartView cv)
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

            cv.ChartName = "TextTokenizer";

            Vector? oneHot  = null;
            string firstWord = "";
            if (vocab.Length > 0) { firstWord = vocab[0]; oneHot = tok.GetWord2OneHot(firstWord); }

            var sb = new StringBuilder();
            sb.AppendLine($"TextTokenizer: vocabSize={vocabSize}, isStem={isStem}");
            sb.AppendLine($"Размерность с OOV: {dim}");
            sb.AppendLine($"Реальный словарь: {vocab.Length} слов");
            sb.AppendLine();
            sb.AppendLine($"Предложение: «{sentence}»");
            sb.AppendLine($"Токены ({seqLen}): [{string.Join(", ", Enumerable.Range(0, seqLen).Select(i => (int)seqVec[i]))}]");
            sb.AppendLine();

            if (vocab.Length > 0)
            {
                sb.AppendLine($"Словарь (первые 15 слов):");
                for (int i = 0; i < System.Math.Min(15, vocab.Length); i++)
                    sb.AppendLine($"  [{i,3}] «{vocab[i]}»  id={tok.GetWord2Token(vocab[i])}");
                sb.AppendLine();
                sb.AppendLine($"One-hot для «{firstWord}»: размерность {oneHot?.Count}, ненулевых: {oneHot?.Count(v => v > 0.5)}");
            }
            return sb.ToString();
        }

        // -- 7. Суммаризация --------------------------------------------

        private static string DoSummarize(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int numSents = I(p, "numSents", 2);
            int textId   = I(p, "textId",   0);
            string text  = Texts[textId];

            var ts      = new TextSummarization();
            string summary   = ts.Summarization(text, num: numSents);
            string[] allSents = TextSummarization.GetSeqs(text);
            int n = allSents.Length;

            cv.ChartName = "Суммаризация текста";

            var sb = new StringBuilder();
            sb.AppendLine($"TextSummarization: {n} предложений -> {numSents} в резюме");
            sb.AppendLine();
            sb.AppendLine("=== Исходный текст ===");
            for (int i = 0; i < n; i++)
                sb.AppendLine($"  {i+1}. {allSents[i].Trim()}");
            sb.AppendLine();
            sb.AppendLine("=== Резюме ===");
            sb.AppendLine(summary);
            sb.AppendLine();
            sb.AppendLine($"Сжатие: {n} -> ~{numSents} предложений ({(double)numSents / n:P0} от оригинала)");
            return sb.ToString();
        }
    }
}
