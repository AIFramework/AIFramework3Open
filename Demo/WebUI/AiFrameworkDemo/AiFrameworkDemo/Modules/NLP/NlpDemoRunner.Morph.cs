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

        private static string DoStemming(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
        {
            int wordSet = I(p, "wordSet", 0);
            var words   = WordSets[wordSet];
            var stemmed = StemmerRus.TransformingWordsArray(words);
            int n = words.Length;

            // -- График: длина слова до и после отсечения окончания --------
            cv.ChartName = "StemmerRus: длина слова до и после стемминга";
            Axes(cv, "слово из набора", "символов");
            cv.AddBar(Idx(n), Vec(words.Select(w => (double)w.Length)), "исходное слово", C(0));
            cv.AddPlot(Idx(n), Vec(stemmed.Select(w => (double)w.Length)), "основа (стем)", C(2), 3);

            double avgRatio = Enumerable.Range(0, n).Average(i => 1.0 - (double)stemmed[i].Length / words[i].Length);
            int unchanged   = Enumerable.Range(0, n).Count(i => stemmed[i].Length == words[i].Length);

            rep.Metric("Слов в наборе", n)
               .Metric("Среднее сокращение", avgRatio, hint: "Средняя доля отсечённых символов", format: "P1")
               .Metric("Без изменений", unchanged, "шт.",
                       hint: "Слова, у которых стеммер ничего не отсёк",
                       tone: unchanged > 0 ? MetricTone.Warn : MetricTone.Good)
               .Note("Столбцы — длина исходной словоформы, линия — длина основы. " +
                     "Разрыв между ними и есть то, что отсекает правиловый стеммер.");

            var t = rep.Table("Словоформа → основа",
                ["Слово", "Основа (стем)", "Было", "Стало", "Сжатие"],
                numeric: [false, false, true, true, true]);

            var sb = new StringBuilder();
            sb.AppendLine($"StemmerRus — набор «{new[] { "Существительные", "Глаголы", "Прилагательные" }[wordSet]}»");
            sb.AppendLine(AxisLegend(words, "Слова на оси X"));
            sb.AppendLine();
            sb.AppendLine($"  {"Слово",-22} {"Стемм",-18} {"Сжатие":>7}");
            sb.AppendLine("  " + new string('-', 50));
            for (int i = 0; i < n; i++)
            {
                double ratio = 1.0 - (double)stemmed[i].Length / words[i].Length;
                sb.AppendLine($"  {words[i],-22} {stemmed[i],-18} {ratio:P0}");
                t.Row(words[i], stemmed[i], words[i].Length.ToString(), stemmed[i].Length.ToString(), $"{ratio:P0}");
            }
            sb.AppendLine();
            sb.AppendLine($"Среднее сокращение: {avgRatio:P1}");
            return sb.ToString();
        }

        // -- 6. Лемматизация --------------------------------------------

        private static string DoLemmatize(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
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

            if (mode == 2) return DoCachingBenchmark(words, lemmatizer, cached, lemmas, xI, cv, rep, n);

            // -- График: словоформа → лемма (и стем для режима сравнения) --
            cv.ChartName = mode == 1
                ? "Лемматизация vs стемминг: длина результата"
                : "Лемматизация: длина словоформы и леммы";
            Axes(cv, "слово из набора", "символов");
            cv.AddBar(xI, Vec(words.Select(w => (double)w.Length)), "словоформа", C(0));
            cv.AddPlot(xI, Vec(lemmas.Select(w => (double)w.Length)), "лемма", C(2), 3);
            if (mode == 1)
                cv.AddPlot(xI, Vec(stemmed.Select(w => (double)w.Length)), "стем", C(4), 3);

            int differ = Enumerable.Range(0, n).Count(i => !string.Equals(lemmas[i], stemmed[i], StringComparison.Ordinal));
            rep.Metric("Слов в наборе", n)
               .Metric("Лемма ≠ стем", differ, "шт.",
                       hint: "Где нормальная форма отличается от механически отсечённой основы",
                       tone: MetricTone.Neutral)
               .Metric("Средняя длина леммы", lemmas.Average(l => (double)l.Length), "симв.", format: "F1")
               .Metric("Средняя длина стема", stemmed.Average(l => (double)l.Length), "симв.", format: "F1")
               .Note("Лемма — словарная форма, стем — результат отсечения по правилам. " +
                     "Стем может не быть настоящим словом, лемма обязана им быть.");

            var t = rep.Table(mode == 1 ? "Словоформа, лемма и стем" : "Словоформа → лемма",
                mode == 1
                    ? ["Словоформа", "Лемма", "Стем", "Совпадают"]
                    : ["Словоформа", "Лемма", "Длина словоформы", "Длина леммы"],
                numeric: mode == 1 ? [false, false, false, false] : [false, false, true, true]);

            var sb = new StringBuilder();
            sb.AppendLine($"RussianLemmatizer — набор «{new[] { "Существительные", "Глаголы", "Смешанный" }[wordSet]}»");
            sb.AppendLine($"Режим: {new[] { "Лемматизация", "Стемм vs Лемма", "Тест кеша" }[mode]}");
            sb.AppendLine(AxisLegend(words, "Слова на оси X"));
            sb.AppendLine();

            if (mode == 1)
            {
                sb.AppendLine($"  {"Слово",-22} {"Лемма",-18} {"Стемм",-18}");
                sb.AppendLine("  " + new string('-', 60));
                for (int i = 0; i < n; i++)
                {
                    sb.AppendLine($"  {words[i],-22} {lemmas[i],-18} {stemmed[i],-18}");
                    t.Row(words[i], lemmas[i], stemmed[i],
                          string.Equals(lemmas[i], stemmed[i], StringComparison.Ordinal) ? "да" : "нет");
                }
            }
            else
            {
                sb.AppendLine($"  {"Слово",-22} {"Лемма",-18}");
                sb.AppendLine("  " + new string('-', 42));
                for (int i = 0; i < n; i++)
                {
                    sb.AppendLine($"  {words[i],-22} {lemmas[i],-18}");
                    t.Row(words[i], lemmas[i], words[i].Length.ToString(), lemmas[i].Length.ToString());
                }
                sb.AppendLine();
                string sample = string.Join(" ", words.Take(5));
                sb.AppendLine($"LemmatizeSentence:");
                sb.AppendLine($"  Вход:  {sample}");
                sb.AppendLine($"  Выход: {lemmatizer.LemmatizeSentence(sample)}");

                rep.Table("LemmatizeSentence — целое предложение",
                        ["Что", "Текст"], numeric: [false, false])
                   .Row("Вход",  sample)
                   .Row("Выход", lemmatizer.LemmatizeSentence(sample));
            }
            return sb.ToString();
        }

        private static string DoCachingBenchmark(string[] words, ILemmatizer lemmatizer, CachingLemmatizer cached,
                                                  string[] lemmas, Vector xI, ChartView cv, ReportBuilder rep, int n)
        {
            var sw = Stopwatch.StartNew();
            for (int r = 0; r < 500; r++) foreach (var w in words) lemmatizer.Lemmatize(w);
            sw.Stop(); long rawMs = sw.ElapsedMilliseconds;

            sw.Restart();
            for (int r = 0; r < 500; r++) foreach (var w in words) cached.Lemmatize(w);
            sw.Stop(); long cacheMs = sw.ElapsedMilliseconds;

            // -- График: время 500 прогонов без кеша и с кешом -------------
            cv.ChartName = $"CachingLemmatizer: 500 × {words.Length} слов";
            Axes(cv, "1 — без кеша, 2 — с кешом", "время, мс");
            cv.AddBar(Idx(2), Vec([rawMs, cacheMs]), "время, мс", C(0));

            // Деление на ноль реально: 500 прогонов по кешу укладываются в <1 мс
            double speedup = cacheMs > 0 ? (double)rawMs / cacheMs : double.PositiveInfinity;
            string speedupText = double.IsInfinity(speedup) ? "> ×" + rawMs : "×" + speedup.ToString("F1");

            rep.Metric("Без кеша", rawMs, "мс", hint: $"500 × {words.Length} вызовов Lemmatize")
               .Metric("С кешом", cacheMs, "мс", hint: "Тот же объём через CachingLemmatizer", tone: MetricTone.Good)
               .Metric("Ускорение", speedupText, tone: MetricTone.Good)
               .Metric("Размер кеша", cached.CacheSize, "записей",
                       hint: "Уникальных слов — повторы берутся из кеша")
               .Note($"Замер выполняется прямо сейчас на этой машине, поэтому числа шумят от запуска к запуску. " +
                     $"Устойчиво лишь соотношение: кеш выигрывает, пока уникальных слов ({cached.CacheSize}) " +
                     $"много меньше числа вызовов ({500 * words.Length}).");

            rep.Table("Замер времени", ["Вариант", "Время, мс", "Вызовов"], numeric: [false, true, true])
               .Row("Прямой вызов Lemmatize", rawMs.ToString(),   (500 * words.Length).ToString())
               .Row("CachingLemmatizer",      cacheMs.ToString(), (500 * words.Length).ToString());

            var lemmaTable = rep.Table("Содержимое кеша", ["Словоформа", "Лемма"], numeric: [false, false]);

            var sb = new StringBuilder();
            sb.AppendLine($"CachingLemmatizer vs прямой (500 × {words.Length} слов):");
            sb.AppendLine($"  Без кеша: {rawMs} мс");
            sb.AppendLine($"  С кешом:  {cacheMs} мс");
            sb.AppendLine($"  Ускорение: {speedupText}");
            sb.AppendLine($"  Размер кеша: {cached.CacheSize} записей");
            sb.AppendLine();
            sb.AppendLine("Леммы:");
            for (int i = 0; i < n; i++)
            {
                sb.AppendLine($"  «{words[i]}» -> «{lemmas[i]}»");
                lemmaTable.Row(words[i], lemmas[i]);
            }
            return sb.ToString();
        }
    }
}
