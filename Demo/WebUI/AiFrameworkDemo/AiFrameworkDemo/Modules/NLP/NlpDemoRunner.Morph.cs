using AI.NLP;
using AI.NLP.Lemmatization;
using AI.NLP.Stemmers;
using AI.DataStructs.Algebraic;
using AI.Charts;
using AiFrameworkDemo.Core;
using System.Diagnostics;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.NLP
{
    public static partial class NlpDemoRunner
    {
        // -- 5. Стемминг ------------------------------------------------

        private static string DoStemming(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int wordSet = I(p, "wordSet", 0);
            var words   = WordSets[wordSet];
            var stemmed = StemmerRus.TransformingWordsArray(words);
            int n = words.Length;

            cv.ChartName = "Стемминг";

            var sb = new StringBuilder();
            sb.AppendLine($"StemmerRus — набор «{new[] { "Существительные", "Глаголы", "Прилагательные" }[wordSet]}»");
            sb.AppendLine();
            sb.AppendLine($"  {"Слово",-22} {"Стемм",-18} {"Сжатие":>7}");
            sb.AppendLine("  " + new string('-', 50));
            for (int i = 0; i < n; i++)
            {
                double ratio = 1.0 - (double)stemmed[i].Length / words[i].Length;
                sb.AppendLine($"  {words[i],-22} {stemmed[i],-18} {ratio:P0}");
            }
            sb.AppendLine();
            sb.AppendLine($"Среднее сокращение: {Enumerable.Range(0, n).Average(i => 1.0 - (double)stemmed[i].Length / words[i].Length):P1}");
            return sb.ToString();
        }

        // -- 6. Лемматизация --------------------------------------------

        private static string DoLemmatize(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int mode    = I(p, "mode",    0);
            int wordSet = I(p, "wordSet", 0);
            var words   = WordSets[wordSet];

            var lemmatizer = Lemmatizer.CreateRussian(withCache: false);
            var cached     = new CachingLemmatizer(lemmatizer, maxSize: 1000);
            string[] lemmas  = words.Select(w => lemmatizer.Lemmatize(w)).ToArray();
            string[] stemmed = StemmerRus.TransformingWordsArray(words);

            int n = words.Length;
            var xI = new Vector(n);
            for (int i = 0; i < n; i++) xI[i] = i + 1;

            if (mode == 2) return DoCachingBenchmark(words, lemmatizer, cached, lemmas, xI, cv, n);

            cv.ChartName = "Лемматизация";

            var sb = new StringBuilder();
            sb.AppendLine($"RussianLemmatizer — набор «{new[] { "Существительные", "Глаголы", "Смешанный" }[wordSet]}»");
            sb.AppendLine($"Режим: {new[] { "Лемматизация", "Стемм vs Лемма", "Тест кеша" }[mode]}");
            sb.AppendLine();

            if (mode == 1)
            {
                sb.AppendLine($"  {"Слово",-22} {"Лемма",-18} {"Стемм",-18}");
                sb.AppendLine("  " + new string('-', 60));
                for (int i = 0; i < n; i++)
                    sb.AppendLine($"  {words[i],-22} {lemmas[i],-18} {stemmed[i],-18}");
            }
            else
            {
                sb.AppendLine($"  {"Слово",-22} {"Лемма",-18}");
                sb.AppendLine("  " + new string('-', 42));
                for (int i = 0; i < n; i++)
                    sb.AppendLine($"  {words[i],-22} {lemmas[i],-18}");
                sb.AppendLine();
                string sample = string.Join(" ", words.Take(5));
                sb.AppendLine($"LemmatizeSentence:");
                sb.AppendLine($"  Вход:  {sample}");
                sb.AppendLine($"  Выход: {lemmatizer.LemmatizeSentence(sample)}");
            }
            return sb.ToString();
        }

        private static string DoCachingBenchmark(string[] words, ILemmatizer lemmatizer, CachingLemmatizer cached,
                                                  string[] lemmas, Vector xI, ChartView cv, int n)
        {
            var sw = Stopwatch.StartNew();
            for (int r = 0; r < 500; r++) foreach (var w in words) lemmatizer.Lemmatize(w);
            sw.Stop(); long rawMs = sw.ElapsedMilliseconds;

            sw.Restart();
            for (int r = 0; r < 500; r++) foreach (var w in words) cached.Lemmatize(w);
            sw.Stop(); long cacheMs = sw.ElapsedMilliseconds;

            cv.ChartName = "Тест кеша";

            var sb = new StringBuilder();
            sb.AppendLine($"CachingLemmatizer vs прямой (500 × {words.Length} слов):");
            sb.AppendLine($"  Без кеша: {rawMs} мс");
            sb.AppendLine($"  С кешом:  {cacheMs} мс");
            sb.AppendLine($"  Ускорение: ×{(rawMs > 0 ? (double)rawMs / cacheMs : 1):F1}");
            sb.AppendLine($"  Размер кеша: {cached.CacheSize} записей");
            sb.AppendLine();
            sb.AppendLine("Леммы:");
            for (int i = 0; i < n; i++)
                sb.AppendLine($"  «{words[i]}» -> «{lemmas[i]}»");
            return sb.ToString();
        }
    }
}
