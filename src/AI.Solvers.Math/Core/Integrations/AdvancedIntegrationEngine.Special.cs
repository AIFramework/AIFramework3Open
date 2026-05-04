using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Integrations;

public static partial class AdvancedIntegrationEngine
{
    #region Специальные функции и нелементарные интегралы

    private static Expression? TrySpecialFunctions(Expression expr, string variable)
    {
        var x = new Variable(variable);

        // ∫ exp(-x²) dx = √π/2 · erf(x)
        if (expr is Exp exp1 && TryExtractGaussian(exp1, variable, out double k1) && k1 < 0)
        {
            double a = -k1;
            return System.Math.Abs(a - 1) < 1e-10
                ? new Multiply(new Constant(System.Math.Sqrt(System.Math.PI) / 2), new Erf(x))
                : new Multiply(
                    new Constant(System.Math.Sqrt(System.Math.PI / (4 * a))),
                    new Erf(new Multiply(new Constant(System.Math.Sqrt(a)), x)));
        }

        // ∫ exp(-k·(x+shift)²) dx
        if (expr is Exp exp2 && exp2.Argument is Multiply m2 &&
            m2.Left is Constant c2 && c2.Value < 0 &&
            m2.Right is Power p2 && p2.Exponent is Constant ce2 && System.Math.Abs(ce2.Value - 2) < 1e-10 &&
            p2.Base is Add addBase)
        {
            double k = -c2.Value;
            return new Multiply(
                new Constant(System.Math.Sqrt(System.Math.PI / k) / 2),
                new Erf(new Multiply(new Constant(System.Math.Sqrt(k)), addBase)));
        }

        // ∫ 1/√(1-x²) dx = asin(x)
        if (IsInvSqrt1MinusX2(expr, variable))
            return new Asin(x);

        // ∫ 1/(1+x²) dx = atan(x)
        if (IsInv1PlusX2(expr, variable))
            return new Atan(x);

        // ∫ sin(x)/x dx = Si(x)
        if (IsSinOverX(expr, variable)) return new Si(x);

        // ∫ cos(x)/x dx = Ci(x)
        if (IsCosOverX(expr, variable)) return new Ci(x);

        // ∫ exp(x)/x dx = Ei(x)
        if (IsExpOverX(expr, variable)) return new Ei(x);

        // ∫ 1/ln(x) dx = li(x)
        if (expr is Power powLi && powLi.Exponent is Constant ceLi && System.Math.Abs(ceLi.Value + 1) < 1e-10 &&
            powLi.Base is Ln lnLi && lnLi.Argument is Variable vLi && vLi.Name == variable)
            return new Li(x);

        // ∫ sin(x²) dx = FresnelS(x)
        if (expr is Sin sinFr && sinFr.Argument is Power powFr &&
            powFr.Base is Variable vFr && vFr.Name == variable &&
            powFr.Exponent is Constant ceFr && System.Math.Abs(ceFr.Value - 2) < 1e-10)
            return new FresnelS(x);

        // ∫ cos(x²) dx = FresnelC(x)
        if (expr is Cos cosFr && cosFr.Argument is Power powFr2 &&
            powFr2.Base is Variable vFr2 && vFr2.Name == variable &&
            powFr2.Exponent is Constant ceFr2 && System.Math.Abs(ceFr2.Value - 2) < 1e-10)
            return new FresnelC(x);

        // Явно нелементарные интегралы
        return TryNonElementary(expr, variable, x);
    }

    private static Expression? TryNonElementary(Expression expr, string variable, Variable x)
    {
        // exp(x²)
        if (expr is Exp expPos && expPos.Argument is Power powPos &&
            powPos.Base is Variable vPos && vPos.Name == variable &&
            powPos.Exponent is Constant cePos && System.Math.Abs(cePos.Value - 2) < 1e-10)
            return new NonElementary(expr, variable, "e^(x²) - нет элементарного решения");

        // exp(sin(x)), exp(cos(x))
        if (expr is Exp expSin && expSin.Argument is Sin sinExp && sinExp.Argument is Variable vs && vs.Name == variable)
            return new NonElementary(expr, variable, "e^(sin(x)) - связан с функциями Бесселя");
        if (expr is Exp expCos && expCos.Argument is Cos cosExp && cosExp.Argument is Variable vc && vc.Name == variable)
            return new NonElementary(expr, variable, "e^(cos(x)) - связан с функциями Бесселя");

        // sqrt(sin(x)), sqrt(cos(x))
        if (IsSqrtTrig(expr, variable, out var trigName))
            return new NonElementary(expr, variable, $"{trigName} - эллиптический интеграл");

        // sin(sin(x)), cos(cos(x))
        if (expr is Sin sinSin && sinSin.Argument is Sin sinInner && sinInner.Argument is Variable vSS && vSS.Name == variable)
            return new NonElementary(expr, variable, "sin(sin(x)) - функции Бесселя-Клиффорда");
        if (expr is Cos cosCos && cosCos.Argument is Cos cosInner && cosInner.Argument is Variable vCC && vCC.Name == variable)
            return new NonElementary(expr, variable, "cos(cos(x)) - функции Бесселя-Клиффорда");

        // ln(sin(x)), ln(cos(x))
        if (expr is Ln lnSin && lnSin.Argument is Sin sinLn && sinLn.Argument is Variable vLS && vLS.Name == variable)
            return new NonElementary(expr, variable, "ln(sin(x)) - связан с дилогарифмом");
        if (expr is Ln lnCos && lnCos.Argument is Cos cosLn && cosLn.Argument is Variable vLC && vLC.Name == variable)
            return new NonElementary(expr, variable, "ln(cos(x)) - связан с дилогарифмом");

        // sqrt(1+x³), 1/sqrt(1+x³)
        if (IsEllipticX3(expr, variable, out var ellDesc))
            return new NonElementary(expr, variable, ellDesc);

        // x/sin(x), x/cos(x)
        if (IsXOverTrig(expr, variable, out var xtrigDesc))
            return new NonElementary(expr, variable, xtrigDesc);

        // sin(x)/sqrt(x), cos(x)/sqrt(x)
        if (IsTrigOverSqrtX(expr, variable, out var fresnelDesc))
            return new NonElementary(expr, variable, fresnelDesc);

        return null;
    }

    #endregion

    #region Паттерн-хелперы

    private static bool TryExtractGaussian(Exp exp, string variable, out double coeff)
    {
        coeff = 0;
        if (exp.Argument is Multiply mult &&
            mult.Left is Constant c && c.Value < 0 &&
            mult.Right is Power pow &&
            pow.Base is Variable v && v.Name == variable &&
            pow.Exponent is Constant ce && System.Math.Abs(ce.Value - 2) < 1e-10)
        {
            coeff = c.Value;
            return true;
        }
        return false;
    }

    private static bool IsInvSqrt1MinusX2(Expression expr, string variable)
    {
        return expr is Power pow && pow.Exponent is Constant ce && System.Math.Abs(ce.Value + 0.5) < 1e-10 &&
               pow.Base is Add add &&
               add.Left is Constant c1 && System.Math.Abs(c1.Value - 1) < 1e-10 &&
               add.Right is Multiply mult &&
               mult.Left is Constant c2 && System.Math.Abs(c2.Value + 1) < 1e-10 &&
               mult.Right is Power pw2 &&
               pw2.Base is Variable v && v.Name == variable &&
               pw2.Exponent is Constant ce2 && System.Math.Abs(ce2.Value - 2) < 1e-10;
    }

    private static bool IsInv1PlusX2(Expression expr, string variable)
    {
        return expr is Power pow && pow.Exponent is Constant ce && System.Math.Abs(ce.Value + 1) < 1e-10 &&
               pow.Base is Add add &&
               add.Left is Constant c && System.Math.Abs(c.Value - 1) < 1e-10 &&
               add.Right is Power pw2 &&
               pw2.Base is Variable v && v.Name == variable &&
               pw2.Exponent is Constant ce2 && System.Math.Abs(ce2.Value - 2) < 1e-10;
    }

    private static bool IsSinOverX(Expression expr, string variable)
    {
        if (expr is not Multiply mult) return false;
        var sinPart = (mult.Left as Sin) ?? (mult.Right as Sin);
        var invX    = FindInvX(mult, variable);
        return sinPart != null && invX && sinPart.Argument is Variable vs && vs.Name == variable;
    }

    private static bool IsCosOverX(Expression expr, string variable)
    {
        if (expr is not Multiply mult) return false;
        var cosPart = (mult.Left as Cos) ?? (mult.Right as Cos);
        var invX    = FindInvX(mult, variable);
        return cosPart != null && invX && cosPart.Argument is Variable vc && vc.Name == variable;
    }

    private static bool IsExpOverX(Expression expr, string variable)
    {
        if (expr is not Multiply mult) return false;
        var expPart = (mult.Left as Exp) ?? (mult.Right as Exp);
        var invX    = FindInvX(mult, variable);
        return expPart != null && invX && expPart.Argument is Variable ve && ve.Name == variable;
    }

    private static bool FindInvX(Multiply mult, string variable)
    {
        bool IsInvX(Expression e) =>
            e is Power p && p.Exponent is Constant ce && System.Math.Abs(ce.Value + 1) < 1e-10 &&
            p.Base is Variable v && v.Name == variable;
        return IsInvX(mult.Left) || IsInvX(mult.Right);
    }

    private static bool IsSqrtTrig(Expression expr, string variable, out string name)
    {
        name = "";
        if (expr is not Power pow || pow.Exponent is not Constant ce || System.Math.Abs(ce.Value - 0.5) > 1e-10)
            return false;
        if (pow.Base is Sin s && s.Argument is Variable vs && vs.Name == variable) { name = "sqrt(sin(x))"; return true; }
        if (pow.Base is Cos c && c.Argument is Variable vc && vc.Name == variable) { name = "sqrt(cos(x))"; return true; }
        return false;
    }

    private static bool IsEllipticX3(Expression expr, string variable, out string desc)
    {
        desc = "";
        if (expr is Power pow)
        {
            bool isSqrt    = pow.Exponent is Constant ce1 && System.Math.Abs(ce1.Value - 0.5) < 1e-10;
            bool isInvSqrt = pow.Exponent is Constant ce2 && System.Math.Abs(ce2.Value + 0.5) < 1e-10;
            if ((isSqrt || isInvSqrt) && pow.Base is Add add &&
                add.Left is Constant c1 && System.Math.Abs(c1.Value - 1) < 1e-10 &&
                add.Right is Power inner &&
                inner.Base is Variable v && v.Name == variable &&
                inner.Exponent is Constant ce && System.Math.Abs(ce.Value - 3) < 1e-10)
            {
                desc = isSqrt ? "sqrt(1+x³) - эллиптический интеграл" : "1/sqrt(1+x³) - эллиптический интеграл";
                return true;
            }
        }
        return false;
    }

    private static bool IsXOverTrig(Expression expr, string variable, out string desc)
    {
        desc = "";
        if (expr is not Multiply mult) return false;

        bool HasVarX(Expression e) => e is Variable v && v.Name == variable;
        bool HasInvSin(Expression e) =>
            e is Power p && p.Exponent is Constant ce && System.Math.Abs(ce.Value + 1) < 1e-10 &&
            p.Base is Sin s && s.Argument is Variable vs && vs.Name == variable;
        bool HasInvCos(Expression e) =>
            e is Power p && p.Exponent is Constant ce && System.Math.Abs(ce.Value + 1) < 1e-10 &&
            p.Base is Cos c && c.Argument is Variable vc && vc.Name == variable;

        if ((HasVarX(mult.Left) && HasInvSin(mult.Right)) || (HasVarX(mult.Right) && HasInvSin(mult.Left)))
            { desc = "x/sin(x) - логарифмический интеграл"; return true; }
        if ((HasVarX(mult.Left) && HasInvCos(mult.Right)) || (HasVarX(mult.Right) && HasInvCos(mult.Left)))
            { desc = "x/cos(x) - логарифмический интеграл"; return true; }

        return false;
    }

    private static bool IsTrigOverSqrtX(Expression expr, string variable, out string desc)
    {
        desc = "";
        if (expr is not Multiply mult) return false;

        bool IsInvSqrtX(Expression e) =>
            e is Power p && p.Exponent is Constant ce && System.Math.Abs(ce.Value + 0.5) < 1e-10 &&
            p.Base is Variable v && v.Name == variable;

        if (mult.Left is Sin s1 && s1.Argument is Variable vs1 && vs1.Name == variable && IsInvSqrtX(mult.Right) ||
            mult.Right is Sin s2 && s2.Argument is Variable vs2 && vs2.Name == variable && IsInvSqrtX(mult.Left))
            { desc = "sin(x)/sqrt(x) - обобщенный интеграл Френеля"; return true; }

        if (mult.Left is Cos c1 && c1.Argument is Variable vc1 && vc1.Name == variable && IsInvSqrtX(mult.Right) ||
            mult.Right is Cos c2 && c2.Argument is Variable vc2 && vc2.Name == variable && IsInvSqrtX(mult.Left))
            { desc = "cos(x)/sqrt(x) - обобщенный интеграл Френеля"; return true; }

        return false;
    }

    #endregion
}
