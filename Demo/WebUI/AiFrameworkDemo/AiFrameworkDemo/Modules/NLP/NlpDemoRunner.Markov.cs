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
            ChartView cv,
            ReportBuilder rep)
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

            string generated = TryGenerate(hmm, seed, ngram, genLength);

            // Модель продолжает только ту n-грамму, которую видела при обучении,
            // и затравка обязана состоять ровно из n слов. Если условие не
            // выполнено, генерация пуста — берём первые слова самого корпуса,
            // чтобы демо показывало работу цепи, а не пустой экран.
            // О подмене сообщаем явно.
            bool seedFallback = false;
            string effectiveSeed = seed;
            if (string.IsNullOrWhiteSpace(generated))
            {
                var corpusWords = corpus.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
                                        .Take(ngram).ToArray();
                if (corpusWords.Length == ngram)
                {
                    string fallback = string.Join(" ", corpusWords);
                    string retry = TryGenerate(hmm, fallback, ngram, genLength);
                    if (!string.IsNullOrWhiteSpace(retry))
                    {
                        generated = retry;
                        effectiveSeed = fallback;
                        seedFallback = true;
                    }
                }
            }

            var probVec = hmm.ProbabilityVector;
            int topN = Math.Min(20, probVec?.Count ?? 0);

            double entropy = 0;
            int[] indices = [];
            if (topN > 0 && probVec != null)
            {
                indices = Enumerable.Range(0, probVec.Count)
                    .OrderByDescending(i => probVec[i])
                    .Take(topN).ToArray();

                // -- График: распределение вероятностей перехода ------------
                // Чем круче падает столбик, тем детерминированнее цепь.
                cv.ChartName = $"Марковская цепь ({ngram}-грамм) — топ-{topN} переходов";
                Axes(cv, "ранг перехода", "P(следующее слово)");
                cv.AddBar(Idx(topN), Vec(indices.Select(i => probVec[i])), "P перехода", C(0));

                // Равномерное распределение — верхняя граница неопределённости
                double uniform = 1.0 / Math.Max(1, probVec.Count);
                cv.AddPlot(Idx(topN), Vec(Enumerable.Repeat(uniform, topN)),
                           "равномерное 1/N", C(4), 2);

                for (int i = 0; i < probVec.Count; i++)
                {
                    double pi = probVec[i];
                    if (pi > 0) entropy -= pi * Math.Log2(pi);
                }
            }
            else
            {
                cv.ChartName = $"Марковская цепь ({ngram}-грамм)";
            }

            string generatedText = string.IsNullOrWhiteSpace(generated)
                ? "(пустая генерация — такой n-граммы в корпусе не было)"
                : effectiveSeed + " " + generated;
            int generatedWords = string.IsNullOrWhiteSpace(generated)
                ? 0 : generated.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            double maxEntropy = probVec is null ? 0 : Math.Log2(Math.Max(1, probVec.Count));
            rep.Metric("n-грамма", ngram, hint: "Длина контекста в словах")
               .Metric("Сгенерировано", generatedWords, "слов",
                       tone: generatedWords == 0 ? MetricTone.Bad : MetricTone.Good)
               .Metric("Состояний модели", probVec?.Count ?? 0,
                       hint: "Размер вектора вероятностей перехода")
               .Metric("Энтропия", entropy, "бит",
                       hint: "Неопределённость следующего слова: 0 — цепь детерминирована",
                       format: "F3")
               .Metric("Максимум энтропии", maxEntropy, "бит",
                       hint: "Достигается при равномерном распределении", format: "F3")
               .Note(seedFallback
                   ? $"Затравка «{seed}» не подошла: она должна состоять ровно из {ngram} слов, " +
                     $"встречавшихся в корпусе подряд. Показана генерация от начала корпуса: «{effectiveSeed}». " +
                     "Это и есть главное ограничение марковской модели — она знает только виденные n-граммы."
                   : "Чем больше n, тем связнее текст и тем ниже энтропия — но тем чаще генерация " +
                     "упирается в контекст, которого не было в корпусе, и обрывается.");

            rep.Table("Сгенерированный текст", ["Что", "Значение"], numeric: [false, false])
               .Row("Затравка (запрошена)", seed)
               .Row("Затравка (использована)", effectiveSeed + (seedFallback ? "  ← подменена" : ""))
               .Row("Результат", generatedText)
               .Row("Корпус", $"{corpus.Length} символов");

            if (topN > 0 && probVec != null)
            {
                var t = rep.Table($"Топ-{topN} вероятностей перехода",
                    ["Ранг", "Индекс состояния", "P(переход)", "Во сколько раз выше равномерного"],
                    numeric: [true, true, true, true],
                    note: "Равномерное распределение = 1/N, где N — число состояний. " +
                          "Отношение выше единицы означает, что модель что-то выучила.");

                double uniformP = 1.0 / Math.Max(1, probVec.Count);
                for (int r = 0; r < indices.Length; r++)
                {
                    double pr = probVec[indices[r]];
                    t.Row((r + 1).ToString(), indices[r].ToString(), F(pr), F(pr / uniformP));
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("> Генерация текста — Марковские цепи");
            sb.AppendLine();
            sb.AppendLine($"  n-грамма:        {ngram}");
            sb.AppendLine($"  Макс. слов:      {genLength}");
            sb.AppendLine($"  Seed:            «{seed}»");
            if (seedFallback)
                sb.AppendLine($"  Seed использован: «{effectiveSeed}» (запрошенный не встречался в корпусе)");
            sb.AppendLine($"  Корпус:          {corpus.Length} символов");
            sb.AppendLine();
            sb.AppendLine("- Сгенерированный текст");
            sb.AppendLine();
            sb.AppendLine(generatedText);
            sb.AppendLine();

            if (topN > 0 && probVec != null)
            {
                sb.AppendLine($"- Статистика модели");
                sb.AppendLine($"  Размер вектора вероятностей: {probVec.Count}");
                sb.AppendLine($"  Энтропия распределения:      {entropy:F3} бит");
                sb.AppendLine($"  Максимум (равномерное):      {Math.Log2(Math.Max(1, probVec.Count)):F3} бит");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Генерация с корректной затравкой.
        ///
        /// Два ограничения MCFast, которые приходится соблюдать здесь:
        ///   • Generate читает ровно NGram токенов (индексы 0..NGram-1) —
        ///     массив короче даёт выход за границы;
        ///   • условие остановки сверяется с началом списка, где лежит сама
        ///     затравка, поэтому служебный токен «&lt;s&gt;» в ней обрывает
        ///     генерацию на первом же слове. Дополнять им нельзя — затравка
        ///     должна состоять из NGram настоящих слов корпуса.
        /// </summary>
        /// <returns>Пустая строка, если затравка короче n-граммы или не встречалась в корпусе.</returns>
        private static string TryGenerate(HMMFast hmm, string seed, int ngram, int genLength)
        {
            var words = seed.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < ngram) return string.Empty;

            // Берём последние NGram слов: продолжение зависит только от них
            var inp = words[^ngram..];

            try
            {
                return hmm.Generate(genLength, inp, new Random(42));
            }
            catch (IndexOutOfRangeException)
            {
                return string.Empty;
            }
        }
    }
}
