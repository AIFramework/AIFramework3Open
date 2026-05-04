using System.Text;
using AI.HighLevelFunctions;
using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Solvers;

/// <summary>
/// Таблица преобразований Лапласа с поддержкой поиска и применения трансформаций
/// </summary>
public static class LaplaceTable
{
    // Структура для хранения преобразования
    private class LaplaceEntry
    {
        public string Pattern { get; set; } = "";
        public string Transform { get; set; } = "";
        public string Description { get; set; } = "";
        public Func<Expression, string, (bool matches, string result)> Matcher { get; set; } = null!;
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
                    return (true, "L{1} = 1/s");
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
                    return (true, $"L{{{var}}} = 1/s²");
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
                    long factorial = FunctionsForEachElements.Factorial(n);
                    return (true, $"L{{t^{n}}} = {factorial}/s^{n + 1}");
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
                        string expArg = a == 1 ? "exp(t)" : a == -1 ? "exp(-t)" : $"exp({a}t)";
                        return (true, $"L{{{expArg}}} = 1/{denom}");
                    }
                    if (exp1.Argument is Variable vexp && vexp.Name == var)
                        return (true, "L{exp(t)} = 1/(s-1)");
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
                        return (true, $"L{{sin({omega}t)}} = {omega}/(s² + {omega * omega})");
                    }
                    if (sin.Argument is Variable vsinSimple && vsinSimple.Name == var)
                        return (true, "L{sin(t)} = 1/(s² + 1)");
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
                        return (true, $"L{{cos({omega}t)}} = s/(s² + {omega * omega})");
                    }
                    if (cos.Argument is Variable vcosSimple && vcosSimple.Name == var)
                        return (true, "L{cos(t)} = s/(s² + 1)");
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
                        return (true, $"L{{sinh({a}t)}} = {a}/(s² - {a * a})");
                    }
                    if (sinh.Argument is Variable vsinhSimple && vsinhSimple.Name == var)
                        return (true, "L{sinh(t)} = 1/(s² - 1)");
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
                        return (true, $"L{{cosh({a}t)}} = s/(s² - {a * a})");
                    }
                    if (cosh.Argument is Variable vcoshSimple && vcoshSimple.Name == var)
                        return (true, "L{cosh(t)} = s/(s² - 1)");
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
        // sin(t)*cos(t) = sin(2t)/2
        if (expr is Multiply mult)
        {
            Sin? sinPart = null;
            Cos? cosPart = null;

            if (mult.Left is Sin s1) sinPart = s1;
            else if (mult.Right is Sin s2) sinPart = s2;

            if (mult.Left is Cos c1) cosPart = c1;
            else if (mult.Right is Cos c2) cosPart = c2;

            // sin(ωt)*cos(ωt) = sin(2ωt)/2
            if (sinPart != null && cosPart != null)
            {
                var sinArg = sinPart.Argument;
                var cosArg = cosPart.Argument;

                // Проверяем, что аргументы одинаковые
                if (sinArg.ToString() == cosArg.ToString())
                {
                    // sin(ωt)*cos(ωt) = sin(2ωt)/2
                    Expression newArg;
                    if (sinArg is Variable v && v.Name == variable)
                    {
                        // sin(t)*cos(t) = sin(2t)/2
                        newArg = new Multiply(new Constant(2), new Variable(variable));
                    }
                    else if (sinArg is Multiply m && m.Left is Constant c && m.Right is Variable vv && vv.Name == variable)
                    {
                        // sin(ωt)*cos(ωt) = sin(2ωt)/2
                        double omega = c.Value;
                        newArg = new Multiply(new Constant(2 * omega), new Variable(variable));
                    }
                    else
                    {
                        return expr; // Не можем упростить
                    }

                    return new Multiply(new Constant(0.5), new Sin(newArg));
                }
            }

  
        }

        return expr;
    }

    /// <summary>
    /// Ищет преобразование Лапласа для выражения
    /// </summary>
    public static string Find(Expression expr, string variable = "t")
    {
        var simplified = ApplyTrigIdentities(expr, variable);

        foreach (var entry in _table)
        {
            var (matches, result) = entry.Matcher(simplified, variable);
            if (matches)
                return result;
        }

        // Обработка произведений с константами
        if (simplified is Multiply mult && mult.Left is Constant c)
        {
            var innerResult = Find(mult.Right, variable);
            if (!innerResult.Contains("требуется") && !innerResult.Contains("не удалось"))
            {
                // Извлекаем преобразование из строки результата
                var parts = innerResult.Split('=');
                if (parts.Length == 2)
                {
                    var transform = parts[1].Trim();
                    return $"L{{{simplified}}} = {c.Value}·{transform}  (по линейности)";
                }
            }
        }

        // Обработка суммы (линейность преобразования Лапласа)
        if (simplified is Add add)
        {
            var leftResult = Find(add.Left, variable);
            var rightResult = Find(add.Right, variable);

            if (!leftResult.Contains("требуется") && !rightResult.Contains("требуется") &&
                !leftResult.Contains("не удалось") && !rightResult.Contains("не удалось"))
            {
                var leftParts = leftResult.Split('=');
                var rightParts = rightResult.Split('=');

                if (leftParts.Length == 2 && rightParts.Length == 2)
                {
                    var leftTransform = leftParts[1].Trim().Split("(")[0].Trim();
                    var rightTransform = rightParts[1].Trim().Split("(")[0].Trim();

                    return $"L{{{simplified}}} = {leftTransform} + {rightTransform}  (по линейности)";
                }
            }
        }

        return $"L{{{simplified}}} - не найдено в таблице";
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

