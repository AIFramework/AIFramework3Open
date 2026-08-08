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
        private static readonly string[] QueryList =
        [
            "нейронная сеть", "экономика рынок", "спорт чемпионат", "наука открытие", "погода климат"
        ];

        private static readonly string[] TopicNames =
        [
            "Нейросети", "Экономика", "Спорт", "Наука", "Погода"
        ];

        // -- 3. TF-IDF -------------------------------------------------

        private static string DoTFIDF(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
        {
            int queryId  = I(p, "queryId",  0);
            int topWords = I(p, "topWords", 5);
            var docSet   = TfidfDocs[0];
            int nDocs    = docSet.Length;

            var tfidf  = new TFIDF(docSet);
            string query  = QueryList[queryId];
            int    bestDoc = tfidf.Search(query);

            // TFIDF индексирует корпус по СТЕММИРОВАННЫМ формам
            // (ProbabilityDictionaryHash внутри конструктора), поэтому запрос и
            // слова документов надо приводить тем же преобразованием — иначе
            // TFWord всегда вернёт 0 и все скоры схлопнутся в ноль.
            var queryTerms = ProbabilityDictionary.GetWords(query, IsStem: true);
            var yScore = new Vector(nDocs);
            for (int d = 0; d < nDocs; d++)
            {
                yScore[d] = queryTerms.Sum(w => tfidf.TF_IDF(w, d));
            }

            // -- График: релевантность каждого документа запросу -----------
            cv.ChartName = $"TF-IDF: релевантность запроса «{query}»";
            Axes(cv, "документ корпуса", "Σ TF·IDF по словам запроса");
            cv.AddBar(Idx(nDocs), yScore, "релевантность", C(1));

            // Победитель — отдельной серией поверх столбцов
            var xBest = new Vector(1); xBest[0] = bestDoc + 1;
            var yBest = new Vector(1); yBest[0] = yScore[bestDoc];
            cv.AddScatterMark6(xBest, yBest, $"лучший: {TopicNames[bestDoc]}", C(2));

            // -- Метрики: главное, что нужно увидеть сразу -----------------
            int matched = queryTerms.Count(t => Enumerable.Range(0, nDocs).Any(d => tfidf.TFWord(t, d) > 0));
            rep.Metric("Лучший документ", TopicNames[bestDoc],
                       hint: "argmax по сумме TF·IDF слов запроса", tone: MetricTone.Good)
               .Metric("Скор победителя", yScore[bestDoc],
                       hint: "Сумма TF·IDF слов запроса в этом документе")
               .Metric("Терминов запроса найдено", $"{matched} из {queryTerms.Length}",
                       hint: "После стемминга запроса; 0 означает, что запрос вне словаря корпуса",
                       tone: matched == 0 ? MetricTone.Bad : MetricTone.Neutral)
               .Metric("Документов в корпусе", nDocs)
               .Note($"Запрос «{query}» приведён к стеммированным формам: {string.Join(", ", queryTerms)}. " +
                     "Индекс TFIDF строится по тем же формам — иначе TF был бы нулевым.");

            var scoreTable = rep.Table("Релевантность по документам",
                ["#", "Документ", "Скор запроса", "Топ-слово документа", "TF-IDF топ-слова"],
                numeric: [true, false, true, false, true]);

            var sb = new StringBuilder();
            sb.AppendLine($"Корпус: {nDocs} документов");
            sb.AppendLine($"Запрос: «{query}»");
            sb.AppendLine($"Лучший документ: #{bestDoc + 1} «{TopicNames[bestDoc]}»");
            sb.AppendLine(AxisLegend(TopicNames.Take(nDocs), "Документы на оси X"));
            sb.AppendLine();

            var wordsTable = rep.Table($"Топ-{topWords} слов каждого документа по TF-IDF",
                ["Документ", "Слово (стем)", "TF-IDF", "TF", "IDF"],
                numeric: [false, false, true, true, true],
                note: "TF — доля слова в документе, IDF — редкость слова в корпусе, TF-IDF — их произведение.");

            for (int d = 0; d < nDocs; d++)
            {
                sb.AppendLine($"Документ {d+1} «{TopicNames[d]}»: score={yScore[d]:F4}");
                var ranked = ProbabilityDictionary.GetWords(docSet[d], IsStem: true).Distinct()
                    .Select(w => (w, score: tfidf.TF_IDF(w, d)))
                    .OrderByDescending(x => x.score).Take(topWords).ToArray();

                foreach (var (w, sc) in ranked)
                {
                    sb.AppendLine($"  «{w}»  TF-IDF={sc:F4}  TF={tfidf.TFWord(w, d):F4}  IDF={tfidf.IDFWord(w):F4}");
                    wordsTable.Row(TopicNames[d], w, F(sc), F(tfidf.TFWord(w, d)), F(tfidf.IDFWord(w)));
                }

                var top = ranked.FirstOrDefault();
                scoreTable.Row((d + 1).ToString(), TopicNames[d], F(yScore[d]),
                               top.w ?? "—", top.w is null ? "—" : F(top.score));
            }
            return sb.ToString();
        }

        // -- 4. BM25 ---------------------------------------------------

        private static string DoBM25(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
        {
            int    queryId  = I(p, "queryId",  0);
            double k1       = I(p, "k1",      15) * 0.1;
            double b        = I(p, "b",         8) * 0.1;
            int    topWords = I(p, "topWords",  5);
            var    docSet   = TfidfDocs[0];
            int    nDocs    = docSet.Length;

            var bm25  = new BM25(docSet, k1, b);
            var tfidf = new TFIDF(docSet);
            string query   = QueryList[queryId];
            int bestBm25   = bm25.Search(query);
            int bestTfidf  = tfidf.Search(query);

            var yBm25      = new Vector(nDocs);
            var yTfidf     = new Vector(nDocs);
            // BM25.Score стеммирует запрос сам; для TFIDF это надо сделать явно.
            var queryTerms = ProbabilityDictionary.GetWords(query, IsStem: true);

            for (int d = 0; d < nDocs; d++)
            {
                yBm25[d]  = bm25.Score(query, d);
                yTfidf[d] = queryTerms.Sum(w => tfidf.TF_IDF(w, d));
            }

            // -- График: BM25 против TF-IDF на одном корпусе ---------------
            // Абсолютные шкалы у метрик разные, сравнивать имеет смысл только
            // ранжирование — поэтому обе нормируем на собственный максимум.
            double maxBm25  = Math.Max(1e-12, Enumerable.Range(0, nDocs).Max(d => yBm25[d]));
            double maxTfidf = Math.Max(1e-12, Enumerable.Range(0, nDocs).Max(d => yTfidf[d]));
            var nBm25  = new Vector(nDocs);
            var nTfidf = new Vector(nDocs);
            for (int d = 0; d < nDocs; d++)
            {
                nBm25[d]  = yBm25[d]  / maxBm25;
                nTfidf[d] = yTfidf[d] / maxTfidf;
            }

            cv.ChartName = $"BM25 vs TF-IDF: «{query}» (k₁={k1:F1}, b={b:F1})";
            Axes(cv, "документ корпуса", "релевантность, нормировка на максимум");
            cv.AddBar(Idx(nDocs), nBm25, "BM25", C(0));
            cv.AddPlot(Idx(nDocs), nTfidf, "TF-IDF", C(3), 3);

            bool agree = bestBm25 == bestTfidf;
            rep.Metric("Лучший (BM25)", TopicNames[bestBm25],
                       hint: "Ранжирование Okapi BM25", tone: MetricTone.Good)
               .Metric("Лучший (TF-IDF)", TopicNames[bestTfidf],
                       hint: "Ранжирование классическим TF-IDF")
               .Metric("Метрики согласны", agree ? "да" : "нет",
                       hint: "Совпадает ли документ-победитель у обеих метрик",
                       tone: agree ? MetricTone.Good : MetricTone.Warn)
               .Metric("k₁", k1, hint: "Насыщение частоты термина: выше — сильнее вклад повторов", format: "F1")
               .Metric("b", b, hint: "Нормализация длины: 0 — длина не учитывается, 1 — учитывается полностью", format: "F1")
               .Note("На графике обе метрики нормированы на собственный максимум: " +
                     "их абсолютные шкалы несравнимы, сравнивать имеет смысл только порядок документов.");

            var cmp = rep.Table("BM25 против TF-IDF",
                ["#", "Документ", "BM25", "BM25 норм.", "TF-IDF", "TF-IDF норм."],
                numeric: [true, false, true, true, true, true]);

            var sb = new StringBuilder();
            sb.AppendLine($"BM25 — параметры: k₁={k1:F1}, b={b:F1}");
            sb.AppendLine($"Корпус: {nDocs} документов");
            sb.AppendLine($"Запрос: «{query}»");
            sb.AppendLine($"Лучший (BM25):   #{bestBm25  + 1} «{TopicNames[bestBm25]}»");
            sb.AppendLine($"Лучший (TF-IDF): #{bestTfidf + 1} «{TopicNames[bestTfidf]}»");
            sb.AppendLine(AxisLegend(TopicNames.Take(nDocs), "Документы на оси X"));
            sb.AppendLine("На графике обе метрики нормированы на свой максимум — сравнивается ранжирование, а не абсолютные значения.");
            sb.AppendLine();

            var wordsTable = rep.Table($"Топ-{topWords} слов каждого документа по BM25",
                ["Документ", "Слово (стем)", "BM25", "TF (сырая частота)", "IDF"],
                numeric: [false, false, true, true, true],
                note: "В BM25 TF — сырое число вхождений, а не доля: насыщение задаётся параметром k₁.");

            for (int d = 0; d < nDocs; d++)
            {
                cmp.Row((d + 1).ToString(), TopicNames[d],
                        F(yBm25[d]), F(nBm25[d]), F(yTfidf[d]), F(nTfidf[d]));

                sb.AppendLine($"Документ {d+1} «{TopicNames[d]}»");
                sb.AppendLine($"  BM25={yBm25[d]:F4}  TF-IDF={yTfidf[d]:F4}");
                var ranked = ProbabilityDictionary.GetWords(docSet[d], IsStem: true).Distinct()
                    .Select(w => (w, bm25Score: bm25.Score(w, d), tf: bm25.TFWord(w, d), idf: bm25.IDFWord(w)))
                    .OrderByDescending(x => x.bm25Score).Take(topWords);
                foreach (var (w, sc, tf, idf) in ranked)
                {
                    sb.AppendLine($"  «{w}»  BM25={sc:F4}  TF={tf}  IDF={idf:F4}");
                    wordsTable.Row(TopicNames[d], w, F(sc), tf.ToString(), F(idf));
                }
            }
            return sb.ToString();
        }
    }
}
