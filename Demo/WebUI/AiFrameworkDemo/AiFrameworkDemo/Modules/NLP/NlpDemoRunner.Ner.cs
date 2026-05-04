using AI.Charts;
using AI.DataPrepaire.NLPUtils.RegexpNLP;
using AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER.SpecialNers;
using AI.DataStructs.Algebraic;
using AiFrameworkDemo.Core;
using System.Text;
using System.Text.RegularExpressions;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.NLP
{
    public static partial class NlpDemoRunner
    {
        private static string DoNer(
            IReadOnlyDictionary<string, double> p,
            IReadOnlyDictionary<string, string> tp,
            ChartView cv)
        {
            string text = T(tp, "_text",
                "Добрый день. В 900 году до н. э. было это. " +
                "Мой номер +8 999 666 555 4. А.В. Александров идет к И.К. Гаврилову. " +
                "Сайт vkre.com/su. Почта zzszzs@mszk.com. " +
                "Адрес ул. Гон, д. 56, кв. 882. Созвонимся в 22:39 или завтра в 09:15.");

            var ner = new CombineNerProcessor();
            string masked = ner.RunProcessor(text);
            string restored = ner.NerDecoder(masked);

            var entityCounts = new Dictionary<string, int>();
            foreach (Match m in Regex.Matches(masked, @"%(\w+?)_\d+%"))
            {
                string type = m.Groups[1].Value;
                entityCounts[type] = entityCounts.GetValueOrDefault(type) + 1;
            }

            var sentences = new SentencesTokenizer();
            var sentsPlain = sentences.Tokenize(text);
            var sentsNer   = sentences.TokenizeWithNer(text, withTrim: false);

            if (entityCounts.Count > 0)
            {
                var types = entityCounts.Keys.OrderBy(k => k).ToArray();
                cv.ChartName = $"NER — найдено {entityCounts.Values.Sum()} сущностей ({types.Length} типов)";
            }
            else
            {
                cv.ChartName = "NER — сущности не найдены";
            }

            var sb = new StringBuilder();
            sb.AppendLine("> NER — распознавание сущностей (RegEx)");
            sb.AppendLine();

            sb.AppendLine("- Исходный текст");
            sb.AppendLine(text);
            sb.AppendLine();

            sb.AppendLine("- Маскированный текст");
            sb.AppendLine(masked);
            sb.AppendLine();

            sb.AppendLine("- Восстановленный текст");
            sb.AppendLine(restored);
            sb.AppendLine();

            if (entityCounts.Count > 0)
            {
                sb.AppendLine("- Найденные сущности");
                foreach (var (type, count) in entityCounts.OrderByDescending(x => x.Value))
                    sb.AppendLine($"  {type,-20} : {count}");
                sb.AppendLine();
            }

            sb.AppendLine("- Предложения (без NER)");
            for (int i = 0; i < sentsPlain.Count; i++)
                sb.AppendLine($"  [{i + 1}] {sentsPlain[i]}");
            sb.AppendLine();

            sb.AppendLine("- Предложения (с NER)");
            for (int i = 0; i < sentsNer.Count; i++)
                sb.AppendLine($"  [{i + 1}] {sentsNer[i]}");

            return sb.ToString();
        }
    }
}
