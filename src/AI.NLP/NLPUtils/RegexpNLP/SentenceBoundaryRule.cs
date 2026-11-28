using System.Collections.Generic;
using System.Linq;

namespace AI.DataPrepaire.NLPUtils.RegexpNLP;

/// <summary>
/// Единственное правило конца предложения: какие знаки завершают и при каком следующем токене.
/// </summary>
/// <remarks>
/// Конец — серия знаков «. ! ? …» с закрывающими кавычками и скобками после неё. Что решает:
/// <list type="bullet">
/// <item>«!» и «?» в серии — конец всегда;</item>
/// <item>многоточие («…», «...») — конец, если следующее слово не со строчной;</item>
/// <item>точка после сокращения — по классу из <see cref="SentenceAbbreviations"/>: приставка не конец,
/// хвост — конец перед заглавной; однобуквенное с заглавной вне таблицы — инициал, не конец;</item>
/// <item>точка после числа — конец перед заглавной («НК-40. Срок», но «п. 3.2. настоящего»);</item>
/// <item>точка после слова — конец всегда.</item>
/// </list>
/// Тире со строчной после конца — слова автора, продолжение: «Готово?» — спросил он.
/// Конец требует пробела после себя; без пробела — только «слово.Заглавная» (склейка при извлечении текста).
/// Фрагмент без единой буквы («1.», «1.1.») предложением не считается: нумерация пунктов остаётся с текстом.
/// Сущности NER защищают знаки внутри себя; знак последним символом сущности решает это правило.
/// </remarks>
internal static class SentenceBoundaryRule
{
    private const string Closers = "»\"”’')]}";
    private const string Openers = "«\"„“‘'([{—–-";

    private enum NextToken { Never, Always, NotLower, Upper }

    /// <summary>
    /// Позиции концов предложений (индекс сразу после конца).
    /// </summary>
    /// <param name="text">Текст.</param>
    /// <param name="entities">Сущности NER: знак внутри защищён, последний символ решает правило.</param>
    /// <param name="closedSpans">Защищённые целиком участки, включая последний символ.</param>
    public static List<int> FindEnds(string text,
        IEnumerable<(int Start, int Length)> entities,
        IEnumerable<(int Start, int Length)> closedSpans)
    {
        var ends = new List<int>();
        if (string.IsNullOrEmpty(text)) return ends;

        var isProtected = new bool[text.Length];
        Protect(isProtected, entities, 1);
        Protect(isProtected, closedSpans, 0);

        var abbreviationAt = new Dictionary<int, AbbreviationClass>();
        foreach (var (start, length, cls) in SentenceAbbreviations.Find(text))
        {
            Protect(isProtected, new[] { (start, length) }, 1);
            abbreviationAt[start + length - 1] = cls;
        }

        int sentenceStart = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (!IsTerminator(text[i]) || isProtected[i]) continue;

            int runEnd = i;
            while (runEnd < text.Length && IsTerminator(text[runEnd])) runEnd++;
            int end = runEnd;
            while (end < text.Length && Closers.IndexOf(text[end]) >= 0) end++;

            if (IsEnd(text, sentenceStart, i, runEnd, end, abbreviationAt))
            {
                ends.Add(end);
                sentenceStart = end;
                i = end - 1;
            }
            else i = runEnd - 1;
        }

        return ends;
    }

    private static bool IsEnd(string text, int sentenceStart, int i, int runEnd, int end,
        Dictionary<int, AbbreviationClass> abbreviationAt)
    {
        int next = end;
        while (next < text.Length && char.IsWhiteSpace(text[next])) next++;

        if (next == text.Length) return false;
        if (!HasLetter(text, sentenceStart, i)) return false;

        var rule = Classify(text, i, runEnd, abbreviationAt);
        if (rule == NextToken.Never) return false;

        if (next == end)
            return i > 0 && char.IsLetter(text[i - 1]) && char.IsUpper(text[next]);

        // Тире и строчная после конца — слова автора: «Готово?» — спросил он.
        if (text[next] is '—' or '–') rule = NextToken.NotLower;

        while (next < text.Length && (Openers.IndexOf(text[next]) >= 0 || char.IsWhiteSpace(text[next]))) next++;
        char first = next < text.Length ? text[next] : ' ';

        return rule switch
        {
            NextToken.Always => true,
            NextToken.NotLower => !char.IsLower(first),
            _ => char.IsUpper(first),
        };
    }

    private static NextToken Classify(string text, int i, int runEnd, Dictionary<int, AbbreviationClass> abbreviationAt)
    {
        string run = text[i..runEnd];
        if (run.IndexOfAny(new[] { '!', '?' }) >= 0) return NextToken.Always;
        if (run.Length > 1 || run[0] == '…') return NextToken.NotLower;

        if (abbreviationAt.TryGetValue(i, out var cls))
            return cls switch
            {
                AbbreviationClass.MayEnd => NextToken.Upper,
                AbbreviationClass.MayEndAfterNumber when PrecededByNumber(text, i) => NextToken.Upper,
                _ => NextToken.Never,
            };

        if (i == 0) return NextToken.Always;
        if (char.IsDigit(text[i - 1])) return NextToken.Upper;

        bool initial = char.IsUpper(text[i - 1]) && (i == 1 || char.IsWhiteSpace(text[i - 2]) || text[i - 2] == '.');
        return initial ? NextToken.Never : NextToken.Always;
    }

    // Предыдущее слово перед сокращением — число («2026 г.», «5 000 руб.», «XIX в.»)
    private static bool PrecededByNumber(string text, int dot)
    {
        int p = dot;
        while (p > 0 && char.IsLetter(text[p - 1])) p--;
        while (p > 0 && char.IsWhiteSpace(text[p - 1])) p--;

        int tokenEnd = p;
        while (p > 0 && !char.IsWhiteSpace(text[p - 1])) p--;
        string token = text[p..tokenEnd];

        return token.Length > 0 && (char.IsDigit(token[^1]) || token.All(c => "IVXLCDM".IndexOf(c) >= 0));
    }

    private static void Protect(bool[] isProtected, IEnumerable<(int Start, int Length)> spans, int keepLast)
    {
        if (spans == null) return;
        foreach (var (start, length) in spans)
            for (int p = start; p < start + length - keepLast && p < isProtected.Length; p++)
                isProtected[p] = true;
    }

    private static bool HasLetter(string text, int from, int to)
    {
        for (int p = from; p < to; p++)
            if (char.IsLetter(text[p])) return true;
        return false;
    }

    private static bool IsTerminator(char c) => c is '.' or '!' or '?' or '…';
}
