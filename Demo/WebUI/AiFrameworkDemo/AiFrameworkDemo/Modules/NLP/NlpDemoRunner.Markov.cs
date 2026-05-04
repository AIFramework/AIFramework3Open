using AI.Charts;
using AI.DataPrepaire.NLPUtils.TextGeneration;
using AI.DataStructs.Algebraic;
using AiFrameworkDemo.Core;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.NLP
{
    public static partial class NlpDemoRunner
    {
        private static string DoMarkovGen(
            IReadOnlyDictionary<string, double> p,
            IReadOnlyDictionary<string, string> tp,
            ChartView cv)
        {
            int ngram     = Math.Clamp(I(p, "ngram", 3), 2, 5);
            int genLength = Math.Clamp(I(p, "genLength", 60), 5, 300);
            int textId    = Math.Clamp(I(p, "textId", 0), 0, Texts.Length - 1);

            string corpus = T(tp, "_corpus");
            if (string.IsNullOrWhiteSpace(corpus))
                corpus = Texts[textId];

            string seed = T(tp, "_seed", "нейронные сети");

            var hmm = new HMMFast { NGram = ngram };
            hmm.Train(corpus, addStart: true);

            var seedWords = seed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int padCount = Math.Max(0, ngram - seedWords.Length - 1);
            var inp = new List<string>();
            for (int i = 0; i < padCount; i++) inp.Add("<s>");
            inp.AddRange(seedWords);

            string generated = hmm.Generate(genLength, inp.ToArray(), new Random(42));

            var probVec = hmm.ProbabilityVector;
            int topN = Math.Min(20, probVec?.Count ?? 0);

            if (topN > 0 && probVec != null)
            {
                var indices = Enumerable.Range(0, probVec.Count)
                    .OrderByDescending(i => probVec[i])
                    .Take(topN).ToArray();

                cv.ChartName = $"Марковская цепь ({ngram}-грамм) — топ-{topN} переходов";
            }
            else
            {
                cv.ChartName = $"Марковская цепь ({ngram}-грамм)";
            }

            var sb = new StringBuilder();
            sb.AppendLine("> Генерация текста — Марковские цепи");
            sb.AppendLine();
            sb.AppendLine($"  n-грамма:        {ngram}");
            sb.AppendLine($"  Макс. слов:      {genLength}");
            sb.AppendLine($"  Seed:            «{seed}»");
            sb.AppendLine($"  Корпус:          {corpus.Length} символов");
            sb.AppendLine();
            sb.AppendLine("- Сгенерированный текст");
            sb.AppendLine();
            string fullText = string.IsNullOrWhiteSpace(generated)
                ? "(пустая генерация — попробуйте другой seed или увеличьте корпус)"
                : seed + " " + generated;
            sb.AppendLine(fullText);
            sb.AppendLine();

            if (topN > 0 && probVec != null)
            {
                sb.AppendLine($"- Статистика модели");
                sb.AppendLine($"  Размер вектора вероятностей: {probVec.Count}");
            }

            return sb.ToString();
        }
    }
}
