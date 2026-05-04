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
        private static readonly SKColor[] Pal =
        [
            new(0x38, 0xBD, 0xF8), new(0xF8, 0x71, 0x71), new(0x4A, 0xDE, 0x80),
            new(0xFB, 0xBF, 0x24), new(0xA7, 0x8B, 0xFA), new(0xFB, 0x92, 0x3C),
            new(0x34, 0xD3, 0x99), new(0xF4, 0x72, 0xB6), new(0xE8, 0x79, 0xF9),
        ];
        private static SKColor C(int i) => Pal[i % Pal.Length];

        // -- Корпусы текстов --------------------------------------------------

        private static readonly string[] Corpora =
        [
            // 0 — AI-термины
            "нейронная сеть обучение данные модель точность loss градиент " +
            "классификация регрессия кластеризация вектор матрица обучение " +
            "нейронная нейронная сеть сеть данные данные модель точность точность " +
            "глубокое обучение трансформер внимание признак слой нейрон активация " +
            "backpropagation оптимизатор батч эпоха переобучение регуляризация dropout",

            // 1 — Сказки
            "жили были дед и баба была у них курочка ряба снесла курочка яичко " +
            "не простое а золотое дед бил бил не разбил баба била била не разбила " +
            "мышка бежала хвостиком махнула яичко упало и разбилось дед плачет баба " +
            "плачет а курочка кудахчет не плачь дед не плачь баба снесу вам яичко",

            // 2 — Смешанный
            "машинное обучение это раздел искусственного интеллекта который изучает " +
            "алгоритмы способные учиться на данных нейронные сети вдохновлены работой " +
            "человеческого мозга данные это новая нефть обработка данных важна " +
            "в эпоху больших данных алгоритм обучается на примерах данные нейронная " +
            "сеть модель обучение данные данные признаки метки классификация",
        ];

        private static readonly string[] FairyCorpora =
        [
            Corpora[1],
            "deep learning neural network training data model accuracy loss gradient " +
            "classification regression clustering backpropagation optimizer batch epoch " +
            "overfitting regularization dropout layer neuron activation transformer " +
            "attention embedding token vocabulary encoder decoder sequence",
            Corpora[0] + " " + Corpora[1],
        ];

        private static readonly string[] EnglishCorpora =
        [
            "the cat sat on the mat the cat is fat the fat cat sat on the mat " +
            "a cat in a hat the cat sat with a rat the hat is on the cat " +
            "cat hat mat fat rat sat",
            "roses are red violets are blue sugar is sweet and so are you " +
            "the quick brown fox jumps over the lazy dog hello world",
            "int float string bool class void return public private static " +
            "method function variable array list dictionary namespace using",
        ];

        // -- Точка входа -------------------------------------------------------

        public static DemoResult Run(string key, IReadOnlyDictionary<string, double> p, DemoSettings s)
        {
            var cv = MakeView(s);
            string txt;
            try
            {
                txt = key switch
                {
                    "normalizers_demo" => DoNormalizers(p, cv),
                    "word_tokenizer"   => DoWordTokenizer(p, cv),
                    "bpe_demo"         => DoBPE(p, cv),
                    "str_metrics"      => DoStringMetrics(p, cv),
                    "ner_demo"         => DoNER(p, cv),
                    "sent_tokenizer"   => DoSentTokenizer(p, cv),
                    "hmm_gen"          => DoHMMGen(p, cv),
                    "text_cls"         => DoTextClassifier(p, cv),
                    "datatable_demo"   => DoDataTable(p, cv),
                    _                  => $"Неизвестный ключ «{key}»",
                };
            }
            catch (Exception ex)
            {
                txt = $"Ошибка: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()}";
            }
            return Png(cv, s, textOutput: txt);
        }

        // -- Вспомогательные методы --------------------------------------------

        private static string DistribName(int d) => d switch { 1 => "Лог-нормальное", 2 => "Равномерное", _ => "Нормальное" };

        private static double StdDev(double[] data)
        {
            double m = data.Average();
            return Math.Sqrt(data.Average(v => (v - m) * (v - m)));
        }
    }

    // -- Расширение Random ----------------------------------------------------

    internal static class RandomExtensions
    {
        public static double NextGaussian(this Random rng, double mean = 0, double std = 1)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            return mean + std * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }
    }
}
