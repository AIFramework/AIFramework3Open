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
        // -- 2. Вероятностный словарь -----------------------------------

        private static string DoProbDict(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int  topN   = I(p, "topN",    15);
            bool stop   = I(p, "isStop",   1) != 0;
            bool stem   = I(p, "isStem",   1) != 0;
            int  textId = I(p, "textId",   0);
            string text = Texts[textId];

            var pd     = new ProbabilityDictionary(isStopDel: stop, isDigitDel: true, isStem: stem);
            var result = pd.Run(text);

            var pdh        = new ProbabilityDictionaryHash(isStem: stem);
            var hashResult = pdh.Run(text);

            int show = System.Math.Min(topN, result.Length);

            cv.ChartName = "Вероятностный словарь";

            var sb = new StringBuilder();
            sb.AppendLine($"ProbabilityDictionary [стоп={stop}, стемм={stem}]");
            sb.AppendLine($"Слов в словаре: {result.Length}");
            sb.AppendLine();
            sb.AppendLine($"Топ-{show} слов:");
            for (int i = 0; i < show; i++)
                sb.AppendLine($"  #{i+1,2} «{result[i].Word,-18}»  P={result[i].Probability:F4}");

            sb.AppendLine();
            sb.AppendLine($"ProbabilityDictionaryHash [стемм={stem}]");
            sb.AppendLine($"Уникальных ключей: {hashResult.Count}");
            foreach (var kv in hashResult.OrderByDescending(kv => kv.Value).Take(5))
                sb.AppendLine($"  «{kv.Key,-18}»  P={kv.Value:F4}");

            sb.AppendLine();
            sb.AppendLine("Стоп-слова (первые 10):");
            sb.AppendLine("  " + string.Join(", ", ProbabilityDictionary.StopWords.Take(10)));
            return sb.ToString();
        }
    }
}
