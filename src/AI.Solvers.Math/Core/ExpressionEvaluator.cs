using AI.HighLevelFunctions;
using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Integrations;
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
        // Это устраняет дублирование: единая численная реализация erf/erfc
        Erf  erf  => FunctionsForEachElements.Erf(Evaluate(erf.Argument,  variables)),
        Erfc erc  => 1.0 - FunctionsForEachElements.Erf(Evaluate(erc.Argument, variables)),

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
        // Числовое интегрирование методом трапеций для больших x
        return TrapezoidalIntegral(t => System.Math.Sin(t) / t, 1e-10, x, 1000);
    }

    private static double NumericalCi(double x)
    {
        // Интегральный косинус Ci(x) = γ + ln(x) + ∫₀ˣ (cos(t)-1)/t dt
        if (x <= 0) return double.NegativeInfinity;
        const double EulerMascheroni = 0.5772156649015329;
        double integral = TrapezoidalIntegral(t => (System.Math.Cos(t) - 1) / t, 1e-10, x, 1000);
        return EulerMascheroni + System.Math.Log(x) + integral;
    }

    private static double NumericalEi(double x)
    {
        // Интегральная показательная функция Ei(x) = P.V. ∫_{-∞}^x e^t/t dt
        if (x <= 0) return double.NegativeInfinity;
        const double EulerMascheroni = 0.5772156649015329;
        // Для малых x: Ei(x) ≈ γ + ln(|x|) + x + x²/4 + x³/18 + ...
        if (System.Math.Abs(x) < 3)
        {
            double result = EulerMascheroni + System.Math.Log(System.Math.Abs(x));
            double term = x, xn = x;
            for (int n = 1; n <= 20; n++)
            {
                result += xn / (n * n);
                xn *= x / (n + 1);
            }
            return result;
        }
        return TrapezoidalIntegral(t => System.Math.Exp(t) / t, -100.0, x, 2000);
    }

    private static double TrapezoidalIntegral(Func<double, double> f, double a, double b, int n)
    {
        double h = (b - a) / n, sum = 0;
        for (int i = 1; i < n; i++)
            sum += f(a + i * h);
        return h * (0.5 * f(a) + sum + 0.5 * f(b));
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
        return TrapezoidalIntegral(t => System.Math.Sin(System.Math.PI * t * t / 2), 0, x, 4096);
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
        return TrapezoidalIntegral(t => System.Math.Cos(System.Math.PI * t * t / 2), 0, x, 4096);
    }

    private static double NumericalLi(double x)
    {
        // li(x) = ∫₀ˣ dt/ln(t).  Для x>1 — главное значение Коши, обходящее особенность в t=1.
        if (x <= 0) return double.NaN;
        if (System.Math.Abs(x - 1.0) < 1e-12) return double.NegativeInfinity;
        const double eps = 1e-6;
        if (x < 1)
        {
            return TrapezoidalIntegral(t => 1.0 / System.Math.Log(t), 1e-10, x, 4096);
        }
        // Главное значение: интеграл от 0 до 1-eps плюс от 1+eps до x.
        double left  = TrapezoidalIntegral(t => 1.0 / System.Math.Log(t), 1e-10, 1.0 - eps, 4096);
        double right = TrapezoidalIntegral(t => 1.0 / System.Math.Log(t), 1.0 + eps, x,       4096);
        return left + right;
    }

    private static double NumericalNonElementary(NonElementary ne, Dictionary<string, double> variables)
    {
        // Числовая оценка определённого интеграла подынтегральной функции
        // от 0 до текущего значения переменной интегрирования.
        if (!variables.TryGetValue(ne.Variable, out double upper))
            throw new ArgumentException(
                $"Для NonElementary требуется значение переменной интегрирования '{ne.Variable}'.");
        var localVars = new Dictionary<string, double>(variables);
        return TrapezoidalIntegral(t =>
        {
            localVars[ne.Variable] = t;
            return Evaluate(ne.Integrand, localVars);
        }, 0.0, upper, 2000);
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
