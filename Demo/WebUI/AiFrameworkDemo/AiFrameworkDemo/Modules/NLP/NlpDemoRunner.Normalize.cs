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

        private static string DoTextNormalize(IReadOnlyDictionary<string, double> p, ChartView cv)
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

            cv.ChartName = "Нормализация текста";

            var sb = new StringBuilder();
            sb.AppendLine($"=== TextStandard (текст {textId}) ===");
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
