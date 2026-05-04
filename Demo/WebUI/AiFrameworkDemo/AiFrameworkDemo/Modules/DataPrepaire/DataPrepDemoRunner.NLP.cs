using AI.DataPrepaire.DataNormalizers;
using AI.DataPrepaire.DataLoader;
using AI.DataPrepaire.DataLoader.Formats;
using AI.DataPrepaire.Tokenizers.TextTokenizers;
using AI.DataPrepaire.NLPUtils;
using AI.DataPrepaire.NLPUtils.RegexpNLP;
using AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER;
using AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER.SpecialNers;
using AI.DataPrepaire.NLPUtils.TextClassification;
using AI.DataPrepaire.NLPUtils.TextGeneration;
using AI.DataStructs.Algebraic;
using AI.Charts;
using AiFrameworkDemo.Core;
using SkiaSharp;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.DataPrepaire
{
    public static partial class DataPrepDemoRunner
    {
        private static string DoStringMetrics(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int pairSet = I(p, "pairSet", 0);

            var pairs = pairSet switch
            {
                1 => new[]
                {
                    ("машина", "машины"),
                    ("нейронная сеть", "нейронные сети"),
                    ("кот", "кит"),
                    ("обучение", "учёба"),
                    ("алгоритм", "программа"),
                },
                2 => new[]
                {
                    ("machine learning", "deep learning"),
                    ("hello world", "hello word"),
                    ("neural", "natural"),
                    ("transformer", "transporter"),
                    ("cat", "bat"),
                },
                _ => new[]
                {
                    ("кот", "кот"),
                    ("кот", "кит"),
                    ("кот", "собака"),
                    ("нейрон", "нейроны"),
                    ("алгоритм", "алгортм"),
                },
            };

            int n = pairs.Length;
            var xI = new Vector(n);
            var lev = new Vector(n);
            var cor = new Vector(n);
            var hcos = new Vector(n);

            for (int i = 0; i < n; i++)
            {
                xI[i]  = i + 1;
                lev[i]  = 1 - CompareStringMethods.LevenshteinDistance(pairs[i].Item1, pairs[i].Item2) /
                              Math.Max(pairs[i].Item1.Length, pairs[i].Item2.Length);
                cor[i]  = CompareStringMethods.WordCorellation(pairs[i].Item1, pairs[i].Item2);
                hcos[i] = CompareStringMethods.HistogramCos(pairs[i].Item1, pairs[i].Item2);
            }

            cv.AddPlot(xI, lev,  "Levenshtein (норм.)", C(0), 2);
            cv.AddPlot(xI, cor,  "WordCorrelation",     C(1), 2);
            cv.AddPlot(xI, hcos, "HistogramCos",        C(2), 2);
            cv.AddScatter(xI, lev,  "", C(0));
            cv.AddScatter(xI, cor,  "", C(1));
            cv.AddScatter(xI, hcos, "", C(2));

            var sb = new StringBuilder();
            sb.AppendLine("Метрики сходства строк (ближе к 1 = похожее):");
            sb.AppendLine();
            sb.AppendLine($"  {"Пара",-35} {"Lev":>6} {"WordCor":>8} {"HisCos":>8}");
            sb.AppendLine(new string('-', 62));
            for (int i = 0; i < n; i++)
                sb.AppendLine($"  «{pairs[i].Item1}» <-> «{pairs[i].Item2}»{new string(' ', Math.Max(0, 25 - pairs[i].Item1.Length - pairs[i].Item2.Length))} {lev[i]:F3}  {cor[i]:F3}  {hcos[i]:F3}");
            sb.AppendLine();
            sb.AppendLine("Описание метрик:");
            sb.AppendLine("  Levenshtein: 1 - (edit_dist / max_len)");
            sb.AppendLine("  WordCorrel:  пересечение множеств символов");
            sb.AppendLine("  HistogramCos: n-граммный косинус (char-level)");

            return sb.ToString();
        }

        private static string DoNER(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int nerType = I(p, "nerType",  0);
            int textSet = I(p, "textSet",  0);

            var texts = textSet switch
            {
                1 => new[]
                {
                    "Совещание в понедельник в 10:00 и в пятницу в 15:30.",
                    "Обед запланирован на 13:00, встреча в 9:45 утра.",
                    "Рабочее время: 09:00 — 18:00, перерыв в 12:30.",
                },
                2 => new[]
                {
                    "Звоните +7 (999) 123-45-67. Пишите: info@company.ru. Встреча в 16:00.",
                    "Менеджер: +7 800 555 35 35. Email: support@example.com. Время: 11:30.",
                    "Тел: 8-800-200-0200. Почта: hello@test.org. Запись в 14:00.",
                },
                _ => new[]
                {
                    "Позвоните мне по номеру +7 999 123-45-67 в любое время.",
                    "Обратитесь по телефону 8 (800) 555-35-35 или +7-495-000-00-00.",
                    "Контакт для связи: +375 29 123 45 67.",
                },
            };

            NerProcessor ner = nerType switch
            {
                1 => new EmailAdressProcessor(),
                2 => new TimeProcessor(),
                3 => new RegexNer(@"\b[A-Z][a-zA-Z]+\b", "proper_noun"),
                _ => new PhoneNerProcessor(),
            };

            string nerName = nerType switch { 1 => "Email", 2 => "Время", 3 => "Существит.", _ => "Телефон" };

            var xI  = new Vector(texts.Length);
            var yHits = new Vector(texts.Length);

            var sb = new StringBuilder();
            sb.AppendLine($"NER-процессор: {nerName}");
            sb.AppendLine();

            for (int i = 0; i < texts.Length; i++)
            {
                xI[i] = i + 1;
                string processed = ner.RunProcessor(texts[i]);
                int hits = ner.NerToNerToken.Count;
                yHits[i] = hits;

                sb.AppendLine($"Текст {i + 1}: {texts[i]}");
                sb.AppendLine($"После NER:  {processed}");
                sb.AppendLine($"Токенов:    {ner.NerToNerToken.Count}");
                sb.AppendLine();
            }

            cv.AddPlot(xI, yHits, "Найдено сущностей", C(0), 2);
            cv.AddScatter(xI, yHits, "Сущности", C(0));

            sb.AppendLine("API:");
            sb.AppendLine("var ner = new PhoneNerProcessor();");
            sb.AppendLine("string result = ner.RunProcessor(text);");
            sb.AppendLine("string original = ner.NerDecoder(result);");

            return sb.ToString();
        }

        private static string DoHMMGen(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int genWords = I(p, "genWords",  20);
            int corpI    = I(p, "corpusId",   0);
            int seed     = I(p, "seed",       42);

            string trainText = FairyCorpora[corpI];

            var hmm = new HMMFast();
            hmm.Train(trainText);

            var rng = new Random(seed);
            var results = new List<string>();
            for (int t = 0; t < 3; t++)
                results.Add(hmm.Generate(genWords, trainText.Split(' ').Take(2).ToArray(), rng));

            var probVec = hmm.TextToVector(trainText.Split(' ').Take(20).Aggregate((a, b) => a + " " + b));
            int showDim = Math.Min(probVec.Count, 30);
            var xP = new Vector(showDim);
            var yP = new Vector(showDim);
            for (int i = 0; i < showDim; i++) { xP[i] = i; yP[i] = probVec[i]; }

            cv.AddPlot(xP, yP, "P(следующий токен)", C(0), 2);
            cv.AddScatter(xP, yP, "Топ вероятности", C(0));

            var sb = new StringBuilder();
            sb.AppendLine($"Обучено на {trainText.Split(' ').Length} токенах.");
            sb.AppendLine();
            sb.AppendLine("Генерация текста:");
            for (int t = 0; t < results.Count; t++)
                sb.AppendLine($"  Вариант {t+1}: {results[t]}");
            sb.AppendLine();
            sb.AppendLine($"Размер вектора вероятностей: {probVec.Count}");
            sb.AppendLine($"Сумма вероятностей: {probVec.Sum():F4}");
            sb.AppendLine($"Топ-5 P:");
            var topP = Enumerable.Range(0, probVec.Count)
                .OrderByDescending(i => probVec[i]).Take(5)
                .Select(i => $"    [{i}]: {probVec[i]:F4}");
            foreach (var t in topP) sb.AppendLine(t);

            return sb.ToString();
        }

        private static string DoTextClassifier(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            double topP   = N(p, "topP",     0.5);
            int maxNGram  = I(p, "maxNGram",   3);
            int classSet  = I(p, "classSet",   0);

            var (classNames, trainTexts, trainLabels, testTexts) = classSet switch
            {
                1 => (
                    new[] { "приветствие", "прощание", "вопрос" },
                    new[] { "привет добрый день здравствуйте", "пока до свидания прощай", "что где когда как почему" },
                    new[] { 0, 1, 2 },
                    new[] { "добрый день всем", "до скорой встречи", "как это работает" }
                ),
                2 => (
                    new[] { "ошибка", "запрос", "жалоба" },
                    new[] { "ошибка баг проблема сбой не работает", "запрос информация помогите как настроить", "жалоба недовольство плохой сервис" },
                    new[] { 0, 1, 2 },
                    new[] { "программа не запускается", "как обновить настройки", "ужасный сервис недоволен" }
                ),
                _ => (
                    new[] { "спорт", "политика", "технологии" },
                    new[] { "футбол гол матч команда игра чемпионат победа", "выборы политика закон правительство депутат", "ИИ технологии программирование разработка нейросеть" },
                    new[] { 0, 1, 2 },
                    new[] { "голы в матче сборной", "закон о выборах принят", "нейронная сеть победила человека" }
                ),
            };

            int numClasses = classNames.Length;
            var cls = new TextRuleClassifier(numClasses, topP, maxNGram);
            cls.Train(trainTexts, trainLabels);

            var xI  = new Vector(testTexts.Length);
            var yPr = new Vector(testTexts.Length);
            var sb  = new StringBuilder();

            sb.AppendLine($"Классов: {numClasses}, top_p={topP}, max_ngram={maxNGram}");
            sb.AppendLine($"Правил: {cls.CountRules}");
            sb.AppendLine();
            sb.AppendLine("Тестовые тексты:");

            for (int i = 0; i < testTexts.Length; i++)
            {
                int pred = cls.Predict(testTexts[i]);
                xI[i]  = i + 1;
                yPr[i] = pred;
                sb.AppendLine($"  «{testTexts[i]}»");
                sb.AppendLine($"  -> класс {pred}: «{(pred >= 0 && pred < classNames.Length ? classNames[pred] : "неизвестно")}»");
                sb.AppendLine();
            }

            cv.AddScatter(xI, yPr, "Предсказанный класс", C(0));
            cv.AddPlot(xI, yPr, "", C(0), 1);

            return sb.ToString();
        }
    }
}
