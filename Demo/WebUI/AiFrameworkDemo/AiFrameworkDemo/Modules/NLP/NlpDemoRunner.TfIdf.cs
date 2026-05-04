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

        private static string DoTFIDF(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int queryId  = I(p, "queryId",  0);
            int topWords = I(p, "topWords", 5);
            var docSet   = TfidfDocs[0];
            int nDocs    = docSet.Length;

            var tfidf  = new TFIDF(docSet);
            string query  = QueryList[queryId];
            int    bestDoc = tfidf.Search(query);

            var queryWords = query.Split(' ');
            var yScore = new Vector(nDocs);
            for (int d = 0; d < nDocs; d++)
            {
                yScore[d] = queryWords.Sum(w => tfidf.TF_IDF(w, d));
            }

            cv.ChartName = "TF-IDF";

            var sb = new StringBuilder();
            sb.AppendLine($"Корпус: {nDocs} документов");
            sb.AppendLine($"Запрос: «{query}»");
            sb.AppendLine($"Лучший документ: #{bestDoc + 1} «{TopicNames[bestDoc]}»");
            sb.AppendLine();

            for (int d = 0; d < nDocs; d++)
            {
                sb.AppendLine($"Документ {d+1} «{TopicNames[d]}»: score={yScore[d]:F4}");
                var ranked = docSet[d].Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct()
                    .Select(w => (w, score: tfidf.TF_IDF(w, d)))
                    .OrderByDescending(x => x.score).Take(topWords);
                foreach (var (w, sc) in ranked)
                    sb.AppendLine($"  «{w}»  TF-IDF={sc:F4}  TF={tfidf.TFWord(w, d):F4}  IDF={tfidf.IDFWord(w):F4}");
            }
            return sb.ToString();
        }

        // -- 4. BM25 ---------------------------------------------------

        private static string DoBM25(IReadOnlyDictionary<string, double> p, ChartView cv)
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
            var queryWords = query.Split(' ');

            for (int d = 0; d < nDocs; d++)
            {
                yBm25[d]  = bm25.Score(query, d);
                yTfidf[d] = queryWords.Sum(w => tfidf.TF_IDF(w, d));
            }

            cv.ChartName = "BM25";

            var sb = new StringBuilder();
            sb.AppendLine($"BM25 — параметры: k₁={k1:F1}, b={b:F1}");
            sb.AppendLine($"Корпус: {nDocs} документов");
            sb.AppendLine($"Запрос: «{query}»");
            sb.AppendLine($"Лучший (BM25):   #{bestBm25  + 1} «{TopicNames[bestBm25]}»");
            sb.AppendLine($"Лучший (TF-IDF): #{bestTfidf + 1} «{TopicNames[bestTfidf]}»");
            sb.AppendLine();

            for (int d = 0; d < nDocs; d++)
            {
                sb.AppendLine($"Документ {d+1} «{TopicNames[d]}»");
                sb.AppendLine($"  BM25={yBm25[d]:F4}  TF-IDF={yTfidf[d]:F4}");
                var ranked = docSet[d].Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct()
                    .Select(w => (w, bm25Score: bm25.Score(w, d), tf: bm25.TFWord(w, d), idf: bm25.IDFWord(w)))
                    .OrderByDescending(x => x.bm25Score).Take(topWords);
                foreach (var (w, sc, tf, idf) in ranked)
                    sb.AppendLine($"  «{w}»  BM25={sc:F4}  TF={tf}  IDF={idf:F4}");
            }
            return sb.ToString();
        }
    }
}
