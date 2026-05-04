using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Integrations;

public static partial class AdvancedIntegrationEngine
{
    #region Точка входа

    public static Expression Integrate(Expression expr, string variable)
    {
        expr = expr.Simplify();

        var result = TryBasicIntegration(expr, variable);
        if (result != null) return result.Simplify();

        result = TryTrigonometricPowers(expr, variable);
        if (result != null) return result.Simplify();

        result = TryTableIntegration(expr, variable);
        if (result != null) return result.Simplify();

        result = TryIntegrationByParts(expr, variable);
        if (result != null) return result.Simplify();

        result = TrySubstitution(expr, variable);
        if (result != null) return result.Simplify();

        result = TrySpecialFunctions(expr, variable);
        if (result != null) return result.Simplify();

        return new UnevaluatedIntegral(expr, variable);
    }

    #endregion

    #region Базовые интегралы

    private static Expression? TryBasicIntegration(Expression expr, string variable)
    {
        var x = new Variable(variable);

        // ∫ c dx = c·x
        if (expr is Constant c)
            return new Multiply(c, x);

        // ∫ x dx = x²/2
        if (expr is Variable v && v.Name == variable)
            return new Multiply(new Constant(0.5), new Power(v, new Constant(2)));

        // ∫ y dx = y·x (другая переменная)
        if (expr is Variable v2)
            return new Multiply(v2, x);

        // ∫ (f+g) dx = ∫f + ∫g
        if (expr is Add add)
            return new Add(Integrate(add.Left, variable), Integrate(add.Right, variable));

        // ∫ c·f dx = c·∫f
        if (expr is Multiply mult)
        {
            if (mult.Left  is Constant c1) return new Multiply(c1, Integrate(mult.Right, variable));
            if (mult.Right is Constant c2) return new Multiply(c2, Integrate(mult.Left,  variable));
        }

        // ∫ x^n dx = x^(n+1)/(n+1), ∫ x^(-1) = ln|x|
        if (expr is Power pow && pow.Base is Variable vb && vb.Name == variable)
        {
            if (pow.Exponent is Constant exp)
            {
                if (System.Math.Abs(exp.Value + 1) < 1e-10)
                    return new Ln(x);
                return new Multiply(
                    new Constant(1.0 / (exp.Value + 1)),
                    new Power(x, new Constant(exp.Value + 1)));
            }
        }

        // ∫ 1/x dx = ln|x|
        if (expr is Power pow2 && pow2.Exponent is Constant ce && ce.Value == -1 &&
            pow2.Base is Variable vp && vp.Name == variable)
            return new Ln(x);

        // Тригонометрические и гиперболические
        if (expr is Sin sin && sin.Argument is Variable vs && vs.Name == variable)
            return new Multiply(new Constant(-1), new Cos(x));
        if (expr is Cos cos && cos.Argument is Variable vc && vc.Name == variable)
            return new Sin(x);
        if (expr is Tan tan && tan.Argument is Variable vt && vt.Name == variable)
            return new Multiply(new Constant(-1), new Ln(new Cos(x)));
        if (expr is Cot cot && cot.Argument is Variable vct && vct.Name == variable)
            return new Ln(new Sin(x));
        if (expr is Sec sec && sec.Argument is Variable vsec && vsec.Name == variable)
            return new Ln(new Add(new Sec(x), new Tan(x)));
        if (expr is Csc csc && csc.Argument is Variable vcsc && vcsc.Name == variable)
            return new Multiply(new Constant(-1), new Ln(new Add(new Csc(x), new Cot(x))));

        if (expr is Sinh sinh && sinh.Argument is Variable vsh && vsh.Name == variable) return new Cosh(x);
        if (expr is Cosh cosh && cosh.Argument is Variable vch && vch.Name == variable) return new Sinh(x);
        if (expr is Tanh tanh && tanh.Argument is Variable vth && vth.Name == variable) return new Ln(new Cosh(x));

        // ∫ e^x dx = e^x
        if (expr is Exp exp2 && exp2.Argument is Variable ve && ve.Name == variable)
            return exp2;

        // ∫ ln(x) dx = x·ln(x) - x
        if (expr is Ln ln && ln.Argument is Variable vl && vl.Name == variable)
            return new Add(new Multiply(x, new Ln(x)), new Multiply(new Constant(-1), x));

        // ∫ sec²(x) dx = tan(x)
        if (expr is Power pow3 &&
            pow3.Base is Sec sec2 &&
            pow3.Exponent is Constant exp3 && System.Math.Abs(exp3.Value - 2) < 1e-10 &&
            sec2.Argument is Variable vsec2 && vsec2.Name == variable)
            return new Tan(x);

        // ∫ asin(x) dx = x·asin(x) + sqrt(1 - x²)
        if (expr is Asin asin && asin.Argument is Variable va && va.Name == variable)
            return new Add(
                new Multiply(x, new Asin(x)),
                new Power(new Add(new Constant(1), new Multiply(new Constant(-1), new Power(x, new Constant(2)))), new Constant(0.5)));

        // ∫ acos(x) dx = x·acos(x) - sqrt(1 - x²)
        if (expr is Acos acos && acos.Argument is Variable vac && vac.Name == variable)
            return new Add(
                new Multiply(x, new Acos(x)),
                new Multiply(new Constant(-1),
                    new Power(new Add(new Constant(1), new Multiply(new Constant(-1), new Power(x, new Constant(2)))), new Constant(0.5))));

        // ∫ atan(x) dx = x·atan(x) - 0.5·ln(1 + x²)
        if (expr is Atan atan && atan.Argument is Variable vat && vat.Name == variable)
            return new Add(
                new Multiply(x, new Atan(x)),
                new Multiply(new Constant(-0.5), new Ln(new Add(new Constant(1), new Power(x, new Constant(2))))));

        // ∫ asinh(x) dx = x·asinh(x) - sqrt(x² + 1)
        if (expr is Asinh asinh && asinh.Argument is Variable vash && vash.Name == variable)
            return new Add(
                new Multiply(x, new Asinh(x)),
                new Multiply(new Constant(-1), new Power(new Add(new Power(x, new Constant(2)), new Constant(1)), new Constant(0.5))));

        // ∫ acosh(x) dx = x·acosh(x) - sqrt(x² - 1)
        if (expr is Acosh acosh && acosh.Argument is Variable vach && vach.Name == variable)
            return new Add(
                new Multiply(x, new Acosh(x)),
                new Multiply(new Constant(-1), new Power(new Add(new Power(x, new Constant(2)), new Constant(-1)), new Constant(0.5))));

        // ∫ atanh(x) dx = x·atanh(x) + 0.5·ln(1 - x²)
        if (expr is Atanh atanh && atanh.Argument is Variable vath && vath.Name == variable)
            return new Add(
                new Multiply(x, new Atanh(x)),
                new Multiply(new Constant(0.5), new Ln(new Add(new Constant(1), new Multiply(new Constant(-1), new Power(x, new Constant(2)))))));

        // ∫ log10(x) dx = x·(log10(x) - log10(e))
        if (expr is Log10 log10 && log10.Argument is Variable vl10 && vl10.Name == variable)
            return new Multiply(x, new Add(new Log10(x), new Constant(-System.Math.Log10(System.Math.E))));

        // ∫ log_b(x) dx = x·(log_b(x) - log_b(e))
        if (expr is Log log && log.Argument is Variable vlg && vlg.Name == variable)
            return new Multiply(x, new Add(new Log(log.Base, x), new Multiply(new Constant(-1), new Log(log.Base, new Constant(System.Math.E)))));

        return null;
    }

    #endregion

    #region Вспомогательный: проверка линейности ax+b

    internal static bool IsLinearInVariable(Expression expr, string variable, out double a, out double b)
    {
        a = 0; b = 0;

        if (expr is Variable v && v.Name == variable) { a = 1; return true; }

        if (expr is Multiply mult)
        {
            if (mult.Left  is Constant c1 && mult.Right is Variable v2 && v2.Name == variable) { a = c1.Value; return System.Math.Abs(a) > 1e-12; }
            if (mult.Right is Constant c2 && mult.Left  is Variable v3 && v3.Name == variable) { a = c2.Value; return System.Math.Abs(a) > 1e-12; }
        }

        if (expr is Add add)
        {
            bool l = IsLinearInVariable(add.Left,  variable, out double a1, out double b1);
            bool r = IsLinearInVariable(add.Right, variable, out double a2, out double b2);
            if (l && r)
            {
                a = a1 + a2; b = b1 + b2;
                // Линейность с нулевым коэффициентом — фактически константа,
                // непригодна для табличных интегралов с множителем 1/a.
                return System.Math.Abs(a) > 1e-12;
            }
        }

        if (expr is Constant c3) { a = 0; b = c3.Value; return true; }

        return false;
    }

    #endregion
}
