using AI.NLP.Similarity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiFramework.Tools.Atlas;

/// <summary>Форма тела метода для сравнения кода с кодом.</summary>
/// <param name="Signature">Подпись MinHash по шинглам нормализованных токенов.</param>
/// <param name="Shingles">Сами шинглы: точный коэффициент Жаккара для пар-кандидатов.</param>
/// <param name="Constants">Числовые литералы, кроме 0, 1, 2: «магические» константы вроде 1e-9 или 0.5772.</param>
/// <param name="Control">Сколько в теле циклов, ветвлений, возвратов и перехватов.</param>
/// <param name="Tokens">Число токенов тела.</param>
/// <param name="Strings">Сколько из них строковых литералов: тело-текст (справка, шаблон) похоже на любое другое такое же.</param>
public sealed record CodeShape(ulong[] Signature, IReadOnlySet<ulong> Shingles, IReadOnlySet<string> Constants, int[] Control, int Tokens, int Strings)
{
    /// <summary>Тело — в основном текст: сходство такого кода ничего не говорит об алгоритме.</summary>
    public bool IsText => Strings * 3 > Tokens;
}

/// <summary>
/// Нормализация тела метода: одинаковый алгоритм с другими именами переменных должен дать
/// одинаковую форму.
/// </summary>
/// <remarks>
/// Имена со строчной буквы (локальные переменные, параметры, закрытые поля <c>_x</c>)
/// заменяются на <c>ID</c>, числа — на <c>NUM</c> (0, 1 и 2 остаются: это границы циклов и
/// индексы, они часть структуры), строки — на <c>STR</c>. Имена с заглавной (типы, методы,
/// свойства) сохраняются: вызов <c>Math.Sqrt</c> отличает один алгоритм от другого. Разбор
/// только синтаксический — семантическая модель здесь не нужна, а тело хранится в индексе.
/// </remarks>
public static class CodeShapes
{
    /// <summary>Длина шингла в токенах.</summary>
    public const int ShingleSize = 5;

    private static readonly MinHash Hasher = new(size: 128, seed: 7);

    /// <summary>Длина подписи: нужна индексу LSH.</summary>
    public static int SignatureSize => Hasher.Size;

    /// <summary>Форма тела; <c>null</c>, если тело пустое.</summary>
    public static CodeShape? Of(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        string source = body.StartsWith("=>", StringComparison.Ordinal)
            ? $"class __C {{ object __M() {body}; }}"
            : $"class __C {{ void __M() {body} }}";

        MethodDeclarationSyntax method = CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        SyntaxNode code = (SyntaxNode?)method.Body ?? method.ExpressionBody!;

        var tokens = new List<ulong>();
        var constants = new HashSet<string>(StringComparer.Ordinal);
        int strings = 0;

        foreach (SyntaxToken token in code.DescendantTokens())
        {
            string normalized = token.Kind() switch
            {
                SyntaxKind.IdentifierToken => token.ValueText.Length > 0 && char.IsUpper(token.ValueText[0]) ? token.ValueText : "ID",
                SyntaxKind.NumericLiteralToken => token.Text is "0" or "1" or "2" ? token.Text : Constant(token.Text, constants),
                SyntaxKind.StringLiteralToken or SyntaxKind.CharacterLiteralToken or SyntaxKind.InterpolatedStringTextToken => "STR",
                _ => token.Text,
            };

            if (normalized == "STR") strings++;
            tokens.Add(MinHash.Hash(normalized));
        }

        var shingles = new HashSet<ulong>();
        for (int i = 0; i + ShingleSize <= tokens.Count; i++) shingles.Add(MinHash.Combine(tokens.GetRange(i, ShingleSize).ToArray()));
        if (tokens.Count is > 0 and < ShingleSize) shingles.Add(MinHash.Combine(tokens.ToArray()));

        int[] control =
        [
            code.DescendantNodes().Count(n => n is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax),
            code.DescendantNodes().Count(n => n is IfStatementSyntax or SwitchStatementSyntax or ConditionalExpressionSyntax or SwitchExpressionSyntax),
            code.DescendantNodes().Count(n => n is ReturnStatementSyntax),
            code.DescendantNodes().Count(n => n is TryStatementSyntax or ThrowStatementSyntax or ThrowExpressionSyntax),
        ];

        return new CodeShape(Hasher.Signature(shingles), shingles, constants, control, tokens.Count, strings);
    }

    /// <summary>Сходство формы управления: 1 — те же числа циклов, ветвлений, возвратов.</summary>
    public static double ControlSimilarity(int[] a, int[] b)
    {
        int total = a.Zip(b, Math.Max).Sum();
        return total == 0 ? 1 : 1 - (double)a.Zip(b, (x, y) => Math.Abs(x - y)).Sum() / total;
    }

    private static string Constant(string text, HashSet<string> constants)
    {
        constants.Add(text.TrimEnd('d', 'D', 'f', 'F', 'm', 'M', 'L', 'l', 'u', 'U'));
        return "NUM";
    }
}
