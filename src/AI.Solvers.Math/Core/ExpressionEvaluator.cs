using AI.HighLevelFunctions;
using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Integrations;
using AI.Solvers.Math.Core.Numerics;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core;

/// <summary>
/// Единственная точка для численного вычисления символьных выражений (AST).
/// Поддерживает все узлы Expression и делегирует спецфункции к
/// <see cref="FunctionsForEachElements"/> из основного фреймворка.
/// </summary>
public static class ExpressionEvaluator
{
    /// <summary>
    /// Вычисляет числовое значение выражения при заданных значениях переменных.
    /// </summary>
    /// <param name="expr">Символьное выражение (AST-узел)</param>
    /// <param name="variables">Словарь значений переменных</param>
    /// <returns>Числовой результат</returns>
    public static double Evaluate(Expression expr, Dictionary<string, double> variables) => expr switch
    {
        // -- Примитивы ------------------------------------------------------
        Constant c  => c.Value,
        Variable v  => variables.TryGetValue(v.Name, out var val) ? val
                       : throw new ArgumentException($"Переменная '{v.Name}' не определена"),

        // -- Арифметика ----------------------------------------------------
        Add      a  => Evaluate(a.Left,      variables) + Evaluate(a.Right,      variables),
        Multiply m  => Evaluate(m.Left,      variables) * Evaluate(m.Right,      variables),
        Divide   d  => Evaluate(d.Numerator, variables) / Evaluate(d.Denominator, variables),
        Power    p  => System.Math.Pow(Evaluate(p.Base, variables), Evaluate(p.Exponent, variables)),
        Abs      ab => System.Math.Abs(Evaluate(ab.Argument, variables)),

        // -- Тригонометрия -------------------------------------------------
        Sin  s  => System.Math.Sin(Evaluate(s.Argument,  variables)),
        Cos  c  => System.Math.Cos(Evaluate(c.Argument,  variables)),
        Tan  t  => System.Math.Tan(Evaluate(t.Argument,  variables)),
        Cot  ct => 1.0 / System.Math.Tan(Evaluate(ct.Argument, variables)),
        Sec  sc => 1.0 / System.Math.Cos(Evaluate(sc.Argument, variables)),
        Csc  cs => 1.0 / System.Math.Sin(Evaluate(cs.Argument, variables)),

        // -- Обратные тригонометрические -----------------------------------
        Asin a  => System.Math.Asin(Evaluate(a.Argument, variables)),
        Acos a  => System.Math.Acos(Evaluate(a.Argument, variables)),
        Atan a  => System.Math.Atan(Evaluate(a.Argument, variables)),

        // -- Гиперболические -----------------------------------------------
        Sinh  sh => System.Math.Sinh(Evaluate(sh.Argument,  variables)),
        Cosh  ch => System.Math.Cosh(Evaluate(ch.Argument,  variables)),
        Tanh  th => System.Math.Tanh(Evaluate(th.Argument,  variables)),
        Asinh ah => System.Math.Asinh(Evaluate(ah.Argument, variables)),
        Acosh ah => System.Math.Acosh(Evaluate(ah.Argument, variables)),
        Atanh ah => System.Math.Atanh(Evaluate(ah.Argument, variables)),

        // -- Показательные и логарифмы -------------------------------------
        Exp   e   => System.Math.Exp(Evaluate(e.Argument,   variables)),
        Ln    l   => System.Math.Log(Evaluate(l.Argument,   variables)),
        Log10 l10 => System.Math.Log10(Evaluate(l10.Argument, variables)),
        Log   lg  => System.Math.Log(Evaluate(lg.Argument, variables), Evaluate(lg.Base, variables)),

        // -- Спецфункции — делегируем к FunctionsForEachElements ----------
        // Это устраняет дублирование: единая численная реализация erf/erfc.
        // erfc берём готовым, а не как 1 - erf: на хвосте разность вырождается в ноль.
        Erf  erf  => FunctionsForEachElements.Erf(Evaluate(erf.Argument,  variables)),
        Erfc erc  => FunctionsForEachElements.Erfc(Evaluate(erc.Argument, variables)),

        // -- Прочие специальные функции ------------------------------------
        Sgn      sg => System.Math.Sign(Evaluate(sg.Argument, variables)),
        Heaviside hv => Evaluate(hv.Argument, variables) >= 0 ? 1.0 : 0.0,

        // -- Нелементарные интегральные функции (численное приближение) ----
        // Si(x) ≈ ∫₀ˣ sin(t)/t dt  (через разложение в ряд для малых x)
        Si si => NumericalSi(Evaluate(si.Argument, variables)),
        Ci ci => NumericalCi(Evaluate(ci.Argument, variables)),
        Ei ei => NumericalEi(Evaluate(ei.Argument, variables)),

        // -- Интегралы Френеля и логарифмический интеграл ------------------
        // S(x) = ∫₀ˣ sin(πt²/2) dt,  C(x) = ∫₀ˣ cos(πt²/2) dt
        // li(x) = ∫₀ˣ dt/ln(t) (P.V. для x > 1)
        FresnelS fs => NumericalFresnelS(Evaluate(fs.Argument, variables)),
        FresnelC fc => NumericalFresnelC(Evaluate(fc.Argument, variables)),
        Li       li => NumericalLi(Evaluate(li.Argument, variables)),

        // -- Неберущийся интеграл — численная квадратура подынтегрального ---
        NonElementary ne => NumericalNonElementary(ne, variables),

        // -- Символьный интеграл, оставшийся невычисленным ---------------
        UnevaluatedIntegral ui => throw new NotSupportedException(
            $"Невозможно вычислить числовое значение невычисленного интеграла ∫{ui.Integrand} d{ui.Variable}. " +
            "Выражение содержит символьный интеграл, не имеющий замкнутой формы."),

        // -- Комплексная константа — возвращаем действительную часть ------
        ComplexConstant cc => cc.Value.Real,

        // -- Неизвестный тип -----------------------------------------------
        _ => throw new NotImplementedException($"Численное вычисление узла '{expr.GetType().Name}' не реализовано")
    };

    // -- Численные приближения для нелементарных функций ------------------

    private static double NumericalSi(double x)
    {
        // Интегральный синус Si(x) = ∫₀ˣ sin(t)/t dt
        // Разложение в ряд: Si(x) = Σ (-1)^n * x^(2n+1) / ((2n+1)*(2n+1)!)
        if (System.Math.Abs(x) < 1e-10) return 0;
        if (System.Math.Abs(x) < 4)
        {
            double result = 0, term = x, xSq = x * x;
            for (int n = 0; n < 20; n++)
            {
                result += term / (2 * n + 1);
                term *= -xSq / ((2 * n + 2) * (2 * n + 3));
            }
            return result;
        }
        // Для больших x ряд теряет разряды — интегрируем численно
        return Quadrature.Integrate(t => System.Math.Sin(t) / t, 0, x);
    }

    private static double NumericalCi(double x)
    {
        // Интегральный косинус Ci(x) = γ + ln(x) + ∫₀ˣ (cos(t)-1)/t dt
        if (x <= 0) return double.NegativeInfinity;
        double integral = Quadrature.Integrate(t => (System.Math.Cos(t) - 1) / t, 0, x);
        return EulerMascheroni + System.Math.Log(x) + integral;
    }

    private const double EulerMascheroni = 0.5772156649015329;

    private static double NumericalEi(double x)
    {
        // Интегральная показательная функция Ei(x) = P.V. ∫_{-∞}^x e^t/t dt.
        // Для x < 0 связана с E₁: Ei(x) = -E₁(-x); квадратурой считать нельзя —
        // путь интегрирования проходит через полюс в нуле.
        if (x == 0) return double.NegativeInfinity;
        if (x < 0) return -ExponentialIntegralE1(-x);

        // Сходящийся ряд Ei(x) = γ + ln|x| + Σ_{n≥1} xⁿ/(n·n!).
        // Слагаемое — именно xⁿ/n!, делённое на n (а не xⁿ/(n+1)!, как было).
        if (x <= 20)
        {
            double result = EulerMascheroni + System.Math.Log(x);
            double term = 1.0;
            for (int n = 1; n <= 200; n++)
            {
                term *= x / n;                     // term = xⁿ/n!
                double add = term / n;
                result += add;
                if (System.Math.Abs(add) < 1e-16 * System.Math.Abs(result)) break;
            }
            return result;
        }

        // Асимптотика Ei(x) ≈ (eˣ/x)·Σ k!/xᵏ — обрываем на наименьшем члене.
        double sum = 1.0, t = 1.0;
        for (int k = 1; k <= 60; k++)
        {
            double next = t * k / x;
            if (System.Math.Abs(next) >= System.Math.Abs(t)) break;
            t = next;
            sum += t;
        }
        return System.Math.Exp(x) / x * sum;
    }

    /// <summary>
    /// E₁(y) = ∫_y^∞ e^(-t)/t dt для y &gt; 0: ряд при малых y, цепная дробь (Лентц) при больших.
    /// Знакопеременный ряд при y &gt; 1 теряет значащие разряды, поэтому там нужна дробь.
    /// </summary>
    private static double ExponentialIntegralE1(double y)
    {
        if (y <= 0) return double.PositiveInfinity;

        if (y <= 1.0)
        {
            double sum = -EulerMascheroni - System.Math.Log(y);
            double term = 1.0;
            for (int n = 1; n <= 100; n++)
            {
                term *= -y / n;                    // term = (-y)ⁿ/n!
                double add = -term / n;            // -(-y)ⁿ/(n·n!)
                sum += add;
                if (System.Math.Abs(add) < 1e-16 * System.Math.Abs(sum)) break;
            }
            return sum;
        }

        // Модифицированный алгоритм Лентца для E₁(y) = e^(-y)·1/(y+1- 1²/(y+3- 2²/(y+5- …)))
        const double tiny = 1e-300;
        double b = y + 1.0, c = 1.0 / tiny, d = 1.0 / b, h = d;
        for (int i = 1; i <= 200; i++)
        {
            double an = -i * (double)i;
            b += 2.0;
            d = 1.0 / (an * d + b);
            c = b + an / c;
            double delta = c * d;
            h *= delta;
            if (System.Math.Abs(delta - 1.0) < 1e-15) break;
        }
        return h * System.Math.Exp(-y);
    }

    private static double NumericalFresnelS(double x)
    {
        // S(x) = ∫₀ˣ sin(π t² / 2) dt
        if (System.Math.Abs(x) < 1e-12) return 0;
        // Разложение в ряд (быстро сходится для |x| <= ~3): Σ (-1)^n (π/2)^(2n+1) x^(4n+3) / ((2n+1)!(4n+3))
        if (System.Math.Abs(x) <= 3)
        {
            double pi2 = System.Math.PI / 2;
            double sum = 0;
            double sign = 1;
            double pow = pi2 * x * x * x;
            double fact = 1;
            for (int n = 0; n < 30; n++)
            {
                sum += sign * pow / (fact * (4 * n + 3));
                sign = -sign;
                pow *= pi2 * pi2 * x * x * x * x;
                fact *= (2 * n + 2) * (2 * n + 3);
            }
            return sum;
        }
        return Quadrature.Integrate(t => System.Math.Sin(System.Math.PI * t * t / 2), 0, x);
    }

    private static double NumericalFresnelC(double x)
    {
        // C(x) = ∫₀ˣ cos(π t² / 2) dt
        if (System.Math.Abs(x) < 1e-12) return 0;
        if (System.Math.Abs(x) <= 3)
        {
            double pi2 = System.Math.PI / 2;
            double sum = 0;
            double sign = 1;
            double pow = x;
            double fact = 1;
            for (int n = 0; n < 30; n++)
            {
                sum += sign * pow / (fact * (4 * n + 1));
                sign = -sign;
                pow *= pi2 * pi2 * x * x * x * x;
                fact *= (2 * n + 1) * (2 * n + 2);
            }
            return sum;
        }
        return Quadrature.Integrate(t => System.Math.Cos(System.Math.PI * t * t / 2), 0, x);
    }

    private static double NumericalLi(double x)
    {
        // li(x) = P.V. ∫₀ˣ dt/ln(t) = Ei(ln x).  Квадратура «слева и справа от t=1»
        // не даёт главного значения: расходимости ±∞ сокращаются только на согласованных
        // сетках, а при разном шаге дают произвольный результат.
        if (x <= 0) return double.NaN;
        if (System.Math.Abs(x - 1.0) < 1e-12) return double.NegativeInfinity;
        return NumericalEi(System.Math.Log(x));
    }

    private static double NumericalNonElementary(NonElementary ne, Dictionary<string, double> variables)
    {
        // Числовая оценка определённого интеграла подынтегральной функции
        // от 0 до текущего значения переменной интегрирования.
        if (!variables.TryGetValue(ne.Variable, out double upper))
            throw new ArgumentException(
                $"Для NonElementary требуется значение переменной интегрирования '{ne.Variable}'.");
        var localVars = new Dictionary<string, double>(variables);
        return Quadrature.Integrate(t =>
        {
            localVars[ne.Variable] = t;
            return Evaluate(ne.Integrand, localVars);
        }, 0.0, upper);
    }

    /// <summary>
    /// Обходит выражение и собирает имена всех встречающихся переменных.
    /// </summary>
    public static void CollectVariables(Expression expr, HashSet<string> variables)
    {
        switch (expr)
        {
            case Variable v:   variables.Add(v.Name); break;
            case Add      a:   CollectVariables(a.Left,      variables); CollectVariables(a.Right,      variables); break;
            case Multiply m:   CollectVariables(m.Left,      variables); CollectVariables(m.Right,      variables); break;
            case Divide   d:   CollectVariables(d.Numerator, variables); CollectVariables(d.Denominator, variables); break;
            case Power    p:   CollectVariables(p.Base,      variables); CollectVariables(p.Exponent,   variables); break;
            default:
                // Для всех унарных функций — обходим через рефлексию-like подход
                var argProp = expr.GetType().GetProperty("Argument");
                if (argProp?.GetValue(expr) is Expression arg)
                    CollectVariables(arg, variables);
                break;
        }
    }
}
