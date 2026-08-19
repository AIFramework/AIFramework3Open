using System.Text;
using AI.HighLevelFunctions;
using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;
using AI.Solvers.Math.Core.Patterns;

namespace AI.Solvers.Math.Core.Solvers;

/// <summary>
/// Таблица преобразований Лапласа с поддержкой поиска и применения трансформаций
/// </summary>
public static class LaplaceTable
{
    /// <summary>Наибольшее n, для которого n! ещё помещается в long (21! уже переполняется).</summary>
    private const int MaxExactFactorial = 20;


    // Структура для хранения преобразования.
    // Matcher возвращает ТОЛЬКО правую часть F(s): собирать «L{f} = F(s)» и разбирать
    // такую строку обратно по '=' и '(' — как делалось раньше — значит терять всё
    // после первой скобки, превращая 1/(s²+1) в «1/».
    private class LaplaceEntry
    {
        public string Pattern { get; set; } = "";
        public string Transform { get; set; } = "";
        public string Description { get; set; } = "";
        public Func<Expression, string, (bool matches, string transform)> Matcher { get; set; } = null!;
    }

    private static readonly List<LaplaceEntry> _table = new()
    {
        new LaplaceEntry
        {
            Pattern = "1",
            Transform = "1/s",
            Description = "Единица",
            Matcher = (expr, var) =>
            {
                if (expr is Constant c && System.Math.Abs(c.Value - 1) < 1e-10)
                    return (true, "1/s");
                return (false, "");
            }
        },

        new LaplaceEntry
        {
            Pattern = "t",
            Transform = "1/s²",
            Description = "Переменная",
            Matcher = (expr, var) =>
            {
                if (expr is Variable v && v.Name == var)
                    return (true, "1/s²");
                return (false, "");
            }
        },

        new LaplaceEntry
        {
            Pattern = "t^n",
            Transform = "n!/s^(n+1)",
            Description = "Степень переменной",
            Matcher = (expr, var) =>
            {
                if (expr is Power pow && pow.Base is Variable vp && vp.Name == var &&
                    pow.Exponent is Constant cp && cp.Value > 0 && cp.Value == System.Math.Floor(cp.Value))
                {
                    int n = (int)cp.Value;
                    // Factorial возвращает long и с 21! молча переполняется в отрицательное
                    // число. За границей точного представления пишем «n!» и рядом
                    // порядок величины через Gamma(n+1) = n!.
                    string numerator = n <= MaxExactFactorial
                        ? FunctionsForEachElements.Factorial(n).ToString()
                        : $"{n}! (≈{FunctionsForEachElements.Gamma(n + 1):G6})";
                    return (true, $"{numerator}/s^{n + 1}");
                }
                return (false, "");
            }
        },
        
        // Экспоненциальные функции
        new LaplaceEntry
        {
            Pattern = "exp(at)",
            Transform = "1/(s-a)",
            Description = "Экспонента",
            Matcher = (expr, var) =>
            {
                if (expr is Exp exp1)
                {
                    if (exp1.Argument is Multiply mult && mult.Left is Constant ca &&
                        mult.Right is Variable va && va.Name == var)
                    {
                        double a = ca.Value;
                        string denom = a >= 0 ? $"(s-{a})" : $"(s+{-a})";
                        return (true, $"1/{denom}");
                    }
                    if (exp1.Argument is Variable vexp && vexp.Name == var)
                        return (true, "1/(s-1)");
                }
                return (false, "");
            }
        },
        
        // Тригонометрические функции
        new LaplaceEntry
        {
            Pattern = "sin(ωt)",
            Transform = "ω/(s²+ω²)",
            Description = "Синус",
            Matcher = (expr, var) =>
            {
                if (expr is Sin sin)
                {
                    if (sin.Argument is Multiply multSin && multSin.Left is Constant csin &&
                        multSin.Right is Variable vsin && vsin.Name == var)
                    {
                        double omega = csin.Value;
                        return (true, $"{omega}/(s² + {omega * omega})");
                    }
                    if (sin.Argument is Variable vsinSimple && vsinSimple.Name == var)
                        return (true, "1/(s² + 1)");
                }
                return (false, "");
            }
        },

        new LaplaceEntry
        {
            Pattern = "cos(ωt)",
            Transform = "s/(s²+ω²)",
            Description = "Косинус",
            Matcher = (expr, var) =>
            {
                if (expr is Cos cos)
                {
                    if (cos.Argument is Multiply multCos && multCos.Left is Constant ccos &&
                        multCos.Right is Variable vcos && vcos.Name == var)
                    {
                        double omega = ccos.Value;
                        return (true, $"s/(s² + {omega * omega})");
                    }
                    if (cos.Argument is Variable vcosSimple && vcosSimple.Name == var)
                        return (true, "s/(s² + 1)");
                }
                return (false, "");
            }
        },
        
        // Гиперболические функции
        new LaplaceEntry
        {
            Pattern = "sinh(at)",
            Transform = "a/(s²-a²)",
            Description = "Гиперболический синус",
            Matcher = (expr, var) =>
            {
                if (expr is Sinh sinh)
                {
                    if (sinh.Argument is Multiply multSinh && multSinh.Left is Constant csinh &&
                        multSinh.Right is Variable vsinh && vsinh.Name == var)
                    {
                        double a = csinh.Value;
                        return (true, $"{a}/(s² - {a * a})");
                    }
                    if (sinh.Argument is Variable vsinhSimple && vsinhSimple.Name == var)
                        return (true, "1/(s² - 1)");
                }
                return (false, "");
            }
        },

        new LaplaceEntry
        {
            Pattern = "cosh(at)",
            Transform = "s/(s²-a²)",
            Description = "Гиперболический косинус",
            Matcher = (expr, var) =>
            {
                if (expr is Cosh cosh)
                {
                    if (cosh.Argument is Multiply multCosh && multCosh.Left is Constant ccosh &&
                        multCosh.Right is Variable vcosh && vcosh.Name == var)
                    {
                        double a = ccosh.Value;
                        return (true, $"s/(s² - {a * a})");
                    }
                    if (cosh.Argument is Variable vcoshSimple && vcoshSimple.Name == var)
                        return (true, "s/(s² - 1)");
                }
                return (false, "");
            }
        }
    };

    /// <summary>
    /// Применяет тригонометрические тождества для упрощения выражений
    /// </summary>
    private static Expression ApplyTrigIdentities(Expression expr, string variable)
    {
        // sin(ωt)·cos(ωt) = sin(2ωt)/2
        if (!ExpressionPatterns.TryMatchSinCosProduct(expr, out var argument))
            return expr;

        if (!ExpressionPatterns.TryMatchLinear(argument, variable, out double omega, out double shift) ||
            System.Math.Abs(shift) > 1e-12)
            return expr;

        return new Multiply(
            new Constant(0.5),
            new Sin(new Multiply(new Constant(2 * omega), new Variable(variable))));
    }

    /// <summary>
    /// Ищет преобразование Лапласа для выражения
    /// </summary>
    public static string Find(Expression expr, string variable = "t")
    {
        var simplified = ApplyTrigIdentities(expr, variable);
        var transform  = FindTransform(simplified, variable);

        return transform is null
            ? $"L{{{simplified}}} - не найдено в таблице"
            : $"L{{{simplified}}} = {transform}";
    }

    /// <summary>
    /// Возвращает правую часть F(s) или null, если образ не найден.
    /// Рекурсия работает с самим F(s), а не с готовой строкой «L{f} = F(s)».
    /// </summary>
    private static string? FindTransform(Expression expr, string variable)
    {
        foreach (var entry in _table)
        {
            var (matches, transform) = entry.Matcher(expr, variable);
            if (matches) return transform;
        }

        // Линейность: L{c·f} = c·F(s)
        if (expr is Multiply mult && mult.Left is Constant c)
        {
            var inner = FindTransform(mult.Right, variable);
            if (inner != null) return $"{c.Value}·({inner})";
        }

        // Линейность: L{f + g} = F(s) + G(s)
        if (expr is Add add)
        {
            var left  = FindTransform(add.Left,  variable);
            var right = FindTransform(add.Right, variable);
            if (left != null && right != null) return $"{left} + {right}";
        }

        return null;
    }

    /// <summary>
    /// Получает полную таблицу преобразований Лапласа
    /// </summary>
    public static string GetFullTable()
    {
        var sb = new StringBuilder();
        sb.AppendLine("+==============================================================+");
        sb.AppendLine("|         ТАБЛИЦА ПРЕОБРАЗОВАНИЙ ЛАПЛАСА L{f(t)} = F(s)       |");
        sb.AppendLine("+==============================================================+");
        sb.AppendLine();
        sb.AppendLine("+--------------------------------------------------------------+");
        sb.AppendLine("| БАЗОВЫЕ ФУНКЦИИ                                              |");
        sb.AppendLine("|--------------------------------------------------------------|");
        sb.AppendLine("| L{1} = 1/s                                                   |");
        sb.AppendLine("| L{t} = 1/s²                                                  |");
        sb.AppendLine("| L{t²} = 2/s³                                                 |");
        sb.AppendLine("| L{tⁿ} = n!/s^(n+1)                                           |");
        sb.AppendLine("+--------------------------------------------------------------+");
        sb.AppendLine();
        sb.AppendLine("+--------------------------------------------------------------+");
        sb.AppendLine("| ЭКСПОНЕНЦИАЛЬНЫЕ ФУНКЦИИ                                     |");
        sb.AppendLine("|--------------------------------------------------------------|");
        sb.AppendLine("| L{exp(at)} = 1/(s-a)                                         |");
        sb.AppendLine("| L{t·exp(at)} = 1/(s-a)²                                      |");
        sb.AppendLine("| L{tⁿ·exp(at)} = n!/(s-a)^(n+1)                               |");
        sb.AppendLine("+--------------------------------------------------------------+");
        sb.AppendLine();
        sb.AppendLine("+--------------------------------------------------------------+");
        sb.AppendLine("| ТРИГОНОМЕТРИЧЕСКИЕ ФУНКЦИИ                                   |");
        sb.AppendLine("|--------------------------------------------------------------|");
        sb.AppendLine("| L{sin(ωt)} = ω/(s² + ω²)                                     |");
        sb.AppendLine("| L{cos(ωt)} = s/(s² + ω²)                                     |");
        sb.AppendLine("| L{sin(ωt+φ)} = (ω·cos(φ) + s·sin(φ))/(s² + ω²)              |");
        sb.AppendLine("| L{t·sin(ωt)} = 2ωs/(s² + ω²)²                                |");
        sb.AppendLine("| L{t·cos(ωt)} = (s² - ω²)/(s² + ω²)²                          |");
        sb.AppendLine("| L{sin(ωt)·cos(ωt)} = ω/(s² + 4ω²)   [= L{sin(2ωt)/2}]       |");
        sb.AppendLine("+--------------------------------------------------------------+");
        sb.AppendLine();
        sb.AppendLine("+--------------------------------------------------------------+");
        sb.AppendLine("| ГИПЕРБОЛИЧЕСКИЕ ФУНКЦИИ                                      |");
        sb.AppendLine("|--------------------------------------------------------------|");
        sb.AppendLine("| L{sinh(at)} = a/(s² - a²)                                    |");
        sb.AppendLine("| L{cosh(at)} = s/(s² - a²)                                    |");
        sb.AppendLine("| L{t·sinh(at)} = 2as/(s² - a²)²                               |");
        sb.AppendLine("| L{t·cosh(at)} = (s² + a²)/(s² - a²)²                         |");
        sb.AppendLine("+--------------------------------------------------------------+");
        sb.AppendLine();
        sb.AppendLine("+--------------------------------------------------------------+");
        sb.AppendLine("| ПРОИЗВЕДЕНИЯ exp(at) с тригонометрическими функциями         |");
        sb.AppendLine("|--------------------------------------------------------------|");
        sb.AppendLine("| L{exp(at)·sin(ωt)} = ω/[(s-a)² + ω²]                         |");
        sb.AppendLine("| L{exp(at)·cos(ωt)} = (s-a)/[(s-a)² + ω²]                     |");
        sb.AppendLine("| L{exp(at)·sinh(bt)} = b/[(s-a)² - b²]                        |");
        sb.AppendLine("| L{exp(at)·cosh(bt)} = (s-a)/[(s-a)² - b²]                    |");
        sb.AppendLine("+--------------------------------------------------------------+");
        sb.AppendLine();
        sb.AppendLine("+--------------------------------------------------------------+");
        sb.AppendLine("| СВОЙСТВА ПРЕОБРАЗОВАНИЯ ЛАПЛАСА                              |");
        sb.AppendLine("|--------------------------------------------------------------|");
        sb.AppendLine("| ЛИНЕЙНОСТЬ: L{af(t) + bg(t)} = a·F(s) + b·G(s)              |");
        sb.AppendLine("| СДВИГ ПО s: L{exp(at)·f(t)} = F(s-a)                        |");
        sb.AppendLine("| СДВИГ ПО t: L{f(t-a)·H(t-a)} = exp(-as)·F(s)                |");
        sb.AppendLine("| ПРОИЗВОДНАЯ: L{f'(t)} = s·F(s) - f(0)                        |");
        sb.AppendLine("| ИНТЕГРАЛ: L{∫₀ᵗ f(τ)dτ} = F(s)/s                            |");
        sb.AppendLine("| МАСШТАБИРОВАНИЕ: L{f(at)} = (1/a)·F(s/a)                     |");
        sb.AppendLine("| УМНОЖЕНИЕ НА t: L{t·f(t)} = -F'(s)                           |");
        sb.AppendLine("| ДЕЛЕНИЕ НА t: L{f(t)/t} = ∫ₛ^∞ F(σ)dσ                       |");
        sb.AppendLine("+--------------------------------------------------------------+");
        sb.AppendLine();
        sb.AppendLine("ПРИМЕЧАНИЕ: H(t) - функция Хевисайда (единичная ступенька)");

        return sb.ToString();
    }
}

