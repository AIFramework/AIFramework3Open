using AI.NLP;
using AI.NLP.Stemmers;
using AI.Script.Binding;
using System.Text.RegularExpressions;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Токены для лексического поиска: одна процедура для текста запроса, описаний и имён в коде.
/// </summary>
/// <remarks>
/// Имена разбиваются по регистру и подчёркиваниям тем же <see cref="ScriptModule.ToSnakeCase"/>,
/// которым язык AIScript выводит имена функций: <c>CalcFFT</c> → «calc fft», <c>KMeans</c> →
/// «k means». Слова обоих языков сводятся к основе стеммерами Портера, поэтому «фильтрами» и
/// «фильтр», «Filtering» и «filter» дают один токен.
/// <para>
/// Для русского стеммер, а не лемматизатор <c>AI.NLP</c>: поиску нужен одинаковый ключ у
/// всех форм слова, а не правильная лемма. Лемматизатор сводит «сигнал» и «сигнала» к
/// «сигнать», а «сигналы» — к «сигнал», и формы расходятся. На эталоне стеммер дал MRR 0,745
/// против 0,723 у лемматизатора и 0,709 у его правил без разбора части речи.
/// </para>
/// </remarks>
internal static class CodeTokenizer
{
    private static readonly Regex Word = new(@"[\p{L}\p{Nd}_]+", RegexOptions.Compiled);
    private static readonly HashSet<string> RussianStop = new(ProbabilityDictionary.StopWords, StringComparer.Ordinal);

    private static readonly HashSet<string> EnglishStop =
    [
        "the", "an", "of", "to", "in", "for", "and", "or", "is", "are", "be", "by", "on", "with", "from", "as", "at", "it", "this", "that",
    ];

    /// <summary>Токены текста в порядке появления, с повторами.</summary>
    public static List<string> Tokens(string text)
    {
        var tokens = new List<string>();

        if (string.IsNullOrEmpty(text)) return tokens;

        foreach (Match word in Word.Matches(text))
        {
            foreach (string part in ScriptModule.ToSnakeCase(word.Value).Split('_', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Normalize(part) is { } token) tokens.Add(token);
            }
        }

        return tokens;
    }

    /// <summary>Русский ли токен: глоссарий связывает русские слова описаний с английскими именами.</summary>
    public static bool IsRussian(string token) => token.Length > 0 && token[0] is >= 'а' and <= 'я' or 'ё';

    private static string? Normalize(string part)
    {
        string lower = part.ToLowerInvariant();

        if (lower.Length < 2 || lower.All(char.IsDigit)) return null;

        if (IsRussian(lower))
        {
            lower = lower.Replace('ё', 'е');
            return RussianStop.Contains(lower) ? null : StemmerRus.TransformingWord(lower);
        }

        return EnglishStop.Contains(lower) ? null : StemmerEng.TransformingWord(lower);
    }
}
