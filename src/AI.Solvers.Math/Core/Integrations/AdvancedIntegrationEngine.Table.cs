using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Integrations;

public static partial class AdvancedIntegrationEngine
{
    #region Табличные интегралы (ax+b линейная подстановка)

    private static Expression? TryTableIntegration(Expression expr, string variable)
    {
        var x = new Variable(variable);

        // ∫ exp(ax+b) dx = (1/a)·exp(ax+b)
        if (expr is Exp exp && IsLinearInVariable(exp.Argument, variable, out var aE, out _) && System.Math.Abs(aE) > 1e-12)
            return new Multiply(new Constant(1.0 / aE), new Exp(exp.Argument));

        // ∫ sin(ax+b) dx = -(1/a)·cos(ax+b)
        if (expr is Sin sin && IsLinearInVariable(sin.Argument, variable, out var aS, out _) && System.Math.Abs(aS) > 1e-12)
            return new Multiply(new Constant(-1.0 / aS), new Cos(sin.Argument));

        // ∫ cos(ax+b) dx = (1/a)·sin(ax+b)
        if (expr is Cos cos && IsLinearInVariable(cos.Argument, variable, out var aC, out _) && System.Math.Abs(aC) > 1e-12)
            return new Multiply(new Constant(1.0 / aC), new Sin(cos.Argument));

        // ∫ tan(ax+b) dx = -(1/a)·ln|cos(ax+b)|
        if (expr is Tan tan && IsLinearInVariable(tan.Argument, variable, out var aT, out _) && System.Math.Abs(aT) > 1e-12)
            return new Multiply(new Constant(-1.0 / aT), new Ln(new Abs(new Cos(tan.Argument))));

        // ∫ cot(ax+b) dx = (1/a)·ln|sin(ax+b)|
        if (expr is Cot cot && IsLinearInVariable(cot.Argument, variable, out var aCt, out _) && System.Math.Abs(aCt) > 1e-12)
            return new Multiply(new Constant(1.0 / aCt), new Ln(new Abs(new Sin(cot.Argument))));

        // ∫ sec(ax+b) dx = (1/a)·ln|sec(ax+b)+tan(ax+b)|
        if (expr is Sec sec && IsLinearInVariable(sec.Argument, variable, out var aSec, out _) && System.Math.Abs(aSec) > 1e-12)
            return new Multiply(new Constant(1.0 / aSec),
                new Ln(new Abs(new Add(new Sec(sec.Argument), new Tan(sec.Argument)))));

        // ∫ csc(ax+b) dx = -(1/a)·ln|csc(ax+b)+cot(ax+b)|
        if (expr is Csc csc && IsLinearInVariable(csc.Argument, variable, out var aCsc, out _) && System.Math.Abs(aCsc) > 1e-12)
            return new Multiply(new Constant(-1.0 / aCsc),
                new Ln(new Abs(new Add(new Csc(csc.Argument), new Cot(csc.Argument)))));

        // ∫ sinh(ax+b) dx = (1/a)·cosh(ax+b)
        if (expr is Sinh sinh && IsLinearInVariable(sinh.Argument, variable, out var aSh, out _) && System.Math.Abs(aSh) > 1e-12)
            return new Multiply(new Constant(1.0 / aSh), new Cosh(sinh.Argument));

        // ∫ cosh(ax+b) dx = (1/a)·sinh(ax+b)
        if (expr is Cosh cosh && IsLinearInVariable(cosh.Argument, variable, out var aCh, out _) && System.Math.Abs(aCh) > 1e-12)
            return new Multiply(new Constant(1.0 / aCh), new Sinh(cosh.Argument));

        // ∫ tanh(ax+b) dx = (1/a)·ln(cosh(ax+b))
        if (expr is Tanh tanh && IsLinearInVariable(tanh.Argument, variable, out var aTh, out _) && System.Math.Abs(aTh) > 1e-12)
            return new Multiply(new Constant(1.0 / aTh), new Ln(new Cosh(tanh.Argument)));

        return TryTableRational(expr, variable, x);
    }

    private static Expression? TryTableRational(Expression expr, string variable, Variable x)
    {
        // ∫ 1/(a²+x²) dx = (1/a)·atan(x/a)
        if (expr is Power pow1 && pow1.Exponent is Constant ce1 && System.Math.Abs(ce1.Value + 1) < 1e-10)
        {
            if (pow1.Base is Add addBase &&
                addBase.Left is Constant c1 && c1.Value > 0 &&
                addBase.Right is Power pw2 &&
                pw2.Base is Variable v && v.Name == variable &&
                pw2.Exponent is Constant ce2 && System.Math.Abs(ce2.Value - 2) < 1e-10)
            {
                double a = System.Math.Sqrt(c1.Value);
                return new Multiply(new Constant(1.0 / a), new Atan(new Multiply(new Constant(1.0 / a), x)));
            }

            // ∫ 1/(ax+b) dx = (1/a)·ln|ax+b|
            if (IsLinearInVariable(pow1.Base, variable, out var aL, out _) && System.Math.Abs(aL) > 1e-12)
                return new Multiply(new Constant(1.0 / aL), new Ln(new Abs(pow1.Base)));
        }

        // ∫ 1/sqrt(a²-x²) dx = arcsin(x/a)
        if (expr is Power pow3 && pow3.Exponent is Constant ce3 && System.Math.Abs(ce3.Value + 0.5) < 1e-10)
        {
            if (pow3.Base is Add add2 &&
                add2.Left is Constant c2 && c2.Value > 0 &&
                add2.Right is Multiply mult &&
                mult.Left is Constant c3 && c3.Value < 0 &&
                mult.Right is Power pow4 &&
                pow4.Base is Variable v2 && v2.Name == variable &&
                pow4.Exponent is Constant ce4 && System.Math.Abs(ce4.Value - 2) < 1e-10)
            {
                double a = System.Math.Sqrt(c2.Value);
                return new Asin(new Multiply(new Constant(1.0 / a), x));
            }
        }

        // ∫ (ax+b)^n dx = (ax+b)^(n+1)/(a(n+1))
        if (expr is Power pow5 && pow5.Exponent is Constant expN)
        {
            if (IsLinearInVariable(pow5.Base, variable, out var a5, out _) && System.Math.Abs(a5) > 1e-12)
            {
                double n = expN.Value;
                if (System.Math.Abs(n + 1) > 1e-10)
                    return new Multiply(new Constant(1.0 / (a5 * (n + 1))), new Power(pow5.Base, new Constant(n + 1)));
            }
        }

        // ∫ ln(x) dx = x·ln(x) - x
        if (expr is Ln ln && ln.Argument is Variable v3 && v3.Name == variable)
            return new Add(new Multiply(x, new Ln(x)), new Multiply(new Constant(-1), x));

        // ∫ sqrt(x) dx = (2/3)·x^(3/2)
        if (expr is Power pow6 && pow6.Base is Variable v4 && v4.Name == variable &&
            pow6.Exponent is Constant ce6 && System.Math.Abs(ce6.Value - 0.5) < 1e-10)
            return new Multiply(new Constant(2.0 / 3), new Power(x, new Constant(1.5)));

        // ∫ 1/sqrt(x) dx = 2·sqrt(x)
        if (expr is Power pow7 && pow7.Base is Variable v5 && v5.Name == variable &&
            pow7.Exponent is Constant ce7 && System.Math.Abs(ce7.Value + 0.5) < 1e-10)
            return new Multiply(new Constant(2), new Power(x, new Constant(0.5)));

        return null;
    }

    #endregion
}
