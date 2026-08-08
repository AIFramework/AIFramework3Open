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
            ChartView cv,
            ReportBuilder rep)
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

            string[] nerTypes = [];
            if (entityCounts.Count > 0)
            {
                // -- График: сколько сущностей каждого типа нашлось ---------
                var ordered = entityCounts.OrderByDescending(x => x.Value).ToArray();
                nerTypes = ordered.Select(x => x.Key).ToArray();

                cv.ChartName = $"NER — найдено {entityCounts.Values.Sum()} сущностей ({nerTypes.Length} типов)";
                Axes(cv, "тип сущности", "количество вхождений");
                cv.AddBar(Idx(nerTypes.Length), Vec(ordered.Select(x => (double)x.Value)),
                          "сущностей", C(0));
            }
            else
            {
                cv.ChartName = "NER — сущности не найдены";
            }

            // Восстановление обязано вернуть исходный текст: если нет —
            // маскирование потеряло информацию, и это надо видеть сразу.
            bool lossless = string.Equals(restored, text, StringComparison.Ordinal);

            rep.Metric("Сущностей найдено", entityCounts.Values.Sum(),
                       tone: entityCounts.Count > 0 ? MetricTone.Good : MetricTone.Warn)
               .Metric("Типов сущностей", nerTypes.Length)
               .Metric("Предложений (с NER)", sentsNer.Count,
                       hint: "Разбиение с учётом сокращений «т. е.», «д. 56»")
               .Metric("Предложений (без NER)", sentsPlain.Count,
                       hint: "Наивное разбиение по точкам")
               .Metric("Восстановление", lossless ? "без потерь" : "с потерями",
                       hint: "Совпадает ли NerDecoder(маска) с исходным текстом",
                       tone: lossless ? MetricTone.Good : MetricTone.Bad)
               .Note("Разница в числе предложений показывает, сколько ложных границ даёт наивное " +
                     "разбиение по точкам: сокращения и инициалы точку содержат, а предложение не заканчивают.");

            if (entityCounts.Count > 0)
            {
                var t = rep.Table("Найденные сущности по типам",
                    ["Тип", "Количество"], numeric: [false, true]);
                foreach (var (type, count) in entityCounts.OrderByDescending(x => x.Value))
                    t.Row(type, count.ToString());
            }

            rep.Table("Этапы обработки", ["Этап", "Текст"], numeric: [false, false])
               .Row("Исходный",       text)
               .Row("Маскированный",  masked)
               .Row("Восстановленный", restored);

            var sentTable = rep.Table("Разбиение на предложения",
                ["#", "С учётом NER", "Наивное"], numeric: [true, false, false]);
            for (int i = 0; i < Math.Max(sentsNer.Count, sentsPlain.Count); i++)
                sentTable.Row((i + 1).ToString(),
                              i < sentsNer.Count   ? sentsNer[i]   : "—",
                              i < sentsPlain.Count ? sentsPlain[i] : "—");

            var sb = new StringBuilder();
            sb.AppendLine("> NER — распознавание сущностей (RegEx)");
            if (nerTypes.Length > 0)
                sb.AppendLine(AxisLegend(nerTypes, "Типы на оси X"));
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
