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

        private static string DoProbDict(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
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

            // -- График: частотный профиль (закон Ципфа) -------------------
            cv.ChartName = $"Частотный профиль: топ-{show} из {result.Length} слов";
            Axes(cv, "ранг слова", "P(слово)");
            if (show > 0)
            {
                cv.AddBar(Idx(show), Vec(result.Take(show).Select(r => r.Probability)),
                          "P(слово)", C(0));

                // Идеальный Ципф P(k) = P(1)/k — эталон для сравнения формы
                double p1 = result[0].Probability;
                cv.AddPlot(Idx(show), Vec(Enumerable.Range(1, show).Select(k => p1 / k)),
                           "закон Ципфа: P₁/k", C(4), 2);
            }

            // -- Метрики -----------------------------------------------------
            double topMass = result.Take(show).Sum(r => r.Probability);
            rep.Metric("Слов в словаре", result.Length, hint: "Уникальных токенов после фильтрации")
               .Metric("Самое частое", show > 0 ? result[0].Word : "—",
                       hint: "Слово с максимальной вероятностью", tone: MetricTone.Good)
               .Metric($"Масса топ-{show}", topMass, hint: "Суммарная вероятность показанных слов", format: "P1")
               .Metric("Стоп-слова", stop ? "удалены" : "оставлены")
               .Metric("Стемминг", stem ? "включён" : "выключен")
               .Note("Столбцы — эмпирические частоты, линия — идеальный закон Ципфа P₁/k. " +
                     "Совпадение формы означает, что текст ведёт себя как естественный язык.");

            var freqTable = rep.Table($"Топ-{show} слов по частоте",
                ["Ранг", "Слово", "P(слово)", "Ципф P₁/k", "Отклонение"],
                numeric: [true, false, true, true, true],
                note: "Отклонение = P(слово) − P₁/k: положительное значит, что слово встречается чаще, чем предсказывает Ципф.");

            var sb = new StringBuilder();
            sb.AppendLine($"ProbabilityDictionary [стоп={stop}, стемм={stem}]");
            sb.AppendLine($"Слов в словаре: {result.Length}");
            if (show > 0)
                sb.AppendLine(AxisLegend(result.Take(show).Select(r => r.Word), "Ранги на оси X"));
            sb.AppendLine();
            sb.AppendLine($"Топ-{show} слов:");
            for (int i = 0; i < show; i++)
            {
                sb.AppendLine($"  #{i+1,2} «{result[i].Word,-18}»  P={result[i].Probability:F4}");
                double zipf = result[0].Probability / (i + 1);
                freqTable.Row((i + 1).ToString(), result[i].Word,
                              F(result[i].Probability), F(zipf), F(result[i].Probability - zipf));
            }

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
