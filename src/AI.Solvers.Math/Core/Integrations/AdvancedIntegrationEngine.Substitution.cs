using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Integrations;

public static partial class AdvancedIntegrationEngine
{
    #region Метод подстановки

    private static Expression? TrySubstitution(Expression expr, string variable)
    {
        if (expr is not Multiply mult) return null;
        var x = new Variable(variable);

        // ∫ x·sin(x²) dx = -cos(x²)/2,  ∫ 2x·sin(x²) dx = -cos(x²)
        if (mult.Right is Sin sinPow && sinPow.Argument is Power pow1 &&
            pow1.Base is Variable vb1 && vb1.Name == variable &&
            pow1.Exponent is Constant ce1 && System.Math.Abs(ce1.Value - 2) < 1e-10)
        {
            if (mult.Left is Variable vl1 && vl1.Name == variable)
                return new Multiply(new Constant(-0.5), new Cos(new Power(x, new Constant(2))));
            if (mult.Left is Multiply m2 && m2.Left is Constant c && System.Math.Abs(c.Value - 2) < 1e-10 &&
                m2.Right is Variable v2 && v2.Name == variable)
                return new Multiply(new Constant(-1), new Cos(new Power(x, new Constant(2))));
        }

        // ∫ x·cos(x²) dx = sin(x²)/2,  ∫ 2x·cos(x²) dx = sin(x²)
        if (mult.Right is Cos cosPow && cosPow.Argument is Power pow2 &&
            pow2.Base is Variable vb2 && vb2.Name == variable &&
            pow2.Exponent is Constant ce2 && System.Math.Abs(ce2.Value - 2) < 1e-10)
        {
            if (mult.Left is Variable vl2 && vl2.Name == variable)
                return new Multiply(new Constant(0.5), new Sin(new Power(x, new Constant(2))));
            if (mult.Left is Multiply m3 && m3.Left is Constant c2 && System.Math.Abs(c2.Value - 2) < 1e-10 &&
                m3.Right is Variable v3 && v3.Name == variable)
                return new Sin(new Power(x, new Constant(2)));
        }

        // ∫ x·exp(x²) dx = exp(x²)/2,  ∫ 2x·exp(x²) dx = exp(x²)
        if (mult.Right is Exp expPow && expPow.Argument is Power pow3 &&
            pow3.Base is Variable vb3 && vb3.Name == variable &&
            pow3.Exponent is Constant ce3 && System.Math.Abs(ce3.Value - 2) < 1e-10)
        {
            if (mult.Left is Variable vl3 && vl3.Name == variable)
                return new Multiply(new Constant(0.5), new Exp(new Power(x, new Constant(2))));
            if (mult.Left is Multiply m4 && m4.Left is Constant c3 && System.Math.Abs(c3.Value - 2) < 1e-10 &&
                m4.Right is Variable v4 && v4.Name == variable)
                return new Exp(new Power(x, new Constant(2)));
        }

        // ∫ x·exp(x²) dx (обратный порядок)
        if (mult.Left is Variable vl4 && vl4.Name == variable &&
            mult.Right is Exp exp2 && exp2.Argument is Power pow4 &&
            pow4.Base is Variable vb4 && vb4.Name == variable &&
            pow4.Exponent is Constant ce4 && System.Math.Abs(ce4.Value - 2) < 1e-10)
            return new Multiply(new Constant(0.5), new Exp(new Power(x, new Constant(2))));

        // ∫ sin(x)·cos(x) dx = sin²(x)/2
        if ((mult.Left is Sin sin2 && mult.Right is Cos cos2) ||
            (mult.Left is Cos cos3 && mult.Right is Sin sin3))
        {
            var sinArg = mult.Left  is Sin s1 ? s1.Argument : ((Sin)mult.Right).Argument;
            var cosArg = mult.Left  is Cos c4 ? c4.Argument : ((Cos)mult.Right).Argument;
            if (sinArg.ToString() == cosArg.ToString())
                return new Multiply(new Constant(0.5), new Power(new Sin(sinArg), new Constant(2)));
        }

        // ∫ x/(x²+c) dx = (1/2)·ln|x²+c|
        if (mult.Left is Variable vx && vx.Name == variable)
        {
            if (mult.Right is Power pow5 && pow5.Exponent is Constant ce5 && System.Math.Abs(ce5.Value + 1) < 1e-10)
            {
                if (pow5.Base is Add add && add.Left is Power powx &&
                    powx.Base is Variable vbx && vbx.Name == variable &&
                    powx.Exponent is Constant cex && System.Math.Abs(cex.Value - 2) < 1e-10)
                    return new Multiply(new Constant(0.5), new Ln(new Abs(pow5.Base)));
            }
        }

        return null;
    }

    #endregion
}
