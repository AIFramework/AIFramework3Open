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
        // -- 1. Нормализация текста -------------------------------------

        private static string DoTextNormalize(IReadOnlyDictionary<string, double> p, ChartView cv, ReportBuilder rep)
        {
            int  textId = I(p, "textId", 0);
            bool lower  = I(p, "isLower", 1) != 0;
            string text = Texts[textId];

            string norm     = TextStandard.Normalize(text, lower);
            string onlyChar = TextStandard.OnlyChars(text, lower);
            string onlyRus  = TextStandard.OnlyRusChars(text);
            string noDup    = TextStandard.NoDoubleWord(text);

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int half  = words.Length / 2;
            string half1 = string.Join(" ", words.Take(half));
            string half2 = string.Join(" ", words.Skip(half));

            static HashSet<string> GetWS(string t) =>
                TextStandard.GetWords(t, s => s.ToLower(), s => s, s => s.Length > 2);

            var setFull1 = GetWS(half1);
            var setFull2 = GetWS(half2);
            double dice     = TextStandard.SimTextDice(setFull1, setFull2);
            double diceAsym = TextStandard.SimTextDiceAsymmetric(setFull1, setFull2);

            // -- График: сколько символов «съедает» каждая нормализация ----
            string[] stageNames = ["Исходный", "Normalize", "OnlyChars", "OnlyRusChars", "NoDoubleWord"];
            var lengths = Vec([text.Length, norm.Length, onlyChar.Length, onlyRus.Length, noDup.Length]);
            var kept    = Vec(new[] { text.Length, norm.Length, onlyChar.Length, onlyRus.Length, noDup.Length }
                              .Select(l => 100.0 * l / Math.Max(1, text.Length)));

            cv.ChartName = "TextStandard: длина текста после каждой обработки";
            Axes(cv, "этап обработки", "символов");
            cv.AddBar(Idx(stageNames.Length), lengths, "длина, симв.", C(0));

            // -- Метрики и таблицы -----------------------------------------
            rep.Metric("Исходная длина", text.Length, "симв.")
               .Metric("После Normalize", norm.Length, "симв.",
                       hint: "Схлопывание пробелов и приведение регистра")
               .Metric("Только кириллица", onlyRus.Length, "симв.",
                       hint: "OnlyRusChars — самая агрессивная фильтрация")
               .Metric("Дайс (симметр.)", dice,
                       hint: "Похожесть половин текста по множествам слов",
                       tone: dice > 0.3 ? MetricTone.Good : MetricTone.Neutral)
               .Metric("Дайс (асимметр.)", diceAsym,
                       hint: "Доля слов первой половины, встретившихся во второй")
               .Note("Текст делится пополам, и половины сравниваются как множества слов длиннее двух символов: " +
                     "так видно, насколько лексика однородна внутри документа.");

            var stagesTable = rep.Table("Этапы обработки TextStandard",
                ["#", "Этап", "Символов", "Доля от исходного", "Результат"],
                numeric: [true, false, true, true, false]);

            string[] stageResults = [text, norm, onlyChar, onlyRus, noDup];
            for (int i = 0; i < stageNames.Length; i++)
                stagesTable.Row((i + 1).ToString(), stageNames[i], stageResults[i].Length.ToString(),
                                $"{kept[i]:F0}%", Truncate(stageResults[i], 70));

            rep.Table("Сравнение половин текста",
                    ["Показатель", "Значение"], numeric: [false, true])
               .Row("Уникальных слов в первой половине", setFull1.Count.ToString())
               .Row("Уникальных слов во второй половине", setFull2.Count.ToString())
               .Row("Коэффициент Дайса (симметричный)", F(dice))
               .Row("Коэффициент Дайса (асимметричный)", F(diceAsym));

            var sb = new StringBuilder();
            sb.AppendLine($"=== TextStandard (текст {textId}) ===");
            sb.AppendLine(AxisLegend(stageNames, "Этапы на оси X"));
            sb.AppendLine("Доля от исходной длины: " + string.Join(", ",
                stageNames.Select((s, i) => $"{s} {kept[i]:F0}%")));
            sb.AppendLine();
            sb.AppendLine($"Исходный  [{text.Length} симв.]: {Truncate(text, 80)}");
            sb.AppendLine($"Normalize [{norm.Length} симв.]: {Truncate(norm, 80)}");
            sb.AppendLine($"OnlyChars [{onlyChar.Length} симв.]: {Truncate(onlyChar, 80)}");
            sb.AppendLine($"OnlyRusCh [{onlyRus.Length} симв.]: {Truncate(onlyRus, 80)}");
            sb.AppendLine($"NoDupWord [{noDup.Length} симв.]: {Truncate(noDup, 80)}");
            sb.AppendLine();
            sb.AppendLine($"Уникальных слов (первая пол.): {setFull1.Count}");
            sb.AppendLine($"Уникальных слов (вторая пол.): {setFull2.Count}");
            sb.AppendLine($"Дайс (симметричный): {dice:F4}");
            sb.AppendLine($"Дайс (асимметр.):    {diceAsym:F4}");
            return sb.ToString();
        }
    }
}
